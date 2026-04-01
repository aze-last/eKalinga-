using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class PendingAppUpdate
    {
        public string Version { get; set; } = string.Empty;
        public string InstallerFileName { get; set; } = string.Empty;
        public string InstallerPath { get; set; } = string.Empty;
        public string InstallerUrl { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string ReleasePageUrl { get; set; } = string.Empty;
        public string PublishedAt { get; set; } = string.Empty;
        public List<string> Notes { get; set; } = [];
        public string DownloadedAtUtc { get; set; } = string.Empty;
    }

    public sealed class UpdateDownloadProgress
    {
        public long BytesReceived { get; init; }
        public long? TotalBytes { get; init; }
        public double PercentComplete { get; init; }
    }

    public static class AppUpdatePackageService
    {
        private const string PendingUpdateStateFileName = "pending-update.json";

        private static readonly HttpClient DownloadClient = new()
        {
            Timeout = TimeSpan.FromMinutes(15)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static PendingAppUpdate? LoadPendingUpdate()
        {
            var updatesDirectory = GetUpdatesDirectory();
            return LoadPendingUpdate(GetPendingUpdateStatePath(updatesDirectory), updatesDirectory);
        }

        internal static PendingAppUpdate? LoadPendingUpdate(string statePath, string updatesDirectory)
        {
            if (!File.Exists(statePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(statePath);
                var pending = JsonSerializer.Deserialize<PendingAppUpdate>(json, JsonOptions);
                if (!IsValidPendingUpdate(pending, updatesDirectory))
                {
                    DeleteIfExists(statePath);
                    return null;
                }

                pending!.InstallerPath = Path.GetFullPath(pending.InstallerPath);
                pending.Sha256 = NormalizeSha256(pending.Sha256);
                return pending;
            }
            catch
            {
                DeleteIfExists(statePath);
                return null;
            }
        }

        public static async Task<PendingAppUpdate> DownloadUpdateAsync(
            UpdateCheckResult update,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await DownloadUpdateAsync(update, GetUpdatesDirectory(), DownloadClient, progress, cancellationToken);
        }

        internal static async Task<PendingAppUpdate> DownloadUpdateAsync(
            UpdateCheckResult update,
            string updatesDirectory,
            HttpClient httpClient,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!update.CanDownloadInstaller)
            {
                throw new InvalidOperationException("The current update result does not include a downloadable installer.");
            }

            Directory.CreateDirectory(updatesDirectory);
            CleanupTransientFiles(updatesDirectory);

            var statePath = GetPendingUpdateStatePath(updatesDirectory);
            ClearPendingUpdate(statePath, updatesDirectory);
            Directory.CreateDirectory(updatesDirectory);

            var installerFileName = ResolveInstallerFileName(update);
            var finalInstallerPath = Path.Combine(updatesDirectory, installerFileName);
            var tempInstallerPath = $"{finalInstallerPath}.download";
            DeleteIfExists(finalInstallerPath);
            DeleteIfExists(tempInstallerPath);

            try
            {
                using var response = await httpClient.GetAsync(
                    update.InstallerUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                ReportProgress(progress, bytesReceived: 0, totalBytes);

                await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long bytesReceived = 0;

                    while (true)
                    {
                        var bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        bytesReceived += bytesRead;
                        ReportProgress(progress, bytesReceived, totalBytes);
                    }

                    await fileStream.FlushAsync(cancellationToken);
                }

                var expectedSha256 = NormalizeSha256(update.Sha256);
                var actualSha256 = ComputeSha256(tempInstallerPath);
                if (!string.Equals(expectedSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteIfExists(tempInstallerPath);
                    throw new InvalidDataException("The downloaded installer failed SHA-256 verification.");
                }

                File.Move(tempInstallerPath, finalInstallerPath, overwrite: true);

                var pending = new PendingAppUpdate
                {
                    Version = update.LatestVersion,
                    InstallerFileName = installerFileName,
                    InstallerPath = finalInstallerPath,
                    InstallerUrl = update.InstallerUrl,
                    Sha256 = actualSha256,
                    ReleasePageUrl = update.ReleasePageUrl,
                    PublishedAt = update.PublishedAt,
                    Notes = update.Notes.ToList(),
                    DownloadedAtUtc = DateTime.UtcNow.ToString("O")
                };

                SavePendingUpdate(pending, statePath, updatesDirectory);
                ReportProgress(progress, new FileInfo(finalInstallerPath).Length, totalBytes ?? new FileInfo(finalInstallerPath).Length);
                return pending;
            }
            catch
            {
                DeleteIfExists(tempInstallerPath);
                throw;
            }
        }

        public static void ClearPendingUpdate()
        {
            var updatesDirectory = GetUpdatesDirectory();
            ClearPendingUpdate(GetPendingUpdateStatePath(updatesDirectory), updatesDirectory);
        }

        internal static void ClearPendingUpdate(string statePath, string updatesDirectory)
        {
            var pending = LoadPendingUpdate(statePath, updatesDirectory);
            if (pending != null && IsChildPath(pending.InstallerPath, updatesDirectory))
            {
                DeleteIfExists(pending.InstallerPath);
            }

            DeleteIfExists(statePath);
            CleanupTransientFiles(updatesDirectory);
            TryDeleteEmptyDirectory(updatesDirectory);
        }

        public static void PerformStartupMaintenance()
        {
            var updatesDirectory = GetUpdatesDirectory();
            PerformStartupMaintenance(GetPendingUpdateStatePath(updatesDirectory), updatesDirectory, AppVersionService.GetCurrentVersion());
        }

        internal static void PerformStartupMaintenance(string statePath, string updatesDirectory, string currentVersion)
        {
            CleanupTransientFiles(updatesDirectory);

            var pending = LoadPendingUpdate(statePath, updatesDirectory);
            if (pending == null)
            {
                TryDeleteEmptyDirectory(updatesDirectory);
                return;
            }

            if (!File.Exists(pending.InstallerPath))
            {
                ClearPendingUpdate(statePath, updatesDirectory);
                return;
            }

            if (AppVersionService.TryParseVersion(currentVersion, out var currentParsed)
                && AppVersionService.TryParseVersion(pending.Version, out var pendingParsed)
                && currentParsed >= pendingParsed)
            {
                ClearPendingUpdate(statePath, updatesDirectory);
            }
        }

        public static void LaunchInstaller(PendingAppUpdate pending)
        {
            if (!IsValidPendingUpdate(pending, GetUpdatesDirectory()) || !File.Exists(pending.InstallerPath))
            {
                throw new FileNotFoundException("The downloaded installer is no longer available.", pending?.InstallerPath);
            }

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = pending.InstallerPath,
                WorkingDirectory = Path.GetDirectoryName(pending.InstallerPath) ?? string.Empty,
                UseShellExecute = true
            });

            if (process == null)
            {
                throw new InvalidOperationException("The installer could not be launched.");
            }
        }

        internal static string GetUpdatesDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "updates");
        }

        internal static string GetPendingUpdateStatePath(string updatesDirectory)
        {
            return Path.Combine(updatesDirectory, PendingUpdateStateFileName);
        }

        internal static void SavePendingUpdate(PendingAppUpdate pending, string statePath, string updatesDirectory)
        {
            if (!IsValidPendingUpdate(pending, updatesDirectory))
            {
                throw new InvalidOperationException("The pending update metadata is invalid.");
            }

            Directory.CreateDirectory(updatesDirectory);
            pending.InstallerPath = Path.GetFullPath(pending.InstallerPath);
            pending.Sha256 = NormalizeSha256(pending.Sha256);
            File.WriteAllText(statePath, JsonSerializer.Serialize(pending, JsonOptions));
        }

        internal static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        internal static string NormalizeSha256(string? sha256)
        {
            return string.Concat((sha256 ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character) && character != '-'))
                .ToLowerInvariant();
        }

        private static string ResolveInstallerFileName(UpdateCheckResult update)
        {
            var fileName = update.InstallerFileName?.Trim();
            if (string.IsNullOrWhiteSpace(fileName)
                && Uri.TryCreate(update.InstallerUrl, UriKind.Absolute, out var installerUri))
            {
                fileName = Path.GetFileName(installerUri.LocalPath);
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"AttendanceShiftingManagement-Setup-{update.LatestVersion}.exe";
            }

            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".exe";
            }

            return fileName;
        }

        private static bool IsValidPendingUpdate(PendingAppUpdate? pending, string updatesDirectory)
        {
            return pending != null
                && !string.IsNullOrWhiteSpace(pending.Version)
                && !string.IsNullOrWhiteSpace(pending.InstallerFileName)
                && !string.IsNullOrWhiteSpace(pending.InstallerPath)
                && !string.IsNullOrWhiteSpace(pending.Sha256)
                && IsChildPath(pending.InstallerPath, updatesDirectory);
        }

        private static bool IsChildPath(string candidatePath, string parentPath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(parentPath))
            {
                return false;
            }

            var normalizedCandidate = Path.GetFullPath(candidatePath);
            var normalizedParent = Path.GetFullPath(parentPath);
            if (!normalizedParent.EndsWith(Path.DirectorySeparatorChar))
            {
                normalizedParent += Path.DirectorySeparatorChar;
            }

            return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static void CleanupTransientFiles(string updatesDirectory)
        {
            if (!Directory.Exists(updatesDirectory))
            {
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(updatesDirectory, "*.download", SearchOption.TopDirectoryOnly))
            {
                DeleteIfExists(filePath);
            }
        }

        private static void TryDeleteEmptyDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                return;
            }

            Directory.Delete(directoryPath);
        }

        private static void DeleteIfExists(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static void ReportProgress(IProgress<UpdateDownloadProgress>? progress, long bytesReceived, long? totalBytes)
        {
            if (progress == null)
            {
                return;
            }

            var percentComplete = totalBytes.HasValue && totalBytes.Value > 0
                ? Math.Min(100d, Math.Round(bytesReceived * 100d / totalBytes.Value, 1))
                : 0d;

            progress.Report(new UpdateDownloadProgress
            {
                BytesReceived = bytesReceived,
                TotalBytes = totalBytes,
                PercentComplete = percentComplete
            });
        }
    }
}

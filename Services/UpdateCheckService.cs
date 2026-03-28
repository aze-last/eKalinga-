using System.Net.Http;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public enum UpdateCheckStatus
    {
        NotChecked,
        NotConfigured,
        UpToDate,
        UpdateAvailable,
        Failed
    }

    public sealed class UpdateManifest
    {
        public string Version { get; set; } = string.Empty;
        public List<string> Notes { get; set; } = [];
        public string PublishedAt { get; set; } = string.Empty;
        public string ReleasePageUrl { get; set; } = string.Empty;
        public string InstallerFileName { get; set; } = string.Empty;
    }

    public sealed class UpdateCheckResult
    {
        public UpdateCheckStatus Status { get; init; }
        public string CurrentVersion { get; init; } = string.Empty;
        public string LatestVersion { get; init; } = string.Empty;
        public string PublishedAt { get; init; } = string.Empty;
        public string ReleasePageUrl { get; init; } = string.Empty;
        public string InstallerFileName { get; init; } = string.Empty;
        public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
        public string Message { get; init; } = string.Empty;
        public bool IsUpdateAvailable => Status == UpdateCheckStatus.UpdateAvailable;
    }

    public static class UpdateCheckService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(string manifestUrl, CancellationToken cancellationToken = default)
        {
            var currentVersion = AppVersionService.GetCurrentVersion();
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.NotConfigured,
                    CurrentVersion = currentVersion,
                    Message = "Set an update manifest URL to enable update checks."
                };
            }

            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));

                var manifestJson = await HttpClient.GetStringAsync(manifestUrl.Trim(), timeoutSource.Token);
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson, JsonOptions);
                if (manifest == null)
                {
                    return new UpdateCheckResult
                    {
                        Status = UpdateCheckStatus.Failed,
                        CurrentVersion = currentVersion,
                        Message = "The update manifest could not be read."
                    };
                }

                var latestVersion = AppVersionService.SanitizeVersion(manifest.Version);
                if (!TryParseVersion(currentVersion, out var currentParsed) || !TryParseVersion(latestVersion, out var latestParsed))
                {
                    return new UpdateCheckResult
                    {
                        Status = UpdateCheckStatus.Failed,
                        CurrentVersion = currentVersion,
                        LatestVersion = latestVersion ?? string.Empty,
                        Message = "The update manifest contains an invalid version number."
                    };
                }

                var status = latestParsed > currentParsed
                    ? UpdateCheckStatus.UpdateAvailable
                    : UpdateCheckStatus.UpToDate;

                return new UpdateCheckResult
                {
                    Status = status,
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion ?? string.Empty,
                    PublishedAt = manifest.PublishedAt?.Trim() ?? string.Empty,
                    ReleasePageUrl = manifest.ReleasePageUrl?.Trim() ?? string.Empty,
                    InstallerFileName = manifest.InstallerFileName?.Trim() ?? string.Empty,
                    Notes = manifest.Notes?.Where(note => !string.IsNullOrWhiteSpace(note)).Select(note => note.Trim()).ToArray() ?? Array.Empty<string>(),
                    Message = status == UpdateCheckStatus.UpdateAvailable
                        ? $"Version {latestVersion} is available."
                        : "This installation is up to date."
                };
            }
            catch (OperationCanceledException)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.Failed,
                    CurrentVersion = currentVersion,
                    Message = "Update check timed out."
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.Failed,
                    CurrentVersion = currentVersion,
                    Message = $"Unable to check for updates right now. {ex.Message}"
                };
            }
        }

        private static bool TryParseVersion(string? versionText, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(versionText))
            {
                return false;
            }

            if (Version.TryParse(versionText, out var parsedVersion) && parsedVersion != null)
            {
                version = parsedVersion;
                return true;
            }

            if (Version.TryParse($"{versionText}.0", out parsedVersion) && parsedVersion != null)
            {
                version = parsedVersion;
                return true;
            }

            return false;
        }
    }
}

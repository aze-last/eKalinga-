using AttendanceShiftingManagement.Services;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace AttendanceShiftingManagement.Tests;

public sealed class AppUpdatePackageServiceTests
{
    [Fact]
    public async Task DownloadUpdateAsync_SavesVerifiedInstallerAndPendingState()
    {
        var tempDirectory = CreateTempDirectory();
        var updatesDirectory = Path.Combine(tempDirectory, "updates");
        var statePath = AppUpdatePackageService.GetPendingUpdateStatePath(updatesDirectory);
        var installerBytes = Encoding.UTF8.GetBytes("fake-installer");
        var sha256 = Convert.ToHexString(SHA256.HashData(installerBytes)).ToLowerInvariant();

        try
        {
            var update = CreateUpdateResult(sha256);
            using var httpClient = new HttpClient(new ByteArrayHttpMessageHandler(installerBytes));

            var pending = await AppUpdatePackageService.DownloadUpdateAsync(update, updatesDirectory, httpClient);
            var loaded = AppUpdatePackageService.LoadPendingUpdate(statePath, updatesDirectory);

            Assert.NotNull(loaded);
            Assert.Equal("1.0.1", pending.Version);
            Assert.Equal("1.0.1", loaded!.Version);
            Assert.True(File.Exists(pending.InstallerPath));
            Assert.Equal(sha256, loaded.Sha256);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenHashMismatch_DoesNotPersistPendingUpdate()
    {
        var tempDirectory = CreateTempDirectory();
        var updatesDirectory = Path.Combine(tempDirectory, "updates");
        var statePath = AppUpdatePackageService.GetPendingUpdateStatePath(updatesDirectory);
        var installerBytes = Encoding.UTF8.GetBytes("fake-installer");

        try
        {
            var update = CreateUpdateResult("deadbeef");
            using var httpClient = new HttpClient(new ByteArrayHttpMessageHandler(installerBytes));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                AppUpdatePackageService.DownloadUpdateAsync(update, updatesDirectory, httpClient));

            Assert.Null(AppUpdatePackageService.LoadPendingUpdate(statePath, updatesDirectory));
            Assert.False(Directory.Exists(updatesDirectory) && Directory.EnumerateFiles(updatesDirectory, "*.exe").Any());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void PerformStartupMaintenance_WhenAppliedVersionIsInstalled_ClearsPendingFiles()
    {
        var tempDirectory = CreateTempDirectory();
        var updatesDirectory = Path.Combine(tempDirectory, "updates");
        Directory.CreateDirectory(updatesDirectory);
        var installerPath = Path.Combine(updatesDirectory, "setup.exe");
        File.WriteAllText(installerPath, "installer");
        var statePath = AppUpdatePackageService.GetPendingUpdateStatePath(updatesDirectory);

        try
        {
            AppUpdatePackageService.SavePendingUpdate(new PendingAppUpdate
            {
                Version = "1.0.1",
                InstallerFileName = "setup.exe",
                InstallerPath = installerPath,
                InstallerUrl = "https://example.com/downloads/setup.exe",
                Sha256 = "abc123",
                ReleasePageUrl = "https://example.com/releases/v1.0.1",
                PublishedAt = "2026-03-28",
                DownloadedAtUtc = DateTime.UtcNow.ToString("O")
            }, statePath, updatesDirectory);

            AppUpdatePackageService.PerformStartupMaintenance(statePath, updatesDirectory, "1.0.1");

            Assert.False(File.Exists(installerPath));
            Assert.False(File.Exists(statePath));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static UpdateCheckResult CreateUpdateResult(string sha256)
    {
        return new UpdateCheckResult
        {
            Status = UpdateCheckStatus.UpdateAvailable,
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.1",
            InstallerFileName = "setup.exe",
            InstallerUrl = "https://example.com/downloads/setup.exe",
            Sha256 = sha256,
            ReleasePageUrl = "https://example.com/releases/v1.0.1",
            PublishedAt = "2026-03-28",
            Notes = ["Added guided updates."],
            Message = "Version 1.0.1 is available."
        };
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private sealed class ByteArrayHttpMessageHandler(byte[] installerBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(installerBytes)
            });
        }
    }
}

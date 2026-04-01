using AttendanceShiftingManagement.Services;
using System.Net;
using System.Net.Http;
using System.Text;

namespace AttendanceShiftingManagement.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_WithoutManifestUrl_ReturnsNotConfigured()
    {
        var result = await UpdateCheckService.CheckForUpdatesAsync(string.Empty);

        Assert.Equal(UpdateCheckStatus.NotConfigured, result.Status);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithDownloadableManifest_ReturnsInstallerMetadata()
    {
        var manifestJson =
            """
            {
              "version": "1.0.1",
              "notes": [
                "Added guided installer updates."
              ],
              "publishedAt": "2026-03-28",
              "releasePageUrl": "https://example.com/releases/v1.0.1",
              "installerFileName": "BarangayAyudaSystem-Setup-1.0.1.exe",
              "installerUrl": "https://example.com/downloads/BarangayAyudaSystem-Setup-1.0.1.exe",
              "sha256": "abc123"
            }
            """;

        var httpClient = CreateHttpClient(manifestJson);
        var result = await UpdateCheckService.CheckForUpdatesAsync(
            "https://example.com/version.json",
            httpClient,
            "1.0.0");

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.True(result.IsUpdateAvailable);
        Assert.True(result.CanDownloadInstaller);
        Assert.Equal("1.0.1", result.LatestVersion);
        Assert.Equal("https://example.com/downloads/BarangayAyudaSystem-Setup-1.0.1.exe", result.InstallerUrl);
        Assert.Equal("abc123", result.Sha256);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithInvalidVersion_ReturnsFailed()
    {
        var manifestJson =
            """
            {
              "version": "banana",
              "notes": [],
              "publishedAt": "2026-03-28"
            }
            """;

        var httpClient = CreateHttpClient(manifestJson);
        var result = await UpdateCheckService.CheckForUpdatesAsync(
            "https://example.com/version.json",
            httpClient,
            "1.0.0");

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Contains("invalid version number", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateHttpClient(string content)
    {
        return new HttpClient(new StubHttpMessageHandler(content))
        {
            BaseAddress = new Uri("https://example.com/")
        };
    }

    private sealed class StubHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}

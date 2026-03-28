using AttendanceShiftingManagement.Services;

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
}

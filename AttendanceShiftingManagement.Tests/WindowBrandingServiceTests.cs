using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class WindowBrandingServiceTests
{
    [Fact]
    public void ResolveIconSource_UsesCustomLogoWhenItExists()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"branding-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var logoPath = Path.Combine(tempDirectory, "custom-logo.png");
            File.WriteAllBytes(logoPath, [1, 2, 3, 4]);

            var source = WindowBrandingService.ResolveIconSource(new SystemProfileSettingsModel
            {
                LogoPath = logoPath
            });

            Assert.Equal(logoPath, source);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveIconSource_FallsBackToDefaultIconWhenCustomLogoIsMissing()
    {
        var source = WindowBrandingService.ResolveIconSource(new SystemProfileSettingsModel
        {
            LogoPath = @"C:\missing\logo.png"
        });

        Assert.Equal("pack://application:,,,/Images/municipal-house.ico", source);
    }
}

using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class AppPreferencesServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsUpdatePreferences()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "apppreferences.json");

        try
        {
            var expected = new AppPreferencesModel
            {
                CheckForUpdatesOnStartup = false,
                UpdateManifestUrl = "https://example.com/version.json"
            };

            AppPreferencesService.Save(expected, runtimePath);
            var loaded = AppPreferencesService.Load(runtimePath);

            Assert.False(loaded.CheckForUpdatesOnStartup);
            Assert.Equal("https://example.com/version.json", loaded.UpdateManifestUrl);
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
    public void Load_ReplacesLegacyManifestUrl_WithDefault()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "apppreferences.json");

        try
        {
            AppPreferencesService.Save(
                new AppPreferencesModel
                {
                    UpdateManifestUrl = "https://raw.githubusercontent.com/aze-last/BarangayAyudaSys/main/version.json"
                },
                runtimePath);
            var loaded = AppPreferencesService.Load(runtimePath);

            Assert.Equal(AppPreferencesService.DefaultUpdateManifestUrl, loaded.UpdateManifestUrl);
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
    public void Load_EmptyManifestUrl_FallsBackToDefault()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "apppreferences.json");

        try
        {
            AppPreferencesService.Save(new AppPreferencesModel { UpdateManifestUrl = "  " }, runtimePath);
            var loaded = AppPreferencesService.Load(runtimePath);

            Assert.Equal(AppPreferencesService.DefaultUpdateManifestUrl, loaded.UpdateManifestUrl);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}

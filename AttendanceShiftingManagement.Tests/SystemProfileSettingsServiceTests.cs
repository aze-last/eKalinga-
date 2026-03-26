using AttendanceShiftingManagement.Services;
using System.Text.Json;

namespace AttendanceShiftingManagement.Tests;

public sealed class SystemProfileSettingsServiceTests
{
    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultsAndGeneratedInstallSerial()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var runtimePath = Path.Combine(tempDirectory, "systemprofile.json");

        try
        {
            var settings = SystemProfileSettingsService.Load(runtimePath);

            Assert.Equal("Barangay Ayuda System", settings.SystemName);
            Assert.Equal(string.Empty, settings.Owner);
            Assert.Equal(string.Empty, settings.CompanyAddress);
            Assert.Equal(string.Empty, settings.Email);
            Assert.Equal(string.Empty, settings.ContactNumber);
            Assert.Equal(string.Empty, settings.LogoPath);
            Assert.Matches(@"^BAS-\d{8}-[A-Z0-9]{4}$", settings.InstallSerial);
            Assert.True(File.Exists(runtimePath));

            using var persistedJson = JsonDocument.Parse(File.ReadAllText(runtimePath));
            Assert.Equal(
                settings.InstallSerial,
                persistedJson.RootElement.GetProperty(nameof(SystemProfileSettingsModel.InstallSerial)).GetString());
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
    public void SaveAndLoad_RoundTripsSystemProfileSettings()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "systemprofile.json");

        try
        {
            var expected = new SystemProfileSettingsModel
            {
                SystemName = "Barangay Ayuda Portal",
                Owner = "Barangay San Isidro",
                CompanyAddress = "Purok 1, Barangay San Isidro, Cebu",
                Email = "help@barangay.local",
                ContactNumber = "09171234567",
                LogoPath = Path.Combine(tempDirectory, "branding", "system-logo.png"),
                InstallSerial = "BAS-20260326-AB12"
            };

            SystemProfileSettingsService.Save(expected, runtimePath);
            var loaded = SystemProfileSettingsService.Load(runtimePath);

            Assert.Equal(expected.SystemName, loaded.SystemName);
            Assert.Equal(expected.Owner, loaded.Owner);
            Assert.Equal(expected.CompanyAddress, loaded.CompanyAddress);
            Assert.Equal(expected.Email, loaded.Email);
            Assert.Equal(expected.ContactNumber, loaded.ContactNumber);
            Assert.Equal(expected.LogoPath, loaded.LogoPath);
            Assert.Equal(expected.InstallSerial, loaded.InstallSerial);
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
    public void BuildLoginBranding_UsesConfiguredValuesAndFallbackDefaults()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var logoPath = Path.Combine(tempDirectory, "custom-logo.png");
        File.WriteAllBytes(logoPath, new byte[] { 1, 2, 3, 4 });

        try
        {
            var configured = SystemProfileSettingsService.BuildLoginBranding(new SystemProfileSettingsModel
            {
                Owner = "Barangay San Isidro",
                SystemName = "Ayuda Operations",
                CompanyAddress = "Purok 1, Barangay San Isidro, Cebu",
                InstallSerial = "BAS-20260326-AB12",
                LogoPath = logoPath
            });

            Assert.Equal("Barangay San Isidro", configured.Title);
            Assert.Equal("Ayuda Operations", configured.Subtitle);
            Assert.Equal("Purok 1, Barangay San Isidro, Cebu", configured.Address);
            Assert.Equal("BAS-20260326-AB12", configured.InstallSerial);
            Assert.True(configured.HasCustomLogo);
            Assert.Equal(logoPath, configured.LogoPath);

            var fallback = SystemProfileSettingsService.BuildLoginBranding(new SystemProfileSettingsModel
            {
                InstallSerial = "BAS-20260326-Z9X8"
            });

            Assert.Equal("Bagong Pilipinas", fallback.Title);
            Assert.Equal("Barangay Ayuda System", fallback.Subtitle);
            Assert.Equal(string.Empty, fallback.Address);
            Assert.Equal("BAS-20260326-Z9X8", fallback.InstallSerial);
            Assert.False(fallback.HasCustomLogo);
            Assert.Equal(string.Empty, fallback.LogoPath);
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
    public void CopyLogoToBrandingFolder_CopiesFileAndReplacesPreviousLogo()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sourceDirectory = Path.Combine(tempDirectory, "source");
        var brandingDirectory = Path.Combine(tempDirectory, "branding");
        Directory.CreateDirectory(sourceDirectory);

        var firstSource = Path.Combine(sourceDirectory, "first.png");
        var secondSource = Path.Combine(sourceDirectory, "second.jpg");
        File.WriteAllBytes(firstSource, new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(secondSource, new byte[] { 5, 6, 7, 8 });

        try
        {
            var firstLogoPath = SystemProfileSettingsService.CopyLogoToBrandingFolder(firstSource, brandingDirectory, existingLogoPath: null);
            Assert.True(File.Exists(firstLogoPath));

            var secondLogoPath = SystemProfileSettingsService.CopyLogoToBrandingFolder(secondSource, brandingDirectory, firstLogoPath);

            Assert.True(File.Exists(secondLogoPath));
            Assert.False(File.Exists(firstLogoPath));
            Assert.StartsWith(brandingDirectory, secondLogoPath, StringComparison.OrdinalIgnoreCase);
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

using AttendanceShiftingManagement.Services;
using System.Text.Json;

namespace AttendanceShiftingManagement.Tests;

public sealed class OtpEmailSettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsOtpEmailSettings_WithProtectedPassword()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "otpemailsettings.json");

        try
        {
            var expected = new OtpEmailSettingsModel
            {
                SmtpHost = "smtp-relay.brevo.com",
                Port = 587,
                UseSsl = true,
                Username = "brevo-user",
                Password = "brevo-secret",
                SenderEmail = "noreply@example.com",
                SenderDisplayName = "eKalinga OTP"
            };

            OtpEmailSettingsService.Save(expected, runtimePath);
            var loaded = OtpEmailSettingsService.Load(runtimePath);

            Assert.Equal(expected.SmtpHost, loaded.SmtpHost);
            Assert.Equal(expected.Port, loaded.Port);
            Assert.Equal(expected.UseSsl, loaded.UseSsl);
            Assert.Equal(expected.Username, loaded.Username);
            Assert.Equal(expected.Password, loaded.Password);
            Assert.Equal(expected.SenderEmail, loaded.SenderEmail);
            Assert.Equal(expected.SenderDisplayName, loaded.SenderDisplayName);

            using var persistedJson = JsonDocument.Parse(File.ReadAllText(runtimePath));
            var storedPassword = persistedJson.RootElement.GetProperty(nameof(OtpEmailSettingsModel.Password)).GetString();
            Assert.StartsWith("dpapi:", storedPassword, StringComparison.Ordinal);
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
    public void IsConfigured_RequiresSenderAndCredentials()
    {
        var configured = new OtpEmailSettingsModel
        {
            Username = "brevo-user",
            Password = "brevo-secret",
            SenderEmail = "noreply@example.com"
        };

        var missingSender = new OtpEmailSettingsModel
        {
            Username = "brevo-user",
            Password = "brevo-secret"
        };

        Assert.True(OtpEmailSettingsService.IsConfigured(configured));
        Assert.False(OtpEmailSettingsService.IsConfigured(missingSender));
    }
}

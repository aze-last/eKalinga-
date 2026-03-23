using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_WithRuntimeFile_EncryptsPasswordAtRest()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "connectionsettings.json");

        try
        {
            var settings = new ConnectionSettingsModel
            {
                SelectedPreset = "Local",
                Presets = new Dictionary<string, DatabaseConnectionPreset>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Local"] = new()
                    {
                        DisplayName = "Local",
                        Server = "127.0.0.1",
                        Port = 3306,
                        Database = "attendance_shifting_db",
                        Username = "root",
                        Password = "plain-text-password"
                    },
                    ["Remote"] = new()
                }
            };

            ConnectionSettingsService.Save(settings, runtimePath);

            var persistedJson = File.ReadAllText(runtimePath);
            Assert.DoesNotContain("plain-text-password", persistedJson, StringComparison.Ordinal);

            var loaded = ConnectionSettingsService.Load(runtimePath);
            Assert.Equal("plain-text-password", loaded.GetPreset("Local").Password);
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

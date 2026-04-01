using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_WithRuntimeFile_PersistsOnlyLanOverrides()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "connectionsettings.json");

        try
        {
            var defaults = ConnectionSettingsService.Load(runtimePath);
            var settings = new ConnectionSettingsModel
            {
                SelectedPreset = "Lan",
                Presets = new Dictionary<string, DatabaseConnectionPreset>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Local"] = new()
                    {
                        DisplayName = "Local",
                        Server = "10.55.55.55",
                        Port = 3310,
                        Database = "should_not_persist_local",
                        Username = "blocked-local",
                        Password = "blocked-local-password"
                    },
                    ["Remote"] = new()
                    {
                        DisplayName = "Remote",
                        Server = "10.66.66.66",
                        Port = 3311,
                        Database = "should_not_persist_remote",
                        Username = "blocked-remote",
                        Password = "blocked-remote-password"
                    },
                    ["Lan"] = new()
                    {
                        DisplayName = "Network (LAN)",
                        Server = "192.168.1.20",
                        Port = 3306,
                        Database = "attendance_shifting_db",
                        Username = "lan-user",
                        Password = "plain-text-password"
                    }
                }
            };

            ConnectionSettingsService.Save(settings, runtimePath);

            var persistedJson = File.ReadAllText(runtimePath);
            Assert.DoesNotContain("plain-text-password", persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("blocked-local-password", persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("blocked-remote-password", persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("blocked-local", persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("blocked-remote", persistedJson, StringComparison.Ordinal);

            var loaded = ConnectionSettingsService.Load(runtimePath);
            Assert.Equal("Lan", loaded.SelectedPreset);
            Assert.Equal(defaults.GetPreset("Local").Server, loaded.GetPreset("Local").Server);
            Assert.Equal(defaults.GetPreset("Remote").Server, loaded.GetPreset("Remote").Server);
            Assert.Equal(defaults.GetPreset("Local").Password, loaded.GetPreset("Local").Password);
            Assert.Equal(defaults.GetPreset("Remote").Password, loaded.GetPreset("Remote").Password);
            Assert.Equal("plain-text-password", loaded.GetPreset("Lan").Password);
            Assert.Equal("192.168.1.20", loaded.GetPreset("Lan").Server);
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
    public void Load_WithRuntimeOverrides_LoadsOnlyLanPresetAndKeepsFixedPresetsFromDefaults()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "connectionsettings.json");

        try
        {
            var defaults = ConnectionSettingsService.Load(runtimePath);
            var settings = new ConnectionSettingsModel
            {
                SelectedPreset = "Lan",
                Presets = new Dictionary<string, DatabaseConnectionPreset>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Local"] = new()
                    {
                        DisplayName = "Local",
                        Server = "10.55.55.55",
                        Port = 3310,
                        Database = "should_not_override_local",
                        Username = "blocked-local",
                        Password = "blocked-local-password"
                    },
                    ["Remote"] = new()
                    {
                        DisplayName = "Remote",
                        Server = "10.66.66.66",
                        Port = 3311,
                        Database = "should_not_override_remote",
                        Username = "blocked-remote",
                        Password = "blocked-remote-password"
                    },
                    ["Lan"] = new()
                    {
                        DisplayName = "Network (LAN)",
                        Server = "192.168.1.77",
                        Port = 3307,
                        Database = "attendance_shifting_db",
                        Username = "lan-client",
                        Password = "lan-password"
                    }
                }
            };

            ConnectionSettingsService.Save(settings, runtimePath);

            var loaded = ConnectionSettingsService.Load(runtimePath);
            Assert.Equal("Lan", loaded.SelectedPreset);
            Assert.Equal(defaults.GetPreset("Local").Server, loaded.GetPreset("Local").Server);
            Assert.Equal(defaults.GetPreset("Remote").Server, loaded.GetPreset("Remote").Server);
            Assert.Equal("192.168.1.77", loaded.GetPreset("Lan").Server);
            Assert.Equal(defaults.GetPreset("Local").Password, loaded.GetPreset("Local").Password);
            Assert.Equal(defaults.GetPreset("Remote").Password, loaded.GetPreset("Remote").Password);
            Assert.Equal("lan-password", loaded.GetPreset("Lan").Password);
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

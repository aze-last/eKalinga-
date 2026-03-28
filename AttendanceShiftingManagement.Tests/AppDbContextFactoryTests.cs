using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class AppDbContextFactoryTests
{
    [Fact]
    public void ResolveConnectionString_PrefersExplicitConnectionStringOverride()
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
                    Database = "local_db",
                    Username = "local_user",
                    Password = "local_pass"
                },
                ["Remote"] = new()
                {
                    DisplayName = "Remote",
                    Server = "remote.host",
                    Port = 3306,
                    Database = "remote_db",
                    Username = "remote_user",
                    Password = "remote_pass"
                }
            }
        };

        var resolved = AppDbContextFactory.ResolveConnectionString(
            settings,
            presetOverride: "Remote",
            connectionStringOverride: "Server=override.host;Port=3307;Database=override_db;User=override_user;Password=override_pass;");

        Assert.Contains("Server=override.host", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remote.host", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveConnectionString_UsesPresetOverrideWhenProvided()
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
                    Database = "local_db",
                    Username = "local_user",
                    Password = "local_pass"
                },
                ["Remote"] = new()
                {
                    DisplayName = "Remote",
                    Server = "remote.host",
                    Port = 3306,
                    Database = "remote_db",
                    Username = "remote_user",
                    Password = "remote_pass"
                }
            }
        };

        var resolved = AppDbContextFactory.ResolveConnectionString(
            settings,
            presetOverride: "Remote",
            connectionStringOverride: null);

        Assert.Contains("Server=remote.host", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Database=remote_db", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database=local_db", resolved, StringComparison.OrdinalIgnoreCase);
    }
}

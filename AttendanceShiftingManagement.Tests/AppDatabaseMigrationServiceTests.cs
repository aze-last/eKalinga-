using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class AppDatabaseMigrationServiceTests
{
    [Fact]
    public void TryValidatePresetForMigration_FailsWhenServerIsMissing()
    {
        var preset = new DatabaseConnectionPreset
        {
            DisplayName = "Remote (Hostinger)",
            Port = 3306,
            Database = "barangay_ayuda_db",
            Username = "root"
        };

        var isValid = AppDatabaseMigrationService.TryValidatePresetForMigration(preset, out var validationMessage);

        Assert.False(isValid);
        Assert.Equal("server or host name is missing.", validationMessage);
    }

    [Fact]
    public void TryValidatePresetForMigration_AllowsConfiguredPreset()
    {
        var preset = new DatabaseConnectionPreset
        {
            DisplayName = "Local",
            Server = "127.0.0.1",
            Port = 3306,
            Database = "barangay_ayuda_db",
            Username = "root",
            Password = string.Empty
        };

        var isValid = AppDatabaseMigrationService.TryValidatePresetForMigration(preset, out var validationMessage);

        Assert.True(isValid);
        Assert.Equal(string.Empty, validationMessage);
    }
}

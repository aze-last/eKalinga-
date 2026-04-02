using AttendanceShiftingManagement.Data;

namespace AttendanceShiftingManagement.Tests;

public sealed class StartupMigrationCoordinatorTests
{
    [Fact]
    public void RequiresLegacyHistoryBaseline_ReturnsTrue_WhenExistingTablesMissInitialMigration()
    {
        var localMigrations = new[]
        {
            "20260323025003_InitialAyudaSchema",
            "20260327050148_AddProjectDistributionWorkflow"
        };
        var appliedMigrations = new[]
        {
            "20260327050148_AddProjectDistributionWorkflow"
        };

        var requiresBaseline = StartupMigrationCoordinator.RequiresLegacyHistoryBaseline(
            hasApplicationTables: true,
            localMigrations,
            appliedMigrations);

        Assert.True(requiresBaseline);
    }

    [Fact]
    public void RequiresLegacyHistoryBaseline_ReturnsFalse_WhenDatabaseIsFresh()
    {
        var localMigrations = new[]
        {
            "20260323025003_InitialAyudaSchema",
            "20260327050148_AddProjectDistributionWorkflow"
        };

        var requiresBaseline = StartupMigrationCoordinator.RequiresLegacyHistoryBaseline(
            hasApplicationTables: false,
            localMigrations,
            Array.Empty<string>());

        Assert.False(requiresBaseline);
    }

    [Fact]
    public void RequiresLegacyHistoryBaseline_ReturnsFalse_WhenInitialMigrationAlreadyApplied()
    {
        var localMigrations = new[]
        {
            "20260323025003_InitialAyudaSchema",
            "20260327050148_AddProjectDistributionWorkflow",
            "20260328010000_FutureMigration"
        };
        var appliedMigrations = new[]
        {
            "20260323025003_InitialAyudaSchema",
            "20260327050148_AddProjectDistributionWorkflow"
        };

        var requiresBaseline = StartupMigrationCoordinator.RequiresLegacyHistoryBaseline(
            hasApplicationTables: true,
            localMigrations,
            appliedMigrations);

        Assert.False(requiresBaseline);
    }
}

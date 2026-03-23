using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AttendanceShiftingManagement.Tests;

public sealed class StartupMigrationTests
{
    [Fact]
    public void AppAssembly_DefinesEfCoreMigrations_ForStartupMigrationMode()
    {
        var migrationTypes = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(migrationTypes);
    }
}

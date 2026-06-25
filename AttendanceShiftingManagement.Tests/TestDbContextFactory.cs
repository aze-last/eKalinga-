using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Tests;

internal static class TestDbContextFactory
{
    public static LocalDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<LocalDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;

        return new LocalDbContext(options);
    }
}

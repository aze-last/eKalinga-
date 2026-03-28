using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Tests;

internal static class TestDbContextFactory
{
    public static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}

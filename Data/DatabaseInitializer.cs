using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize(bool resetDatabase, bool migrateOnStartup)
        {
            using var context = new AppDbContext();

            if (resetDatabase)
            {
                context.Database.EnsureDeleted();
            }

            if (migrateOnStartup)
            {
                context.Database.Migrate();
            }
            else
            {
                context.Database.EnsureCreated();
            }

            DbSeeder.Seed(context);
        }
    }
}

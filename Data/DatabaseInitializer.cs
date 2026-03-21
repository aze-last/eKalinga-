namespace AttendanceShiftingManagement.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize(bool resetDatabase, bool migrateOnStartup)
        {
            using var context = new AppDbContext();
            _ = migrateOnStartup;

            if (resetDatabase)
            {
                context.Database.EnsureDeleted();
            }

            // The active barangay model has drifted from the legacy attendance migrations.
            // Use the current model as the source of truth for fresh databases.
            context.Database.EnsureCreated();

            DbSeeder.Seed(context);
        }
    }
}

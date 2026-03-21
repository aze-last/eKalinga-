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
            // Fresh databases can still use EnsureCreated(), but partially initialized
            // databases need explicit repair for the current barangay/login schema.
            RuntimeSchemaBootstrapper.EnsureRuntimeSchema(context);

            DbSeeder.Seed(context);
        }
    }
}

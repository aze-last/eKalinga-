using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public static class DatabaseInitializer
    {
        private static readonly object SyncLock = new();
        private static bool _initialized;
        private static string? _initializedConnectionString;

        public static void Initialize(bool resetDatabase, bool migrateOnStartup)
        {
            lock (SyncLock)
            {
                using var context = new AppDbContext();
                var connectionString = context.Database.GetConnectionString();

                if (_initialized &&
                    !resetDatabase &&
                    string.Equals(_initializedConnectionString, connectionString, StringComparison.Ordinal))
                {
                    return;
                }

                InitializeCore(context, resetDatabase, migrateOnStartup);
                _initialized = true;
                _initializedConnectionString = connectionString;
            }
        }

        private static void InitializeCore(AppDbContext context, bool resetDatabase, bool migrateOnStartup)
        {
            if (resetDatabase)
            {
                context.Database.EnsureDeleted();
            }

            if (migrateOnStartup)
            {
                StartupMigrationCoordinator.EnsureMigrated(context);
            }
            else
            {
                // Fallback path for environments that deliberately keep migration mode off.
                RuntimeSchemaBootstrapper.EnsureRuntimeSchema(context);
            }

            DbSeeder.Seed(context);
        }
    }
}

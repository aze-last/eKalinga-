using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Net.Sockets;

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
            try
            {
                // Test the connection first to provide better error messages
                context.Database.OpenConnection();
                context.Database.CloseConnection();
            }
            catch (Exception ex)
            {
                throw GetDetailedDatabaseException(ex);
            }

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
        }

        private static Exception GetDetailedDatabaseException(Exception ex)
        {
            var message = GetDetailedErrorMessage(ex);
            return new InvalidOperationException(message, ex);
        }

        private static string GetDetailedErrorMessage(Exception ex)
        {
            if (ex is MySqlException mysqlEx)
            {
                return mysqlEx.Number switch
                {
                    1045 => "Database authentication failed. Invalid username or password. Check ConnectionSettings.",
                    1049 => "Database does not exist. Verify the database name in ConnectionSettings.",
                    _ => $"MySQL Error {mysqlEx.Number}: {mysqlEx.Message}"
                };
            }

            if (ex is SocketException socketEx)
            {
                return $"Cannot connect to database server. Check if MySQL is running on the configured host and port. ({socketEx.Message})";
            }

            if (ex is TimeoutException)
            {
                return "Database connection timed out. The MySQL server may be unresponsive or the network is unreachable.";
            }

            if (ex.InnerException != null)
            {
                return GetDetailedErrorMessage(ex.InnerException);
            }

            return $"Database connection failed: {ex.Message}";
        }
    }
}

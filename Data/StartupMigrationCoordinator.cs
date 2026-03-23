using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Reflection;

namespace AttendanceShiftingManagement.Data
{
    internal static class StartupMigrationCoordinator
    {
        public static void EnsureMigrated(AppDbContext context)
        {
            var connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The active database connection string is missing.");

            RuntimeSchemaBootstrapper.EnsureDatabaseExists(connectionString);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            var historyTableExists = TableExists(connection, "__EFMigrationsHistory");
            var hasApplicationTables = HasApplicationTables(connection, historyTableExists);

            if (!hasApplicationTables)
            {
                context.Database.Migrate();
                return;
            }

            if (!historyTableExists)
            {
                RuntimeSchemaBootstrapper.RepairLegacySchema(connectionString);
                BaselineMigrationsHistory(connection, context);
            }

            context.Database.Migrate();
        }

        private static bool HasApplicationTables(MySqlConnection connection, bool historyTableExists)
        {
            using var command = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_TYPE = 'BASE TABLE';
                """,
                connection);

            var tableCount = Convert.ToInt32(command.ExecuteScalar());
            return historyTableExists ? tableCount > 1 : tableCount > 0;
        }

        private static bool TableExists(MySqlConnection connection, string tableName)
        {
            using var command = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND TABLE_TYPE = 'BASE TABLE';
                """,
                connection);

            command.Parameters.AddWithValue("@tableName", tableName);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static void BaselineMigrationsHistory(MySqlConnection connection, AppDbContext context)
        {
            var latestMigration = context.Database.GetMigrations().LastOrDefault();
            if (string.IsNullOrWhiteSpace(latestMigration))
            {
                throw new InvalidOperationException("No EF Core migrations were found for startup migration mode.");
            }

            using var createTableCommand = new MySqlCommand(
                """
                CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                    `MigrationId` varchar(150) NOT NULL,
                    `ProductVersion` varchar(32) NOT NULL,
                    PRIMARY KEY (`MigrationId`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                connection);
            createTableCommand.ExecuteNonQuery();

            using var insertCommand = new MySqlCommand(
                """
                INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
                SELECT @migrationId, @productVersion
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `__EFMigrationsHistory`
                    WHERE `MigrationId` = @migrationId
                );
                """,
                connection);

            insertCommand.Parameters.AddWithValue("@migrationId", latestMigration);
            insertCommand.Parameters.AddWithValue("@productVersion", GetProductVersion());
            insertCommand.ExecuteNonQuery();
        }

        private static string GetProductVersion()
        {
            var informationalVersion = typeof(DbContext).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            return string.IsNullOrWhiteSpace(informationalVersion)
                ? "9.0.0"
                : informationalVersion.Split('+')[0];
        }
    }
}

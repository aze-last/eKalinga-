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
            var localMigrations = context.Database.GetMigrations().ToArray();

            RuntimeSchemaBootstrapper.EnsureDatabaseExists(connectionString);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            var historyTableExists = TableExists(connection, "__EFMigrationsHistory");
            var hasApplicationTables = HasApplicationTables(connection, historyTableExists);
            var appliedMigrations = historyTableExists
                ? GetAppliedMigrationIds(connection)
                : new List<string>();

            if (!hasApplicationTables)
            {
                ApplyMigrationsAndRepairs(context, connectionString);
                return;
            }

            if (RequiresLegacyHistoryBaseline(hasApplicationTables, localMigrations, appliedMigrations))
            {
                RuntimeSchemaBootstrapper.RepairLegacySchema(connectionString);
                BaselineMigrationsHistory(connection, localMigrations);
            }

            ApplyMigrationsAndRepairs(context, connectionString);
        }

        private static void ApplyMigrationsAndRepairs(AppDbContext context, string connectionString)
        {
            var pendingMigrations = context.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Contains("20260619043025_AddSyncIdToAllEntities") || 
                pendingMigrations.Contains("20260620083405_UpdateGoodsDistributionSchema"))
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                if (ColumnExists(connection, "users", "SyncId"))
                {
                    using var createTableCommand = new MySqlCommand(
                        """
                        CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                            `MigrationId` varchar(150) NOT NULL,
                            `ProductVersion` varchar(32) NOT NULL,
                            PRIMARY KEY (`MigrationId`)
                        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                        """, connection);
                    createTableCommand.ExecuteNonQuery();

                    using var insertCommand = new MySqlCommand(
                        """
                        INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
                        VALUES ('20260619043025_AddSyncIdToAllEntities', '8.0.0');
                        INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
                        VALUES ('20260620083405_UpdateGoodsDistributionSchema', '8.0.0');
                        """, connection);
                    insertCommand.ExecuteNonQuery();
                }
            }

            context.Database.Migrate();

            // Keep startup migration mode aligned with the runtime bootstrap repairs so
            // upgraded databases self-heal when a table or column was introduced outside
            // the existing migration chain.
            RuntimeSchemaBootstrapper.RepairLegacySchema(connectionString);
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

        private static bool ColumnExists(MySqlConnection connection, string tableName, string columnName)
        {
            using var command = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName;
                """,
                connection);

            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@columnName", columnName);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        internal static bool RequiresLegacyHistoryBaseline(
            bool hasApplicationTables,
            IReadOnlyList<string> localMigrations,
            IReadOnlyCollection<string> appliedMigrations)
        {
            ArgumentNullException.ThrowIfNull(localMigrations);
            ArgumentNullException.ThrowIfNull(appliedMigrations);

            if (!hasApplicationTables || localMigrations.Count == 0)
            {
                return false;
            }

            var firstMigration = localMigrations[0];
            return !appliedMigrations.Contains(firstMigration, StringComparer.Ordinal);
        }

        private static List<string> GetAppliedMigrationIds(MySqlConnection connection)
        {
            using var command = new MySqlCommand(
                """
                SELECT `MigrationId`
                FROM `__EFMigrationsHistory`
                ORDER BY `MigrationId`;
                """,
                connection);

            var appliedMigrations = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    appliedMigrations.Add(reader.GetString(0));
                }
            }

            return appliedMigrations;
        }

        private static void BaselineMigrationsHistory(MySqlConnection connection, IReadOnlyList<string> localMigrations)
        {
            ArgumentNullException.ThrowIfNull(localMigrations);

            if (localMigrations.Count == 0)
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

            foreach (var migrationId in localMigrations)
            {
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

                insertCommand.Parameters.AddWithValue("@migrationId", migrationId);
                insertCommand.Parameters.AddWithValue("@productVersion", GetProductVersion());
                insertCommand.ExecuteNonQuery();
            }
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

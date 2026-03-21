using MySqlConnector;
using System.Data;

namespace AttendanceShiftingManagement.Services
{
    public sealed class DatabaseTableReviewResult
    {
        public string TableName { get; init; } = string.Empty;
        public bool ExistsInRemote { get; init; }
        public bool ExistsInLocal { get; init; }
        public int RemoteRowCount { get; init; }
        public int LocalRowCount { get; init; }
        public IReadOnlyList<string> ColumnNames { get; init; } = Array.Empty<string>();
        public DataTable PreviewRows { get; init; } = new();
    }

    public sealed class DatabaseTableSyncResult
    {
        public bool IsSuccess { get; init; }
        public int CopiedRowCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public static class DatabaseTableReviewService
    {
        public static async Task<IReadOnlyList<string>> ListTablesAsync(
            DatabaseConnectionPreset preset,
            CancellationToken cancellationToken = default)
        {
            ValidatePreset(preset, nameof(preset));

            const string sql =
                """
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = @databaseName
                  AND table_type = 'BASE TABLE'
                ORDER BY table_name;
                """;

            var tables = new List<string>();

            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(preset));
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@databaseName", preset.Database);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        public static async Task<DatabaseTableReviewResult> ReviewTableAsync(
            DatabaseConnectionPreset sourcePreset,
            DatabaseConnectionPreset localPreset,
            string tableName,
            int previewLimit = 100,
            CancellationToken cancellationToken = default)
        {
            ValidatePreset(sourcePreset, nameof(sourcePreset));
            ValidatePreset(localPreset, nameof(localPreset));
            ValidateTableName(tableName);

            await using var sourceConnection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(sourcePreset));
            await sourceConnection.OpenAsync(cancellationToken);

            var existsInRemote = await TableExistsAsync(sourceConnection, sourcePreset.Database, tableName, cancellationToken);
            if (!existsInRemote)
            {
                return new DatabaseTableReviewResult
                {
                    TableName = tableName,
                    ExistsInRemote = false,
                    ExistsInLocal = false
                };
            }

            await EnsureDatabaseExistsAsync(localPreset, cancellationToken);

            await using var localConnection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(localPreset));
            await localConnection.OpenAsync(cancellationToken);

            var existsInLocal = await TableExistsAsync(localConnection, localPreset.Database, tableName, cancellationToken);
            var remoteRowCount = await GetRowCountAsync(sourceConnection, tableName, cancellationToken);
            var localRowCount = existsInLocal
                ? await GetRowCountAsync(localConnection, tableName, cancellationToken)
                : 0;

            var previewRows = await LoadPreviewRowsAsync(sourceConnection, tableName, previewLimit, cancellationToken);

            return new DatabaseTableReviewResult
            {
                TableName = tableName,
                ExistsInRemote = true,
                ExistsInLocal = existsInLocal,
                RemoteRowCount = remoteRowCount,
                LocalRowCount = localRowCount,
                ColumnNames = previewRows.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList(),
                PreviewRows = previewRows
            };
        }

        public static async Task<DatabaseTableSyncResult> SyncTableToLocalAsync(
            DatabaseConnectionPreset sourcePreset,
            DatabaseConnectionPreset localPreset,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidatePreset(sourcePreset, nameof(sourcePreset));
                ValidatePreset(localPreset, nameof(localPreset));
                ValidateTableName(tableName);

                if (ConnectionsMatch(sourcePreset, localPreset))
                {
                    return new DatabaseTableSyncResult
                    {
                        IsSuccess = false,
                        Message = "Source and active target are the same database."
                    };
                }

                await EnsureDatabaseExistsAsync(localPreset, cancellationToken);

                await using var sourceConnection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(sourcePreset));
                await sourceConnection.OpenAsync(cancellationToken);

                var sourceTableExists = await TableExistsAsync(sourceConnection, sourcePreset.Database, tableName, cancellationToken);
                if (!sourceTableExists)
                {
                    return new DatabaseTableSyncResult
                    {
                        IsSuccess = false,
                        Message = $"Source table `{tableName}` was not found in {sourcePreset.Database}."
                    };
                }

                var createTableSql = await GetCreateTableSqlAsync(sourceConnection, tableName, cancellationToken);
                var insertableColumns = await GetInsertableColumnsAsync(sourceConnection, tableName, cancellationToken);

                await using var targetConnection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(localPreset));
                await targetConnection.OpenAsync(cancellationToken);
                await using var transaction = await targetConnection.BeginTransactionAsync(cancellationToken);

                await ExecuteNonQueryAsync(
                    targetConnection,
                    transaction,
                    $"DROP TABLE IF EXISTS `{EscapeIdentifier(tableName)}`;",
                    cancellationToken);

                await ExecuteNonQueryAsync(
                    targetConnection,
                    transaction,
                    createTableSql,
                    cancellationToken);

                var copiedRowCount = await CopyRowsAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    tableName,
                    insertableColumns,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new DatabaseTableSyncResult
                {
                    IsSuccess = true,
                    CopiedRowCount = copiedRowCount,
                    Message = $"Synced {copiedRowCount} row(s) from remote `{tableName}` into `{localPreset.Database}`."
                };
            }
            catch (Exception ex)
            {
                return new DatabaseTableSyncResult
                {
                    IsSuccess = false,
                    Message = $"Table sync failed: {ex.Message}"
                };
            }
        }

        private static async Task<int> CopyRowsAsync(
            MySqlConnection sourceConnection,
            MySqlConnection targetConnection,
            MySqlTransaction transaction,
            string tableName,
            IReadOnlyList<string> columnNames,
            CancellationToken cancellationToken)
        {
            var quotedColumns = string.Join(", ", columnNames.Select(column => $"`{EscapeIdentifier(column)}`"));
            var selectSql = $"SELECT {quotedColumns} FROM `{EscapeIdentifier(tableName)}`;";
            var insertSql = BuildInsertSql(tableName, columnNames);

            await using var selectCommand = new MySqlCommand(selectSql, sourceConnection);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            await using var insertCommand = new MySqlCommand(insertSql, targetConnection, transaction);

            var copiedRows = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                insertCommand.Parameters.Clear();

                for (var index = 0; index < columnNames.Count; index++)
                {
                    var parameter = insertCommand.Parameters.AddWithValue($"@p{index}", reader.IsDBNull(index) ? DBNull.Value : reader.GetValue(index));
                    parameter.IsNullable = true;
                }

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                copiedRows++;
            }

            return copiedRows;
        }

        private static string BuildInsertSql(string tableName, IReadOnlyList<string> columnNames)
        {
            var quotedColumns = string.Join(", ", columnNames.Select(column => $"`{EscapeIdentifier(column)}`"));
            var parameterNames = string.Join(", ", Enumerable.Range(0, columnNames.Count).Select(index => $"@p{index}"));
            return $"INSERT INTO `{EscapeIdentifier(tableName)}` ({quotedColumns}) VALUES ({parameterNames});";
        }

        private static async Task<DataTable> LoadPreviewRowsAsync(
            MySqlConnection connection,
            string tableName,
            int previewLimit,
            CancellationToken cancellationToken)
        {
            var previewTable = new DataTable();
            var safeLimit = Math.Clamp(previewLimit, 1, 250);

            await using var command = new MySqlCommand(
                $"SELECT * FROM `{EscapeIdentifier(tableName)}` LIMIT {safeLimit};",
                connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            for (var index = 0; index < reader.FieldCount; index++)
            {
                previewTable.Columns.Add(reader.GetName(index), typeof(string));
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = previewTable.NewRow();
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[index] = reader.IsDBNull(index) ? string.Empty : reader.GetValue(index)?.ToString() ?? string.Empty;
                }

                previewTable.Rows.Add(row);
            }

            return previewTable;
        }

        private static async Task<int> GetRowCountAsync(
            MySqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand(
                $"SELECT COUNT(*) FROM `{EscapeIdentifier(tableName)}`;",
                connection);

            var count = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(count);
        }

        private static async Task<string> GetCreateTableSqlAsync(
            MySqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand(
                $"SHOW CREATE TABLE `{EscapeIdentifier(tableName)}`;",
                connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Unable to read schema for `{tableName}`.");
            }

            return reader.GetString(1);
        }

        private static async Task<IReadOnlyList<string>> GetInsertableColumnsAsync(
            MySqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            var columns = new List<string>();

            await using var command = new MySqlCommand(
                $"SHOW COLUMNS FROM `{EscapeIdentifier(tableName)}`;",
                connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var fieldOrdinal = reader.GetOrdinal("Field");
            var extraOrdinal = reader.GetOrdinal("Extra");

            while (await reader.ReadAsync(cancellationToken))
            {
                var fieldName = reader.GetString(fieldOrdinal);
                var extra = reader.IsDBNull(extraOrdinal) ? string.Empty : reader.GetString(extraOrdinal);
                if (extra.Contains("GENERATED", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                columns.Add(fieldName);
            }

            return columns;
        }

        private static async Task EnsureDatabaseExistsAsync(
            DatabaseConnectionPreset preset,
            CancellationToken cancellationToken)
        {
            var builder = new MySqlConnectionStringBuilder(ConnectionSettingsService.BuildConnectionString(preset))
            {
                Database = string.Empty
            };

            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var databaseName = EscapeIdentifier(preset.Database);
            await using var command = new MySqlCommand(
                $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<bool> TableExistsAsync(
            MySqlConnection connection,
            string databaseName,
            string tableName,
            CancellationToken cancellationToken)
        {
            const string sql =
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @databaseName
                  AND table_name = @tableName;
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@databaseName", databaseName);
            command.Parameters.AddWithValue("@tableName", tableName);

            var count = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(count) > 0;
        }

        private static async Task ExecuteNonQueryAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void ValidatePreset(DatabaseConnectionPreset preset, string parameterName)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(preset.Server))
            {
                throw new InvalidOperationException($"The preset `{parameterName}` is missing a server value.");
            }

            if (string.IsNullOrWhiteSpace(preset.Database))
            {
                throw new InvalidOperationException($"The preset `{parameterName}` is missing a database name.");
            }

            if (string.IsNullOrWhiteSpace(preset.Username))
            {
                throw new InvalidOperationException($"The preset `{parameterName}` is missing a username.");
            }
        }

        private static void ValidateTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new InvalidOperationException("Select a table first.");
            }
        }

        private static bool ConnectionsMatch(DatabaseConnectionPreset left, DatabaseConnectionPreset right)
        {
            return string.Equals(left.Server?.Trim(), right.Server?.Trim(), StringComparison.OrdinalIgnoreCase)
                && left.Port == right.Port
                && string.Equals(left.Database?.Trim(), right.Database?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Username?.Trim(), right.Username?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeIdentifier(string identifier)
        {
            return identifier.Replace("`", "``", StringComparison.Ordinal);
        }
    }
}

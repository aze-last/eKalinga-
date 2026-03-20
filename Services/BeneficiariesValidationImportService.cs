using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public sealed class BeneficiariesValidationImportResult
    {
        public bool IsSuccess { get; init; }
        public int ImportedRowCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public static class BeneficiariesValidationImportService
    {
        public const string TableName = "beneficiaries_validation";

        public static async Task<BeneficiariesValidationImportResult> ImportToLocalAsync(
            DatabaseConnectionPreset sourcePreset,
            DatabaseConnectionPreset localPreset,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidatePreset(sourcePreset, nameof(sourcePreset));
                ValidatePreset(localPreset, nameof(localPreset));

                if (ConnectionsMatch(sourcePreset, localPreset))
                {
                    return new BeneficiariesValidationImportResult
                    {
                        IsSuccess = false,
                        Message = "Source and Local target are the same database. Choose LAN or Remote as the source."
                    };
                }

                await EnsureDatabaseExistsAsync(localPreset, cancellationToken);

                await using var sourceConnection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(sourcePreset));
                await sourceConnection.OpenAsync(cancellationToken);

                var sourceTableExists = await TableExistsAsync(
                    sourceConnection,
                    sourcePreset.Database,
                    TableName,
                    cancellationToken);

                if (!sourceTableExists)
                {
                    return new BeneficiariesValidationImportResult
                    {
                        IsSuccess = false,
                        Message = $"Source table `{TableName}` was not found in {sourcePreset.Database}."
                    };
                }

                var createTableSql = await GetCreateTableSqlAsync(sourceConnection, cancellationToken);
                var insertableColumns = await GetInsertableColumnsAsync(sourceConnection, cancellationToken);
                if (insertableColumns.Count == 0)
                {
                    return new BeneficiariesValidationImportResult
                    {
                        IsSuccess = false,
                        Message = $"Source table `{TableName}` has no importable columns."
                    };
                }

                await using var targetConnection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(localPreset));
                await targetConnection.OpenAsync(cancellationToken);
                await using var transaction = await targetConnection.BeginTransactionAsync(cancellationToken);

                await ExecuteNonQueryAsync(
                    targetConnection,
                    transaction,
                    $"DROP TABLE IF EXISTS `{TableName}`;",
                    cancellationToken);

                await ExecuteNonQueryAsync(
                    targetConnection,
                    transaction,
                    createTableSql,
                    cancellationToken);

                var importedRowCount = await CopyRowsAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    insertableColumns,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BeneficiariesValidationImportResult
                {
                    IsSuccess = true,
                    ImportedRowCount = importedRowCount,
                    Message = $"Imported {importedRowCount} row(s) from {sourcePreset.Database}.{TableName} into the Local database {localPreset.Database}."
                };
            }
            catch (Exception ex)
            {
                return new BeneficiariesValidationImportResult
                {
                    IsSuccess = false,
                    Message = $"Import failed: {ex.Message}"
                };
            }
        }

        private static async Task<int> CopyRowsAsync(
            MySqlConnection sourceConnection,
            MySqlConnection targetConnection,
            MySqlTransaction transaction,
            IReadOnlyList<string> columnNames,
            CancellationToken cancellationToken)
        {
            var quotedColumns = string.Join(", ", columnNames.Select(column => $"`{column}`"));
            var selectSql = $"SELECT {quotedColumns} FROM `{TableName}`;";
            var insertSql = BuildInsertSql(columnNames);

            await using var selectCommand = new MySqlCommand(selectSql, sourceConnection);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            await using var insertCommand = new MySqlCommand(insertSql, targetConnection, transaction);

            var importedRows = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                insertCommand.Parameters.Clear();

                for (var index = 0; index < columnNames.Count; index++)
                {
                    var parameter = insertCommand.Parameters.AddWithValue($"@p{index}", reader.IsDBNull(index) ? DBNull.Value : reader.GetValue(index));
                    parameter.IsNullable = true;
                }

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                importedRows++;
            }

            return importedRows;
        }

        private static string BuildInsertSql(IReadOnlyList<string> columnNames)
        {
            var quotedColumns = string.Join(", ", columnNames.Select(column => $"`{column}`"));
            var parameterNames = string.Join(", ", Enumerable.Range(0, columnNames.Count).Select(index => $"@p{index}"));
            return $"INSERT INTO `{TableName}` ({quotedColumns}) VALUES ({parameterNames});";
        }

        private static async Task<string> GetCreateTableSqlAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand($"SHOW CREATE TABLE `{TableName}`;", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Unable to read schema for `{TableName}`.");
            }

            return reader.GetString(1);
        }

        private static async Task<IReadOnlyList<string>> GetInsertableColumnsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
        {
            var columns = new List<string>();

            await using var command = new MySqlCommand($"SHOW COLUMNS FROM `{TableName}`;", connection);
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

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return count > 0;
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

        private static bool ConnectionsMatch(DatabaseConnectionPreset left, DatabaseConnectionPreset right)
        {
            return string.Equals(left.Server?.Trim(), right.Server?.Trim(), StringComparison.OrdinalIgnoreCase)
                && left.Port == right.Port
                && string.Equals(left.Database?.Trim(), right.Database?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Username?.Trim(), right.Username?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new InvalidOperationException("Identifier cannot be empty.");
            }

            return identifier.Replace("`", "``", StringComparison.Ordinal);
        }
    }
}

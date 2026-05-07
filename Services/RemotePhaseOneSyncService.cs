using AttendanceShiftingManagement.Data;
using MySqlConnector;
using System.Data.Common;

namespace AttendanceShiftingManagement.Services
{
    public sealed record RemotePhaseOneTableSyncResult(string TableName, int RowCount);

    public sealed class RemotePhaseOneSyncResult
    {
        public bool IsSuccess { get; init; }
        public bool WasSkipped { get; init; }
        public string Message { get; init; } = string.Empty;
        public IReadOnlyList<RemotePhaseOneTableSyncResult> TableResults { get; init; } = Array.Empty<RemotePhaseOneTableSyncResult>();
        public int TotalRows => TableResults.Sum(item => item.RowCount);
    }

    public static class RemotePhaseOneSyncService
    {
        private const string LocalPresetKey = "Local";
        private const string RemotePresetKey = "Remote";

        private static readonly string[] DeleteOrder =
        {
            "beneficiary_assistance_ledger",
            "ayuda_project_claims",
            "cash_for_work_participants",
            "assistance_cases",
            "cash_for_work_events",
            "ayuda_project_beneficiaries",
            "budget_ledger_entries",
            "ayuda_programs"
        };

        private static readonly string[] InsertOrder =
        {
            "ayuda_programs",
            "budget_ledger_entries",
            "assistance_cases",
            "cash_for_work_events",
            "ayuda_project_beneficiaries",
            "ayuda_project_claims",
            "cash_for_work_participants",
            "beneficiary_assistance_ledger"
        };

        public static async Task<RemotePhaseOneSyncResult> SyncFromRemoteToLocalAsync(CancellationToken cancellationToken = default)
        {
            var settings = ConnectionSettingsService.Load();
            var localPreset = settings.GetPreset(LocalPresetKey);
            var remotePreset = settings.GetPreset(RemotePresetKey);

            if (!ConnectionSettingsService.IsPresetConfigured(remotePreset))
            {
                return new RemotePhaseOneSyncResult
                {
                    IsSuccess = true,
                    WasSkipped = true,
                    Message = "Remote sync skipped because the Remote app database preset is not configured."
                };
            }

            var localConnectionString = ConnectionSettingsService.BuildConnectionString(localPreset);
            var remoteConnectionString = ConnectionSettingsService.BuildConnectionString(remotePreset);
            var tableResults = new List<RemotePhaseOneTableSyncResult>();

            await using var remoteConnection = new MySqlConnection(remoteConnectionString);
            await using var localConnection = new MySqlConnection(localConnectionString);

            try
            {
                await remoteConnection.OpenAsync(cancellationToken);
                await localConnection.OpenAsync(cancellationToken);

                foreach (var tableName in InsertOrder)
                {
                    if (!await TableExistsAsync(remoteConnection, tableName, cancellationToken))
                    {
                        return new RemotePhaseOneSyncResult
                        {
                            IsSuccess = false,
                            Message = $"Remote sync failed because table `{tableName}` does not exist in the Remote database."
                        };
                    }

                    if (!await TableExistsAsync(localConnection, tableName, cancellationToken))
                    {
                        return new RemotePhaseOneSyncResult
                        {
                            IsSuccess = false,
                            Message = $"Remote sync failed because table `{tableName}` does not exist in the Local database."
                        };
                    }
                }

                await using var transaction = await localConnection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await SetForeignKeyChecksAsync(localConnection, transaction, false, cancellationToken);

                    foreach (var tableName in DeleteOrder)
                    {
                        await ExecuteNonQueryAsync(
                            localConnection,
                            transaction,
                            $"DELETE FROM `{tableName}`;",
                            cancellationToken);
                    }

                    foreach (var tableName in InsertOrder)
                    {
                        var rowCount = await CopyTableAsync(
                            remoteConnection,
                            localConnection,
                            transaction,
                            tableName,
                            cancellationToken);
                        tableResults.Add(new RemotePhaseOneTableSyncResult(tableName, rowCount));
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
                finally
                {
                    await SetForeignKeyChecksAsync(localConnection, transaction: null, enabled: true, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                return new RemotePhaseOneSyncResult
                {
                    IsSuccess = false,
                    Message = $"Remote sync failed. {ex.Message}",
                    TableResults = tableResults
                };
            }

            return new RemotePhaseOneSyncResult
            {
                IsSuccess = true,
                Message = $"Remote review sync refreshed {tableResults.Count} tables and {tableResults.Sum(item => item.RowCount)} row(s).",
                TableResults = tableResults
            };
        }

        private static async Task<int> CopyTableAsync(
            MySqlConnection remoteConnection,
            MySqlConnection localConnection,
            MySqlTransaction transaction,
            string tableName,
            CancellationToken cancellationToken)
        {
            await using var remoteCommand = new MySqlCommand($"SELECT * FROM `{tableName}`;", remoteConnection);
            await using var reader = await remoteCommand.ExecuteReaderAsync(cancellationToken);

            var columns = reader.GetColumnSchema()
                .Select(column => column.ColumnName)
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .Cast<string>()
                .ToArray();

            if (columns.Length == 0)
            {
                return 0;
            }

            var parameterNames = columns
                .Select((_, index) => $"@p{index}")
                .ToArray();

            var insertSql =
                $"INSERT INTO `{tableName}` ({string.Join(", ", columns.Select(column => $"`{column}`"))}) " +
                $"VALUES ({string.Join(", ", parameterNames)});";

            await using var localCommand = new MySqlCommand(insertSql, localConnection, transaction);
            foreach (var parameterName in parameterNames)
            {
                localCommand.Parameters.AddWithValue(parameterName, DBNull.Value);
            }

            var rowCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                for (var index = 0; index < columns.Length; index++)
                {
                    localCommand.Parameters[index].Value = await reader.IsDBNullAsync(index, cancellationToken)
                        ? DBNull.Value
                        : reader.GetValue(index);
                }

                await localCommand.ExecuteNonQueryAsync(cancellationToken);
                rowCount++;
            }

            return rowCount;
        }

        private static async Task<bool> TableExistsAsync(
            MySqlConnection connection,
            string tableName,
            CancellationToken cancellationToken)
        {
            const string sql =
                """
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName;
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tableName", tableName);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }

        private static async Task SetForeignKeyChecksAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            bool enabled,
            CancellationToken cancellationToken)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                $"SET FOREIGN_KEY_CHECKS={(enabled ? 1 : 0)};",
                cancellationToken);
        }

        private static async Task ExecuteNonQueryAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

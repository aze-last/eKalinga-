using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public sealed record GgmsBudgetSyncResult(bool IsSuccess, string Message, int? SnapshotId = null);

    public sealed class GgmsBudgetSyncService
    {
        private static readonly string[] SpentColumnCandidates = ["SpentAmount", "Spent", "Spend"];
        private readonly BudgetRuntimeOptions _options;

        public GgmsBudgetSyncService(BudgetRuntimeOptions? options = null)
        {
            _options = options ?? BudgetRuntimeOptions.Load();
        }

        public async Task<GgmsBudgetSyncResult> SyncAyudaBudgetAsync(LocalDbContext context, int recordedByUserId)
        {
            if (!HasConnectionDetails(_options.GgmsConnection))
            {
                return new GgmsBudgetSyncResult(false, "GGMS source settings are incomplete. Open Settings and complete the GGMS Budget Source connection details.");
            }

            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(_options.GgmsConnection));
            await connection.OpenAsync();

            var spentCommandText = BuildSpentAmountQuery(_options.GgmsConsolidatedTransactionTable);

            await using var spentCommand = new MySqlCommand(spentCommandText, connection);
            spentCommand.Parameters.AddWithValue("@officeCode", _options.AyudaOfficeCode);
            var spentAmount = Convert.ToDecimal(await spentCommand.ExecuteScalarAsync());

            var commandText =
                $"""
                SELECT
                    CAST(alloc.`Id` AS CHAR),
                    alloc.`YearlyBudgetId`,
                    alloc.`office_code`,
                    alloc.`AllocatedAmount`,
                    @spentAmount,
                    COALESCE(office.`name`, 'Ayuda')
                FROM `{_options.GgmsAllocationTable}` AS alloc
                LEFT JOIN `{_options.GgmsOfficeTable}` AS office
                    ON office.`office_code` = alloc.`office_code`
                WHERE alloc.`office_code` = @officeCode
                ORDER BY alloc.`YearlyBudgetId` DESC, alloc.`Id` DESC
                LIMIT 1;
                """;

            await using var command = new MySqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@officeCode", _options.AyudaOfficeCode);
            command.Parameters.AddWithValue("@spentAmount", spentAmount);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new GgmsBudgetSyncResult(false, $"No GGMS allocation row was found for office code '{_options.AyudaOfficeCode}'.");
            }

            var budgetService = new BudgetManagementService(context);
            var snapshotResult = await budgetService.RecordGovernmentSnapshotAsync(
                new GovernmentBudgetSnapshotRequest(
                    reader.GetString(2),
                    reader.GetString(5),
                    reader.GetInt32(1),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4),
                    reader.GetString(0),
                    GovernmentBudgetSyncStatus.Synced,
                    DateTime.Now),
                recordedByUserId);

            return new GgmsBudgetSyncResult(snapshotResult.IsSuccess, snapshotResult.Message, snapshotResult.SnapshotId);
        }

        public static string BuildSpentAmountQuery(string? configuredTableName)
        {
            var tableName = !string.IsNullOrWhiteSpace(configuredTableName)
                ? configuredTableName.Trim()
                : "consolidated_transactions";

            return $"""
                SELECT COALESCE(SUM(amount), 0)
                FROM `{tableName}`
                WHERE office_id = @officeCode AND status = 'Released';
                """;
        }

        private static bool HasConnectionDetails(DatabaseConnectionPreset preset)
        {
            return !string.IsNullOrWhiteSpace(preset.Server)
                && !string.IsNullOrWhiteSpace(preset.Database)
                && !string.IsNullOrWhiteSpace(preset.Username);
        }
    }
}

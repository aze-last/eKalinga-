using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public sealed record GgmsBudgetSyncResult(bool IsSuccess, string Message, int? SnapshotId = null);

    public sealed class GgmsBudgetSyncService
    {
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

            var allocation = await ReadBudgetAllocationAsync(connection, spentAmount)
                ?? await ReadLegacyAllocationAsync(connection, spentAmount);

            if (allocation == null)
            {
                return new GgmsBudgetSyncResult(false, $"No GGMS allocation row was found for office code '{_options.AyudaOfficeCode}' in '{_options.GgmsBudgetAllocationTable}' or '{_options.GgmsAllocationTable}'.");
            }

            var budgetService = new BudgetManagementService(context);
            var snapshotResult = await budgetService.RecordGovernmentSnapshotAsync(allocation, recordedByUserId);

            return new GgmsBudgetSyncResult(snapshotResult.IsSuccess, snapshotResult.Message, snapshotResult.SnapshotId);
        }

        /// <summary>
        /// Reads the newest allocation from the new-style GGMS table (budget_allocations):
        /// office resolved by joining tbl_offices.id = alloc.office_id and matching office_code.
        /// Returns null when the table has no row for the office (e.g. GGMS not migrated yet).
        /// </summary>
        private async Task<GovernmentBudgetSnapshotRequest?> ReadBudgetAllocationAsync(MySqlConnection connection, decimal spentAmount)
        {
            try
            {
                var commandText = BuildBudgetAllocationQuery(_options.GgmsBudgetAllocationTable, _options.GgmsOfficeTable);
                await using var command = new MySqlCommand(commandText, connection);
                command.Parameters.AddWithValue("@officeCode", _options.AyudaOfficeCode);

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new GovernmentBudgetSnapshotRequest(
                    reader.GetString(2),
                    reader.GetString(5),
                    reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                    reader.GetDecimal(3),
                    spentAmount,
                    reader.GetString(0),
                    GovernmentBudgetSyncStatus.Synced,
                    DateTime.Now);
            }
            catch (MySqlException)
            {
                // Table absent or schema-incompatible on this GGMS instance — fall back to legacy.
                return null;
            }
        }

        private async Task<GovernmentBudgetSnapshotRequest?> ReadLegacyAllocationAsync(MySqlConnection connection, decimal spentAmount)
        {
            var commandText = BuildLegacyAllocationQuery(_options.GgmsAllocationTable, _options.GgmsOfficeTable);
            await using var command = new MySqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@officeCode", _options.AyudaOfficeCode);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new GovernmentBudgetSnapshotRequest(
                reader.GetString(2),
                reader.GetString(5),
                reader.GetInt32(1),
                reader.GetDecimal(3),
                spentAmount,
                reader.GetString(0),
                GovernmentBudgetSyncStatus.Synced,
                DateTime.Now);
        }

        public static string BuildBudgetAllocationQuery(string? allocationTable, string? officeTable)
        {
            var allocation = NormalizeTableName(allocationTable, "budget_allocations");
            var office = NormalizeTableName(officeTable, "tbl_offices");

            return $"""
                SELECT
                    CAST(alloc.`id` AS CHAR),
                    alloc.`master_budget_id`,
                    office.`office_code`,
                    alloc.`amount`,
                    alloc.`used_amount`,
                    COALESCE(office.`name`, 'Ayuda')
                FROM `{allocation}` AS alloc
                INNER JOIN `{office}` AS office
                    ON office.`id` = alloc.`office_id`
                WHERE office.`office_code` = @officeCode
                ORDER BY alloc.`master_budget_id` DESC, alloc.`id` DESC
                LIMIT 1;
                """;
        }

        public static string BuildLegacyAllocationQuery(string? allocationTable, string? officeTable)
        {
            var allocation = NormalizeTableName(allocationTable, "officeallocations");
            var office = NormalizeTableName(officeTable, "tbl_offices");

            return $"""
                SELECT
                    CAST(alloc.`Id` AS CHAR),
                    alloc.`YearlyBudgetId`,
                    alloc.`office_code`,
                    alloc.`AllocatedAmount`,
                    alloc.`SpentAmount`,
                    COALESCE(office.`name`, 'Ayuda')
                FROM `{allocation}` AS alloc
                LEFT JOIN `{office}` AS office
                    ON office.`office_code` = alloc.`office_code`
                WHERE alloc.`office_code` = @officeCode
                ORDER BY alloc.`YearlyBudgetId` DESC, alloc.`Id` DESC
                LIMIT 1;
                """;
        }

        public static string BuildSpentAmountQuery(string? configuredTableName)
        {
            var tableName = NormalizeTableName(configuredTableName, "consolidated_transactions");

            return $"""
                SELECT COALESCE(SUM(amount), 0)
                FROM `{tableName}`
                WHERE office_id = @officeCode AND status = 'Released';
                """;
        }

        private static string NormalizeTableName(string? configured, string fallback)
        {
            return !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : fallback;
        }

        private static bool HasConnectionDetails(DatabaseConnectionPreset preset)
        {
            return !string.IsNullOrWhiteSpace(preset.Server)
                && !string.IsNullOrWhiteSpace(preset.Database)
                && !string.IsNullOrWhiteSpace(preset.Username);
        }
    }
}

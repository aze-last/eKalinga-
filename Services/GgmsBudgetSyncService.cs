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

        public async Task<GgmsBudgetSyncResult> SyncAyudaBudgetAsync(AppDbContext context, int recordedByUserId)
        {
            if (!HasConnectionDetails(_options.GgmsConnection))
            {
                return new GgmsBudgetSyncResult(false, "GGMS connection settings are incomplete. Update the Budget:GgmsConnection section in appsettings.json.");
            }

            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(_options.GgmsConnection));
            await connection.OpenAsync();

            var spentColumn = await DetectSpentColumnAsync(connection);
            var spentExpression = spentColumn == null
                ? "0"
                : $"COALESCE(alloc.`{spentColumn}`, 0)";

            var commandText =
                $"""
                SELECT
                    CAST(alloc.`Id` AS CHAR),
                    alloc.`YearlyBudgetId`,
                    alloc.`office_code`,
                    alloc.`AllocatedAmount`,
                    {spentExpression},
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

        private async Task<string?> DetectSpentColumnAsync(MySqlConnection connection)
        {
            foreach (var columnName in SpentColumnCandidates)
            {
                await using var command = new MySqlCommand(
                    """
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = @tableName
                      AND COLUMN_NAME = @columnName;
                    """,
                    connection);

                command.Parameters.AddWithValue("@tableName", _options.GgmsAllocationTable);
                command.Parameters.AddWithValue("@columnName", columnName);

                var exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                if (exists)
                {
                    return columnName;
                }
            }

            return null;
        }

        private static bool HasConnectionDetails(DatabaseConnectionPreset preset)
        {
            return !string.IsNullOrWhiteSpace(preset.Server)
                && !string.IsNullOrWhiteSpace(preset.Database)
                && !string.IsNullOrWhiteSpace(preset.Username);
        }
    }
}

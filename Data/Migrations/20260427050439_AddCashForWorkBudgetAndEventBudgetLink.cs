using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashForWorkBudgetAndEventBudgetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `cash_for_work_budgets` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `budget_code` varchar(40) NOT NULL,
                    `budget_name` varchar(150) NOT NULL,
                    `description` varchar(500) NULL,
                    `budget_cap` decimal(18,2) NULL,
                    `is_active` tinyint(1) NOT NULL,
                    `created_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_cash_for_work_budgets_budget_code` (`budget_code`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """);

            EnsureColumnExists(
                migrationBuilder,
                "cash_for_work_events",
                "cash_for_work_budget_id",
                "ALTER TABLE `cash_for_work_events` ADD COLUMN `cash_for_work_budget_id` int NULL;");

            EnsureColumnExists(
                migrationBuilder,
                "budget_ledger_entries",
                "cash_for_work_budget_id",
                "ALTER TABLE `budget_ledger_entries` ADD COLUMN `cash_for_work_budget_id` int NULL;");

            migrationBuilder.Sql("""
INSERT INTO cash_for_work_budgets (budget_code, budget_name, description, budget_cap, is_active, created_by_user_id, created_at, updated_at)
SELECT CONCAT('LEGACY-', ap.id),
       ap.program_name,
       ap.description,
       ap.budget_cap,
       ap.is_active,
       ap.created_by_user_id,
       ap.created_at,
       ap.updated_at
FROM ayuda_programs ap
WHERE ap.program_type = 'CashForWork';
""");

            migrationBuilder.Sql("""
UPDATE cash_for_work_events cfe
INNER JOIN ayuda_programs ap ON ap.id = cfe.ayuda_program_id AND ap.program_type = 'CashForWork'
INNER JOIN cash_for_work_budgets cfb ON cfb.budget_code COLLATE utf8mb4_unicode_ci = CONCAT('LEGACY-', ap.id) COLLATE utf8mb4_unicode_ci
SET cfe.cash_for_work_budget_id = cfb.id;
""");

            migrationBuilder.Sql("""
UPDATE budget_ledger_entries ble
INNER JOIN ayuda_programs ap ON ap.id = ble.program_id AND ap.program_type = 'CashForWork'
INNER JOIN cash_for_work_budgets cfb ON cfb.budget_code COLLATE utf8mb4_unicode_ci = CONCAT('LEGACY-', ap.id) COLLATE utf8mb4_unicode_ci
SET ble.cash_for_work_budget_id = cfb.id
WHERE ble.entry_type = 'Release' AND ble.feature_source = 'CashForWork';
""");

            EnsureIndexExists(
                migrationBuilder,
                "cash_for_work_events",
                "IX_cash_for_work_events_cash_for_work_budget_id",
                "CREATE INDEX `IX_cash_for_work_events_cash_for_work_budget_id` ON `cash_for_work_events` (`cash_for_work_budget_id`);");

            EnsureIndexExists(
                migrationBuilder,
                "cash_for_work_budgets",
                "IX_cash_for_work_budgets_budget_code",
                "CREATE UNIQUE INDEX `IX_cash_for_work_budgets_budget_code` ON `cash_for_work_budgets` (`budget_code`);");

            EnsureForeignKeyExists(
                migrationBuilder,
                "cash_for_work_events",
                "FK_cash_for_work_events_cash_for_work_budgets_cash_for_work_bud~",
                "ALTER TABLE `cash_for_work_events` ADD CONSTRAINT `FK_cash_for_work_events_cash_for_work_budgets_cash_for_work_bud~` FOREIGN KEY (`cash_for_work_budget_id`) REFERENCES `cash_for_work_budgets` (`id`) ON DELETE SET NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `cash_for_work_events` DROP FOREIGN KEY IF EXISTS `FK_cash_for_work_events_cash_for_work_budgets_cash_for_work_bud~`;");
            migrationBuilder.DropTable(
                name: "cash_for_work_budgets");

            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_cash_for_work_events_cash_for_work_budget_id` ON `cash_for_work_events`;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_cash_for_work_budgets_budget_code` ON `cash_for_work_budgets`;");
            migrationBuilder.Sql("ALTER TABLE `cash_for_work_events` DROP COLUMN IF EXISTS `cash_for_work_budget_id`;");
            migrationBuilder.Sql("ALTER TABLE `budget_ledger_entries` DROP COLUMN IF EXISTS `cash_for_work_budget_id`;");
        }

        private static void EnsureColumnExists(MigrationBuilder migrationBuilder, string tableName, string columnName, string alterStatement)
        {
            migrationBuilder.Sql($$"""
            SET @column_exists := (
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = '{{tableName}}'
                  AND COLUMN_NAME = '{{columnName}}'
            );
            SET @sql := IF(@column_exists = 0, '{{EscapeSqlLiteral(alterStatement)}}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
        }

        private static void EnsureIndexExists(MigrationBuilder migrationBuilder, string tableName, string indexName, string createIndexStatement)
        {
            migrationBuilder.Sql($$"""
            SET @index_exists := (
                SELECT COUNT(*)
                FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = '{{tableName}}'
                  AND INDEX_NAME = '{{indexName}}'
            );
            SET @sql := IF(@index_exists = 0, '{{EscapeSqlLiteral(createIndexStatement)}}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
        }

        private static void EnsureForeignKeyExists(MigrationBuilder migrationBuilder, string tableName, string foreignKeyName, string createForeignKeyStatement)
        {
            migrationBuilder.Sql($$"""
            SET @fk_exists := (
                SELECT COUNT(*)
                FROM information_schema.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = '{{tableName}}'
                  AND CONSTRAINT_NAME = '{{foreignKeyName}}'
                  AND CONSTRAINT_TYPE = 'FOREIGN KEY'
            );
            SET @sql := IF(@fk_exists = 0, '{{EscapeSqlLiteral(createForeignKeyStatement)}}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
        }

        private static string EscapeSqlLiteral(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistanceCaseBudgetAndDistributionBeneficiaryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `assistance_case_budgets` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `budget_code` varchar(40) NOT NULL,
                    `budget_name` varchar(150) NOT NULL,
                    `description` varchar(500) NULL,
                    `assistance_type` varchar(120) NULL,
                    `budget_cap` decimal(18,2) NULL,
                    `is_active` tinyint(1) NOT NULL,
                    `created_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_assistance_case_budgets_budget_code` (`budget_code`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """);

            EnsureColumnExists(
                migrationBuilder,
                "budget_ledger_entries",
                "assistance_case_budget_id",
                "ALTER TABLE `budget_ledger_entries` ADD COLUMN `assistance_case_budget_id` int NULL;");

            EnsureColumnExists(
                migrationBuilder,
                "ayuda_project_beneficiaries",
                "status",
                "ALTER TABLE `ayuda_project_beneficiaries` ADD COLUMN `status` longtext NOT NULL DEFAULT 'Pending';");

            EnsureColumnExists(
                migrationBuilder,
                "ayuda_project_beneficiaries",
                "status_reason",
                "ALTER TABLE `ayuda_project_beneficiaries` ADD COLUMN `status_reason` varchar(1000) NULL;");

            EnsureColumnExists(
                migrationBuilder,
                "ayuda_project_beneficiaries",
                "status_updated_at",
                "ALTER TABLE `ayuda_project_beneficiaries` ADD COLUMN `status_updated_at` datetime(6) NULL;");

            EnsureColumnExists(
                migrationBuilder,
                "ayuda_project_beneficiaries",
                "status_updated_by_user_id",
                "ALTER TABLE `ayuda_project_beneficiaries` ADD COLUMN `status_updated_by_user_id` int NULL;");

            EnsureColumnExists(
                migrationBuilder,
                "assistance_cases",
                "assistance_case_budget_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `assistance_case_budget_id` int NULL;");

            EnsureIndexExists(
                migrationBuilder,
                "assistance_cases",
                "IX_assistance_cases_assistance_case_budget_id",
                "CREATE INDEX `IX_assistance_cases_assistance_case_budget_id` ON `assistance_cases` (`assistance_case_budget_id`);");

            EnsureIndexExists(
                migrationBuilder,
                "assistance_case_budgets",
                "IX_assistance_case_budgets_budget_code",
                "CREATE UNIQUE INDEX `IX_assistance_case_budgets_budget_code` ON `assistance_case_budgets` (`budget_code`);");

            migrationBuilder.Sql(
                """
                INSERT INTO assistance_case_budgets
                    (budget_code, budget_name, description, assistance_type, budget_cap, is_active, created_by_user_id, created_at, updated_at)
                SELECT
                    CONCAT('ACB-', LPAD(ayuda_programs.id, 4, '0')),
                    program_name,
                    description,
                    assistance_type,
                    budget_cap,
                    is_active,
                    created_by_user_id,
                    created_at,
                    updated_at
                FROM ayuda_programs
                WHERE program_type = 'AssistanceCase'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM assistance_case_budgets budget
                      WHERE budget.budget_code COLLATE utf8mb4_unicode_ci = CONCAT('ACB-', LPAD(ayuda_programs.id, 4, '0')) COLLATE utf8mb4_unicode_ci
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE assistance_cases assistance_case
                INNER JOIN ayuda_programs program
                    ON program.id = assistance_case.ayuda_program_id
                   AND program.program_type = 'AssistanceCase'
                INNER JOIN assistance_case_budgets budget
                    ON budget.budget_code COLLATE utf8mb4_unicode_ci = CONCAT('ACB-', LPAD(program.id, 4, '0')) COLLATE utf8mb4_unicode_ci
                SET assistance_case.assistance_case_budget_id = budget.id
                WHERE assistance_case.assistance_case_budget_id IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE budget_ledger_entries ledger
                INNER JOIN assistance_cases assistance_case
                    ON assistance_case.budget_ledger_entry_id = ledger.id
                SET ledger.assistance_case_budget_id = assistance_case.assistance_case_budget_id
                WHERE ledger.feature_source = 'AssistanceCase'
                  AND assistance_case.assistance_case_budget_id IS NOT NULL;
                """);

            EnsureForeignKeyExists(
                migrationBuilder,
                "assistance_cases",
                "FK_assistance_cases_assistance_case_budgets_assistance_case_bud~",
                "ALTER TABLE `assistance_cases` ADD CONSTRAINT `FK_assistance_cases_assistance_case_budgets_assistance_case_bud~` FOREIGN KEY (`assistance_case_budget_id`) REFERENCES `assistance_case_budgets` (`id`) ON DELETE SET NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `assistance_cases` DROP FOREIGN KEY IF EXISTS `FK_assistance_cases_assistance_case_budgets_assistance_case_bud~`;");
            migrationBuilder.DropTable(
                name: "assistance_case_budgets");

            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_assistance_cases_assistance_case_budget_id` ON `assistance_cases`;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_assistance_case_budgets_budget_code` ON `assistance_case_budgets`;");
            migrationBuilder.Sql("ALTER TABLE `budget_ledger_entries` DROP COLUMN IF EXISTS `assistance_case_budget_id`;");
            migrationBuilder.Sql("ALTER TABLE `ayuda_project_beneficiaries` DROP COLUMN IF EXISTS `status`;");
            migrationBuilder.Sql("ALTER TABLE `ayuda_project_beneficiaries` DROP COLUMN IF EXISTS `status_reason`;");
            migrationBuilder.Sql("ALTER TABLE `ayuda_project_beneficiaries` DROP COLUMN IF EXISTS `status_updated_at`;");
            migrationBuilder.Sql("ALTER TABLE `ayuda_project_beneficiaries` DROP COLUMN IF EXISTS `status_updated_by_user_id`;");
            migrationBuilder.Sql("ALTER TABLE `assistance_cases` DROP COLUMN IF EXISTS `assistance_case_budget_id`;");
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

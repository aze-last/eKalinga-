using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncIdToAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `ayuda_project_budget_sources` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `ayuda_program_id` int NOT NULL,
                    `budget_bucket_id` int NOT NULL,
                    `budget_bucket_type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `priority` int NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_ayuda_project_budget_sources_ayuda_program_id_priority` (`ayuda_program_id`, `priority`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

                CREATE TABLE IF NOT EXISTS `user_permissions` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `user_id` int NOT NULL,
                    `can_access_dashboard` tinyint(1) NOT NULL,
                    `can_access_master_list` tinyint(1) NOT NULL,
                    `can_access_assistance_cases` tinyint(1) NOT NULL,
                    `can_access_budget` tinyint(1) NOT NULL,
                    `can_access_distribution` tinyint(1) NOT NULL,
                    `can_access_cash_for_work` tinyint(1) NOT NULL,
                    `can_access_borrowing` tinyint(1) NOT NULL,
                    `can_access_reports` tinyint(1) NOT NULL,
                    `can_access_ggms_transactions` tinyint(1) NOT NULL,
                    `can_access_app_database` tinyint(1) NOT NULL,
                    `can_access_ggms_budget_source` tinyint(1) NOT NULL,
                    `can_access_scanning_portal` tinyint(1) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_user_permissions_user_id` (`user_id`),
                    CONSTRAINT `FK_user_permissions_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "users",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "user_profiles",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "user_permissions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "scanner_sessions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "scanner_sessions",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "private_donations",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "households",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "household_members",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "government_budget_snapshots",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "cash_for_work_participants",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "cash_for_work_participants",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "cash_for_work_events",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "cash_for_work_budgets",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "cash_for_work_attendance",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "cash_for_work_attendance",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "budget_ledger_entries",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "budget_ledger_entries",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "BeneficiaryStaging",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BeneficiaryStaging",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "beneficiary_digital_ids",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "beneficiary_digital_ids",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "beneficiary_assistance_ledger",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "beneficiary_assistance_ledger",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "ayuda_project_claims",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "ayuda_project_budget_sources",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "ayuda_project_beneficiaries",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "ayuda_programs",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "assistance_cases",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "assistance_case_budgets",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "activity_logs",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "activity_logs",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "scanner_sessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "scanner_sessions");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "private_donations");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "households");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "household_members");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "government_budget_snapshots");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "cash_for_work_participants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "cash_for_work_participants");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "cash_for_work_events");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "cash_for_work_attendance");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "cash_for_work_attendance");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "budget_ledger_entries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "budget_ledger_entries");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "BeneficiaryStaging");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BeneficiaryStaging");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "beneficiary_digital_ids");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "beneficiary_digital_ids");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "beneficiary_assistance_ledger");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "beneficiary_assistance_ledger");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "ayuda_project_claims");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "ayuda_project_budget_sources");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "ayuda_project_beneficiaries");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "assistance_cases");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "assistance_case_budgets");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "activity_logs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "activity_logs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetManagementSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ayuda_program_id",
                table: "cash_for_work_events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "budget_ledger_entry_id",
                table: "cash_for_work_events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "release_amount",
                table: "cash_for_work_events",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "released_at",
                table: "cash_for_work_events",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ayuda_program_id",
                table: "assistance_cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "budget_ledger_entry_id",
                table: "assistance_cases",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ayuda_programs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    program_code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    program_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    program_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_programs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "budget_ledger_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    entry_type = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    feature_source = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_record_id = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    program_id = table.Column<int>(type: "int", nullable: true),
                    recipient_count = table.Column<int>(type: "int", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    government_portion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    private_portion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    entry_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    remarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recorded_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_ledger_entries", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "government_budget_snapshots",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    office_code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    office_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    yearly_budget_id = table.Column<int>(type: "int", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    spent_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    source_row_id = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sync_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    synced_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_government_budget_snapshots", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "private_donations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    donor_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    donor_name = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_received = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    reference_number = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    remarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    proof_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    proof_reference_number = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    proof_file_path = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_private_donations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_events_ayuda_program_id",
                table: "cash_for_work_events",
                column: "ayuda_program_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_events_budget_ledger_entry_id",
                table: "cash_for_work_events",
                column: "budget_ledger_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_ayuda_program_id",
                table: "assistance_cases",
                column: "ayuda_program_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_budget_ledger_entry_id",
                table: "assistance_cases",
                column: "budget_ledger_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_programs_program_code",
                table: "ayuda_programs",
                column: "program_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_feature_source_source_record_id_entry_~",
                table: "budget_ledger_entries",
                columns: new[] { "feature_source", "source_record_id", "entry_type" });

            migrationBuilder.AddForeignKey(
                name: "FK_assistance_cases_ayuda_programs_ayuda_program_id",
                table: "assistance_cases",
                column: "ayuda_program_id",
                principalTable: "ayuda_programs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_assistance_cases_budget_ledger_entries_budget_ledger_entry_id",
                table: "assistance_cases",
                column: "budget_ledger_entry_id",
                principalTable: "budget_ledger_entries",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_for_work_events_ayuda_programs_ayuda_program_id",
                table: "cash_for_work_events",
                column: "ayuda_program_id",
                principalTable: "ayuda_programs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_for_work_events_budget_ledger_entries_budget_ledger_ent~",
                table: "cash_for_work_events",
                column: "budget_ledger_entry_id",
                principalTable: "budget_ledger_entries",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assistance_cases_ayuda_programs_ayuda_program_id",
                table: "assistance_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_assistance_cases_budget_ledger_entries_budget_ledger_entry_id",
                table: "assistance_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_for_work_events_ayuda_programs_ayuda_program_id",
                table: "cash_for_work_events");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_for_work_events_budget_ledger_entries_budget_ledger_ent~",
                table: "cash_for_work_events");

            migrationBuilder.DropTable(
                name: "ayuda_programs");

            migrationBuilder.DropTable(
                name: "budget_ledger_entries");

            migrationBuilder.DropTable(
                name: "government_budget_snapshots");

            migrationBuilder.DropTable(
                name: "private_donations");

            migrationBuilder.DropIndex(
                name: "IX_cash_for_work_events_ayuda_program_id",
                table: "cash_for_work_events");

            migrationBuilder.DropIndex(
                name: "IX_cash_for_work_events_budget_ledger_entry_id",
                table: "cash_for_work_events");

            migrationBuilder.DropIndex(
                name: "IX_assistance_cases_ayuda_program_id",
                table: "assistance_cases");

            migrationBuilder.DropIndex(
                name: "IX_assistance_cases_budget_ledger_entry_id",
                table: "assistance_cases");

            migrationBuilder.DropColumn(
                name: "ayuda_program_id",
                table: "cash_for_work_events");

            migrationBuilder.DropColumn(
                name: "budget_ledger_entry_id",
                table: "cash_for_work_events");

            migrationBuilder.DropColumn(
                name: "release_amount",
                table: "cash_for_work_events");

            migrationBuilder.DropColumn(
                name: "released_at",
                table: "cash_for_work_events");

            migrationBuilder.DropColumn(
                name: "ayuda_program_id",
                table: "assistance_cases");

            migrationBuilder.DropColumn(
                name: "budget_ledger_entry_id",
                table: "assistance_cases");
        }
    }
}

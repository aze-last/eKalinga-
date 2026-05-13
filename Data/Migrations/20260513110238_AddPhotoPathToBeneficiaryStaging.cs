using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoPathToBeneficiaryStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "BeneficiaryStaging",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_assistance_case_budget_id",
                table: "budget_ledger_entries",
                column: "assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_cash_for_work_budget_id",
                table: "budget_ledger_entries",
                column: "cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_program_id",
                table: "budget_ledger_entries",
                column: "program_id");

            migrationBuilder.AddForeignKey(
                name: "FK_budget_ledger_entries_assistance_case_budgets_assistance_cas~",
                table: "budget_ledger_entries",
                column: "assistance_case_budget_id",
                principalTable: "assistance_case_budgets",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_budget_ledger_entries_ayuda_programs_program_id",
                table: "budget_ledger_entries",
                column: "program_id",
                principalTable: "ayuda_programs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_budget_ledger_entries_cash_for_work_budgets_cash_for_work_bu~",
                table: "budget_ledger_entries",
                column: "cash_for_work_budget_id",
                principalTable: "cash_for_work_budgets",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_budget_ledger_entries_assistance_case_budgets_assistance_cas~",
                table: "budget_ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_budget_ledger_entries_ayuda_programs_program_id",
                table: "budget_ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_budget_ledger_entries_cash_for_work_budgets_cash_for_work_bu~",
                table: "budget_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_budget_ledger_entries_assistance_case_budget_id",
                table: "budget_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_budget_ledger_entries_cash_for_work_budget_id",
                table: "budget_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_budget_ledger_entries_program_id",
                table: "budget_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "BeneficiaryStaging");
        }
    }
}

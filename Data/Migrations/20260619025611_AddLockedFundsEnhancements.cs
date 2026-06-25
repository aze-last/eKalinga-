using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLockedFundsEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "target_assistance_case_budget_id",
                table: "private_donations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_cash_for_work_budget_id",
                table: "private_donations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_program_id",
                table: "private_donations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_assistance_case_budget_id",
                table: "government_budget_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_cash_for_work_budget_id",
                table: "government_budget_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_program_id",
                table: "government_budget_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_private_donations_target_assistance_case_budget_id",
                table: "private_donations",
                column: "target_assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_private_donations_target_cash_for_work_budget_id",
                table: "private_donations",
                column: "target_cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_private_donations_target_program_id",
                table: "private_donations",
                column: "target_program_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrivateDonation_SingleTarget",
                table: "private_donations",
                sql: "(target_program_id IS NOT NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL) OR (target_program_id IS NULL AND target_assistance_case_budget_id IS NOT NULL AND target_cash_for_work_budget_id IS NULL) OR (target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NOT NULL) OR (target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_government_budget_snapshots_target_assistance_case_budget_id",
                table: "government_budget_snapshots",
                column: "target_assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_government_budget_snapshots_target_cash_for_work_budget_id",
                table: "government_budget_snapshots",
                column: "target_cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_government_budget_snapshots_target_program_id",
                table: "government_budget_snapshots",
                column: "target_program_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GovernmentBudgetSnapshot_SingleTarget",
                table: "government_budget_snapshots",
                sql: "(target_program_id IS NOT NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL) OR (target_program_id IS NULL AND target_assistance_case_budget_id IS NOT NULL AND target_cash_for_work_budget_id IS NULL) OR (target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NOT NULL) OR (target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_government_budget_snapshots_assistance_case_budgets_target_a~",
                table: "government_budget_snapshots",
                column: "target_assistance_case_budget_id",
                principalTable: "assistance_case_budgets",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_government_budget_snapshots_ayuda_programs_target_program_id",
                table: "government_budget_snapshots",
                column: "target_program_id",
                principalTable: "ayuda_programs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_government_budget_snapshots_cash_for_work_budgets_target_cas~",
                table: "government_budget_snapshots",
                column: "target_cash_for_work_budget_id",
                principalTable: "cash_for_work_budgets",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_private_donations_assistance_case_budgets_target_assistance_~",
                table: "private_donations",
                column: "target_assistance_case_budget_id",
                principalTable: "assistance_case_budgets",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_private_donations_ayuda_programs_target_program_id",
                table: "private_donations",
                column: "target_program_id",
                principalTable: "ayuda_programs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_private_donations_cash_for_work_budgets_target_cash_for_work~",
                table: "private_donations",
                column: "target_cash_for_work_budget_id",
                principalTable: "cash_for_work_budgets",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_government_budget_snapshots_assistance_case_budgets_target_a~",
                table: "government_budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_government_budget_snapshots_ayuda_programs_target_program_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_government_budget_snapshots_cash_for_work_budgets_target_cas~",
                table: "government_budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_private_donations_assistance_case_budgets_target_assistance_~",
                table: "private_donations");

            migrationBuilder.DropForeignKey(
                name: "FK_private_donations_ayuda_programs_target_program_id",
                table: "private_donations");

            migrationBuilder.DropForeignKey(
                name: "FK_private_donations_cash_for_work_budgets_target_cash_for_work~",
                table: "private_donations");

            migrationBuilder.DropIndex(
                name: "IX_private_donations_target_assistance_case_budget_id",
                table: "private_donations");

            migrationBuilder.DropIndex(
                name: "IX_private_donations_target_cash_for_work_budget_id",
                table: "private_donations");

            migrationBuilder.DropIndex(
                name: "IX_private_donations_target_program_id",
                table: "private_donations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrivateDonation_SingleTarget",
                table: "private_donations");

            migrationBuilder.DropIndex(
                name: "IX_government_budget_snapshots_target_assistance_case_budget_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_government_budget_snapshots_target_cash_for_work_budget_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_government_budget_snapshots_target_program_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GovernmentBudgetSnapshot_SingleTarget",
                table: "government_budget_snapshots");

            migrationBuilder.DropColumn(
                name: "target_assistance_case_budget_id",
                table: "private_donations");

            migrationBuilder.DropColumn(
                name: "target_cash_for_work_budget_id",
                table: "private_donations");

            migrationBuilder.DropColumn(
                name: "target_program_id",
                table: "private_donations");

            migrationBuilder.DropColumn(
                name: "target_assistance_case_budget_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropColumn(
                name: "target_cash_for_work_budget_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropColumn(
                name: "target_program_id",
                table: "government_budget_snapshots");

        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashForWorkFundingSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "daily_rate",
                table: "cash_for_work_budgets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "end_date",
                table: "cash_for_work_budgets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_donation_id",
                table: "cash_for_work_budgets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_ggms_budget_id",
                table: "cash_for_work_budgets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_project_details_id",
                table: "cash_for_work_budgets",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "start_date",
                table: "cash_for_work_budgets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_budgets_source_donation_id",
                table: "cash_for_work_budgets",
                column: "source_donation_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_budgets_source_ggms_budget_id",
                table: "cash_for_work_budgets",
                column: "source_ggms_budget_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_for_work_budgets_government_budget_snapshots_source_ggm~",
                table: "cash_for_work_budgets",
                column: "source_ggms_budget_id",
                principalTable: "government_budget_snapshots",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_cash_for_work_budgets_private_donations_source_donation_id",
                table: "cash_for_work_budgets",
                column: "source_donation_id",
                principalTable: "private_donations",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_for_work_budgets_government_budget_snapshots_source_ggm~",
                table: "cash_for_work_budgets");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_for_work_budgets_private_donations_source_donation_id",
                table: "cash_for_work_budgets");

            migrationBuilder.DropIndex(
                name: "IX_cash_for_work_budgets_source_donation_id",
                table: "cash_for_work_budgets");

            migrationBuilder.DropIndex(
                name: "IX_cash_for_work_budgets_source_ggms_budget_id",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "daily_rate",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "end_date",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "source_donation_id",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "source_ggms_budget_id",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "source_project_details_id",
                table: "cash_for_work_budgets");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "cash_for_work_budgets");
        }
    }
}

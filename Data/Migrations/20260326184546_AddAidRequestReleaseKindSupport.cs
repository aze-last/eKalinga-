using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAidRequestReleaseKindSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "release_kind",
                table: "budget_ledger_entries",
                type: "varchar(32)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "release_kind",
                table: "assistance_cases",
                type: "varchar(32)",
                nullable: false,
                defaultValue: "Cash")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                UPDATE `assistance_cases`
                SET `release_kind` = 'Cash'
                WHERE `release_kind` IS NULL
                   OR `release_kind` = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE `budget_ledger_entries`
                SET `release_kind` = 'Cash'
                WHERE `entry_type` = 'Release'
                  AND (`release_kind` IS NULL OR `release_kind` = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "release_kind",
                table: "budget_ledger_entries");

            migrationBuilder.DropColumn(
                name: "release_kind",
                table: "assistance_cases");
        }
    }
}

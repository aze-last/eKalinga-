using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDistributionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ayuda_program_id",
                table: "scanner_sessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assistance_type",
                table: "ayuda_programs",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "budget_cap",
                table: "ayuda_programs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "distribution_status",
                table: "ayuda_programs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "end_date",
                table: "ayuda_programs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_description",
                table: "ayuda_programs",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "start_date",
                table: "ayuda_programs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_amount",
                table: "ayuda_programs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ayuda_project_beneficiaries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ayuda_program_id = table.Column<int>(type: "int", nullable: false),
                    beneficiary_staging_id = table.Column<int>(type: "int", nullable: false),
                    household_id = table.Column<int>(type: "int", nullable: true),
                    household_member_id = table.Column<int>(type: "int", nullable: true),
                    beneficiary_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    civil_registry_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    full_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    added_by_user_id = table.Column<int>(type: "int", nullable: false),
                    added_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_project_beneficiaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_ayuda_project_beneficiaries_ayuda_programs_ayuda_program_id",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ayuda_project_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ayuda_program_id = table.Column<int>(type: "int", nullable: false),
                    beneficiary_staging_id = table.Column<int>(type: "int", nullable: false),
                    project_beneficiary_id = table.Column<int>(type: "int", nullable: true),
                    household_id = table.Column<int>(type: "int", nullable: true),
                    household_member_id = table.Column<int>(type: "int", nullable: true),
                    beneficiary_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    civil_registry_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    full_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    assistance_type_snapshot = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_description_snapshot = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    unit_amount_snapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    qr_payload = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    remarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claimed_by_user_id = table.Column<int>(type: "int", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_project_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_ayuda_project_claims_ayuda_programs_ayuda_program_id",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_project_beneficiaries_ayuda_program_id_beneficiary_sta~",
                table: "ayuda_project_beneficiaries",
                columns: new[] { "ayuda_program_id", "beneficiary_staging_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_project_claims_ayuda_program_id_beneficiary_staging_id",
                table: "ayuda_project_claims",
                columns: new[] { "ayuda_program_id", "beneficiary_staging_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ayuda_project_beneficiaries");

            migrationBuilder.DropTable(
                name: "ayuda_project_claims");

            migrationBuilder.DropColumn(
                name: "ayuda_program_id",
                table: "scanner_sessions");

            migrationBuilder.DropColumn(
                name: "assistance_type",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "budget_cap",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "distribution_status",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "end_date",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "item_description",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "ayuda_programs");

            migrationBuilder.DropColumn(
                name: "unit_amount",
                table: "ayuda_programs");
        }
    }
}

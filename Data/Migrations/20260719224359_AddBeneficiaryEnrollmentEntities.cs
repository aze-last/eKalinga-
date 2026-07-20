using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiaryEnrollmentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "ayuda_project_claims",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "beneficiary_community_tax_payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SyncId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    beneficiary_staging_id = table.Column<int>(type: "int", nullable: false),
                    ayuda_program_id = table.Column<int>(type: "int", nullable: true),
                    cedula_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    paid_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    paid_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beneficiary_community_tax_payments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "beneficiary_requirement_documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SyncId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    beneficiary_staging_id = table.Column<int>(type: "int", nullable: false),
                    ayuda_program_id = table.Column<int>(type: "int", nullable: true),
                    document_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    submitted_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    remarks = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beneficiary_requirement_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_beneficiary_requirement_documents_ayuda_programs_ayuda_progr~",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_beneficiary_requirement_documents_ayuda_program_id",
                table: "beneficiary_requirement_documents",
                column: "ayuda_program_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beneficiary_community_tax_payments");

            migrationBuilder.DropTable(
                name: "beneficiary_requirement_documents");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "ayuda_project_claims");
        }
    }
}

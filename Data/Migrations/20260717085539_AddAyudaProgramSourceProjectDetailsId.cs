using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Scaffolding also swept in pre-existing drift columns (private_donations goods fields,
    /// ayuda_project_claims snapshots, ayuda_programs item/source-link fields) that already
    /// exist on live databases outside the migration chain. Those operations were removed from
    /// this migration — RuntimeSchemaBootstrapper.RepairLegacySchema self-heals them instead —
    /// so only the genuinely new column is applied here.
    /// </remarks>
    public partial class AddAyudaProgramSourceProjectDetailsId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_project_details_id",
                table: "ayuda_programs",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_project_details_id",
                table: "ayuda_programs");
        }
    }
}

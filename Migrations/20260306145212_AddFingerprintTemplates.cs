using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddFingerprintTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fingerprint_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    finger_index = table.Column<int>(type: "int", nullable: false),
                    template_data = table.Column<byte[]>(type: "longblob", nullable: false),
                    template_format = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quality_score = table.Column<int>(type: "int", nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    enrolled_by_user_id = table.Column<int>(type: "int", nullable: true),
                    last_verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fingerprint_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_fingerprint_templates_users_enrolled_by_user_id",
                        column: x => x.enrolled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fingerprint_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fingerprint_templates_enrolled_by_user_id",
                table: "fingerprint_templates",
                column: "enrolled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_fingerprint_templates_is_active",
                table: "fingerprint_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_fingerprint_templates_user_id_finger_index",
                table: "fingerprint_templates",
                columns: new[] { "user_id", "finger_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fingerprint_templates");
        }
    }
}

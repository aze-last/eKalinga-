using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAidRequestValidatedBeneficiarySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET @fk_name = (
                    SELECT CONSTRAINT_NAME
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'assistance_cases'
                      AND COLUMN_NAME = 'household_id'
                      AND REFERENCED_TABLE_NAME = 'households'
                    LIMIT 1
                );
                """);
            migrationBuilder.Sql("SET @drop_fk_sql = IF(@fk_name IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `assistance_cases` DROP FOREIGN KEY `', @fk_name, '`'));");
            migrationBuilder.Sql("PREPARE aid_stmt FROM @drop_fk_sql;");
            migrationBuilder.Sql("EXECUTE aid_stmt;");
            migrationBuilder.Sql("DEALLOCATE PREPARE aid_stmt;");

            migrationBuilder.AlterColumn<int>(
                name: "household_id",
                table: "assistance_cases",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "validated_beneficiary_id",
                table: "assistance_cases",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "validated_beneficiary_name",
                table: "assistance_cases",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "validated_civil_registry_id",
                table: "assistance_cases",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                SET @fk_exists = (
                    SELECT COUNT(*)
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'assistance_cases'
                      AND COLUMN_NAME = 'household_id'
                      AND REFERENCED_TABLE_NAME = 'households'
                );
                """);
            migrationBuilder.Sql("SET @add_fk_sql = IF(@fk_exists = 0, 'ALTER TABLE `assistance_cases` ADD CONSTRAINT `FK_assistance_cases_households_household_id` FOREIGN KEY (`household_id`) REFERENCES `households` (`id`)', 'SELECT 1');");
            migrationBuilder.Sql("PREPARE aid_stmt FROM @add_fk_sql;");
            migrationBuilder.Sql("EXECUTE aid_stmt;");
            migrationBuilder.Sql("DEALLOCATE PREPARE aid_stmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET @fk_name = (
                    SELECT CONSTRAINT_NAME
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'assistance_cases'
                      AND COLUMN_NAME = 'household_id'
                      AND REFERENCED_TABLE_NAME = 'households'
                    LIMIT 1
                );
                """);
            migrationBuilder.Sql("SET @drop_fk_sql = IF(@fk_name IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `assistance_cases` DROP FOREIGN KEY `', @fk_name, '`'));");
            migrationBuilder.Sql("PREPARE aid_stmt FROM @drop_fk_sql;");
            migrationBuilder.Sql("EXECUTE aid_stmt;");
            migrationBuilder.Sql("DEALLOCATE PREPARE aid_stmt;");

            migrationBuilder.DropColumn(
                name: "validated_beneficiary_id",
                table: "assistance_cases");

            migrationBuilder.DropColumn(
                name: "validated_beneficiary_name",
                table: "assistance_cases");

            migrationBuilder.DropColumn(
                name: "validated_civil_registry_id",
                table: "assistance_cases");

            migrationBuilder.AlterColumn<int>(
                name: "household_id",
                table: "assistance_cases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.Sql(
                """
                SET @fk_exists = (
                    SELECT COUNT(*)
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'assistance_cases'
                      AND COLUMN_NAME = 'household_id'
                      AND REFERENCED_TABLE_NAME = 'households'
                );
                """);
            migrationBuilder.Sql("SET @add_fk_sql = IF(@fk_exists = 0, 'ALTER TABLE `assistance_cases` ADD CONSTRAINT `FK_assistance_cases_households_household_id` FOREIGN KEY (`household_id`) REFERENCES `households` (`id`) ON DELETE CASCADE', 'SELECT 1');");
            migrationBuilder.Sql("PREPARE aid_stmt FROM @add_fk_sql;");
            migrationBuilder.Sql("EXECUTE aid_stmt;");
            migrationBuilder.Sql("DEALLOCATE PREPARE aid_stmt;");
        }
    }
}

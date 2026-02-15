using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize(bool resetDatabase, bool migrateOnStartup)
        {
            using var context = new AppDbContext();

            if (resetDatabase)
            {
                context.Database.EnsureDeleted();
            }

            if (migrateOnStartup)
            {
                context.Database.Migrate();
            }
            else
            {
                context.Database.EnsureCreated();
            }

            EnsurePhase2Tables(context);
            DbSeeder.Seed(context);
        }

        private static void EnsurePhase2Tables(AppDbContext context)
        {
            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `recruitment_candidates` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `full_name` varchar(150) NOT NULL,
                    `email` varchar(150) NOT NULL,
                    `source` varchar(50) NOT NULL,
                    `stage` varchar(50) NOT NULL,
                    `applied_at` datetime(6) NOT NULL,
                    `interviewed_at` datetime(6) NULL,
                    `offered_at` datetime(6) NULL,
                    `hired_at` datetime(6) NULL,
                    `expected_salary` decimal(18,2) NULL,
                    `notes` varchar(500) NULL,
                    `is_active` tinyint(1) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`)
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `employee_exits` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `employee_id` int NOT NULL,
                    `exit_type` varchar(50) NOT NULL,
                    `is_voluntary` tinyint(1) NOT NULL,
                    `reason` varchar(500) NOT NULL,
                    `last_working_date` datetime(6) NOT NULL,
                    `recorded_by` int NOT NULL,
                    `notes` varchar(500) NULL,
                    `recorded_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_employee_exits_employee_id` (`employee_id`),
                    CONSTRAINT `FK_employee_exits_employees_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_employee_exits_users_recorded_by` FOREIGN KEY (`recorded_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT
                );
                """);

        }
    }
}

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
            EnsurePhase3Tables(context);
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

        private static void EnsurePhase3Tables(AppDbContext context)
        {
            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `performance_goals` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `employee_id` int NOT NULL,
                    `goal_title` varchar(200) NOT NULL,
                    `completion_percent` decimal(5,2) NOT NULL,
                    `review_score` decimal(5,2) NULL,
                    `manager_feedback_score` decimal(5,2) NULL,
                    `due_date` datetime(6) NOT NULL,
                    `status` varchar(50) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_performance_goals_employee_id` (`employee_id`),
                    CONSTRAINT `FK_performance_goals_employees_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE CASCADE
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `training_records` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `employee_id` int NOT NULL,
                    `training_name` varchar(200) NOT NULL,
                    `is_mandatory` tinyint(1) NOT NULL,
                    `assigned_at` datetime(6) NOT NULL,
                    `due_date` datetime(6) NULL,
                    `completed_at` datetime(6) NULL,
                    `effectiveness_score` decimal(5,2) NULL,
                    `status` varchar(50) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_training_records_employee_id` (`employee_id`),
                    CONSTRAINT `FK_training_records_employees_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE CASCADE
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `engagement_surveys` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `employee_id` int NOT NULL,
                    `survey_date` datetime(6) NOT NULL,
                    `enps_score` int NOT NULL,
                    `engagement_score` decimal(5,2) NOT NULL,
                    `wellbeing_score` decimal(5,2) NOT NULL,
                    `burnout_risk` varchar(50) NOT NULL,
                    `comments` varchar(500) NULL,
                    `created_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_engagement_surveys_employee_id` (`employee_id`),
                    INDEX `IX_engagement_surveys_survey_date` (`survey_date`),
                    CONSTRAINT `FK_engagement_surveys_employees_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE CASCADE
                );
                """);
        }
    }
}

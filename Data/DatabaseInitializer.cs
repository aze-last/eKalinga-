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

            EnsureBaseSchema(context, migrateOnStartup);

            EnsurePhase2Tables(context);
            EnsurePhase3Tables(context);
            EnsureBarangayTables(context);
            DbSeeder.Seed(context);
            EnsureAttendanceEmployeeLinks(context);
        }

        private static void EnsureBaseSchema(AppDbContext context, bool migrateOnStartup)
        {
            var hasLegacyCoreTables = DatabaseHasLegacyCoreTables(context);
            var hasAppliedMigrations = MigrationHistoryHasEntries(context);

            if (migrateOnStartup)
            {
                // Legacy databases may already contain the original schema without EF migration history.
                // In that case, replaying the initial migration throws "table already exists".
                if (hasLegacyCoreTables && !hasAppliedMigrations)
                {
                    return;
                }

                context.Database.Migrate();
                return;
            }

            if (!hasLegacyCoreTables)
            {
                context.Database.EnsureCreated();
            }
        }

        private static bool DatabaseHasLegacyCoreTables(AppDbContext context)
        {
            return TableExists(context, "users")
                || TableExists(context, "positions")
                || TableExists(context, "employees")
                || TableExists(context, "holidays");
        }

        private static bool MigrationHistoryHasEntries(AppDbContext context)
        {
            if (!TableExists(context, "__EFMigrationsHistory"))
            {
                return false;
            }

            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM `__EFMigrationsHistory`;";
                var result = command.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static bool TableExists(AppDbContext context, string tableName)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM information_schema.tables " +
                    "WHERE table_schema = DATABASE() AND table_name = @tableName;";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
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

        private static void EnsureBarangayTables(AppDbContext context)
        {
            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `households` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `household_code` varchar(50) NOT NULL,
                    `head_name` varchar(150) NOT NULL,
                    `address_line` varchar(250) NOT NULL,
                    `purok` varchar(100) NOT NULL,
                    `contact_number` varchar(50) NOT NULL,
                    `status` varchar(50) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_households_household_code` (`household_code`)
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `household_members` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `household_id` int NOT NULL,
                    `full_name` varchar(150) NOT NULL,
                    `relationship_to_head` varchar(100) NOT NULL,
                    `occupation` varchar(100) NOT NULL,
                    `is_cash_for_work_eligible` tinyint(1) NOT NULL,
                    `notes` varchar(500) NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_hhm_household_id` (`household_id`),
                    CONSTRAINT `FK_hhm_household` FOREIGN KEY (`household_id`) REFERENCES `households` (`id`) ON DELETE CASCADE
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `cash_for_work_events` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `title` varchar(150) NOT NULL,
                    `location` varchar(150) NOT NULL,
                    `event_date` datetime(6) NOT NULL,
                    `start_time` time(6) NOT NULL,
                    `end_time` time(6) NOT NULL,
                    `notes` varchar(500) NULL,
                    `created_by_user_id` int NOT NULL,
                    `status` varchar(50) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_cfw_events_date_status` (`event_date`, `status`),
                    CONSTRAINT `FK_cfw_event_creator` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `cash_for_work_participants` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `event_id` int NOT NULL,
                    `household_member_id` int NOT NULL,
                    `added_by_user_id` int NOT NULL,
                    `added_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `UX_cfw_participants_evt_mem` (`event_id`, `household_member_id`),
                    CONSTRAINT `FK_cfw_part_event` FOREIGN KEY (`event_id`) REFERENCES `cash_for_work_events` (`id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_cfw_part_member` FOREIGN KEY (`household_member_id`) REFERENCES `household_members` (`id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_cfw_part_added_by` FOREIGN KEY (`added_by_user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
                );
                """);

            context.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS `cash_for_work_attendance` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `participant_id` int NOT NULL,
                    `attendance_date` datetime(6) NOT NULL,
                    `status` varchar(50) NOT NULL,
                    `source` varchar(50) NOT NULL,
                    `ocr_extracted_name` varchar(150) NULL,
                    `recorded_by_user_id` int NOT NULL,
                    `recorded_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `UX_cfw_att_part_date` (`participant_id`, `attendance_date`),
                    CONSTRAINT `FK_cfw_att_participant` FOREIGN KEY (`participant_id`) REFERENCES `cash_for_work_participants` (`id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_cfw_att_recorded_by` FOREIGN KEY (`recorded_by_user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
                );
                """);
        }

        private static void EnsureAttendanceEmployeeLinks(AppDbContext context)
        {
            var fallbackPositionId = context.Positions
                .OrderBy(p => p.Id)
                .Select(p => (int?)p.Id)
                .FirstOrDefault();

            if (!fallbackPositionId.HasValue)
            {
                return;
            }

            var usersToLink = context.Users
                .Where(u => u.IsActive && (u.Role == Models.UserRole.Manager || u.Role == Models.UserRole.ShiftManager || u.Role == Models.UserRole.Crew))
                .ToList();

            bool hasChanges = false;
            foreach (var user in usersToLink)
            {
                var employee = context.Employees.FirstOrDefault(e => e.UserId == user.Id);
                if (employee == null)
                {
                    context.Employees.Add(new Models.Employee
                    {
                        UserId = user.Id,
                        FullName = string.IsNullOrWhiteSpace(user.Username) ? "Unnamed Employee" : user.Username,
                        PositionId = ResolveDefaultPositionId(context, user.Role, fallbackPositionId.Value),
                        HourlyRate = user.Role is Models.UserRole.Manager or Models.UserRole.ShiftManager ? 80.00m : 65.00m,
                        DateHired = DateTime.Today,
                        Status = Models.EmployeeStatus.Active
                    });
                    hasChanges = true;
                    continue;
                }

                if (employee.Status != Models.EmployeeStatus.Active)
                {
                    employee.Status = Models.EmployeeStatus.Active;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                context.SaveChanges();
            }
        }

        private static int ResolveDefaultPositionId(AppDbContext context, Models.UserRole role, int fallbackPositionId)
        {
            if (role == Models.UserRole.ShiftManager)
            {
                return context.Positions
                    .Where(p => p.Name == "Shift Manager" || p.Name.IndexOf("Scheduling", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(p => p.Name.IndexOf("Shift Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(p => (int?)p.Id)
                    .FirstOrDefault() ?? fallbackPositionId;
            }

            if (role == Models.UserRole.Manager)
            {
                return context.Positions
                    .Where(p => p.Name.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(p => p.Name.IndexOf("Shift Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(p => (int?)p.Id)
                    .FirstOrDefault() ?? fallbackPositionId;
            }

            return fallbackPositionId;
        }
    }
}

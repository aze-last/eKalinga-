using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AttendanceShiftingManagement.Data
{
    internal static class RuntimeSchemaBootstrapper
    {
        public static void EnsureRuntimeSchema(AppDbContext context)
        {
            var connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The active database connection string is missing.");

            if (!context.Database.CanConnect())
            {
                EnsureDatabaseExists(connectionString);
            }

            // `EnsureCreated()` still handles completely fresh databases.
            // If the selected app database already has legacy tables, EF no-ops here,
            // so we repair the barangay/current login schema explicitly afterward.
            context.Database.EnsureCreated();
            ExecuteSchemaRepairs(connectionString);
        }

        private static void EnsureDatabaseExists(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Database))
            {
                throw new InvalidOperationException("The active database name is missing from the connection string.");
            }

            var databaseName = builder.Database;
            builder.Database = string.Empty;

            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = new MySqlCommand(
                $"CREATE DATABASE IF NOT EXISTS `{EscapeIdentifier(databaseName)}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;",
                connection);

            command.ExecuteNonQuery();
        }

        private static void ExecuteSchemaRepairs(string connectionString)
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            foreach (var script in GetSchemaScripts())
            {
                using var command = new MySqlCommand(script, connection);
                command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<string> GetSchemaScripts()
        {
            return
            [
                """
                CREATE TABLE IF NOT EXISTS `users` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `username` varchar(50) NOT NULL,
                    `email` varchar(100) NOT NULL,
                    `password_hash` varchar(255) NOT NULL,
                    `role` longtext NOT NULL,
                    `is_active` tinyint(1) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `activity_logs` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `user_id` int NULL,
                    `action` varchar(100) NOT NULL,
                    `entity` varchar(100) NOT NULL,
                    `entity_id` int NULL,
                    `details` varchar(1000) NOT NULL,
                    `ip_address` varchar(50) NOT NULL,
                    `timestamp` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_activity_logs_user_id` (`user_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `user_profiles` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `user_id` int NOT NULL,
                    `full_name` varchar(150) NOT NULL,
                    `nickname` varchar(80) NOT NULL,
                    `phone` varchar(30) NOT NULL,
                    `address` varchar(255) NOT NULL,
                    `emergency_contact_name` varchar(120) NOT NULL,
                    `emergency_contact_phone` varchar(30) NOT NULL,
                    `photo_path` varchar(255) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_user_profiles_user_id` (`user_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `positions` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) NOT NULL,
                    `area` longtext NOT NULL,
                    PRIMARY KEY (`id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `employees` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `user_id` int NOT NULL,
                    `full_name` varchar(150) NOT NULL,
                    `position_id` int NOT NULL,
                    `hourly_rate` decimal(65,30) NOT NULL,
                    `date_hired` datetime(6) NOT NULL,
                    `status` longtext NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_employees_user_id` (`user_id`),
                    KEY `IX_employees_position_id` (`position_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `households` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `household_code` varchar(50) NOT NULL,
                    `head_name` varchar(150) NOT NULL,
                    `address_line` varchar(250) NOT NULL,
                    `purok` varchar(100) NOT NULL,
                    `contact_number` varchar(50) NOT NULL,
                    `status` longtext NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_households_household_code` (`household_code`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
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
                    KEY `IX_household_members_household_id` (`household_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `BeneficiaryStaging` (
                    `StagingID` int NOT NULL AUTO_INCREMENT,
                    `ResidentsId` bigint NULL,
                    `BeneficiaryId` varchar(120) NULL,
                    `CivilRegistryId` varchar(120) NULL,
                    `LastName` varchar(150) NULL,
                    `FirstName` varchar(150) NULL,
                    `MiddleName` varchar(150) NULL,
                    `FullName` varchar(200) NULL,
                    `Sex` varchar(30) NULL,
                    `DateOfBirth` varchar(40) NULL,
                    `Age` varchar(20) NULL,
                    `MaritalStatus` varchar(50) NULL,
                    `Address` varchar(255) NULL,
                    `IsPwd` tinyint(1) NOT NULL,
                    `PwdIdNo` varchar(120) NULL,
                    `DisabilityType` varchar(150) NULL,
                    `CauseOfDisability` varchar(255) NULL,
                    `IsSenior` tinyint(1) NOT NULL,
                    `SeniorIdNo` varchar(120) NULL,
                    `VerificationStatus` int NOT NULL,
                    `ImportedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`StagingID`),
                    KEY `IX_BeneficiaryStaging_CivilRegistryId` (`CivilRegistryId`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
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
                    `status` longtext NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_cash_for_work_events_created_by_user_id` (`created_by_user_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `cash_for_work_participants` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `event_id` int NOT NULL,
                    `household_member_id` int NOT NULL,
                    `added_by_user_id` int NOT NULL,
                    `added_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_cash_for_work_participants_event_member` (`event_id`, `household_member_id`),
                    KEY `IX_cash_for_work_participants_added_by_user_id` (`added_by_user_id`),
                    KEY `IX_cash_for_work_participants_household_member_id` (`household_member_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `cash_for_work_attendance` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `participant_id` int NOT NULL,
                    `attendance_date` datetime(6) NOT NULL,
                    `status` longtext NOT NULL,
                    `source` longtext NOT NULL,
                    `ocr_extracted_name` varchar(150) NULL,
                    `recorded_by_user_id` int NOT NULL,
                    `recorded_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_cash_for_work_attendance_participant_id` (`participant_id`),
                    KEY `IX_cash_for_work_attendance_recorded_by_user_id` (`recorded_by_user_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """
            ];
        }

        private static string EscapeIdentifier(string identifier)
        {
            return identifier.Replace("`", "``", StringComparison.Ordinal);
        }
    }
}

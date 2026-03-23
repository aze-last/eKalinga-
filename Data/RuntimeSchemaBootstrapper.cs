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

            // `EnsureCreated()` handles fresh databases. The follow-up repair scripts keep
            // existing databases aligned with the current Ayuda-only schema.
            context.Database.EnsureCreated();
            ExecuteSchemaRepairs(connectionString);
        }

        internal static void EnsureDatabaseExists(string connectionString)
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

        internal static void RepairLegacySchema(string connectionString)
        {
            ExecuteSchemaRepairs(connectionString);
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

            EnsureColumnExists(
                connection,
                "BeneficiaryStaging",
                "LinkedHouseholdId",
                "ALTER TABLE `BeneficiaryStaging` ADD COLUMN `LinkedHouseholdId` int NULL;");

            EnsureColumnExists(
                connection,
                "BeneficiaryStaging",
                "LinkedHouseholdMemberId",
                "ALTER TABLE `BeneficiaryStaging` ADD COLUMN `LinkedHouseholdMemberId` int NULL;");

            EnsureColumnExists(
                connection,
                "BeneficiaryStaging",
                "ReviewedByUserId",
                "ALTER TABLE `BeneficiaryStaging` ADD COLUMN `ReviewedByUserId` int NULL;");

            EnsureColumnExists(
                connection,
                "BeneficiaryStaging",
                "ReviewNotes",
                "ALTER TABLE `BeneficiaryStaging` ADD COLUMN `ReviewNotes` varchar(1000) NULL;");

            EnsureColumnExists(
                connection,
                "BeneficiaryStaging",
                "ReviewedAt",
                "ALTER TABLE `BeneficiaryStaging` ADD COLUMN `ReviewedAt` datetime(6) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "household_member_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `household_member_id` int NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "approved_amount",
                "ALTER TABLE `assistance_cases` ADD COLUMN `approved_amount` decimal(18,2) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "scheduled_release_date",
                "ALTER TABLE `assistance_cases` ADD COLUMN `scheduled_release_date` datetime(6) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "summary",
                "ALTER TABLE `assistance_cases` ADD COLUMN `summary` varchar(250) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "notes",
                "ALTER TABLE `assistance_cases` ADD COLUMN `notes` varchar(1000) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "resolution_notes",
                "ALTER TABLE `assistance_cases` ADD COLUMN `resolution_notes` varchar(1000) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "reviewed_by_user_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `reviewed_by_user_id` int NULL;");
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
                    `LinkedHouseholdId` int NULL,
                    `LinkedHouseholdMemberId` int NULL,
                    `ReviewedByUserId` int NULL,
                    `ReviewNotes` varchar(1000) NULL,
                    `ReviewedAt` datetime(6) NULL,
                    `ImportedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`StagingID`),
                    KEY `IX_BeneficiaryStaging_CivilRegistryId` (`CivilRegistryId`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `assistance_cases` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `case_number` varchar(40) NOT NULL,
                    `household_id` int NOT NULL,
                    `household_member_id` int NULL,
                    `assistance_type` varchar(120) NOT NULL,
                    `priority` longtext NOT NULL,
                    `status` longtext NOT NULL,
                    `requested_amount` decimal(18,2) NULL,
                    `approved_amount` decimal(18,2) NULL,
                    `requested_on` datetime(6) NOT NULL,
                    `scheduled_release_date` datetime(6) NULL,
                    `summary` varchar(250) NULL,
                    `notes` varchar(1000) NULL,
                    `resolution_notes` varchar(1000) NULL,
                    `created_by_user_id` int NOT NULL,
                    `reviewed_by_user_id` int NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_assistance_cases_case_number` (`case_number`),
                    KEY `IX_assistance_cases_household_id` (`household_id`),
                    KEY `IX_assistance_cases_household_member_id` (`household_member_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `grievance_records` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `grievance_number` varchar(40) NOT NULL,
                    `civil_registry_id` varchar(120) NULL,
                    `beneficiary_id` varchar(120) NULL,
                    `staging_id` int NULL,
                    `assistance_case_id` int NULL,
                    `cash_for_work_event_id` int NULL,
                    `type` longtext NOT NULL,
                    `status` longtext NOT NULL,
                    `title` varchar(200) NOT NULL,
                    `description` varchar(1000) NOT NULL,
                    `filed_by_user_id` int NOT NULL,
                    `assigned_to_user_id` int NULL,
                    `resolution_remarks` varchar(1000) NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    `resolved_at` datetime(6) NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_grievance_records_grievance_number` (`grievance_number`),
                    KEY `IX_grievance_records_civil_registry_id` (`civil_registry_id`),
                    KEY `IX_grievance_records_beneficiary_id` (`beneficiary_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `beneficiary_assistance_ledger` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `civil_registry_id` varchar(120) NULL,
                    `beneficiary_id` varchar(120) NULL,
                    `source_module` longtext NOT NULL,
                    `source_record_id` varchar(80) NULL,
                    `release_date` datetime(6) NOT NULL,
                    `amount` decimal(18,2) NOT NULL,
                    `remarks` varchar(1000) NOT NULL,
                    `recorded_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_beneficiary_assistance_ledger_civil_registry_id` (`civil_registry_id`),
                    KEY `IX_beneficiary_assistance_ledger_beneficiary_id` (`beneficiary_id`)
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

        private static void EnsureColumnExists(MySqlConnection connection, string tableName, string columnName, string alterStatement)
        {
            if (ColumnExists(connection, tableName, columnName))
            {
                return;
            }

            using var alterCommand = new MySqlCommand(alterStatement, connection);
            alterCommand.ExecuteNonQuery();
        }

        private static bool ColumnExists(MySqlConnection connection, string tableName, string columnName)
        {
            using var command = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName;
                """,
                connection);

            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@columnName", columnName);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static string EscapeIdentifier(string identifier)
        {
            return identifier.Replace("`", "``", StringComparison.Ordinal);
        }
    }
}

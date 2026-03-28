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
            // existing databases aligned with the current runtime schema.
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

            try
            {
                using var existingDatabaseConnection = new MySqlConnection(builder.ConnectionString);
                existingDatabaseConnection.Open();
                return;
            }
            catch (MySqlException ex) when (ex.Number == 1049)
            {
                // Unknown database. Fall through and attempt to create it.
            }

            var databaseName = builder.Database;
            builder.Database = string.Empty;

            try
            {
                using var connection = new MySqlConnection(builder.ConnectionString);
                connection.Open();

                using var command = new MySqlCommand(
                    $"CREATE DATABASE IF NOT EXISTS `{EscapeIdentifier(databaseName)}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;",
                    connection);

                command.ExecuteNonQuery();
            }
            catch (MySqlException ex) when (ex.Number is 1044 or 1045 or 1142)
            {
                throw new InvalidOperationException(
                    $"Database '{databaseName}' does not exist and the configured MySQL account cannot create databases. Create it first in your hosting panel, then rerun migrations.",
                    ex);
            }
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
                "validated_beneficiary_name",
                "ALTER TABLE `assistance_cases` ADD COLUMN `validated_beneficiary_name` varchar(150) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "validated_beneficiary_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `validated_beneficiary_id` varchar(120) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "validated_civil_registry_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `validated_civil_registry_id` varchar(120) NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "release_kind",
                "ALTER TABLE `assistance_cases` ADD COLUMN `release_kind` varchar(32) NOT NULL DEFAULT 'Cash';");

            EnsureColumnNullable(
                connection,
                "assistance_cases",
                "household_id",
                "ALTER TABLE `assistance_cases` MODIFY COLUMN `household_id` int NULL;");

            EnsureColumnNullable(
                connection,
                "beneficiary_digital_ids",
                "household_id",
                "ALTER TABLE `beneficiary_digital_ids` MODIFY COLUMN `household_id` int NULL;");

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

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "ayuda_program_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `ayuda_program_id` int NULL;");

            EnsureColumnExists(
                connection,
                "assistance_cases",
                "budget_ledger_entry_id",
                "ALTER TABLE `assistance_cases` ADD COLUMN `budget_ledger_entry_id` int NULL;");

            EnsureColumnExists(
                connection,
                "budget_ledger_entries",
                "release_kind",
                "ALTER TABLE `budget_ledger_entries` ADD COLUMN `release_kind` varchar(32) NULL;");

            EnsureColumnExists(
                connection,
                "scanner_sessions",
                "ayuda_program_id",
                "ALTER TABLE `scanner_sessions` ADD COLUMN `ayuda_program_id` int NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "assistance_type",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `assistance_type` varchar(120) NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "unit_amount",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `unit_amount` decimal(18,2) NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "item_description",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `item_description` varchar(250) NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "start_date",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `start_date` datetime(6) NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "end_date",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `end_date` datetime(6) NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "budget_cap",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `budget_cap` decimal(18,2) NULL;");

            EnsureColumnExists(
                connection,
                "ayuda_programs",
                "distribution_status",
                "ALTER TABLE `ayuda_programs` ADD COLUMN `distribution_status` longtext NULL;");

            EnsureColumnExists(
                connection,
                "cash_for_work_events",
                "ayuda_program_id",
                "ALTER TABLE `cash_for_work_events` ADD COLUMN `ayuda_program_id` int NULL;");

            EnsureColumnExists(
                connection,
                "cash_for_work_events",
                "budget_ledger_entry_id",
                "ALTER TABLE `cash_for_work_events` ADD COLUMN `budget_ledger_entry_id` int NULL;");

            EnsureColumnExists(
                connection,
                "cash_for_work_events",
                "release_amount",
                "ALTER TABLE `cash_for_work_events` ADD COLUMN `release_amount` decimal(18,2) NULL;");

            EnsureColumnExists(
                connection,
                "cash_for_work_events",
                "released_at",
                "ALTER TABLE `cash_for_work_events` ADD COLUMN `released_at` datetime(6) NULL;");

            EnsureColumnExists(
                connection,
                "cash_for_work_participants",
                "beneficiary_staging_id",
                "ALTER TABLE `cash_for_work_participants` ADD COLUMN `beneficiary_staging_id` int NULL;");

            EnsureColumnNullable(
                connection,
                "cash_for_work_participants",
                "household_member_id",
                "ALTER TABLE `cash_for_work_participants` MODIFY COLUMN `household_member_id` int NULL;");

            ExecuteNonQuery(
                connection,
                """
                CREATE TABLE IF NOT EXISTS `ayuda_project_beneficiaries` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `ayuda_program_id` int NOT NULL,
                    `beneficiary_staging_id` int NOT NULL,
                    `household_id` int NULL,
                    `household_member_id` int NULL,
                    `beneficiary_id` varchar(120) NULL,
                    `civil_registry_id` varchar(120) NULL,
                    `full_name` varchar(200) NOT NULL,
                    `added_by_user_id` int NOT NULL,
                    `added_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_ayuda_project_beneficiaries_program_beneficiary` (`ayuda_program_id`, `beneficiary_staging_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """);

            ExecuteNonQuery(
                connection,
                """
                CREATE TABLE IF NOT EXISTS `ayuda_project_claims` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `ayuda_program_id` int NOT NULL,
                    `beneficiary_staging_id` int NOT NULL,
                    `project_beneficiary_id` int NULL,
                    `household_id` int NULL,
                    `household_member_id` int NULL,
                    `beneficiary_id` varchar(120) NULL,
                    `civil_registry_id` varchar(120) NULL,
                    `full_name` varchar(200) NOT NULL,
                    `assistance_type_snapshot` varchar(120) NULL,
                    `item_description_snapshot` varchar(250) NULL,
                    `unit_amount_snapshot` decimal(18,2) NULL,
                    `qr_payload` varchar(200) NULL,
                    `remarks` varchar(1000) NULL,
                    `claimed_by_user_id` int NOT NULL,
                    `claimed_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_ayuda_project_claims_program_beneficiary` (`ayuda_program_id`, `beneficiary_staging_id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """);

            ExecuteNonQuery(
                connection,
                """
                UPDATE `assistance_cases`
                SET `release_kind` = 'Cash'
                WHERE `release_kind` IS NULL
                   OR `release_kind` = '';
                """);

            ExecuteNonQuery(
                connection,
                """
                UPDATE `ayuda_programs`
                SET `distribution_status` = 'Draft'
                WHERE `distribution_status` IS NULL
                   OR `distribution_status` = '';
                """);

            ExecuteNonQuery(
                connection,
                """
                UPDATE `budget_ledger_entries`
                SET `release_kind` = 'Cash'
                WHERE `entry_type` = 'Release'
                  AND (`release_kind` IS NULL OR `release_kind` = '');
                """);
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
                CREATE TABLE IF NOT EXISTS `system_registrations` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `company_serial_number` varchar(80) NOT NULL,
                    `company_name` varchar(180) NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    `last_validated_at` datetime(6) NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_system_registrations_company_serial_number` (`company_serial_number`)
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
                CREATE TABLE IF NOT EXISTS `beneficiary_digital_ids` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `beneficiary_staging_id` int NOT NULL,
                    `household_id` int NULL,
                    `household_member_id` int NULL,
                    `card_number` varchar(40) NOT NULL,
                    `qr_payload` varchar(200) NOT NULL,
                    `photo_path` varchar(255) NULL,
                    `issued_by_user_id` int NOT NULL,
                    `issued_at` datetime(6) NOT NULL,
                    `last_printed_at` datetime(6) NULL,
                    `is_active` tinyint(1) NOT NULL,
                    `revoked_at` datetime(6) NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_beneficiary_digital_ids_beneficiary_staging_id` (`beneficiary_staging_id`),
                    UNIQUE KEY `IX_beneficiary_digital_ids_card_number` (`card_number`),
                    UNIQUE KEY `IX_beneficiary_digital_ids_qr_payload` (`qr_payload`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `scanner_sessions` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `mode` longtext NOT NULL,
                    `session_token` varchar(80) NOT NULL,
                    `pin_hash` varchar(128) NOT NULL,
                    `cash_for_work_event_id` int NULL,
                    `created_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `expires_at` datetime(6) NOT NULL,
                    `last_accessed_at` datetime(6) NULL,
                    `is_active` tinyint(1) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_scanner_sessions_session_token` (`session_token`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `assistance_cases` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `case_number` varchar(40) NOT NULL,
                    `household_id` int NULL,
                    `household_member_id` int NULL,
                    `validated_beneficiary_name` varchar(150) NULL,
                    `validated_beneficiary_id` varchar(120) NULL,
                    `validated_civil_registry_id` varchar(120) NULL,
                    `assistance_type` varchar(120) NOT NULL,
                    `release_kind` varchar(32) NOT NULL DEFAULT 'Cash',
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
                CREATE TABLE IF NOT EXISTS `ayuda_programs` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `program_code` varchar(40) NOT NULL,
                    `program_name` varchar(150) NOT NULL,
                    `program_type` longtext NOT NULL,
                    `description` varchar(500) NULL,
                    `is_active` tinyint(1) NOT NULL,
                    `created_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_ayuda_programs_program_code` (`program_code`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `government_budget_snapshots` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `office_code` varchar(40) NOT NULL,
                    `office_name` varchar(120) NOT NULL,
                    `yearly_budget_id` int NOT NULL,
                    `allocated_amount` decimal(18,2) NOT NULL,
                    `spent_amount` decimal(18,2) NOT NULL,
                    `source_row_id` varchar(80) NULL,
                    `sync_status` longtext NOT NULL,
                    `synced_at` datetime(6) NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_government_budget_snapshots_office_code` (`office_code`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `private_donations` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `donor_type` longtext NOT NULL,
                    `donor_name` varchar(180) NOT NULL,
                    `amount` decimal(18,2) NOT NULL,
                    `date_received` datetime(6) NOT NULL,
                    `reference_number` varchar(80) NULL,
                    `remarks` varchar(1000) NULL,
                    `proof_type` longtext NOT NULL,
                    `proof_reference_number` varchar(80) NULL,
                    `proof_file_path` varchar(255) NULL,
                    `received_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """,
                """
                CREATE TABLE IF NOT EXISTS `budget_ledger_entries` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `entry_type` longtext NOT NULL,
                    `feature_source` longtext NOT NULL,
                    `source_record_id` varchar(80) NOT NULL,
                    `program_id` int NULL,
                    `recipient_count` int NOT NULL,
                    `total_amount` decimal(18,2) NOT NULL,
                    `government_portion` decimal(18,2) NOT NULL,
                    `private_portion` decimal(18,2) NOT NULL,
                    `entry_date` datetime(6) NOT NULL,
                    `remarks` varchar(1000) NULL,
                    `release_kind` varchar(32) NULL,
                    `recorded_by_user_id` int NOT NULL,
                    `created_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `IX_budget_ledger_entries_source` (`feature_source`(255), `source_record_id`, `entry_type`(255))
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
                    `ayuda_program_id` int NULL,
                    `budget_ledger_entry_id` int NULL,
                    `release_amount` decimal(18,2) NULL,
                    `released_at` datetime(6) NULL,
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
                    `beneficiary_staging_id` int NULL,
                    `household_member_id` int NULL,
                    `added_by_user_id` int NOT NULL,
                    `added_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `IX_cash_for_work_participants_event_beneficiary` (`event_id`, `beneficiary_staging_id`),
                    KEY `IX_cash_for_work_participants_added_by_user_id` (`added_by_user_id`),
                    KEY `IX_cash_for_work_participants_beneficiary_staging_id` (`beneficiary_staging_id`),
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

        private static void EnsureColumnNullable(MySqlConnection connection, string tableName, string columnName, string alterStatement)
        {
            if (ColumnAllowsNull(connection, tableName, columnName))
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

        private static bool ColumnAllowsNull(MySqlConnection connection, string tableName, string columnName)
        {
            using var command = new MySqlCommand(
                """
                SELECT IS_NULLABLE
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName
                LIMIT 1;
                """,
                connection);

            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@columnName", columnName);

            return string.Equals(
                Convert.ToString(command.ExecuteScalar()),
                "YES",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ExecuteNonQuery(MySqlConnection connection, string statement)
        {
            using var command = new MySqlCommand(statement, connection);
            command.ExecuteNonQuery();
        }

        private static string EscapeIdentifier(string identifier)
        {
            return identifier.Replace("`", "``", StringComparison.Ordinal);
        }
    }
}

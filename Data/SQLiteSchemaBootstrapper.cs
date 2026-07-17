using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    internal static class SQLiteSchemaBootstrapper
    {
        public static void EnsureSQLiteSchema(LocalDbContext context)
        {
            if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                return;
            }

            var connection = context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                // Ensure cache tables exist
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS digital_id_photo_cache (
                            BeneficiaryIdHash TEXT PRIMARY KEY,
                            EncryptedBeneficiaryId TEXT NOT NULL,
                            EncryptedPhotoBytes TEXT NOT NULL,
                            PhotoHash TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        );";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS digital_id_status_cache (
                            BeneficiaryIdHash TEXT PRIMARY KEY,
                            EncryptedBeneficiaryId TEXT NOT NULL,
                            EncryptedStatus TEXT NOT NULL,
                            EncryptedExpiryDate TEXT NOT NULL,
                            EncryptedCardNumber TEXT NOT NULL,
                            EncryptedQrPayload TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        );";
                    cmd.ExecuteNonQuery();

                    // e-Kard CRS verification contract caches (pull-sync style)
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS crs_status_cache (
                            BeneficiaryId TEXT PRIMARY KEY,
                            EncryptedIdNumber TEXT NOT NULL,
                            EncryptedStatus TEXT NOT NULL,
                            EncryptedIssuedDate TEXT NOT NULL,
                            EncryptedExpiryDate TEXT NOT NULL,
                            EncryptedRevokedAt TEXT NOT NULL,
                            EncryptedRevocationReason TEXT NOT NULL,
                            SyncedAt TEXT NOT NULL
                        );";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS crs_photo_cache (
                            DemographicCharacteristicId INTEGER PRIMARY KEY,
                            BeneficiaryId TEXT NOT NULL,
                            EncryptedPhotoBytes TEXT NOT NULL,
                            PhotoConfirmedAbsent INTEGER NOT NULL DEFAULT 0,
                            SourceUpdatedAt TEXT NULL,
                            SyncedAt TEXT NOT NULL
                        );";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS crs_pending_access_logs (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            PayloadJson TEXT NOT NULL,
                            CreatedAt TEXT NOT NULL
                        );";
                    cmd.ExecuteNonQuery();

                    // Local-only mirror of GGMS project_details sub-allocations (read-only from GGMS)
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ggms_project_cache (
                            GgmsProjectCacheId INTEGER PRIMARY KEY AUTOINCREMENT,
                            ProjectDetailsId TEXT NOT NULL UNIQUE,
                            YearlyBudgetId INTEGER NOT NULL DEFAULT 0,
                            OfficeCode TEXT NOT NULL,
                            ProjectName TEXT NOT NULL,
                            Description TEXT NULL,
                            SystemName TEXT NULL,
                            TotalBudget DECIMAL(18,2) NOT NULL DEFAULT 0,
                            Status TEXT NOT NULL DEFAULT 'active',
                            VoucherCode TEXT NULL,
                            SourceCreatedAt TEXT NULL,
                            SourceUpdatedAt TEXT NULL,
                            CachedAt TEXT NOT NULL,
                            IsLinked INTEGER NOT NULL DEFAULT 0
                        );";
                    cmd.ExecuteNonQuery();
                }

                // Verify column existence for private_donations
                var existingColumns = GetTableColumns(connection, "private_donations");

                EnsureColumnExists(connection, "private_donations", "donation_type", "TEXT NOT NULL DEFAULT 'Cash'", existingColumns);
                EnsureColumnExists(connection, "private_donations", "item_name", "TEXT NULL", existingColumns);
                EnsureColumnExists(connection, "private_donations", "quantity", "DECIMAL(18,2) NULL", existingColumns);
                EnsureColumnExists(connection, "private_donations", "unit_of_measure", "TEXT NULL", existingColumns);

                // GGMS project link on ayuda_programs (project_details_id, e.g. OPP-2026-0006).
                // Skipped when the table does not exist yet (EF creates it on first run).
                var ayudaProgramColumns = GetTableColumns(connection, "ayuda_programs");
                if (ayudaProgramColumns.Count > 0)
                {
                    EnsureColumnExists(connection, "ayuda_programs", "source_project_details_id", "TEXT NULL", ayudaProgramColumns);
                }
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }
        }

        private static HashSet<string> GetTableColumns(DbConnection connection, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var columnName = reader["name"]?.ToString();
                if (!string.IsNullOrEmpty(columnName))
                {
                    columns.Add(columnName);
                }
            }
            return columns;
        }

        private static void EnsureColumnExists(DbConnection connection, string tableName, string columnName, string columnDefinition, HashSet<string> existingColumns)
        {
            if (existingColumns.Contains(columnName))
            {
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            command.ExecuteNonQuery();
        }
    }
}

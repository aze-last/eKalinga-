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
                // Verify column existence for private_donations
                var existingColumns = GetTableColumns(connection, "private_donations");

                EnsureColumnExists(connection, "private_donations", "donation_type", "TEXT NOT NULL DEFAULT 'Cash'", existingColumns);
                EnsureColumnExists(connection, "private_donations", "item_name", "TEXT NULL", existingColumns);
                EnsureColumnExists(connection, "private_donations", "quantity", "DECIMAL(18,2) NULL", existingColumns);
                EnsureColumnExists(connection, "private_donations", "unit_of_measure", "TEXT NULL", existingColumns);
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

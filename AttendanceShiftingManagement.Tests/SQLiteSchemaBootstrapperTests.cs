using System;
using System.IO;
using System.Linq;
using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AttendanceShiftingManagement.Tests
{
    public sealed class SQLiteSchemaBootstrapperTests : IDisposable
    {
        private readonly string _dbPath;

        public SQLiteSchemaBootstrapperTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Fact]
        public void EnsureSQLiteSchema_AddsMissingColumnsToExistingDatabase()
        {
            // 1. Create a fresh SQLite database using DbContext options
            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;

            // Create a table private_donations manually WITHOUT the new columns
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE private_donations (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            donor_type TEXT NOT NULL,
                            donor_name TEXT NOT NULL,
                            amount DECIMAL(18,2) NOT NULL,
                            date_received TEXT NOT NULL
                        );";
                    cmd.ExecuteNonQuery();
                }
            }

            // Verify they are initially missing
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(private_donations);";
                    using (var reader = cmd.ExecuteReader())
                    {
                        var cols = "";
                        while (reader.Read())
                        {
                            cols += reader["name"].ToString() + ",";
                        }
                        Assert.DoesNotContain("donation_type", cols);
                        Assert.DoesNotContain("item_name", cols);
                    }
                }
            }

            // 2. Instantiate context and call bootstrapper
            using (var context = new LocalDbContext(options))
            {
                SQLiteSchemaBootstrapper.EnsureSQLiteSchema(context);
            }

            // 3. Verify columns now exist
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(private_donations);";
                    using (var reader = cmd.ExecuteReader())
                    {
                        var columns = new System.Collections.Generic.List<string>();
                        while (reader.Read())
                        {
                            columns.Add(reader["name"]?.ToString() ?? string.Empty);
                        }

                        Assert.Contains("donation_type", columns);
                        Assert.Contains("item_name", columns);
                        Assert.Contains("quantity", columns);
                        Assert.Contains("unit_of_measure", columns);
                    }
                }
            }
        }
    }
}

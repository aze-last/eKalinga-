using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Temporary diagnostic logger for tracing database context usage
    /// during scan validation and claim confirmation workflows.
    /// Writes to scan_diagnostic.log in the application root.
    /// </summary>
    internal static class ScanDiagnosticLogger
    {
        private static readonly object Lock = new();

        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "scan_diagnostic.log");

        public static void Log(string stage, LocalDbContext context, string message)
        {
            var provider = context.Database.ProviderName ?? "UNKNOWN_PROVIDER";
            var connString = "NO_CONNECTION_STRING";
            try
            {
                connString = context.Database.GetConnectionString() ?? "NO_CONNECTION_STRING";
            }
            catch
            {
                // Handle non-relational providers in unit tests
            }

            // Extract just the database name from the connection string for readability
            var dbName = ExtractDatabaseName(connString, provider);

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{stage}] " +
                       $"Provider={provider} | DB={dbName} | {message}";

            lock (Lock)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }

        public static void LogSeparator()
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath,
                    $"{'='  + new string('=', 119)}{Environment.NewLine}");
            }
        }

        private static string ExtractDatabaseName(string connectionString, string provider)
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                // SQLite: "Data Source=ams.db"
                var idx = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return connectionString[(idx + 12)..].Trim().TrimEnd(';');
                }

                return connectionString;
            }

            // MySQL: extract Database= value
            var dbIdx = connectionString.IndexOf("Database=", StringComparison.OrdinalIgnoreCase);
            if (dbIdx >= 0)
            {
                var start = dbIdx + 9;
                var end = connectionString.IndexOf(';', start);
                return end > start
                    ? connectionString[start..end]
                    : connectionString[start..];
            }

            return connectionString.Length > 80
                ? connectionString[..80] + "..."
                : connectionString;
        }
    }
}

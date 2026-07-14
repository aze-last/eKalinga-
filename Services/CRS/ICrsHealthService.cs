using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public interface ICrsHealthService
    {
        bool Connected { get; }
        long LatencyMs { get; }
        string RemoteVersion { get; }
        DateTime? LastSuccessfulConnection { get; }
        DateTime? LastFailure { get; }
        string FailureReason { get; }
        int ConsecutiveFailures { get; }
        bool IsCompatible { get; }

        Task CheckHealthAsync(CancellationToken cancellationToken);
    }

    public class CrsHealthService : ICrsHealthService
    {
        private readonly ICrsConnectionProvider _connectionProvider;
        private static readonly object SyncLock = new();

        public bool Connected { get; private set; }
        public long LatencyMs { get; private set; } = -1;
        public string RemoteVersion { get; private set; } = "Unknown";
        public DateTime? LastSuccessfulConnection { get; private set; }
        public DateTime? LastFailure { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public int ConsecutiveFailures { get; private set; }
        public bool IsCompatible { get; private set; } = true;

        private static readonly string[] RequiredValBeneficiaryColumns = new[]
        {
            "id", "residents_id", "beneficiary_id", "civilregistry_id", "last_name", "first_name", "middle_name", "full_name", "is_pwd", "is_senior"
        };

        public CrsHealthService(ICrsConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        public async Task CheckHealthAsync(CancellationToken cancellationToken)
        {
            var connString = _connectionProvider.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString))
            {
                RecordFailure("Connection string is not configured.");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await using var conn = new MySqlConnection(connString);
                await conn.OpenAsync(cancellationToken);

                RemoteVersion = conn.ServerVersion;

                await using var cmd = new MySqlCommand("SELECT 1;", conn);
                await cmd.ExecuteScalarAsync(cancellationToken);

                bool schemaOk = await VerifySchemaAsync(conn, cancellationToken);

                stopwatch.Stop();

                lock (SyncLock)
                {
                    Connected = true;
                    LatencyMs = stopwatch.ElapsedMilliseconds;
                    LastSuccessfulConnection = DateTime.UtcNow;
                    ConsecutiveFailures = 0;
                    FailureReason = string.Empty;
                    IsCompatible = schemaOk;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RecordFailure(ex.Message);
            }
        }

        private void RecordFailure(string message)
        {
            lock (SyncLock)
            {
                Connected = false;
                LatencyMs = -1;
                LastFailure = DateTime.UtcNow;
                FailureReason = message;
                ConsecutiveFailures++;
            }
        }

        private async Task<bool> VerifySchemaAsync(MySqlConnection conn, CancellationToken cancellationToken)
        {
            try
            {
                await using var cmd = new MySqlCommand("DESCRIBE val_beneficiaries;", conn);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                var existingColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingColumns.Add(reader.GetString(0));
                }
                await reader.CloseAsync();

                foreach (var reqCol in RequiredValBeneficiaryColumns)
                {
                    if (!existingColumns.Contains(reqCol))
                    {
                        RecordFailure($"Schema incompatibility: missing column '{reqCol}' in 'val_beneficiaries'.");
                        return false;
                    }
                }

                await using var cmdIds = new MySqlCommand("DESCRIBE beneficiary_digital_ids;", conn);
                using var readerIds = await cmdIds.ExecuteReaderAsync(cancellationToken);
                var existingIdCols = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await readerIds.ReadAsync(cancellationToken))
                {
                    existingIdCols.Add(readerIds.GetString(0));
                }
                await readerIds.CloseAsync();

                if (!existingIdCols.Contains("qr_payload") || !existingIdCols.Contains("is_active") || !existingIdCols.Contains("card_number"))
                {
                    RecordFailure("Schema incompatibility: missing core column in 'beneficiary_digital_ids'.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                FailureReason = $"Schema validation failed: {ex.Message}";
                return false;
            }
        }
    }
}

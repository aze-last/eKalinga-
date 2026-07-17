using MySqlConnector;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Raw ADO.NET implementation of the e-Kard CRS verification contract queries.
    /// SQL literals follow the contract document verbatim: most-recent-row status
    /// lookup (never a status-equals-Active filter), two-hop photo lookup (never
    /// through an ORM), append-only record_access_logs insert.
    /// </summary>
    public class CrsGateway : ICrsGateway
    {
        private readonly ICrsContractConnectionProvider _connectionProvider;

        public CrsGateway(ICrsContractConnectionProvider? connectionProvider = null)
        {
            _connectionProvider = connectionProvider ?? new CrsContractConnectionProvider();
        }

        public async Task<CrsDigitalIdRow?> GetLatestDigitalIdRowAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = new MySqlCommand(@"
                SELECT id_number, status, issued_date, expiry_date, revoked_at, revocation_reason
                FROM digital_ids
                WHERE beneficiary_id = @beneficiaryId AND IsDeleted = 0
                ORDER BY issued_date DESC
                LIMIT 1;", connection);
            command.Parameters.AddWithValue("@beneficiaryId", beneficiaryId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new CrsDigitalIdRow(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? null : ReadDateTime(reader, 2),
                reader.IsDBNull(3) ? null : ReadDateTime(reader, 3),
                reader.IsDBNull(4) ? null : ReadDateTime(reader, 4),
                reader.IsDBNull(5) ? null : reader.GetString(5));
        }

        public async Task<long?> GetDemographicCharacteristicIdAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = new MySqlCommand(@"
                SELECT demographic_characteristic_id FROM val_beneficiaries
                WHERE beneficiary_id = @beneficiaryId AND IsDeleted = 0
                LIMIT 1;", connection);
            command.Parameters.AddWithValue("@beneficiaryId", beneficiaryId);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt64(result);
        }

        public async Task<DateTime?> GetPhotoUpdatedAtAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            // Cheap freshness probe — no blob transferred (contract Part 2).
            await using var command = new MySqlCommand(@"
                SELECT updated_at FROM demographic_characteristics
                WHERE id = @id
                LIMIT 1;", connection);
            command.Parameters.AddWithValue("@id", demographicCharacteristicId);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDateTime(result);
        }

        public async Task<CrsPhotoRow?> GetPhotoAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = new MySqlCommand(@"
                SELECT profile_picture, updated_at FROM demographic_characteristics
                WHERE id = @id
                LIMIT 1;", connection);
            command.Parameters.AddWithValue("@id", demographicCharacteristicId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new CrsPhotoRow(
                reader.IsDBNull(0) ? null : (byte[])reader[0],
                reader.IsDBNull(1) ? null : ReadDateTime(reader, 1));
        }

        public async Task InsertAccessLogAsync(CrsAccessLogEntry entry, CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = new MySqlCommand(@"
                INSERT INTO record_access_logs
                    (user_id, user_name, record_type, reference_no, action_taken, accessed_at, SyncId)
                VALUES
                    (@user_id, @user_name, @record_type, @reference_no, @action_taken, @accessed_at, @sync_id);", connection);
            command.Parameters.AddWithValue("@user_id", (object?)entry.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@user_name", Truncate(entry.UserName, 255));
            command.Parameters.AddWithValue("@record_type", Truncate(entry.RecordType, 100));
            command.Parameters.AddWithValue("@reference_no", Truncate(entry.ReferenceNo, 100));
            // Live CRS column is varchar(50); a longer value is silently cut mid-word
            // (or rejected under strict mode), so trim deliberately before sending.
            command.Parameters.AddWithValue("@action_taken", Truncate(entry.ActionTaken, 50));
            command.Parameters.AddWithValue("@accessed_at", entry.AccessedAt);
            command.Parameters.AddWithValue("@sync_id", entry.SyncId);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public async Task<CrsSchemaProbeResult> ProbeSchemaAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                var missing = await FindMissingColumnAsync(connection, "digital_ids",
                    new[] { "id_number", "status", "issued_date", "expiry_date", "revoked_at", "revocation_reason", "beneficiary_id", "IsDeleted" }, cancellationToken);
                if (missing != null)
                {
                    return new CrsSchemaProbeResult(false, $"digital_ids missing column '{missing}'.");
                }

                missing = await FindMissingColumnAsync(connection, "val_beneficiaries",
                    new[] { "beneficiary_id", "demographic_characteristic_id", "IsDeleted" }, cancellationToken);
                if (missing != null)
                {
                    return new CrsSchemaProbeResult(false, $"val_beneficiaries missing column '{missing}'.");
                }

                missing = await FindMissingColumnAsync(connection, "demographic_characteristics",
                    new[] { "id", "profile_picture", "updated_at" }, cancellationToken);
                if (missing != null)
                {
                    return new CrsSchemaProbeResult(false, $"demographic_characteristics missing column '{missing}'.");
                }

                missing = await FindMissingColumnAsync(connection, "record_access_logs",
                    new[] { "user_id", "user_name", "record_type", "reference_no", "action_taken", "accessed_at", "SyncId" }, cancellationToken);
                if (missing != null)
                {
                    return new CrsSchemaProbeResult(false, $"record_access_logs missing column '{missing}'.");
                }

                return new CrsSchemaProbeResult(true, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CrsSchemaProbeResult(false, ex.Message);
            }
        }

        private static async Task<string?> FindMissingColumnAsync(MySqlConnection connection, string tableName, string[] requiredColumns, CancellationToken cancellationToken)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = new MySqlCommand($"DESCRIBE `{tableName}`;", connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    existing.Add(reader.GetString(0));
                }
            }

            foreach (var column in requiredColumns)
            {
                if (!existing.Contains(column))
                {
                    return column;
                }
            }

            return null;
        }

        private static DateTime ReadDateTime(MySqlDataReader reader, int ordinal)
        {
            // AllowZeroDateTime=true makes DATE/DATETIME come back as MySqlDateTime.
            var value = reader.GetValue(ordinal);
            if (value is MySqlDateTime mySqlDateTime)
            {
                return mySqlDateTime.IsValidDateTime ? mySqlDateTime.GetDateTime() : DateTime.MinValue;
            }

            return Convert.ToDateTime(value);
        }
    }
}

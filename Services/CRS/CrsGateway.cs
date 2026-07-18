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

        public async Task<IReadOnlyList<CrsValBeneficiaryRow>> GetAllValidatedBeneficiariesAsync(CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            // READ only — same column set the staging import uses. Per the CRS schema-drift
            // notice, `age` is never selected (removed post-migration); it is computed from
            // date_of_birth instead.
            await using var command = new MySqlCommand(@"
                SELECT residents_id, beneficiary_id, civilregistry_id, last_name, first_name,
                       middle_name, full_name, sex, date_of_birth, marital_status, address,
                       is_pwd, pwd_id_no, disability_type, cause_of_disability, is_senior, senior_id_no
                FROM val_beneficiaries
                WHERE IsDeleted = 0;", connection);

            var rows = new List<CrsValBeneficiaryRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var dateOfBirth = ReadNullableString(reader, 8);
                rows.Add(new CrsValBeneficiaryRow(
                    reader.IsDBNull(0) ? null : Convert.ToInt64(reader.GetValue(0)),
                    ReadNullableString(reader, 1),
                    ReadNullableString(reader, 2),
                    ReadNullableString(reader, 3),
                    ReadNullableString(reader, 4),
                    ReadNullableString(reader, 5),
                    ReadNullableString(reader, 6),
                    ReadNullableString(reader, 7),
                    dateOfBirth,
                    CrsAgeCalculator.CalculateAgeText(dateOfBirth),
                    ReadNullableString(reader, 9),
                    ReadNullableString(reader, 10),
                    ReadBoolean(reader, 11),
                    ReadNullableString(reader, 12),
                    ReadNullableString(reader, 13),
                    ReadNullableString(reader, 14),
                    ReadBoolean(reader, 15),
                    ReadNullableString(reader, 16)));
            }

            return rows;
        }

        public async Task<IReadOnlyList<CrsDigitalIdListRow>> GetAllLatestDigitalIdRowsAsync(CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionProvider.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            // READ only — most-recent row per beneficiary (contract rule: never a
            // status-equals-Active filter; ordering mirrors the single-row lookup).
            await using var command = new MySqlCommand(@"
                SELECT d.beneficiary_id, d.id_number, d.status, d.issued_date, d.expiry_date, d.revoked_at, d.revocation_reason
                FROM digital_ids d
                INNER JOIN (
                    SELECT beneficiary_id, MAX(issued_date) AS latest_issued
                    FROM digital_ids
                    WHERE IsDeleted = 0
                    GROUP BY beneficiary_id
                ) latest ON latest.beneficiary_id = d.beneficiary_id AND latest.latest_issued = d.issued_date
                WHERE d.IsDeleted = 0;", connection);

            var rows = new List<CrsDigitalIdListRow>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var beneficiaryId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(beneficiaryId) || !seen.Add(beneficiaryId))
                {
                    // Ties on issued_date could return duplicates — keep the first.
                    continue;
                }

                rows.Add(new CrsDigitalIdListRow(
                    beneficiaryId,
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? null : ReadDateTime(reader, 3),
                    reader.IsDBNull(4) ? null : ReadDateTime(reader, 4),
                    reader.IsDBNull(5) ? null : ReadDateTime(reader, 5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }

            return rows;
        }

        private static string? ReadNullableString(MySqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            if (value is MySqlDateTime mySqlDateTime)
            {
                return mySqlDateTime.IsValidDateTime
                    ? mySqlDateTime.GetDateTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                    : null;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool ReadBoolean(MySqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                bool booleanValue => booleanValue,
                string stringValue => stringValue.Equals("true", StringComparison.OrdinalIgnoreCase) || stringValue == "1",
                _ => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) != 0
            };
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

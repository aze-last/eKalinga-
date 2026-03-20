using AttendanceShiftingManagement.Models;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public sealed class MasterListSnapshot
    {
        public IReadOnlyList<MasterListBeneficiary> Beneficiaries { get; init; } = Array.Empty<MasterListBeneficiary>();
        public string SourceDatabase { get; init; } = string.Empty;
        public string SourceServer { get; init; } = string.Empty;
        public DateTime? LastUpdatedAt { get; init; }
    }

    public static class MasterListService
    {
        private const string TableName = "val_beneficiaries";

        public static async Task<MasterListSnapshot> LoadLocalSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var settings = ConnectionSettingsService.Load();
            var localPreset = settings.GetPreset("Local");

            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(localPreset));
            await connection.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(connection, localPreset.Database, cancellationToken))
            {
                throw new InvalidOperationException("Local table `val_beneficiaries` was not found. Sync it first from System Tools.");
            }

            var beneficiaries = new List<MasterListBeneficiary>();

            const string sql =
                """
                SELECT
                    id,
                    residents_id,
                    beneficiary_id,
                    user_id,
                    civilregistry_id,
                    last_name,
                    first_name,
                    middle_name,
                    full_name,
                    sex,
                    date_of_birth,
                    age,
                    marital_status,
                    address,
                    is_pwd,
                    pwd_id_no,
                    is_senior,
                    senior_id_no,
                    disability_type,
                    cause_of_disability,
                    created_at,
                    updated_at
                FROM val_beneficiaries
                ORDER BY full_name, beneficiary_id;
                """;

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                beneficiaries.Add(new MasterListBeneficiary
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    ResidentsId = reader.GetInt64(reader.GetOrdinal("residents_id")),
                    BeneficiaryId = GetString(reader, "beneficiary_id"),
                    UserId = IsDBNull(reader, "user_id") ? null : reader.GetInt32(reader.GetOrdinal("user_id")),
                    CivilRegistryId = GetString(reader, "civilregistry_id"),
                    LastName = GetString(reader, "last_name"),
                    FirstName = GetString(reader, "first_name"),
                    MiddleName = GetString(reader, "middle_name"),
                    FullName = GetString(reader, "full_name"),
                    Sex = GetString(reader, "sex"),
                    DateOfBirth = GetString(reader, "date_of_birth"),
                    Age = GetString(reader, "age"),
                    MaritalStatus = GetString(reader, "marital_status"),
                    Address = GetString(reader, "address"),
                    IsPwd = GetBoolean(reader, "is_pwd"),
                    PwdIdNo = GetString(reader, "pwd_id_no"),
                    IsSenior = GetBoolean(reader, "is_senior"),
                    SeniorIdNo = GetString(reader, "senior_id_no"),
                    DisabilityType = GetString(reader, "disability_type"),
                    CauseOfDisability = GetString(reader, "cause_of_disability"),
                    CreatedAt = IsDBNull(reader, "created_at") ? null : reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = IsDBNull(reader, "updated_at") ? null : reader.GetDateTime(reader.GetOrdinal("updated_at"))
                });
            }

            return new MasterListSnapshot
            {
                Beneficiaries = beneficiaries,
                SourceDatabase = localPreset.Database,
                SourceServer = localPreset.Server,
                LastUpdatedAt = beneficiaries.MaxBy(item => item.UpdatedAt)?.UpdatedAt
            };
        }

        private static async Task<bool> TableExistsAsync(MySqlConnection connection, string databaseName, CancellationToken cancellationToken)
        {
            const string sql =
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @databaseName
                  AND table_name = @tableName
                  AND table_type = 'BASE TABLE';
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@databaseName", databaseName);
            command.Parameters.AddWithValue("@tableName", TableName);

            var count = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(count) > 0;
        }

        private static string GetString(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static bool GetBoolean(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            var rawValue = reader.GetValue(ordinal);
            return rawValue switch
            {
                bool boolValue => boolValue,
                sbyte signedByte => signedByte != 0,
                byte unsignedByte => unsignedByte != 0,
                short shortValue => shortValue != 0,
                ushort unsignedShort => unsignedShort != 0,
                int intValue => intValue != 0,
                uint unsignedInt => unsignedInt != 0,
                long longValue => longValue != 0,
                ulong unsignedLong => unsignedLong != 0,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed != 0,
                _ => Convert.ToBoolean(rawValue)
            };
        }

        private static bool IsDBNull(MySqlDataReader reader, string columnName)
        {
            return reader.IsDBNull(reader.GetOrdinal(columnName));
        }
    }
}

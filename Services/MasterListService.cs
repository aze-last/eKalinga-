using AttendanceShiftingManagement.Models;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    internal static class MasterListQuickFilters
    {
        public const string AllBeneficiaries = "All beneficiaries";
        public const string SeniorCitizens = "Senior citizens";
        public const string PersonsWithDisability = "PWD";
        public const string WithCivilRegistryId = "With civil registry ID";
        public const string MissingCivilRegistryId = "Missing civil registry ID";
        public const string Approved = "Approved";
        public const string Pending = "Pending";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            AllBeneficiaries,
            SeniorCitizens,
            PersonsWithDisability,
            WithCivilRegistryId,
            MissingCivilRegistryId,
            Approved,
            Pending
        };
    }

    internal sealed class MasterListPageRequest
    {
        public string SearchText { get; init; } = string.Empty;
        public IReadOnlyList<string> QuickFilters { get; init; } = Array.Empty<string>();
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 100;
    }

    internal sealed class MasterListPageResult
    {
        public IReadOnlyList<MasterListBeneficiary> Beneficiaries { get; init; } = Array.Empty<MasterListBeneficiary>();
        public int TotalBeneficiaries { get; init; }
        public int LinkedCivilRegistryCount { get; init; }
        public int SeniorCount { get; init; }
        public int PwdCount { get; init; }
        public int FilteredBeneficiaryCount { get; init; }
        public string SourceDatabase { get; init; } = string.Empty;
        public string SourceServer { get; init; } = string.Empty;
        public DateTime? LastUpdatedAt { get; init; }
    }

    internal interface IMasterListQueryService
    {
        Task<MasterListPageResult> LoadPageAsync(MasterListPageRequest request, CancellationToken cancellationToken = default);
    }

    internal sealed class MasterListService : IMasterListQueryService
    {
        private const string TableName = "val_beneficiaries";
        private const int MaxPageSize = 500;

        public async Task<MasterListPageResult> LoadPageAsync(MasterListPageRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

            var settings = ConnectionSettingsService.Load();
            var activePreset = settings.GetPreset(settings.SelectedPreset);

            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(activePreset));
            await connection.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(connection, activePreset.Database, cancellationToken))
            {
                throw new InvalidOperationException($"Table `val_beneficiaries` was not found in the active database `{activePreset.Database}`. Snapshot it first from Settings or Load Tables.");
            }

            var normalizedRequest = new MasterListPageRequest
            {
                SearchText = request.SearchText,
                QuickFilters = request.QuickFilters,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var summary = await LoadSummaryAsync(connection, cancellationToken);
            var filteredCount = await LoadFilteredCountAsync(connection, normalizedRequest, cancellationToken);

            var beneficiaries = filteredCount == 0
                ? Array.Empty<MasterListBeneficiary>()
                : await LoadBeneficiariesAsync(connection, normalizedRequest, cancellationToken);

            return new MasterListPageResult
            {
                Beneficiaries = beneficiaries,
                TotalBeneficiaries = summary.TotalBeneficiaries,
                LinkedCivilRegistryCount = summary.LinkedCivilRegistryCount,
                SeniorCount = summary.SeniorCount,
                PwdCount = summary.PwdCount,
                FilteredBeneficiaryCount = filteredCount,
                SourceDatabase = activePreset.Database,
                SourceServer = activePreset.Server,
                LastUpdatedAt = summary.LastUpdatedAt
            };
        }

        private static async Task<SummarySnapshot> LoadSummaryAsync(MySqlConnection connection, CancellationToken cancellationToken)
        {
            const string sql =
                """
                SELECT
                    COUNT(*) AS total_count,
                    SUM(CASE WHEN TRIM(COALESCE(v.civilregistry_id, '')) <> '' THEN 1 ELSE 0 END) AS linked_count,
                    SUM(CASE WHEN COALESCE(v.is_senior, 0) <> 0 THEN 1 ELSE 0 END) AS senior_count,
                    SUM(CASE WHEN COALESCE(v.is_pwd, 0) <> 0 THEN 1 ELSE 0 END) AS pwd_count,
                    MAX(v.updated_at) AS last_updated
                FROM val_beneficiaries v;
                """;

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return new SummarySnapshot(0, 0, 0, 0, null);
            }

            return new SummarySnapshot(
                GetIntValue(reader, "total_count"),
                GetIntValue(reader, "linked_count"),
                GetIntValue(reader, "senior_count"),
                GetIntValue(reader, "pwd_count"),
                reader.IsDBNull(reader.GetOrdinal("last_updated"))
                    ? null
                    : (DateTime?)reader.GetDateTime(reader.GetOrdinal("last_updated")));
        }

        private static async Task<int> LoadFilteredCountAsync(MySqlConnection connection, MasterListPageRequest request, CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand();
            command.Connection = connection;

            var whereClause = BuildWhereClause(command, request);
            command.CommandText = 
                $"""
                SELECT COUNT(*) 
                FROM {TableName} v
                LEFT JOIN BeneficiaryStaging s ON v.residents_id = s.ResidentsId
                {whereClause};
                """;

            var count = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(count);
        }

        private static async Task<IReadOnlyList<MasterListBeneficiary>> LoadBeneficiariesAsync(MySqlConnection connection, MasterListPageRequest request, CancellationToken cancellationToken)
        {
            var beneficiaries = new List<MasterListBeneficiary>();

            await using var command = new MySqlCommand();
            command.Connection = connection;

            var whereClause = BuildWhereClause(command, request);
            command.CommandText =
                $"""
                SELECT
                    v.id,
                    v.residents_id,
                    v.beneficiary_id,
                    v.user_id,
                    v.civilregistry_id,
                    v.last_name,
                    v.first_name,
                    v.middle_name,
                    v.full_name,
                    v.sex,
                    v.date_of_birth,
                    v.age,
                    v.marital_status,
                    v.address,
                    v.is_pwd,
                    v.pwd_id_no,
                    v.is_senior,
                    v.senior_id_no,
                    v.disability_type,
                    v.cause_of_disability,
                    v.created_at,
                    v.updated_at,
                    COALESCE(s.VerificationStatus, 0) as verification_status
                FROM {TableName} v
                LEFT JOIN BeneficiaryStaging s ON v.residents_id = s.ResidentsId
                {whereClause}
                ORDER BY v.full_name, v.beneficiary_id
                LIMIT @pageSize OFFSET @offset;
                """;

            command.Parameters.AddWithValue("@pageSize", request.PageSize);
            command.Parameters.AddWithValue("@offset", (request.PageNumber - 1) * request.PageSize);

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
                    VerificationStatus = (VerificationStatus)reader.GetInt32(reader.GetOrdinal("verification_status")),
                    CreatedAt = IsDBNull(reader, "created_at") ? null : reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = IsDBNull(reader, "updated_at") ? null : reader.GetDateTime(reader.GetOrdinal("updated_at"))
                });
            }

            return beneficiaries;
        }

        private static string BuildWhereClause(MySqlCommand command, MasterListPageRequest request)
        {
            var clauses = new List<string>();

            foreach (var filter in request.QuickFilters)
            {
                switch (filter)
                {
                    case MasterListQuickFilters.SeniorCitizens:
                        clauses.Add("COALESCE(v.is_senior, 0) <> 0");
                        break;
                    case MasterListQuickFilters.PersonsWithDisability:
                        clauses.Add("COALESCE(v.is_pwd, 0) <> 0");
                        break;
                    case MasterListQuickFilters.WithCivilRegistryId:
                        clauses.Add("TRIM(COALESCE(v.civilregistry_id, '')) <> ''");
                        break;
                    case MasterListQuickFilters.MissingCivilRegistryId:
                        clauses.Add("TRIM(COALESCE(v.civilregistry_id, '')) = ''");
                        break;
                    case MasterListQuickFilters.Approved:
                        clauses.Add("COALESCE(s.VerificationStatus, 0) = 1");
                        break;
                    case MasterListQuickFilters.Pending:
                        clauses.Add("COALESCE(s.VerificationStatus, 0) = 0");
                        break;
                }
            }

            var searchText = request.SearchText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                clauses.Add(
                    """
                    (
                        COALESCE(v.full_name, '') LIKE @searchPattern
                        OR COALESCE(v.beneficiary_id, '') LIKE @searchPattern
                        OR COALESCE(v.civilregistry_id, '') LIKE @searchPattern
                        OR COALESCE(v.address, '') LIKE @searchPattern
                        OR COALESCE(v.sex, '') LIKE @searchPattern
                    )
                    """);

                command.Parameters.AddWithValue("@searchPattern", $"%{searchText}%");
            }

            return clauses.Count == 0
                ? string.Empty
                : $" WHERE {string.Join(" AND ", clauses)}";
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

        private static int GetIntValue(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return 0;
            }

            return Convert.ToInt32(reader.GetValue(ordinal));
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

        private sealed record SummarySnapshot(
            int TotalBeneficiaries,
            int LinkedCivilRegistryCount,
            int SeniorCount,
            int PwdCount,
            DateTime? LastUpdatedAt);
    }
}

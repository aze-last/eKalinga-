using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Globalization;

namespace AttendanceShiftingManagement.Services
{
    public sealed class CrsBeneficiaryImportResult
    {
        public bool IsSuccess { get; init; }
        public int ImportedCount { get; init; }
        public int SkippedCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public static class CrsBeneficiaryImportService
    {
        public const string SourceTableName = "val_beneficiaries";

        public static async Task<CrsBeneficiaryImportResult> ImportPendingAsync(
            DatabaseConnectionPreset? sourcePreset = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                sourcePreset ??= MunicipalityImportConnectionSettingsService.Load();
                ValidatePreset(sourcePreset);

                await using var sourceConnection = new MySqlConnection(
                    ConnectionSettingsService.BuildConnectionString(sourcePreset));
                await sourceConnection.OpenAsync(cancellationToken);

                var sourceTableExists = await TableExistsAsync(
                    sourceConnection,
                    sourcePreset.Database,
                    SourceTableName,
                    cancellationToken);

                if (!sourceTableExists)
                {
                    return new CrsBeneficiaryImportResult
                    {
                        IsSuccess = false,
                        Message = $"Source table `{SourceTableName}` was not found in {sourcePreset.Database}."
                    };
                }

                await using var command = new MySqlCommand(
                    """
                    SELECT
                        `residents_id`,
                        `beneficiary_id`,
                        `civilregistry_id`,
                        `last_name`,
                        `first_name`,
                        `middle_name`,
                        `full_name`,
                        `sex`,
                        `date_of_birth`,
                        `age`,
                        `marital_status`,
                        `address`,
                        `is_pwd`,
                        `pwd_id_no`,
                        `disability_type`,
                        `cause_of_disability`,
                        `is_senior`,
                        `senior_id_no`
                    FROM `val_beneficiaries`;
                    """,
                    sourceConnection);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await using var context = new AppDbContext();

                var existingCivilRegistryIds = new HashSet<string>(
                    await context.BeneficiaryStaging
                        .AsNoTracking()
                        .Where(row => row.CivilRegistryId != null && row.CivilRegistryId != string.Empty)
                        .Select(row => row.CivilRegistryId!)
                        .ToListAsync(cancellationToken),
                    StringComparer.OrdinalIgnoreCase);
                var existingBeneficiaryIds = new HashSet<string>(
                    await context.BeneficiaryStaging
                        .AsNoTracking()
                        .Where(row => row.BeneficiaryId != null && row.BeneficiaryId != string.Empty)
                        .Select(row => row.BeneficiaryId!)
                        .ToListAsync(cancellationToken),
                    StringComparer.OrdinalIgnoreCase);
                var existingResidentsIds = new HashSet<long>(
                    await context.BeneficiaryStaging
                        .AsNoTracking()
                        .Where(row => row.ResidentsId.HasValue)
                        .Select(row => row.ResidentsId!.Value)
                        .ToListAsync(cancellationToken));
                var existingFingerprints = new HashSet<string>(
                    (await context.BeneficiaryStaging
                        .AsNoTracking()
                        .Select(row => new
                        {
                            row.FullName,
                            row.FirstName,
                            row.MiddleName,
                            row.LastName,
                            row.DateOfBirth
                        })
                        .ToListAsync(cancellationToken))
                    .Select(row => BeneficiaryImportDeduplication.BuildFingerprint(
                        ResolveDisplayName(row.FullName, row.FirstName, row.MiddleName, row.LastName),
                        row.DateOfBirth))
                    .Where(fingerprint => !string.IsNullOrWhiteSpace(fingerprint)),
                    StringComparer.OrdinalIgnoreCase);

                var importedCount = 0;
                var skippedCount = 0;

                while (await reader.ReadAsync(cancellationToken))
                {
                    var residentsId = GetNullableInt64(reader, "residents_id");
                    var beneficiaryId = GetNullableString(reader, "beneficiary_id")?.Trim();
                    var civilRegistryId = GetNullableString(reader, "civilregistry_id")?.Trim();
                    var firstName = GetNullableString(reader, "first_name");
                    var middleName = GetNullableString(reader, "middle_name");
                    var lastName = GetNullableString(reader, "last_name");
                    var fullName = GetNullableString(reader, "full_name");
                    var dateOfBirth = GetNullableString(reader, "date_of_birth");

                    var dedupDecision = BeneficiaryImportDeduplication.Evaluate(
                        residentsId,
                        beneficiaryId,
                        civilRegistryId,
                        ResolveDisplayName(fullName, firstName, middleName, lastName),
                        dateOfBirth,
                        new BeneficiaryImportDeduplicationSnapshot(
                            existingCivilRegistryIds,
                            existingBeneficiaryIds,
                            existingResidentsIds,
                            existingFingerprints));

                    if (dedupDecision.ShouldSkip)
                    {
                        skippedCount++;
                        continue;
                    }

                    context.BeneficiaryStaging.Add(new BeneficiaryStaging
                    {
                        ResidentsId = residentsId,
                        BeneficiaryId = beneficiaryId,
                        CivilRegistryId = civilRegistryId,
                        LastName = lastName,
                        FirstName = firstName,
                        MiddleName = middleName,
                        FullName = fullName,
                        Sex = GetNullableString(reader, "sex"),
                        DateOfBirth = dateOfBirth,
                        Age = GetNullableString(reader, "age"),
                        MaritalStatus = GetNullableString(reader, "marital_status"),
                        Address = GetNullableString(reader, "address"),
                        IsPwd = GetBoolean(reader, "is_pwd"),
                        PwdIdNo = GetNullableString(reader, "pwd_id_no"),
                        DisabilityType = GetNullableString(reader, "disability_type"),
                        CauseOfDisability = GetNullableString(reader, "cause_of_disability"),
                        IsSenior = GetBoolean(reader, "is_senior"),
                        SeniorIdNo = GetNullableString(reader, "senior_id_no"),
                        VerificationStatus = VerificationStatus.Pending,
                        ImportedAt = DateTime.Now
                    });

                    if (!string.IsNullOrWhiteSpace(civilRegistryId))
                    {
                        existingCivilRegistryIds.Add(civilRegistryId);
                    }

                    if (!string.IsNullOrWhiteSpace(beneficiaryId))
                    {
                        existingBeneficiaryIds.Add(beneficiaryId);
                    }

                    if (residentsId.HasValue)
                    {
                        existingResidentsIds.Add(residentsId.Value);
                    }

                    var fingerprint = BeneficiaryImportDeduplication.BuildFingerprint(
                        ResolveDisplayName(fullName, firstName, middleName, lastName),
                        dateOfBirth);
                    if (!string.IsNullOrWhiteSpace(fingerprint))
                    {
                        existingFingerprints.Add(fingerprint);
                    }

                    importedCount++;
                }

                if (importedCount > 0)
                {
                    await context.SaveChangesAsync(cancellationToken);
                }

                return new CrsBeneficiaryImportResult
                {
                    IsSuccess = true,
                    ImportedCount = importedCount,
                    SkippedCount = skippedCount,
                    Message = $"Imported {importedCount} beneficiary row(s); skipped {skippedCount} duplicate or existing identity row(s)."
                };
            }
            catch (Exception ex)
            {
                return new CrsBeneficiaryImportResult
                {
                    IsSuccess = false,
                    Message = $"CRS import failed: {ex.Message}"
                };
            }
        }

        private static void ValidatePreset(DatabaseConnectionPreset preset)
        {
            if (string.IsNullOrWhiteSpace(preset.Server))
            {
                throw new InvalidOperationException("The CRS source preset is missing a server value.");
            }

            if (string.IsNullOrWhiteSpace(preset.Database))
            {
                throw new InvalidOperationException("The CRS source preset is missing a database name.");
            }

            if (string.IsNullOrWhiteSpace(preset.Username))
            {
                throw new InvalidOperationException("The CRS source preset is missing a username.");
            }
        }

        private static async Task<bool> TableExistsAsync(
            MySqlConnection connection,
            string databaseName,
            string tableName,
            CancellationToken cancellationToken)
        {
            const string sql =
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @databaseName
                  AND table_name = @tableName;
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@databaseName", databaseName);
            command.Parameters.AddWithValue("@tableName", tableName);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            return count > 0;
        }

        private static string? GetNullableString(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static long? GetNullableInt64(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static bool GetBoolean(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                bool booleanValue => booleanValue,
                sbyte signedByteValue => signedByteValue != 0,
                byte byteValue => byteValue != 0,
                short shortValue => shortValue != 0,
                ushort unsignedShortValue => unsignedShortValue != 0,
                int intValue => intValue != 0,
                uint unsignedIntValue => unsignedIntValue != 0,
                long longValue => longValue != 0,
                ulong unsignedLongValue => unsignedLongValue != 0,
                string stringValue => stringValue.Equals("true", StringComparison.OrdinalIgnoreCase) || stringValue == "1",
                _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
            };
        }

        private static string ResolveDisplayName(string? fullName, string? firstName, string? middleName, string? lastName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Trim();
            }

            return string.Join(" ", new[] { firstName, middleName, lastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public interface IGgmsConsolidatedTransactionService
    {
        Task<string?> TryWriteAssistanceCaseReleaseAsync(AppDbContext context, AssistanceCase assistanceCase);
        Task<string?> TryWriteProjectDistributionClaimAsync(AppDbContext context, AyudaProgram? program, AyudaProjectClaim claim);
        Task<string?> TryWriteCashForWorkReleaseAsync(
            AppDbContext context,
            CashForWorkEvent cashForWorkEvent,
            IReadOnlyCollection<CashForWorkParticipant> participants,
            IReadOnlyCollection<int> releasedParticipantIds,
            decimal totalAmount);
    }

    public sealed class NullGgmsConsolidatedTransactionService : IGgmsConsolidatedTransactionService
    {
        public static readonly NullGgmsConsolidatedTransactionService Instance = new();

        private NullGgmsConsolidatedTransactionService()
        {
        }

        public Task<string?> TryWriteAssistanceCaseReleaseAsync(AppDbContext context, AssistanceCase assistanceCase)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> TryWriteProjectDistributionClaimAsync(AppDbContext context, AyudaProgram? program, AyudaProjectClaim claim)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> TryWriteCashForWorkReleaseAsync(
            AppDbContext context,
            CashForWorkEvent cashForWorkEvent,
            IReadOnlyCollection<CashForWorkParticipant> participants,
            IReadOnlyCollection<int> releasedParticipantIds,
            decimal totalAmount)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public sealed class GgmsConsolidatedTransactionService : IGgmsConsolidatedTransactionService
    {
        private const string ConsolidatedTransactionsTable = "consolidated_transactions";
        private const int SharedColumnMaxLength = 45;
        private const string DefaultOfficeId = "OFF-2026-0006";
        private const string OfficeName = "eKalinga+";
        private const string AidRequestProjectName = "Aid Request";
        private const string ProjectDistributionProjectName = "Project Distribution";
        private const string CashForWorkProjectName = "Cash For Work";

        private static readonly DatabaseConnectionPreset DefaultConnection = new()
        {
            DisplayName = "GGMS Consolidated Transactions",
            Server = "194.59.164.58",
            Port = 3306,
            Database = "u621755393_ggms",
            Username = "u621755393_ggms_user",
            Password = "Ggms@2026"
        };

        private readonly DatabaseConnectionPreset _ggmsConnection;
        private readonly string _officeId;

        public GgmsConsolidatedTransactionService(BudgetRuntimeOptions? options = null)
        {
            var runtimeOptions = options ?? BudgetRuntimeOptions.Load();
            _ggmsConnection = ConnectionSettingsService.IsPresetConfigured(runtimeOptions.GgmsConnection)
                ? ClonePreset(runtimeOptions.GgmsConnection)
                : ClonePreset(DefaultConnection);
            _officeId = NormalizeAndLimit(runtimeOptions.AyudaOfficeCode, SharedColumnMaxLength)
                ?? DefaultOfficeId;
        }

        public async Task<string?> TryWriteAssistanceCaseReleaseAsync(AppDbContext context, AssistanceCase assistanceCase)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(assistanceCase);

            var beneficiary = await ResolveBeneficiaryAsync(
                context,
                assistanceCase.ValidatedBeneficiaryId,
                assistanceCase.ValidatedCivilRegistryId,
                assistanceCase.ValidatedBeneficiaryName,
                $"AMS-{assistanceCase.CaseNumber}");

            var entry = new GgmsConsolidatedTransactionEntry(
                beneficiary.BeneficiaryId,
                beneficiary.CivilRegistryId,
                NormalizeAndLimit($"AMS-{assistanceCase.CaseNumber}", SharedColumnMaxLength),
                AidRequestProjectName,
                _officeId,
                beneficiary.FullName,
                beneficiary.FirstName,
                beneficiary.MiddleName,
                beneficiary.LastName,
                OfficeName,
                NormalizeAndLimit(assistanceCase.AssistanceType, SharedColumnMaxLength) ?? "Aid Request",
                assistanceCase.ApprovedAmount,
                assistanceCase.UpdatedAt.Date,
                "Released");

            return await TryInsertEntriesAsync([entry]);
        }

        public async Task<string?> TryWriteProjectDistributionClaimAsync(AppDbContext context, AyudaProgram? program, AyudaProjectClaim claim)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(claim);

            var beneficiary = await ResolveBeneficiaryAsync(
                context,
                claim.BeneficiaryId,
                claim.CivilRegistryId,
                claim.FullName,
                $"AMS-PD-{claim.Id:D6}");

            var transactionType = NormalizeAndLimit(
                claim.AssistanceTypeSnapshot
                    ?? program?.AssistanceType
                    ?? program?.ProgramName
                    ?? "Project Claim",
                SharedColumnMaxLength);

            var entry = new GgmsConsolidatedTransactionEntry(
                beneficiary.BeneficiaryId,
                beneficiary.CivilRegistryId,
                NormalizeAndLimit($"AMS-PD-{claim.Id:D6}", SharedColumnMaxLength),
                ProjectDistributionProjectName,
                _officeId,
                beneficiary.FullName,
                beneficiary.FirstName,
                beneficiary.MiddleName,
                beneficiary.LastName,
                OfficeName,
                transactionType ?? "Project Claim",
                claim.UnitAmountSnapshot,
                claim.ClaimedAt.Date,
                "Released");

            return await TryInsertEntriesAsync([entry]);
        }

        public async Task<string?> TryWriteCashForWorkReleaseAsync(
            AppDbContext context,
            CashForWorkEvent cashForWorkEvent,
            IReadOnlyCollection<CashForWorkParticipant> participants,
            IReadOnlyCollection<int> releasedParticipantIds,
            decimal totalAmount)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(cashForWorkEvent);
            ArgumentNullException.ThrowIfNull(participants);
            ArgumentNullException.ThrowIfNull(releasedParticipantIds);

            if (releasedParticipantIds.Count <= 0)
            {
                return null;
            }

            var releasedParticipantSet = releasedParticipantIds.ToHashSet();
            var perParticipantAmount = totalAmount / releasedParticipantIds.Count;
            var budgetLabel = cashForWorkEvent.CashForWorkBudget?.BudgetName;
            if (string.IsNullOrWhiteSpace(budgetLabel) && cashForWorkEvent.CashForWorkBudgetId.HasValue)
            {
                budgetLabel = await context.CashForWorkBudgets
                    .AsNoTracking()
                    .Where(item => item.Id == cashForWorkEvent.CashForWorkBudgetId.Value)
                    .Select(item => item.BudgetName)
                    .FirstOrDefaultAsync();
            }

            var transactionType = NormalizeAndLimit(
                cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar
                    ? "Seminar Release"
                    : budgetLabel
                        ?? cashForWorkEvent.AyudaProgram?.ProgramName
                        ?? "Cash-for-Work Payout",
                SharedColumnMaxLength) ?? "Cash-for-Work Payout";
            var releaseDate = (cashForWorkEvent.ReleasedAt ?? DateTime.Now).Date;

            var entries = new List<GgmsConsolidatedTransactionEntry>();
            foreach (var participant in participants)
            {
                if (!releasedParticipantSet.Contains(participant.Id))
                {
                    continue;
                }

                var beneficiary = BuildBeneficiaryIdentity(
                    participant.Beneficiary,
                    participant.Beneficiary?.BeneficiaryId,
                    participant.Beneficiary?.CivilRegistryId,
                    BuildDisplayName(participant.Beneficiary),
                    $"AMS-{participant.BeneficiaryStagingId ?? participant.Id}");

                entries.Add(new GgmsConsolidatedTransactionEntry(
                    beneficiary.BeneficiaryId,
                    beneficiary.CivilRegistryId,
                    NormalizeAndLimit($"AMS-{cashForWorkEvent.Id:D6}-{participant.Id:D6}", SharedColumnMaxLength),
                    CashForWorkProjectName,
                    _officeId,
                    beneficiary.FullName,
                    beneficiary.FirstName,
                    beneficiary.MiddleName,
                    beneficiary.LastName,
                    OfficeName,
                    transactionType,
                    perParticipantAmount,
                    releaseDate,
                    "Released"));
            }

            return await TryInsertEntriesAsync(entries);
        }

        private async Task<string?> TryInsertEntriesAsync(IReadOnlyCollection<GgmsConsolidatedTransactionEntry> entries)
        {
            if (entries.Count == 0)
            {
                return null;
            }

            try
            {
                await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(_ggmsConnection));
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                var hasProjectNameColumn = await HasProjectNameColumnAsync(connection, transaction);

                foreach (var entry in entries)
                {
                    await using var command = new MySqlCommand(
                        BuildInsertCommandText(hasProjectNameColumn),
                        connection,
                        transaction);

                    command.Parameters.AddWithValue("@beneficiary_id", ToDbValue(entry.BeneficiaryId));
                    command.Parameters.AddWithValue("@civil_registry_id", ToDbValue(entry.CivilRegistryId));
                    command.Parameters.AddWithValue("@project_code", ToDbValue(entry.ProjectCode));
                    if (hasProjectNameColumn)
                    {
                        command.Parameters.AddWithValue("@project_name", ToDbValue(entry.ProjectName));
                    }
                    command.Parameters.AddWithValue("@office_id", ToDbValue(entry.OfficeId));
                    command.Parameters.AddWithValue("@full_name", ToDbValue(entry.FullName));
                    command.Parameters.AddWithValue("@first_name", ToDbValue(entry.FirstName));
                    command.Parameters.AddWithValue("@middle_name", ToDbValue(entry.MiddleName));
                    command.Parameters.AddWithValue("@last_name", ToDbValue(entry.LastName));
                    command.Parameters.AddWithValue("@office_name", ToDbValue(entry.OfficeName));
                    command.Parameters.AddWithValue("@transaction_type", ToDbValue(entry.TransactionType));
                    command.Parameters.AddWithValue("@amount", entry.Amount.HasValue ? entry.Amount.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@transaction_date", entry.TransactionDate.Date);
                    command.Parameters.AddWithValue("@status", ToDbValue(entry.Status));

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string BuildInsertCommandText(bool includeProjectNameColumn)
        {
            return includeProjectNameColumn
                ? $"""
                INSERT INTO {ConsolidatedTransactionsTable}
                (beneficiary_id, civil_registry_id, project_code, project_name, office_id,
                 full_name, first_name, middle_name, last_name,
                 office_name, transaction_type, amount, transaction_date, status)
                VALUES
                (@beneficiary_id, @civil_registry_id, @project_code, @project_name, @office_id,
                 @full_name, @first_name, @middle_name, @last_name,
                 @office_name, @transaction_type, @amount, @transaction_date, @status);
                """
                : $"""
                INSERT INTO {ConsolidatedTransactionsTable}
                (beneficiary_id, civil_registry_id, project_code, office_id,
                 full_name, first_name, middle_name, last_name,
                 office_name, transaction_type, amount, transaction_date, status)
                VALUES
                (@beneficiary_id, @civil_registry_id, @project_code, @office_id,
                 @full_name, @first_name, @middle_name, @last_name,
                 @office_name, @transaction_type, @amount, @transaction_date, @status);
                """;
        }

        private static async Task<bool> HasProjectNameColumnAsync(MySqlConnection connection, MySqlTransaction transaction)
        {
            await using var command = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = 'project_name';
                """,
                connection,
                transaction);

            command.Parameters.AddWithValue("@tableName", ConsolidatedTransactionsTable);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private static async Task<GgmsBeneficiaryIdentity> ResolveBeneficiaryAsync(
            AppDbContext context,
            string? beneficiaryId,
            string? civilRegistryId,
            string? fullName,
            string fallbackBeneficiaryId)
        {
            var normalizedBeneficiaryId = NormalizeNullable(beneficiaryId);
            var normalizedCivilRegistryId = NormalizeNullable(civilRegistryId);
            var normalizedFullName = NormalizeNullable(fullName);

            BeneficiaryStaging? beneficiary = null;

            // 1. Try BeneficiaryId (Case-Insensitive)
            if (!string.IsNullOrWhiteSpace(normalizedBeneficiaryId))
            {
                beneficiary = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.BeneficiaryId == normalizedBeneficiaryId);
            }

            // 2. Try CivilRegistryId (Case-Insensitive)
            if (beneficiary == null && !string.IsNullOrWhiteSpace(normalizedCivilRegistryId))
            {
                beneficiary = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.CivilRegistryId == normalizedCivilRegistryId);
            }

            // 3. Try FullName (Case-Insensitive & Trimmed)
            if (beneficiary == null && !string.IsNullOrWhiteSpace(normalizedFullName))
            {
                beneficiary = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => 
                        EF.Functions.Like(item.FullName, normalizedFullName) || 
                        item.FullName == normalizedFullName);
            }

            // 4. Try components if name lookup failed
            if (beneficiary == null && !string.IsNullOrWhiteSpace(normalizedFullName))
            {
                var parts = ParseNameParts(normalizedFullName);
                if (!string.IsNullOrWhiteSpace(parts.LastName) && !string.IsNullOrWhiteSpace(parts.FirstName))
                {
                    beneficiary = await context.BeneficiaryStaging
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => 
                            item.LastName == parts.LastName && 
                            item.FirstName == parts.FirstName);
                }
            }

            return BuildBeneficiaryIdentity(
                beneficiary,
                normalizedBeneficiaryId,
                normalizedCivilRegistryId,
                normalizedFullName,
                fallbackBeneficiaryId);
        }

        private static GgmsBeneficiaryIdentity BuildBeneficiaryIdentity(
            BeneficiaryStaging? beneficiary,
            string? fallbackBeneficiaryId,
            string? fallbackCivilRegistryId,
            string? fallbackFullName,
            string uniqueFallbackBeneficiaryId)
        {
            var fullName = NormalizeNullable(BuildDisplayName(beneficiary)) ?? NormalizeNullable(fallbackFullName);
            var firstName = NormalizeNullable(beneficiary?.FirstName);
            var middleName = NormalizeNullable(beneficiary?.MiddleName);
            var lastName = NormalizeNullable(beneficiary?.LastName);

            if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(fullName))
            {
                var parsed = ParseNameParts(fullName);
                firstName = parsed.FirstName;
                middleName ??= parsed.MiddleName;
                lastName ??= parsed.LastName;
            }

            var beneficiaryId = NormalizeNullable(beneficiary?.BeneficiaryId)
                ?? NormalizeNullable(fallbackBeneficiaryId)
                ?? NormalizeNullable(uniqueFallbackBeneficiaryId)
                ?? $"GGMS-{Guid.NewGuid():N}"[..SharedColumnMaxLength];

            var resolvedFullName = NormalizeNullable(fullName)
                ?? string.Join(" ", new[] { firstName, middleName, lastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            return new GgmsBeneficiaryIdentity(
                NormalizeAndLimit(beneficiaryId, SharedColumnMaxLength) ?? NormalizeAndLimit(uniqueFallbackBeneficiaryId, SharedColumnMaxLength)!,
                NormalizeAndLimit(beneficiary?.CivilRegistryId ?? fallbackCivilRegistryId, SharedColumnMaxLength),
                NormalizeAndLimit(resolvedFullName, SharedColumnMaxLength),
                NormalizeAndLimit(firstName, SharedColumnMaxLength),
                NormalizeAndLimit(middleName, SharedColumnMaxLength),
                NormalizeAndLimit(lastName, SharedColumnMaxLength));
        }

        private static (string? FirstName, string? MiddleName, string? LastName) ParseNameParts(string fullName)
        {
            var parts = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return parts.Length switch
            {
                0 => (null, null, null),
                1 => (parts[0], null, null),
                2 => (parts[0], null, parts[1]),
                _ => (parts[0], string.Join(" ", parts.Skip(1).Take(parts.Length - 2)), parts[^1])
            };
        }

        private static string? BuildDisplayName(BeneficiaryStaging? beneficiary)
        {
            if (beneficiary == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(beneficiary.FullName))
            {
                return beneficiary.FullName.Trim();
            }

            return string.Join(
                " ",
                new[] { beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
        }

        private static DatabaseConnectionPreset ClonePreset(DatabaseConnectionPreset preset)
        {
            return new DatabaseConnectionPreset
            {
                DisplayName = preset.DisplayName,
                Server = preset.Server,
                Port = preset.Port,
                Database = preset.Database,
                Username = preset.Username,
                Password = preset.Password
            };
        }

        private static object ToDbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value;
        }

        private static string? NormalizeAndLimit(string? value, int maxLength)
        {
            var normalized = NormalizeNullable(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed record GgmsConsolidatedTransactionEntry(
            string? BeneficiaryId,
            string? CivilRegistryId,
            string? ProjectCode,
            string? ProjectName,
            string? OfficeId,
            string? FullName,
            string? FirstName,
            string? MiddleName,
            string? LastName,
            string? OfficeName,
            string? TransactionType,
            decimal? Amount,
            DateTime TransactionDate,
            string Status);

        private sealed record GgmsBeneficiaryIdentity(
            string BeneficiaryId,
            string? CivilRegistryId,
            string? FullName,
            string? FirstName,
            string? MiddleName,
            string? LastName);
    }
}

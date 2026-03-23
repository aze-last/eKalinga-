using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record AssistanceLedgerOperationResult(bool IsSuccess, string Message, int? EntryId = null);

    public sealed record AssistanceLedgerManualEntryRequest(
        int StagingId,
        decimal Amount,
        DateTime ReleaseDate,
        string? Remarks);

    public sealed record BeneficiaryAssistanceWarningSummary(
        string? CivilRegistryId,
        string? BeneficiaryId,
        decimal TotalAmountReceived,
        decimal WarningThreshold,
        bool IsAboveThreshold,
        int EntryCount,
        DateTime? MostRecentReleaseDate);

    public sealed class BeneficiaryAssistanceLedgerService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public BeneficiaryAssistanceLedgerService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        public async Task<AssistanceLedgerOperationResult> RecordManualEntryAsync(AssistanceLedgerManualEntryRequest request, int recordedByUserId)
        {
            if (request.Amount <= 0)
            {
                return new AssistanceLedgerOperationResult(false, "Assistance amount must be greater than zero.");
            }

            var remarks = NormalizeNullable(request.Remarks);
            if (remarks == null)
            {
                return new AssistanceLedgerOperationResult(false, "Provide remarks before recording manual assistance history.");
            }

            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == request.StagingId);

            if (staging == null)
            {
                return new AssistanceLedgerOperationResult(false, "The selected imported beneficiary no longer exists.");
            }

            var entry = new BeneficiaryAssistanceLedgerEntry
            {
                CivilRegistryId = NormalizeNullable(staging.CivilRegistryId),
                BeneficiaryId = NormalizeNullable(staging.BeneficiaryId),
                SourceModule = BeneficiaryAssistanceSourceModule.ManualHistory,
                SourceRecordId = $"staging:{staging.StagingID}",
                ReleaseDate = request.ReleaseDate.Date,
                Amount = request.Amount,
                Remarks = remarks,
                RecordedByUserId = recordedByUserId,
                CreatedAt = DateTime.Now
            };

            _context.BeneficiaryAssistanceLedgerEntries.Add(entry);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                recordedByUserId,
                "BeneficiaryAssistanceRecorded",
                "BeneficiaryAssistanceLedgerEntry",
                entry.Id,
                $"Recorded {entry.Amount:N2} assistance history for beneficiary '{entry.CivilRegistryId ?? entry.BeneficiaryId ?? "unknown"}'.");

            return new AssistanceLedgerOperationResult(true, "Recorded assistance history.", entry.Id);
        }

        public async Task<BeneficiaryAssistanceWarningSummary> GetWarningSummaryAsync(string? civilRegistryId, string? beneficiaryId, decimal warningThreshold)
        {
            var normalizedCivilRegistryId = NormalizeNullable(civilRegistryId);
            var normalizedBeneficiaryId = NormalizeNullable(beneficiaryId);
            var matchingEntries = await QueryByIdentity(normalizedCivilRegistryId, normalizedBeneficiaryId)
                .OrderByDescending(item => item.ReleaseDate)
                .ToListAsync();

            var total = matchingEntries.Sum(item => item.Amount);
            return new BeneficiaryAssistanceWarningSummary(
                normalizedCivilRegistryId,
                normalizedBeneficiaryId,
                total,
                warningThreshold,
                total >= warningThreshold && warningThreshold > 0,
                matchingEntries.Count,
                matchingEntries.FirstOrDefault()?.ReleaseDate);
        }

        public async Task<BeneficiaryAssistanceWarningSummary> GetWarningSummaryForStagingAsync(int stagingId, decimal warningThreshold)
        {
            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == stagingId);

            if (staging == null)
            {
                return new BeneficiaryAssistanceWarningSummary(null, null, 0m, warningThreshold, false, 0, null);
            }

            return await GetWarningSummaryAsync(staging.CivilRegistryId, staging.BeneficiaryId, warningThreshold);
        }

        public async Task<IReadOnlyList<BeneficiaryAssistanceLedgerEntry>> GetEntriesAsync(string? civilRegistryId, string? beneficiaryId)
        {
            var normalizedCivilRegistryId = NormalizeNullable(civilRegistryId);
            var normalizedBeneficiaryId = NormalizeNullable(beneficiaryId);

            return await QueryByIdentity(normalizedCivilRegistryId, normalizedBeneficiaryId)
                .OrderByDescending(item => item.ReleaseDate)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<BeneficiaryAssistanceLedgerEntry>> GetEntriesForStagingAsync(int stagingId)
        {
            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == stagingId);

            if (staging == null)
            {
                return Array.Empty<BeneficiaryAssistanceLedgerEntry>();
            }

            return await GetEntriesAsync(staging.CivilRegistryId, staging.BeneficiaryId);
        }

        private IQueryable<BeneficiaryAssistanceLedgerEntry> QueryByIdentity(string? civilRegistryId, string? beneficiaryId)
        {
            var query = _context.BeneficiaryAssistanceLedgerEntries
                .AsNoTracking();

            if (civilRegistryId != null)
            {
                return query.Where(item => item.CivilRegistryId == civilRegistryId);
            }

            if (beneficiaryId != null)
            {
                return query.Where(item => item.BeneficiaryId == beneficiaryId);
            }

            return query.Where(_ => false);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

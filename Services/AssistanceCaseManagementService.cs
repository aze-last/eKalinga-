using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record AssistanceCaseOperationResult(bool IsSuccess, string Message, int? AssistanceCaseId = null);

    public sealed record AssistanceCaseUpsertRequest(
        int? HouseholdId,
        int? HouseholdMemberId,
        string? ValidatedBeneficiaryName,
        string? ValidatedBeneficiaryId,
        string? ValidatedCivilRegistryId,
        string AssistanceType,
        AssistanceCasePriority Priority,
        AssistanceReleaseKind ReleaseKind,
        decimal? AssistanceAmount,
        DateTime RequestedOn,
        DateTime? ScheduledReleaseDate,
        string? Summary,
        int? AyudaProgramId = null);

    public sealed class AssistanceCaseManagementService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public AssistanceCaseManagementService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        public async Task<AssistanceCaseOperationResult> CreateAsync(AssistanceCaseUpsertRequest request, int actedByUserId)
        {
            var validatedBeneficiaryName = NormalizeNullable(request.ValidatedBeneficiaryName);
            var validation = await ValidateReferencesAsync(
                validatedBeneficiaryName,
                request.AyudaProgramId);
            if (validation is not null)
            {
                return validation;
            }

            var assistanceCase = new AssistanceCase
            {
                CaseNumber = await GenerateCaseNumberAsync(),
                HouseholdId = null,
                HouseholdMemberId = null,
                ValidatedBeneficiaryName = validatedBeneficiaryName,
                ValidatedBeneficiaryId = NormalizeNullable(request.ValidatedBeneficiaryId),
                ValidatedCivilRegistryId = NormalizeNullable(request.ValidatedCivilRegistryId),
                AssistanceType = NormalizeRequired(request.AssistanceType),
                ReleaseKind = request.ReleaseKind,
                Priority = request.Priority,
                Status = AssistanceCaseStatus.Pending,
                RequestedAmount = request.AssistanceAmount,
                ApprovedAmount = request.AssistanceAmount,
                RequestedOn = request.RequestedOn,
                ScheduledReleaseDate = request.ScheduledReleaseDate,
                Summary = NormalizeNullable(request.Summary),
                Notes = null,
                AyudaProgramId = request.AyudaProgramId,
                CreatedByUserId = actedByUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.AssistanceCases.Add(assistanceCase);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseCreated",
                "AssistanceCase",
                assistanceCase.Id,
                $"Created aid request '{assistanceCase.CaseNumber}'.");

            return new AssistanceCaseOperationResult(
                true,
                $"Created aid request {assistanceCase.CaseNumber}.",
                assistanceCase.Id);
        }

        public async Task<AssistanceCaseOperationResult> UpdateAsync(int assistanceCaseId, AssistanceCaseUpsertRequest request, int actedByUserId)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            if (assistanceCase.Status is AssistanceCaseStatus.Closed or AssistanceCaseStatus.Cancelled)
            {
                return new AssistanceCaseOperationResult(false, "Closed or cancelled aid requests can no longer be edited.");
            }

            var validatedBeneficiaryName = NormalizeNullable(request.ValidatedBeneficiaryName);
            var validation = await ValidateReferencesAsync(
                validatedBeneficiaryName,
                request.AyudaProgramId);
            if (validation is not null)
            {
                return validation;
            }

            assistanceCase.HouseholdId = null;
            assistanceCase.HouseholdMemberId = null;
            assistanceCase.ValidatedBeneficiaryName = validatedBeneficiaryName;
            assistanceCase.ValidatedBeneficiaryId = NormalizeNullable(request.ValidatedBeneficiaryId);
            assistanceCase.ValidatedCivilRegistryId = NormalizeNullable(request.ValidatedCivilRegistryId);
            assistanceCase.AssistanceType = NormalizeRequired(request.AssistanceType);
            assistanceCase.ReleaseKind = request.ReleaseKind;
            assistanceCase.Priority = request.Priority;
            assistanceCase.RequestedAmount = request.AssistanceAmount;
            assistanceCase.ApprovedAmount = request.AssistanceAmount;
            assistanceCase.RequestedOn = request.RequestedOn;
            assistanceCase.ScheduledReleaseDate = request.ScheduledReleaseDate;
            assistanceCase.Summary = NormalizeNullable(request.Summary);
            assistanceCase.Notes = null;
            assistanceCase.AyudaProgramId = request.AyudaProgramId;
            assistanceCase.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseUpdated",
                "AssistanceCase",
                assistanceCase.Id,
                $"Updated aid request '{assistanceCase.CaseNumber}'.");

            return new AssistanceCaseOperationResult(
                true,
                $"Updated aid request {assistanceCase.CaseNumber}.",
                assistanceCase.Id);
        }

        public async Task<AssistanceCaseOperationResult> ChangeStatusAsync(int assistanceCaseId, AssistanceCaseStatus targetStatus, int actedByUserId, string? resolutionNotes)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            if (targetStatus == AssistanceCaseStatus.Released)
            {
                if (!assistanceCase.ApprovedAmount.HasValue || assistanceCase.ApprovedAmount.Value <= 0)
                {
                    return new AssistanceCaseOperationResult(false, "Assistance amount is required before releasing this aid request.");
                }

                if (!assistanceCase.AyudaProgramId.HasValue)
                {
                    return new AssistanceCaseOperationResult(false, "Select an ayuda program before releasing this aid request.");
                }

                if (assistanceCase.BudgetLedgerEntryId.HasValue)
                {
                    return new AssistanceCaseOperationResult(false, "This aid request already has a recorded budget release.");
                }

                var budgetService = new BudgetManagementService(_context, _auditService);
                var budgetResult = await budgetService.RecordReleaseAsync(
                    new BudgetReleaseRequest(
                        assistanceCase.AyudaProgramId.Value,
                        BudgetLedgerFeatureSource.AssistanceCase,
                        $"assistance:{assistanceCase.Id}",
                        1,
                        assistanceCase.ReleaseKind,
                        assistanceCase.ApprovedAmount.Value,
                        DateTime.Now,
                        assistanceCase.Summary ?? assistanceCase.AssistanceType),
                    actedByUserId);

                if (!budgetResult.IsSuccess)
                {
                    return new AssistanceCaseOperationResult(false, budgetResult.Message);
                }

                assistanceCase.BudgetLedgerEntryId = budgetResult.LedgerEntryId;
            }

            assistanceCase.Status = targetStatus;
            assistanceCase.ResolutionNotes = null;
            assistanceCase.ReviewedByUserId = actedByUserId;
            assistanceCase.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseStatusChanged",
                "AssistanceCase",
                assistanceCase.Id,
                $"Changed aid request '{assistanceCase.CaseNumber}' to {targetStatus}.");

            return new AssistanceCaseOperationResult(
                true,
                $"Updated {assistanceCase.CaseNumber} to {targetStatus}.",
                assistanceCase.Id);
        }

        public async Task<AssistanceCaseOperationResult> DeleteAsync(int assistanceCaseId, int actedByUserId)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            if (assistanceCase.BudgetLedgerEntryId.HasValue)
            {
                return new AssistanceCaseOperationResult(false, "This aid request is already connected to budget and cannot be deleted.");
            }

            _context.AssistanceCases.Remove(assistanceCase);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseDeleted",
                "AssistanceCase",
                assistanceCaseId,
                $"Deleted aid request '{assistanceCase.CaseNumber}'.");

            return new AssistanceCaseOperationResult(true, $"Deleted aid request {assistanceCase.CaseNumber}.");
        }

        private async Task<AssistanceCaseOperationResult?> ValidateReferencesAsync(string? validatedBeneficiaryName, int? ayudaProgramId)
        {
            if (string.IsNullOrWhiteSpace(validatedBeneficiaryName))
            {
                return new AssistanceCaseOperationResult(false, "Select a validated beneficiary before saving this aid request.");
            }

            if (!ayudaProgramId.HasValue)
            {
                return null;
            }

            var programExists = await _context.AyudaPrograms
                .AsNoTracking()
                .AnyAsync(item => item.Id == ayudaProgramId.Value && item.IsActive);

            return programExists
                ? null
                : new AssistanceCaseOperationResult(false, "The selected ayuda program no longer exists.");
        }

        private async Task<string> GenerateCaseNumberAsync()
        {
            var prefix = $"AR-{DateTime.Now:yyyyMMdd}";
            var nextSequence = await _context.AssistanceCases
                .CountAsync(item => item.CaseNumber.StartsWith(prefix)) + 1;

            return $"{prefix}-{nextSequence:0000}";
        }

        private static string NormalizeRequired(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException("Assistance type is required.")
                : value.Trim();
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

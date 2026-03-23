using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record AssistanceCaseOperationResult(bool IsSuccess, string Message, int? AssistanceCaseId = null);

    public sealed record AssistanceCaseUpsertRequest(
        int HouseholdId,
        int? HouseholdMemberId,
        string AssistanceType,
        AssistanceCasePriority Priority,
        decimal? RequestedAmount,
        decimal? ApprovedAmount,
        DateTime RequestedOn,
        DateTime? ScheduledReleaseDate,
        string? Summary,
        string? Notes);

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
            var validation = await ValidateReferencesAsync(request.HouseholdId, request.HouseholdMemberId);
            if (validation is not null)
            {
                return validation;
            }

            var assistanceCase = new AssistanceCase
            {
                CaseNumber = await GenerateCaseNumberAsync(),
                HouseholdId = request.HouseholdId,
                HouseholdMemberId = request.HouseholdMemberId,
                AssistanceType = NormalizeRequired(request.AssistanceType),
                Priority = request.Priority,
                Status = AssistanceCaseStatus.Pending,
                RequestedAmount = request.RequestedAmount,
                ApprovedAmount = request.ApprovedAmount,
                RequestedOn = request.RequestedOn,
                ScheduledReleaseDate = request.ScheduledReleaseDate,
                Summary = NormalizeNullable(request.Summary),
                Notes = NormalizeNullable(request.Notes),
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
                $"Created assistance case '{assistanceCase.CaseNumber}' for household #{assistanceCase.HouseholdId}.");

            return new AssistanceCaseOperationResult(
                true,
                $"Created assistance case {assistanceCase.CaseNumber}.",
                assistanceCase.Id);
        }

        public async Task<AssistanceCaseOperationResult> UpdateAsync(int assistanceCaseId, AssistanceCaseUpsertRequest request, int actedByUserId)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected assistance case no longer exists.");
            }

            if (assistanceCase.Status is AssistanceCaseStatus.Closed or AssistanceCaseStatus.Cancelled)
            {
                return new AssistanceCaseOperationResult(false, "Closed or cancelled cases can no longer be edited.");
            }

            var validation = await ValidateReferencesAsync(request.HouseholdId, request.HouseholdMemberId);
            if (validation is not null)
            {
                return validation;
            }

            assistanceCase.HouseholdId = request.HouseholdId;
            assistanceCase.HouseholdMemberId = request.HouseholdMemberId;
            assistanceCase.AssistanceType = NormalizeRequired(request.AssistanceType);
            assistanceCase.Priority = request.Priority;
            assistanceCase.RequestedAmount = request.RequestedAmount;
            assistanceCase.ApprovedAmount = request.ApprovedAmount;
            assistanceCase.RequestedOn = request.RequestedOn;
            assistanceCase.ScheduledReleaseDate = request.ScheduledReleaseDate;
            assistanceCase.Summary = NormalizeNullable(request.Summary);
            assistanceCase.Notes = NormalizeNullable(request.Notes);
            assistanceCase.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseUpdated",
                "AssistanceCase",
                assistanceCase.Id,
                $"Updated assistance case '{assistanceCase.CaseNumber}'.");

            return new AssistanceCaseOperationResult(
                true,
                $"Updated assistance case {assistanceCase.CaseNumber}.",
                assistanceCase.Id);
        }

        public async Task<AssistanceCaseOperationResult> ChangeStatusAsync(int assistanceCaseId, AssistanceCaseStatus targetStatus, int actedByUserId, string? resolutionNotes)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected assistance case no longer exists.");
            }

            if (targetStatus is AssistanceCaseStatus.Rejected or AssistanceCaseStatus.Closed or AssistanceCaseStatus.Cancelled &&
                string.IsNullOrWhiteSpace(resolutionNotes))
            {
                return new AssistanceCaseOperationResult(false, "Provide resolution notes before changing to this status.");
            }

            assistanceCase.Status = targetStatus;
            assistanceCase.ResolutionNotes = NormalizeNullable(resolutionNotes);
            assistanceCase.ReviewedByUserId = actedByUserId;
            assistanceCase.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseStatusChanged",
                "AssistanceCase",
                assistanceCase.Id,
                $"Changed assistance case '{assistanceCase.CaseNumber}' to {targetStatus}.");

            return new AssistanceCaseOperationResult(
                true,
                $"Updated {assistanceCase.CaseNumber} to {targetStatus}.",
                assistanceCase.Id);
        }

        private async Task<AssistanceCaseOperationResult?> ValidateReferencesAsync(int householdId, int? householdMemberId)
        {
            var householdExists = await _context.Households
                .AsNoTracking()
                .AnyAsync(item => item.Id == householdId);

            if (!householdExists)
            {
                return new AssistanceCaseOperationResult(false, "The selected household no longer exists.");
            }

            if (!householdMemberId.HasValue)
            {
                return null;
            }

            var memberExists = await _context.HouseholdMembers
                .AsNoTracking()
                .AnyAsync(item => item.Id == householdMemberId.Value && item.HouseholdId == householdId);

            return memberExists
                ? null
                : new AssistanceCaseOperationResult(false, "The selected household member does not belong to the selected household.");
        }

        private async Task<string> GenerateCaseNumberAsync()
        {
            var prefix = $"AC-{DateTime.Now:yyyyMMdd}";
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

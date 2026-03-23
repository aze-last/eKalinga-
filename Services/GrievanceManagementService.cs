using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record GrievanceOperationResult(bool IsSuccess, string Message, int? GrievanceId = null);

    public sealed record GrievanceCreateRequest(
        int StagingId,
        GrievanceType Type,
        string Title,
        string Description,
        int? AssistanceCaseId,
        int? CashForWorkEventId);

    public sealed record GrievanceUpdateRequest(
        GrievanceType Type,
        string Title,
        string Description,
        int? AssistanceCaseId,
        int? CashForWorkEventId,
        int? AssignedToUserId);

    public sealed class GrievanceManagementService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public GrievanceManagementService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        public async Task<GrievanceOperationResult> CreateAsync(GrievanceCreateRequest request, int actedByUserId)
        {
            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == request.StagingId);

            if (staging == null)
            {
                return new GrievanceOperationResult(false, "The selected imported beneficiary no longer exists.");
            }

            var grievance = new GrievanceRecord
            {
                GrievanceNumber = await GenerateGrievanceNumberAsync(),
                CivilRegistryId = NormalizeNullable(staging.CivilRegistryId),
                BeneficiaryId = NormalizeNullable(staging.BeneficiaryId),
                StagingId = staging.StagingID,
                AssistanceCaseId = request.AssistanceCaseId,
                CashForWorkEventId = request.CashForWorkEventId,
                Type = request.Type,
                Status = GrievanceStatus.Open,
                Title = NormalizeRequired(request.Title, "Grievance title is required."),
                Description = NormalizeRequired(request.Description, "Grievance description is required."),
                FiledByUserId = actedByUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.GrievanceRecords.Add(grievance);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "GrievanceCreated",
                "GrievanceRecord",
                grievance.Id,
                $"Created grievance '{grievance.GrievanceNumber}' for beneficiary staging #{grievance.StagingId}.");

            return new GrievanceOperationResult(true, $"Created grievance {grievance.GrievanceNumber}.", grievance.Id);
        }

        public async Task<GrievanceOperationResult> UpdateAsync(int grievanceId, GrievanceUpdateRequest request, int actedByUserId, string? correctionRemarks)
        {
            var grievance = await _context.GrievanceRecords
                .FirstOrDefaultAsync(item => item.Id == grievanceId);

            if (grievance == null)
            {
                return new GrievanceOperationResult(false, "The selected grievance no longer exists.");
            }

            var normalizedTitle = NormalizeRequired(request.Title, "Grievance title is required.");
            var normalizedDescription = NormalizeRequired(request.Description, "Grievance description is required.");
            var hasSensitiveChange =
                grievance.Type != request.Type
                || !string.Equals(grievance.Title, normalizedTitle, StringComparison.Ordinal)
                || !string.Equals(grievance.Description, normalizedDescription, StringComparison.Ordinal)
                || grievance.AssistanceCaseId != request.AssistanceCaseId
                || grievance.CashForWorkEventId != request.CashForWorkEventId
                || grievance.AssignedToUserId != request.AssignedToUserId;

            if (hasSensitiveChange && string.IsNullOrWhiteSpace(correctionRemarks))
            {
                return new GrievanceOperationResult(false, "Provide correction remarks before changing grievance details.");
            }

            grievance.Type = request.Type;
            grievance.Title = normalizedTitle;
            grievance.Description = normalizedDescription;
            grievance.AssistanceCaseId = request.AssistanceCaseId;
            grievance.CashForWorkEventId = request.CashForWorkEventId;
            grievance.AssignedToUserId = request.AssignedToUserId;
            grievance.ResolutionRemarks = NormalizeNullable(correctionRemarks);
            grievance.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "GrievanceUpdated",
                "GrievanceRecord",
                grievance.Id,
                $"Updated grievance '{grievance.GrievanceNumber}'.");

            return new GrievanceOperationResult(true, $"Updated grievance {grievance.GrievanceNumber}.", grievance.Id);
        }

        public async Task<GrievanceOperationResult> ChangeStatusAsync(int grievanceId, GrievanceStatus targetStatus, int actedByUserId, string? remarks)
        {
            var grievance = await _context.GrievanceRecords
                .FirstOrDefaultAsync(item => item.Id == grievanceId);

            if (grievance == null)
            {
                return new GrievanceOperationResult(false, "The selected grievance no longer exists.");
            }

            if (targetStatus == GrievanceStatus.Rejected && string.IsNullOrWhiteSpace(remarks))
            {
                return new GrievanceOperationResult(false, "Provide rejection remarks before rejecting this grievance.");
            }

            grievance.Status = targetStatus;
            grievance.ResolutionRemarks = NormalizeNullable(remarks) ?? grievance.ResolutionRemarks;
            grievance.ResolvedAt = targetStatus is GrievanceStatus.Resolved or GrievanceStatus.Rejected
                ? DateTime.Now
                : null;
            grievance.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "GrievanceStatusChanged",
                "GrievanceRecord",
                grievance.Id,
                $"Changed grievance '{grievance.GrievanceNumber}' to {targetStatus}.");

            return new GrievanceOperationResult(true, $"Updated grievance {grievance.GrievanceNumber} to {targetStatus}.", grievance.Id);
        }

        private async Task<string> GenerateGrievanceNumberAsync()
        {
            var prefix = $"GR-{DateTime.Now:yyyyMMdd}";
            var nextSequence = await _context.GrievanceRecords
                .CountAsync(item => item.GrievanceNumber.StartsWith(prefix)) + 1;

            return $"{prefix}-{nextSequence:0000}";
        }

        private static string NormalizeRequired(string? value, string message)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException(message)
                : value.Trim();
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

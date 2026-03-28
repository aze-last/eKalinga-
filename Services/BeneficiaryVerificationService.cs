using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record BeneficiaryVerificationOperationResult(bool IsSuccess, string Message);

    public sealed record BeneficiaryApprovalRequest(
        int StagingId,
        int HouseholdId,
        int? ExistingHouseholdMemberId,
        string? ReviewNotes,
        BeneficiaryCorrectionRequest? Corrections = null);

    public sealed record BeneficiaryCorrectionRequest(
        int StagingId,
        string? BeneficiaryId,
        string? CivilRegistryId,
        string? FirstName,
        string? MiddleName,
        string? LastName,
        string? FullName,
        string? Sex,
        string? DateOfBirth,
        string? Age,
        string? MaritalStatus,
        string? Address,
        string? PwdIdNo,
        string? SeniorIdNo,
        string? DisabilityType,
        string? ReviewNotes);

    public sealed class BeneficiaryVerificationService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public BeneficiaryVerificationService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        public async Task<BeneficiaryVerificationOperationResult> ApproveAsync(BeneficiaryApprovalRequest request, int actedByUserId)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == request.StagingId);

            if (stagingRow == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected staging record no longer exists.");
            }

            if (stagingRow.VerificationStatus is not VerificationStatus.Pending and not VerificationStatus.Verified)
            {
                return new BeneficiaryVerificationOperationResult(false, "Only pending or verified records can be approved.");
            }

            var household = await _context.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.HouseholdId);

            if (household == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected household no longer exists.");
            }

            if (request.Corrections != null)
            {
                ApplyCorrections(stagingRow, request.Corrections, actedByUserId);
            }

            var fullName = BuildDisplayName(stagingRow);
            HouseholdMember? createdHouseholdMember = null;
            var linkedHouseholdMemberId = request.ExistingHouseholdMemberId;

            if (linkedHouseholdMemberId.HasValue)
            {
                var existingMember = await _context.HouseholdMembers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(member =>
                        member.Id == linkedHouseholdMemberId.Value &&
                        member.HouseholdId == request.HouseholdId);

                if (existingMember == null)
                {
                    return new BeneficiaryVerificationOperationResult(false, "The selected household member no longer exists.");
                }
            }
            else
            {
                createdHouseholdMember = new HouseholdMember
                {
                    HouseholdId = household.Id,
                    FullName = fullName,
                    RelationshipToHead = "Imported Beneficiary",
                    Occupation = "Unspecified",
                    IsCashForWorkEligible = false,
                    Notes = BuildApprovalNotes(stagingRow, request.ReviewNotes),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.HouseholdMembers.Add(createdHouseholdMember);
            }

            stagingRow.VerificationStatus = VerificationStatus.Approved;
            stagingRow.LinkedHouseholdId = request.HouseholdId;
            stagingRow.ReviewNotes = NormalizeNullable(request.ReviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
            await _context.SaveChangesAsync();

            if (createdHouseholdMember != null)
            {
                linkedHouseholdMemberId = createdHouseholdMember.Id;
            }

            stagingRow.LinkedHouseholdMemberId = linkedHouseholdMemberId;
            await _context.SaveChangesAsync();

            var digitalIdService = new BeneficiaryDigitalIdService(_context, _auditService);
            await digitalIdService.EnsureIssuedAsync(stagingRow.StagingID, actedByUserId);

            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryApproved",
                "BeneficiaryStaging",
                request.StagingId,
                linkedHouseholdMemberId.HasValue && createdHouseholdMember == null
                    ? $"Approved '{fullName}' by linking to existing household member #{linkedHouseholdMemberId.Value} in household '{household.HouseholdCode}'."
                    : $"Approved '{fullName}' into household '{household.HouseholdCode}' as a new household member.");

            return new BeneficiaryVerificationOperationResult(
                true,
                linkedHouseholdMemberId.HasValue && createdHouseholdMember == null
                    ? $"Approved {fullName} by linking to an existing member in household {household.HouseholdCode}."
                    : $"Approved {fullName} into household {household.HouseholdCode}.");
        }

        public async Task<BeneficiaryVerificationOperationResult> SaveCorrectionsAsync(BeneficiaryCorrectionRequest request, int actedByUserId)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == request.StagingId);

            if (stagingRow == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected staging record no longer exists.");
            }

            if (stagingRow.VerificationStatus == VerificationStatus.Approved)
            {
                return new BeneficiaryVerificationOperationResult(false, "Approved records can no longer be edited from staging.");
            }

            stagingRow.BeneficiaryId = NormalizeNullable(request.BeneficiaryId);
            stagingRow.CivilRegistryId = NormalizeNullable(request.CivilRegistryId);
            stagingRow.FirstName = NormalizeNullable(request.FirstName);
            stagingRow.MiddleName = NormalizeNullable(request.MiddleName);
            stagingRow.LastName = NormalizeNullable(request.LastName);
            stagingRow.FullName = NormalizeNullable(request.FullName) ?? BuildDisplayName(stagingRow);
            stagingRow.Sex = NormalizeNullable(request.Sex);
            stagingRow.DateOfBirth = NormalizeNullable(request.DateOfBirth);
            stagingRow.Age = NormalizeNullable(request.Age);
            stagingRow.MaritalStatus = NormalizeNullable(request.MaritalStatus);
            stagingRow.Address = NormalizeNullable(request.Address);
            stagingRow.PwdIdNo = NormalizeNullable(request.PwdIdNo);
            stagingRow.SeniorIdNo = NormalizeNullable(request.SeniorIdNo);
            stagingRow.DisabilityType = NormalizeNullable(request.DisabilityType);
            stagingRow.ReviewNotes = NormalizeNullable(request.ReviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryCorrected",
                "BeneficiaryStaging",
                request.StagingId,
                $"Saved registry corrections for '{BuildDisplayName(stagingRow)}'.");

            return new BeneficiaryVerificationOperationResult(true, $"Saved corrections for {BuildDisplayName(stagingRow)}.");
        }

        public async Task<BeneficiaryVerificationOperationResult> VerifyAsync(int stagingId, int actedByUserId, string? reviewNotes)
        {
            return await UpdateStatusAsync(
                stagingId,
                VerificationStatus.Verified,
                actedByUserId,
                reviewNotes,
                "BeneficiaryVerified",
                "Verified staged beneficiary",
                requireReason: false);
        }

        public async Task<BeneficiaryVerificationOperationResult> MarkDuplicateAsync(int stagingId, int actedByUserId, string? reviewNotes)
        {
            return await UpdateStatusAsync(
                stagingId,
                VerificationStatus.Duplicate,
                actedByUserId,
                reviewNotes,
                "BeneficiaryMarkedDuplicate",
                "Marked staged beneficiary as duplicate",
                requireReason: true);
        }

        public async Task<BeneficiaryVerificationOperationResult> MarkInactiveAsync(int stagingId, int actedByUserId, string? reviewNotes)
        {
            return await UpdateStatusAsync(
                stagingId,
                VerificationStatus.Inactive,
                actedByUserId,
                reviewNotes,
                "BeneficiaryMarkedInactive",
                "Marked staged beneficiary as inactive",
                requireReason: true);
        }

        public async Task<BeneficiaryVerificationOperationResult> RejectAsync(int stagingId, int actedByUserId, string? reviewNotes)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == stagingId);

            if (stagingRow == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected staging record no longer exists.");
            }

            if (stagingRow.VerificationStatus == VerificationStatus.Approved)
            {
                return new BeneficiaryVerificationOperationResult(false, "Approved records cannot be rejected from staging.");
            }

            if (string.IsNullOrWhiteSpace(reviewNotes))
            {
                return new BeneficiaryVerificationOperationResult(false, "Provide a reason before rejecting the staged beneficiary.");
            }

            stagingRow.VerificationStatus = VerificationStatus.Rejected;
            stagingRow.ReviewNotes = NormalizeNullable(reviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
            stagingRow.LinkedHouseholdId = null;
            stagingRow.LinkedHouseholdMemberId = null;
            await _context.SaveChangesAsync();

            var fullName = BuildDisplayName(stagingRow);
            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryRejected",
                "BeneficiaryStaging",
                stagingRow.StagingID,
                $"Rejected staged beneficiary '{fullName}'.");

            return new BeneficiaryVerificationOperationResult(true, $"Rejected {fullName}.");
        }

        private async Task<BeneficiaryVerificationOperationResult> UpdateStatusAsync(
            int stagingId,
            VerificationStatus targetStatus,
            int actedByUserId,
            string? reviewNotes,
            string auditAction,
            string successVerb,
            bool requireReason)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == stagingId);

            if (stagingRow == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected staging record no longer exists.");
            }

            if (stagingRow.VerificationStatus == VerificationStatus.Approved)
            {
                return new BeneficiaryVerificationOperationResult(false, "Approved records can no longer be updated from staging.");
            }

            if (requireReason && string.IsNullOrWhiteSpace(reviewNotes))
            {
                return new BeneficiaryVerificationOperationResult(false, "Provide a reason before changing this beneficiary status.");
            }

            stagingRow.VerificationStatus = targetStatus;
            stagingRow.ReviewNotes = NormalizeNullable(reviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
            stagingRow.LinkedHouseholdId = null;
            stagingRow.LinkedHouseholdMemberId = null;
            await _context.SaveChangesAsync();

            var fullName = BuildDisplayName(stagingRow);
            await _auditService.LogActivityAsync(
                actedByUserId,
                auditAction,
                "BeneficiaryStaging",
                stagingRow.StagingID,
                $"{successVerb} '{fullName}'.");

            return new BeneficiaryVerificationOperationResult(true, $"{successVerb} {fullName}.");
        }

        private static void ApplyCorrections(BeneficiaryStaging stagingRow, BeneficiaryCorrectionRequest request, int actedByUserId)
        {
            stagingRow.BeneficiaryId = NormalizeNullable(request.BeneficiaryId);
            stagingRow.CivilRegistryId = NormalizeNullable(request.CivilRegistryId);
            stagingRow.FirstName = NormalizeNullable(request.FirstName);
            stagingRow.MiddleName = NormalizeNullable(request.MiddleName);
            stagingRow.LastName = NormalizeNullable(request.LastName);
            stagingRow.FullName = NormalizeNullable(request.FullName) ?? BuildDisplayName(stagingRow);
            stagingRow.Sex = NormalizeNullable(request.Sex);
            stagingRow.DateOfBirth = NormalizeNullable(request.DateOfBirth);
            stagingRow.Age = NormalizeNullable(request.Age);
            stagingRow.MaritalStatus = NormalizeNullable(request.MaritalStatus);
            stagingRow.Address = NormalizeNullable(request.Address);
            stagingRow.PwdIdNo = NormalizeNullable(request.PwdIdNo);
            stagingRow.SeniorIdNo = NormalizeNullable(request.SeniorIdNo);
            stagingRow.DisabilityType = NormalizeNullable(request.DisabilityType);
            stagingRow.ReviewNotes = NormalizeNullable(request.ReviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
        }

        private static string BuildDisplayName(BeneficiaryStaging row)
        {
            var fullName = NormalizeNullable(row.FullName);
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string BuildApprovalNotes(BeneficiaryStaging row, string? reviewNotes)
        {
            var notes = new List<string>();

            if (!string.IsNullOrWhiteSpace(row.CivilRegistryId))
            {
                notes.Add($"CivilRegistryId: {row.CivilRegistryId}");
            }

            if (!string.IsNullOrWhiteSpace(row.BeneficiaryId))
            {
                notes.Add($"BeneficiaryId: {row.BeneficiaryId}");
            }

            if (row.IsPwd)
            {
                notes.Add("PWD");
            }

            if (row.IsSenior)
            {
                notes.Add("Senior");
            }

            var normalizedReviewNotes = NormalizeNullable(reviewNotes);
            if (!string.IsNullOrWhiteSpace(normalizedReviewNotes))
            {
                notes.Add($"Review: {normalizedReviewNotes}");
            }

            return string.Join(" | ", notes);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

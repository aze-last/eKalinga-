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
        bool IsPwd,
        string? PwdIdNo,
        bool IsSenior,
        string? SeniorIdNo,
        string? DisabilityType,
        string? CauseOfDisability,
        string? ReviewNotes);

    public sealed class BeneficiaryVerificationService
    {
        private readonly LocalDbContext _context;
        private readonly AuditService _auditService;

        public BeneficiaryVerificationService(LocalDbContext context, AuditService? auditService = null)
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

            if (request.Corrections != null)
            {
                ApplyCorrections(stagingRow, request.Corrections, actedByUserId);
            }

            var collision = await CheckForCollisionAsync(
                stagingRow.StagingID,
                stagingRow.BeneficiaryId,
                stagingRow.CivilRegistryId,
                stagingRow.FullName,
                stagingRow.DateOfBirth);

            if (collision != null)
            {
                return new BeneficiaryVerificationOperationResult(false, collision);
            }

            var fullName = BuildDisplayName(stagingRow);
            stagingRow.VerificationStatus = VerificationStatus.Approved;
            stagingRow.LinkedHouseholdId = null;
            stagingRow.LinkedHouseholdMemberId = null;
            stagingRow.ReviewNotes = NormalizeNullable(request.ReviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
            await _context.SaveChangesAsync();

            var digitalIdService = new BeneficiaryDigitalIdService(_context, _auditService);
            await digitalIdService.EnsureIssuedAsync(stagingRow.StagingID, actedByUserId);

            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryApproved",
                "BeneficiaryStaging",
                request.StagingId,
                $"Approved '{fullName}' from the validated beneficiary queue.");

            return new BeneficiaryVerificationOperationResult(
                true,
                $"Approved {fullName}.");
        }

        public async Task<BeneficiaryVerificationOperationResult> SaveCorrectionsAsync(BeneficiaryCorrectionRequest request, int actedByUserId)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == request.StagingId);

            if (stagingRow == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected staging record no longer exists.");
            }

            var wasApproved = stagingRow.VerificationStatus == VerificationStatus.Approved;
            var previousBeneficiaryId = NormalizeNullable(stagingRow.BeneficiaryId);
            var previousCivilRegistryId = NormalizeNullable(stagingRow.CivilRegistryId);

            var collision = await CheckForCollisionAsync(
                request.StagingId,
                request.BeneficiaryId,
                request.CivilRegistryId,
                request.FullName ?? BuildDisplayName(request, stagingRow),
                request.DateOfBirth);

            if (collision != null)
            {
                return new BeneficiaryVerificationOperationResult(false, collision);
            }

            ApplyCorrections(stagingRow, request, actedByUserId);

            if (wasApproved)
            {
                await SyncApprovedBeneficiaryReferencesAsync(
                    stagingRow.StagingID,
                    previousBeneficiaryId,
                    previousCivilRegistryId,
                    stagingRow);
            }

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryCorrected",
                "BeneficiaryStaging",
                request.StagingId,
                $"Saved registry corrections for '{BuildDisplayName(stagingRow)}'.");

            return new BeneficiaryVerificationOperationResult(true, $"Saved corrections for {BuildDisplayName(stagingRow)}.");
        }

        public async Task<BeneficiaryVerificationOperationResult> ReturnToPendingAsync(int stagingId, int actedByUserId, string? reviewNotes)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == stagingId);

            if (stagingRow == null)
            {
                return new BeneficiaryVerificationOperationResult(false, "The selected staging record no longer exists.");
            }

            if (stagingRow.VerificationStatus == VerificationStatus.Pending)
            {
                return new BeneficiaryVerificationOperationResult(false, "This beneficiary is already pending review.");
            }

            var benefitConflict = await EnsureNoActiveBenefitsAsync(stagingRow);
            if (benefitConflict != null)
            {
                return new BeneficiaryVerificationOperationResult(false, benefitConflict);
            }

            stagingRow.VerificationStatus = VerificationStatus.Pending;
            stagingRow.ReviewNotes = NormalizeNullable(reviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
            stagingRow.LinkedHouseholdId = null;
            stagingRow.LinkedHouseholdMemberId = null;

            var activeDigitalId = await _context.BeneficiaryDigitalIds
                .FirstOrDefaultAsync(item => item.BeneficiaryStagingId == stagingId && item.IsActive);

            if (activeDigitalId != null)
            {
                activeDigitalId.IsActive = false;
                activeDigitalId.RevokedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            var fullName = BuildDisplayName(stagingRow);
            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryReturnedToPending",
                "BeneficiaryStaging",
                stagingRow.StagingID,
                $"Returned '{fullName}' to pending review.");

            return new BeneficiaryVerificationOperationResult(true, $"{fullName} is back in pending review.");
        }

        private async Task SyncApprovedBeneficiaryReferencesAsync(
            int stagingId,
            string? previousBeneficiaryId,
            string? previousCivilRegistryId,
            BeneficiaryStaging stagingRow)
        {
            var fullName = BuildDisplayName(stagingRow);
            var beneficiaryId = NormalizeNullable(stagingRow.BeneficiaryId);
            var civilRegistryId = NormalizeNullable(stagingRow.CivilRegistryId);

            var projectBeneficiaries = await _context.AyudaProjectBeneficiaries
                .Where(item => item.BeneficiaryStagingId == stagingId)
                .ToListAsync();

            foreach (var projectBeneficiary in projectBeneficiaries)
            {
                projectBeneficiary.FullName = fullName;
                projectBeneficiary.BeneficiaryId = beneficiaryId;
                projectBeneficiary.CivilRegistryId = civilRegistryId;
            }

            var projectClaims = await _context.AyudaProjectClaims
                .Where(item => item.BeneficiaryStagingId == stagingId)
                .ToListAsync();

            foreach (var projectClaim in projectClaims)
            {
                projectClaim.FullName = fullName;
                projectClaim.BeneficiaryId = beneficiaryId;
                projectClaim.CivilRegistryId = civilRegistryId;
            }

            var assistanceCases = await LoadAssistanceCasesByPreviousIdentityAsync(previousBeneficiaryId, previousCivilRegistryId);
            foreach (var assistanceCase in assistanceCases)
            {
                assistanceCase.ValidatedBeneficiaryName = fullName;
                assistanceCase.ValidatedBeneficiaryId = beneficiaryId;
                assistanceCase.ValidatedCivilRegistryId = civilRegistryId;
                assistanceCase.UpdatedAt = DateTime.Now;
            }

            var ledgerEntries = await LoadLedgerEntriesByPreviousIdentityAsync(previousBeneficiaryId, previousCivilRegistryId);
            foreach (var ledgerEntry in ledgerEntries)
            {
                ledgerEntry.BeneficiaryId = beneficiaryId;
                ledgerEntry.CivilRegistryId = civilRegistryId;
            }
        }

        private async Task<List<AssistanceCase>> LoadAssistanceCasesByPreviousIdentityAsync(string? previousBeneficiaryId, string? previousCivilRegistryId)
        {
            if (string.IsNullOrWhiteSpace(previousBeneficiaryId) && string.IsNullOrWhiteSpace(previousCivilRegistryId))
            {
                return [];
            }

            if (!string.IsNullOrWhiteSpace(previousBeneficiaryId) && !string.IsNullOrWhiteSpace(previousCivilRegistryId))
            {
                return await _context.AssistanceCases
                    .Where(item =>
                        item.ValidatedBeneficiaryId == previousBeneficiaryId
                        || item.ValidatedCivilRegistryId == previousCivilRegistryId)
                    .ToListAsync();
            }

            if (!string.IsNullOrWhiteSpace(previousBeneficiaryId))
            {
                return await _context.AssistanceCases
                    .Where(item => item.ValidatedBeneficiaryId == previousBeneficiaryId)
                    .ToListAsync();
            }

            return await _context.AssistanceCases
                .Where(item => item.ValidatedCivilRegistryId == previousCivilRegistryId)
                .ToListAsync();
        }

        private async Task<List<BeneficiaryAssistanceLedgerEntry>> LoadLedgerEntriesByPreviousIdentityAsync(string? previousBeneficiaryId, string? previousCivilRegistryId)
        {
            if (string.IsNullOrWhiteSpace(previousBeneficiaryId) && string.IsNullOrWhiteSpace(previousCivilRegistryId))
            {
                return [];
            }

            if (!string.IsNullOrWhiteSpace(previousBeneficiaryId) && !string.IsNullOrWhiteSpace(previousCivilRegistryId))
            {
                return await _context.BeneficiaryAssistanceLedgerEntries
                    .Where(item =>
                        item.BeneficiaryId == previousBeneficiaryId
                        || item.CivilRegistryId == previousCivilRegistryId)
                    .ToListAsync();
            }

            if (!string.IsNullOrWhiteSpace(previousBeneficiaryId))
            {
                return await _context.BeneficiaryAssistanceLedgerEntries
                    .Where(item => item.BeneficiaryId == previousBeneficiaryId)
                    .ToListAsync();
            }

            return await _context.BeneficiaryAssistanceLedgerEntries
                .Where(item => item.CivilRegistryId == previousCivilRegistryId)
                .ToListAsync();
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

            var benefitConflict = await EnsureNoActiveBenefitsAsync(stagingRow);
            if (benefitConflict != null)
            {
                return new BeneficiaryVerificationOperationResult(false, benefitConflict);
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

            var benefitConflict = await EnsureNoActiveBenefitsAsync(stagingRow);
            if (benefitConflict != null)
            {
                return new BeneficiaryVerificationOperationResult(false, benefitConflict);
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
            stagingRow.IsPwd = request.IsPwd;
            stagingRow.IsSenior = request.IsSenior;
            stagingRow.PwdIdNo = request.IsPwd ? NormalizeNullable(request.PwdIdNo) : null;
            stagingRow.SeniorIdNo = request.IsSenior ? NormalizeNullable(request.SeniorIdNo) : null;
            stagingRow.DisabilityType = request.IsPwd ? NormalizeNullable(request.DisabilityType) : null;
            stagingRow.CauseOfDisability = request.IsPwd ? NormalizeNullable(request.CauseOfDisability) : null;
            stagingRow.ReviewNotes = NormalizeNullable(request.ReviewNotes);
            stagingRow.ReviewedAt = DateTime.Now;
            stagingRow.ReviewedByUserId = actedByUserId;
        }

        private async Task<string?> EnsureNoActiveBenefitsAsync(BeneficiaryStaging row)
        {
            // Check for Aid Requests (AssistanceCases)
            var hasAidRequests = await _context.AssistanceCases
                .AnyAsync(ac => (ac.ValidatedBeneficiaryId == row.BeneficiaryId || ac.ValidatedCivilRegistryId == row.CivilRegistryId) 
                    && ac.Status != AssistanceCaseStatus.Cancelled 
                    && ac.Status != AssistanceCaseStatus.Rejected);
            
            if (hasAidRequests)
            {
                return "Cannot change this beneficiary status because they have an active or released aid request.";
            }

            // Check for Project Enrollment
            var isInProject = await _context.AyudaProjectBeneficiaries
                .AnyAsync(pb => pb.BeneficiaryStagingId == row.StagingID);

            if (isInProject)
            {
                return "Cannot change this beneficiary status because they are enrolled in an ayuda project.";
            }

            // Check for Project Claims
            var hasProjectClaims = await _context.AyudaProjectClaims
                .AnyAsync(pc => pc.BeneficiaryStagingId == row.StagingID);

            if (hasProjectClaims)
            {
                return "Cannot change this beneficiary status because they have already claimed benefits from an ayuda project.";
            }

            // Check for Cash-for-Work Participation
            var isCfwParticipant = await _context.CashForWorkParticipants
                .AnyAsync(p => p.BeneficiaryStagingId == row.StagingID);

            if (isCfwParticipant)
            {
                return "Cannot change this beneficiary status because they are a participant in a cash-for-work event.";
            }

            return null;
        }

        private async Task<string?> CheckForCollisionAsync(
            int stagingId,
            string? beneficiaryId,
            string? civilRegistryId,
            string? fullName,
            string? dateOfBirth)
        {
            var normalizedCivilRegistryId = NormalizeNullable(civilRegistryId);
            var normalizedBeneficiaryId = NormalizeNullable(beneficiaryId);
            var fingerprint = BeneficiaryImportDeduplication.BuildFingerprint(fullName, dateOfBirth);

            var query = _context.BeneficiaryStaging
                .AsNoTracking()
                .Where(row => row.StagingID != stagingId && row.VerificationStatus == VerificationStatus.Approved);

            // Check IDs
            if (!string.IsNullOrWhiteSpace(normalizedCivilRegistryId))
            {
                if (await query.AnyAsync(row => row.CivilRegistryId == normalizedCivilRegistryId))
                {
                    return "Another approved beneficiary already exists with this Civil Registry ID.";
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedBeneficiaryId))
            {
                if (await query.AnyAsync(row => row.BeneficiaryId == normalizedBeneficiaryId))
                {
                    return "Another approved beneficiary already exists with this Beneficiary ID.";
                }
            }

            // Check Fingerprint (Name + DOB)
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                var candidates = await query
                    .Where(row => row.FullName != null && row.DateOfBirth != null)
                    .Select(row => new { row.FullName, row.DateOfBirth })
                    .ToListAsync();

                if (candidates.Any(c => BeneficiaryImportDeduplication.BuildFingerprint(c.FullName, c.DateOfBirth) == fingerprint))
                {
                    return "Another approved beneficiary already exists with the same name and date of birth.";
                }
            }

            return null;
        }

        private static string BuildDisplayName(BeneficiaryCorrectionRequest request, BeneficiaryStaging row)
        {
            var fullName = NormalizeNullable(request.FullName);
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return string.Join(" ", new[] { request.FirstName ?? row.FirstName, request.MiddleName ?? row.MiddleName, request.LastName ?? row.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
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

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

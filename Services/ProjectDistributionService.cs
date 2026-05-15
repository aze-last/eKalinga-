using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record ProjectDistributionOperationResult(
        bool IsSuccess,
        string Message,
        int? ProjectBeneficiaryId = null,
        int? ProjectClaimId = null);

    public sealed record ProjectDistributionQualificationResult(
        bool IsQualified,
        bool AlreadyClaimed,
        string Message,
        bool IsIncluded = false,
        DistributionBeneficiaryStatus? BeneficiaryStatus = null,
        bool CanRelease = false,
        int? ProjectBeneficiaryId = null,
        int? ProjectClaimId = null);

    public sealed class ProjectDistributionService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly BudgetManagementService _budgetService;
        private readonly IGgmsConsolidatedTransactionService _ggmsConsolidatedTransactionService;

        public ProjectDistributionService(AppDbContext context)
            : this(context, null)
        {
        }

        public ProjectDistributionService(
            AppDbContext context,
            AuditService? auditService = null,
            IGgmsConsolidatedTransactionService? ggmsConsolidatedTransactionService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
            _budgetService = new BudgetManagementService(context, _auditService);
            _ggmsConsolidatedTransactionService = ggmsConsolidatedTransactionService ?? NullGgmsConsolidatedTransactionService.Instance;
        }

        public async Task<ProjectDistributionOperationResult> AddBeneficiaryAsync(int ayudaProgramId, int beneficiaryStagingId, int actedByUserId)
        {
            var program = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == ayudaProgramId && item.IsActive);

            if (program == null)
            {
                return new ProjectDistributionOperationResult(false, "Select an active project/program first.");
            }

            if (program.DistributionStatus == AyudaProgramDistributionStatus.Closed)
            {
                return new ProjectDistributionOperationResult(false, $"Beneficiaries can only be added to projects that are not 'Closed' (Current status: {program.DistributionStatus}).");
            }

            var beneficiary = await _context.BeneficiaryStaging
                .AsNoTracking()
                .Where(item => item.StagingID == beneficiaryStagingId)
                .Select(item => new
                {
                    item.StagingID,
                    item.LinkedHouseholdId,
                    item.LinkedHouseholdMemberId,
                    item.BeneficiaryId,
                    item.CivilRegistryId,
                    item.FirstName,
                    item.MiddleName,
                    item.LastName,
                    item.FullName,
                    item.VerificationStatus
                })
                .FirstOrDefaultAsync();

            if (beneficiary == null)
            {
                return new ProjectDistributionOperationResult(false, "The selected approved beneficiary could not be found.");
            }

            if (beneficiary.VerificationStatus != VerificationStatus.Approved)
            {
                return new ProjectDistributionOperationResult(false, "Only approved beneficiaries can be added to a project.");
            }

            var existingMembership = await _context.AyudaProjectBeneficiaries
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (existingMembership != null)
            {
                return new ProjectDistributionOperationResult(false, "This beneficiary is already included in the selected project.", existingMembership.Id);
            }

            var fullName = string.IsNullOrWhiteSpace(beneficiary.FullName)
                ? BuildDisplayName(beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName)
                : beneficiary.FullName;

            var membership = new AyudaProjectBeneficiary
            {
                AyudaProgramId = ayudaProgramId,
                BeneficiaryStagingId = beneficiaryStagingId,
                HouseholdId = beneficiary.LinkedHouseholdId,
                HouseholdMemberId = beneficiary.LinkedHouseholdMemberId,
                BeneficiaryId = NormalizeNullable(beneficiary.BeneficiaryId),
                CivilRegistryId = NormalizeNullable(beneficiary.CivilRegistryId),
                FullName = fullName,
                Status = DistributionBeneficiaryStatus.Pending,
                StatusUpdatedByUserId = actedByUserId,
                StatusUpdatedAt = DateTime.Now,
                AddedByUserId = actedByUserId,
                AddedAt = DateTime.Now
            };

            _context.AyudaProjectBeneficiaries.Add(membership);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "ProjectBeneficiaryAdded",
                nameof(AyudaProjectBeneficiary),
                membership.Id,
                $"Added beneficiary '{membership.FullName}' to project/program #{ayudaProgramId}.");

            return new ProjectDistributionOperationResult(true, "Beneficiary added to project.", membership.Id);
        }

        public async Task<ProjectDistributionOperationResult> BulkAddBeneficiariesAsync(int ayudaProgramId, IEnumerable<int> beneficiaryStagingIds, int actedByUserId)
        {
            var program = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == ayudaProgramId && item.IsActive);

            if (program == null)
            {
                return new ProjectDistributionOperationResult(false, "Select an active project/program first.");
            }

            if (program.DistributionStatus == AyudaProgramDistributionStatus.Closed)
            {
                return new ProjectDistributionOperationResult(false, $"Beneficiaries can only be added to projects that are not 'Closed' (Current status: {program.DistributionStatus}).");
            }

            var stagingIds = beneficiaryStagingIds.Distinct().ToList();
            if (stagingIds.Count == 0)
            {
                return new ProjectDistributionOperationResult(false, "No beneficiaries selected for bulk add.");
            }

            var existingMemberships = await _context.AyudaProjectBeneficiaries
                .Where(item => item.AyudaProgramId == ayudaProgramId && stagingIds.Contains(item.BeneficiaryStagingId))
                .Select(item => item.BeneficiaryStagingId)
                .ToListAsync();

            var newStagingIds = stagingIds.Except(existingMemberships).ToList();
            if (newStagingIds.Count == 0)
            {
                return new ProjectDistributionOperationResult(false, "All selected beneficiaries are already included in this project.");
            }

            var beneficiaries = await _context.BeneficiaryStaging
                .AsNoTracking()
                .Where(item => newStagingIds.Contains(item.StagingID) && item.VerificationStatus == VerificationStatus.Approved)
                .Select(item => new
                {
                    item.StagingID,
                    item.LinkedHouseholdId,
                    item.LinkedHouseholdMemberId,
                    item.BeneficiaryId,
                    item.CivilRegistryId,
                    item.FirstName,
                    item.MiddleName,
                    item.LastName,
                    item.FullName
                })
                .ToListAsync();

            if (beneficiaries.Count == 0)
            {
                return new ProjectDistributionOperationResult(false, "None of the selected beneficiaries could be found or are approved.");
            }

            var newMemberships = new List<AyudaProjectBeneficiary>();
            var now = DateTime.Now;

            foreach (var b in beneficiaries)
            {
                newMemberships.Add(new AyudaProjectBeneficiary
                {
                    AyudaProgramId = ayudaProgramId,
                    BeneficiaryStagingId = b.StagingID,
                    HouseholdId = b.LinkedHouseholdId,
                    HouseholdMemberId = b.LinkedHouseholdMemberId,
                    BeneficiaryId = NormalizeNullable(b.BeneficiaryId),
                    CivilRegistryId = NormalizeNullable(b.CivilRegistryId),
                    FullName = string.IsNullOrWhiteSpace(b.FullName) 
                        ? BuildDisplayName(b.FirstName, b.MiddleName, b.LastName) 
                        : b.FullName,
                    Status = DistributionBeneficiaryStatus.Pending,
                    StatusUpdatedByUserId = actedByUserId,
                    StatusUpdatedAt = now,
                    AddedByUserId = actedByUserId,
                    AddedAt = now
                });
            }

            _context.AyudaProjectBeneficiaries.AddRange(newMemberships);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "ProjectBeneficiariesBulkAdded",
                nameof(AyudaProjectBeneficiary),
                ayudaProgramId,
                $"Bulk added {newMemberships.Count} beneficiaries to project/program #{ayudaProgramId}.");

            return new ProjectDistributionOperationResult(true, $"Successfully enrolled {newMemberships.Count} beneficiaries to the project.");
        }

        public async Task<ProjectDistributionOperationResult> BulkRecordClaimsAsync(
            int ayudaProgramId,
            IEnumerable<int> beneficiaryStagingIds,
            int actedByUserId,
            string? remarks)
        {
            var program = await _context.AyudaPrograms.FirstOrDefaultAsync(p => p.Id == ayudaProgramId);
            if (program == null)
            {
                return new ProjectDistributionOperationResult(false, "Select an active project/program first.");
            }

            var lifecycleValidation = ValidateProjectLifecycle(program);
            if (lifecycleValidation != null)
            {
                return lifecycleValidation;
            }

            var stagingIds = beneficiaryStagingIds.Distinct().ToList();
            if (stagingIds.Count == 0)
            {
                return new ProjectDistributionOperationResult(false, "No beneficiaries selected for bulk claim.");
            }

            var memberships = await _context.AyudaProjectBeneficiaries
                .Where(item => item.AyudaProgramId == ayudaProgramId && stagingIds.Contains(item.BeneficiaryStagingId))
                .ToListAsync();

            if (memberships.Count == 0)
            {
                return new ProjectDistributionOperationResult(false, "None of the selected beneficiaries are enrolled in this project.");
            }

            var successfulClaims = 0;
            var now = DateTime.Now;
            var historyService = new BeneficiaryAssistanceLedgerService(_context, _auditService);

            foreach (var membership in memberships)
            {
                if (membership.Status == DistributionBeneficiaryStatus.Released) continue;
                if (membership.Status == DistributionBeneficiaryStatus.Rejected) continue;

                var existingClaim = await _context.AyudaProjectClaims
                    .AnyAsync(c => c.AyudaProgramId == ayudaProgramId && c.BeneficiaryStagingId == membership.BeneficiaryStagingId);
                
                if (existingClaim)
                {
                    membership.Status = DistributionBeneficiaryStatus.Released;
                    continue;
                }

                // Check budget cap for this individual claim if applicable
                if (program.BudgetCap is > 0 && program.UnitAmount is > 0)
                {
                    var currentProjectSpend = await _context.AyudaProjectClaims
                        .AsNoTracking()
                        .Where(item => item.AyudaProgramId == ayudaProgramId)
                        .SumAsync(item => (decimal?)item.UnitAmountSnapshot) ?? 0m;

                    if (currentProjectSpend + program.UnitAmount.Value > program.BudgetCap.Value)
                    {
                        break; // Stop if budget cap reached
                    }
                }

                var claim = new AyudaProjectClaim
                {
                    AyudaProgramId = ayudaProgramId,
                    BeneficiaryStagingId = membership.BeneficiaryStagingId,
                    ProjectBeneficiaryId = membership.Id,
                    HouseholdId = membership.HouseholdId,
                    HouseholdMemberId = membership.HouseholdMemberId,
                    BeneficiaryId = membership.BeneficiaryId,
                    CivilRegistryId = membership.CivilRegistryId,
                    FullName = membership.FullName,
                    AssistanceTypeSnapshot = NormalizeNullable(program.AssistanceType),
                    ItemDescriptionSnapshot = NormalizeNullable(program.ItemDescription),
                    UnitAmountSnapshot = program.UnitAmount,
                    QrPayload = "BULK-RELEASE-INIT",
                    Remarks = remarks ?? $"Bulk release for {membership.FullName}.",
                    ClaimedByUserId = actedByUserId,
                    ClaimedAt = now
                };

                _context.AyudaProjectClaims.Add(claim);
                
                membership.Status = DistributionBeneficiaryStatus.Released;
                membership.StatusUpdatedByUserId = actedByUserId;
                membership.StatusUpdatedAt = now;

                if (program.UnitAmount is > 0)
                {
                    var releaseResult = await _budgetService.RecordReleaseAsync(
                        new BudgetReleaseRequest(
                            ayudaProgramId,
                            BudgetLedgerFeatureSource.ProjectDistribution,
                            BuildClaimSourceRecordId(ayudaProgramId, membership.BeneficiaryStagingId),
                            1,
                            program.ReleaseKind,
                            program.UnitAmount.Value,
                            now,
                            remarks ?? $"Bulk release for {membership.FullName}."),
                        actedByUserId);

                    if (!releaseResult.IsSuccess)
                    {
                        _context.AyudaProjectClaims.Remove(claim);
                        membership.Status = DistributionBeneficiaryStatus.Pending;
                        continue;
                    }
                }

                await historyService.RecordEntryAsync(
                    membership.CivilRegistryId,
                    membership.BeneficiaryId,
                    BeneficiaryAssistanceSourceModule.ProjectDistribution,
                    BuildClaimSourceRecordId(ayudaProgramId, membership.BeneficiaryStagingId),
                    now,
                    program.UnitAmount ?? 0m,
                    remarks ?? $"Bulk release for {membership.FullName}.",
                    actedByUserId);

                successfulClaims++;
            }

            await _context.SaveChangesAsync();

            if (successfulClaims > 0)
            {
                var claimsToSync = await _context.AyudaProjectClaims
                    .Where(c => c.AyudaProgramId == ayudaProgramId && c.ClaimedAt == now)
                    .ToListAsync();

                var ggmsWarningMessage = await _ggmsConsolidatedTransactionService.TryWriteBulkProjectDistributionClaimsAsync(_context, program, claimsToSync);

                await _auditService.LogActivityAsync(
                    actedByUserId,
                    "ProjectBeneficiariesBulkClaimed",
                    nameof(AyudaProjectClaim),
                    ayudaProgramId,
                    $"Bulk recorded {successfulClaims} claims for project/program #{ayudaProgramId}.{(string.IsNullOrWhiteSpace(ggmsWarningMessage) ? "" : $" GGMS warning: {ggmsWarningMessage}")}");
            }

            return successfulClaims > 0 
                ? new ProjectDistributionOperationResult(true, $"Successfully recorded {successfulClaims} project claims.")
                : new ProjectDistributionOperationResult(false, "No claims were recorded (check budget caps or enrollment status).");
        }

        public async Task<ProjectDistributionQualificationResult> EvaluateQualificationAsync(int ayudaProgramId, int beneficiaryStagingId)
        {
            var membership = await ResolveMembershipAsync(ayudaProgramId, beneficiaryStagingId);

            if (membership == null)
            {
                return new ProjectDistributionQualificationResult(false, false, "Beneficiary is not included in the selected project.");
            }

            if (membership.Status == DistributionBeneficiaryStatus.Rejected)
            {
                return new ProjectDistributionQualificationResult(
                    true,
                    false,
                    "Beneficiary is rejected for this project.",
                    true,
                    membership.Status,
                    false,
                    membership.Id);
            }

            var claim = await FindExistingClaimAsync(ayudaProgramId, membership);

            if (claim != null)
            {
                return new ProjectDistributionQualificationResult(
                    true,
                    true,
                    "Beneficiary already claimed for this project.",
                    true,
                    DistributionBeneficiaryStatus.Released,
                    false,
                    membership.Id,
                    claim.Id);
            }

            return new ProjectDistributionQualificationResult(
                true,
                false,
                membership.Status == DistributionBeneficiaryStatus.Pending
                    ? "Beneficiary is pending release for this project."
                    : "Beneficiary is qualified for this project.",
                true,
                membership.Status,
                membership.Status == DistributionBeneficiaryStatus.Pending,
                membership.Id);
        }

        public async Task<ProjectDistributionOperationResult> UpdateBeneficiaryStatusAsync(
            int ayudaProgramId,
            int beneficiaryStagingId,
            DistributionBeneficiaryStatus targetStatus,
            int actedByUserId,
            string? reason)
        {
            var membership = await _context.AyudaProjectBeneficiaries
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (membership == null)
            {
                return new ProjectDistributionOperationResult(false, "Beneficiary is not included in the selected project.");
            }

            if (membership.Status == DistributionBeneficiaryStatus.Released)
            {
                return new ProjectDistributionOperationResult(false, "Released beneficiaries can no longer be changed from the distribution list.", membership.Id);
            }

            if (targetStatus == DistributionBeneficiaryStatus.Released)
            {
                return new ProjectDistributionOperationResult(false, "Use the scanner release flow to mark beneficiaries as released.", membership.Id);
            }

            membership.Status = targetStatus;
            membership.StatusReason = NormalizeNullable(reason);
            membership.StatusUpdatedByUserId = actedByUserId;
            membership.StatusUpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "ProjectBeneficiaryStatusChanged",
                nameof(AyudaProjectBeneficiary),
                membership.Id,
                $"Updated distribution beneficiary '{membership.FullName}' to {targetStatus} for project/program #{ayudaProgramId}.");

            return new ProjectDistributionOperationResult(true, $"Beneficiary marked as {targetStatus}.", membership.Id);
        }

        public async Task<ProjectDistributionOperationResult> RecordClaimAsync(
            int ayudaProgramId,
            int beneficiaryStagingId,
            int actedByUserId,
            string? qrPayload,
            string? remarks)
        {
            if (string.IsNullOrWhiteSpace(qrPayload))
            {
                return new ProjectDistributionOperationResult(false, "Identity QR payload is required for project claims.");
            }

            // Verify identity via QR payload and resolve correct membership
            var digitalIdService = new BeneficiaryDigitalIdService(_context);
            var lookupResult = await digitalIdService.LookupByQrPayloadAsync(qrPayload);

            if (lookupResult == null)
            {
                var existingDigitalId = await digitalIdService.GetByStagingIdAsync(beneficiaryStagingId);
                var looksLikeLocalPayload = qrPayload.StartsWith("ASM-BID|", StringComparison.OrdinalIgnoreCase);
                if (existingDigitalId != null || !looksLikeLocalPayload)
                {
                    return new ProjectDistributionOperationResult(false, "Beneficiary identity mismatch. The scanned ID does not match the selected record.");
                }
            }
            else
            {
                // Use the QR lookup result to find the correct membership
                beneficiaryStagingId = lookupResult.BeneficiaryStagingId;
            }

            var qualification = await EvaluateQualificationAsync(ayudaProgramId, beneficiaryStagingId);
            if (!qualification.IsQualified)
            {
                return new ProjectDistributionOperationResult(false, qualification.Message);
            }

            if (qualification.AlreadyClaimed)
            {
                return new ProjectDistributionOperationResult(false, qualification.Message, qualification.ProjectBeneficiaryId, qualification.ProjectClaimId);
            }

            if (!qualification.CanRelease)
            {
                return new ProjectDistributionOperationResult(false, qualification.Message, qualification.ProjectBeneficiaryId, qualification.ProjectClaimId);
            }

            var program = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == ayudaProgramId);

            var lifecycleValidation = ValidateProjectLifecycle(program);
            if (lifecycleValidation != null)
            {
                return lifecycleValidation;
            }

            if (program != null &&
                program.ReleaseKind == AssistanceReleaseKind.Cash &&
                program.UnitAmount is not > 0)
            {
                return new ProjectDistributionOperationResult(false, "This cash distribution project has no unit amount configured. Update the program before releasing.");
            }

            var membershipSnapshot = await _context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .FirstAsync(item => item.Id == qualification.ProjectBeneficiaryId);

            if (program?.BudgetCap is > 0)
            {
                var currentProjectSpend = await _context.AyudaProjectClaims
                    .AsNoTracking()
                    .Where(item => item.AyudaProgramId == ayudaProgramId)
                    .SumAsync(item => (decimal?)item.UnitAmountSnapshot) ?? 0m;

                var nextClaimAmount = program.UnitAmount ?? 0m;
                if (currentProjectSpend + nextClaimAmount > program.BudgetCap.Value)
                {
                    return new ProjectDistributionOperationResult(false, "Project budget cap would be exceeded by this claim.");
                }
            }

            var claim = new AyudaProjectClaim
            {
                AyudaProgramId = ayudaProgramId,
                BeneficiaryStagingId = membershipSnapshot.BeneficiaryStagingId,
                ProjectBeneficiaryId = membershipSnapshot.Id,
                HouseholdId = membershipSnapshot.HouseholdId,
                HouseholdMemberId = membershipSnapshot.HouseholdMemberId,
                BeneficiaryId = membershipSnapshot.BeneficiaryId,
                CivilRegistryId = membershipSnapshot.CivilRegistryId,
                FullName = membershipSnapshot.FullName,
                AssistanceTypeSnapshot = NormalizeNullable(program?.AssistanceType),
                ItemDescriptionSnapshot = NormalizeNullable(program?.ItemDescription),
                UnitAmountSnapshot = program?.UnitAmount,
                QrPayload = NormalizeNullable(qrPayload),
                Remarks = NormalizeNullable(remarks),
                ClaimedByUserId = actedByUserId,
                ClaimedAt = DateTime.Now
            };

            _context.AyudaProjectClaims.Add(claim);
            await _context.SaveChangesAsync();

            var membership = await _context.AyudaProjectBeneficiaries
                .FirstAsync(item => item.Id == qualification.ProjectBeneficiaryId);
            membership.Status = DistributionBeneficiaryStatus.Released;
            membership.StatusReason = null;
            membership.StatusUpdatedByUserId = actedByUserId;
            membership.StatusUpdatedAt = claim.ClaimedAt;

            if (program?.UnitAmount is > 0)
            {
                var releaseResult = await _budgetService.RecordReleaseAsync(
                    new BudgetReleaseRequest(
                        ayudaProgramId,
                        BudgetLedgerFeatureSource.ProjectDistribution,
                        BuildClaimSourceRecordId(ayudaProgramId, membershipSnapshot.BeneficiaryStagingId),
                        1,
                        ResolveReleaseKind(program),
                        program.UnitAmount.Value,
                        claim.ClaimedAt,
                        NormalizeNullable(remarks) ?? $"Project claim for {membership.FullName}."),
                    actedByUserId);

                if (!releaseResult.IsSuccess)
                {
                    _context.AyudaProjectClaims.Remove(claim);
                    await _context.SaveChangesAsync();
                    return new ProjectDistributionOperationResult(false, releaseResult.Message, membership.Id);
                }

            }

            // Keep the beneficiary digital ID lookup history in sync with project releases,
            // even when the distribution does not carry a cash amount.
            var historyService = new BeneficiaryAssistanceLedgerService(_context, _auditService);
            await historyService.RecordEntryAsync(
                membership.CivilRegistryId,
                membership.BeneficiaryId,
                BeneficiaryAssistanceSourceModule.ProjectDistribution,
                $"project-claim:{ayudaProgramId}:{membershipSnapshot.BeneficiaryStagingId}",
                claim.ClaimedAt,
                program?.UnitAmount ?? 0m,
                NormalizeNullable(remarks) ?? $"Project claim for {membership.FullName}.",
                actedByUserId);

            await _context.SaveChangesAsync();

            var ggmsWarningMessage = await _ggmsConsolidatedTransactionService.TryWriteProjectDistributionClaimAsync(_context, program, claim);

            await _auditService.LogActivityAsync(
                actedByUserId,
                "ProjectBeneficiaryClaimed",
                nameof(AyudaProjectClaim),
                claim.Id,
                $"Recorded project claim for '{claim.FullName}' under project/program #{ayudaProgramId}.{(string.IsNullOrWhiteSpace(ggmsWarningMessage) ? "" : $" GGMS warning: {ggmsWarningMessage}")}");

            var successMessage = "Project claim recorded.";
            if (!string.IsNullOrWhiteSpace(ggmsWarningMessage))
            {
                successMessage = $"{successMessage} GGMS sync warning: {ggmsWarningMessage}";
            }

            return new ProjectDistributionOperationResult(true, successMessage, membership.Id, claim.Id);
        }

        public async Task<IReadOnlyList<AyudaProjectBeneficiary>> GetBeneficiariesAsync(int ayudaProgramId)
        {
            return await _context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .Where(item => item.AyudaProgramId == ayudaProgramId)
                .OrderBy(item => item.Status)
                .ThenBy(item => item.FullName)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AyudaProjectClaim>> GetClaimsAsync(int ayudaProgramId)
        {
            return await _context.AyudaProjectClaims
                .AsNoTracking()
                .Where(item => item.AyudaProgramId == ayudaProgramId)
                .OrderByDescending(item => item.ClaimedAt)
                .ToListAsync();
        }

        private static string BuildDisplayName(string? first, string? middle, string? last)
        {
            return string.Join(" ", new[] { first, middle, last }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string BuildDisplayName(BeneficiaryStaging beneficiary)
        {
            if (!string.IsNullOrWhiteSpace(beneficiary.FullName))
            {
                return beneficiary.FullName.Trim();
            }

            return BuildDisplayName(beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string BuildClaimSourceRecordId(int ayudaProgramId, int beneficiaryStagingId)
        {
            return $"project-claim:{ayudaProgramId}:{beneficiaryStagingId}";
        }

        private async Task<AyudaProjectBeneficiary?> ResolveMembershipAsync(int ayudaProgramId, int beneficiaryStagingId)
        {
            var exactMembership = await _context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (exactMembership != null)
            {
                return exactMembership;
            }

            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == beneficiaryStagingId);

            if (staging == null)
            {
                return null;
            }

            var beneficiaryId = NormalizeNullable(staging.BeneficiaryId);
            if (beneficiaryId != null)
            {
                var beneficiaryMatch = await _context.AyudaProjectBeneficiaries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.AyudaProgramId == ayudaProgramId &&
                        item.BeneficiaryId == beneficiaryId);

                if (beneficiaryMatch != null)
                {
                    return beneficiaryMatch;
                }
            }

            var civilRegistryId = NormalizeNullable(staging.CivilRegistryId);
            if (civilRegistryId != null)
            {
                return await _context.AyudaProjectBeneficiaries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.AyudaProgramId == ayudaProgramId &&
                        item.CivilRegistryId == civilRegistryId);
            }

            return null;
        }

        private async Task<AyudaProjectClaim?> FindExistingClaimAsync(int ayudaProgramId, AyudaProjectBeneficiary membership)
        {
            var exactClaim = await _context.AyudaProjectClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.ProjectBeneficiaryId == membership.Id);

            if (exactClaim != null)
            {
                return exactClaim;
            }

            if (!string.IsNullOrWhiteSpace(membership.BeneficiaryId))
            {
                var beneficiaryClaim = await _context.AyudaProjectClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.AyudaProgramId == ayudaProgramId &&
                        item.BeneficiaryId == membership.BeneficiaryId);

                if (beneficiaryClaim != null)
                {
                    return beneficiaryClaim;
                }
            }

            if (!string.IsNullOrWhiteSpace(membership.CivilRegistryId))
            {
                return await _context.AyudaProjectClaims
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.AyudaProgramId == ayudaProgramId &&
                        item.CivilRegistryId == membership.CivilRegistryId);
            }

            return await _context.AyudaProjectClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == membership.BeneficiaryStagingId);
        }

        private static ProjectDistributionOperationResult? ValidateProjectLifecycle(AyudaProgram? program)
        {
            if (program == null)
            {
                return new ProjectDistributionOperationResult(false, "The project/program was not found.");
            }

            if (program.DistributionStatus == AyudaProgramDistributionStatus.Closed)
            {
                return new ProjectDistributionOperationResult(false, "This project distribution is already 'Closed'.");
            }

            var today = DateTime.Today;
            if (program.StartDate.HasValue && today < program.StartDate.Value.Date)
            {
                return new ProjectDistributionOperationResult(false, $"This distribution has not started yet (Scheduled: {program.StartDate:MMM dd, yyyy}).");
            }

            if (program.EndDate.HasValue && today > program.EndDate.Value.Date)
            {
                return new ProjectDistributionOperationResult(false, $"This distribution has already ended (Scheduled: {program.EndDate:MMM dd, yyyy}).");
            }

            return null;
        }

        private static AssistanceReleaseKind ResolveReleaseKind(AyudaProgram program)
        {
            return program.ReleaseKind;
        }
    }
}

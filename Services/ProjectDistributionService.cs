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
        int? ProjectBeneficiaryId = null,
        int? ProjectClaimId = null);

    public sealed class ProjectDistributionService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly BudgetManagementService _budgetService;

        public ProjectDistributionService(AppDbContext context)
            : this(context, null)
        {
        }

        public ProjectDistributionService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
            _budgetService = new BudgetManagementService(context, _auditService);
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

            var beneficiary = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == beneficiaryStagingId);

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

            var membership = new AyudaProjectBeneficiary
            {
                AyudaProgramId = ayudaProgramId,
                BeneficiaryStagingId = beneficiaryStagingId,
                HouseholdId = beneficiary.LinkedHouseholdId,
                HouseholdMemberId = beneficiary.LinkedHouseholdMemberId,
                BeneficiaryId = NormalizeNullable(beneficiary.BeneficiaryId),
                CivilRegistryId = NormalizeNullable(beneficiary.CivilRegistryId),
                FullName = BuildDisplayName(beneficiary),
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

        public async Task<ProjectDistributionQualificationResult> EvaluateQualificationAsync(int ayudaProgramId, int beneficiaryStagingId)
        {
            var membership = await _context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (membership == null)
            {
                return new ProjectDistributionQualificationResult(false, false, "Beneficiary is not included in the selected project.");
            }

            var claim = await _context.AyudaProjectClaims
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (claim != null)
            {
                return new ProjectDistributionQualificationResult(
                    true,
                    true,
                    "Beneficiary already claimed for this project.",
                    membership.Id,
                    claim.Id);
            }

            return new ProjectDistributionQualificationResult(
                true,
                false,
                "Beneficiary is qualified for this project.",
                membership.Id);
        }

        public async Task<ProjectDistributionOperationResult> RecordClaimAsync(
            int ayudaProgramId,
            int beneficiaryStagingId,
            int actedByUserId,
            string? qrPayload,
            string? remarks)
        {
            var qualification = await EvaluateQualificationAsync(ayudaProgramId, beneficiaryStagingId);
            if (!qualification.IsQualified)
            {
                return new ProjectDistributionOperationResult(false, qualification.Message);
            }

            if (qualification.AlreadyClaimed)
            {
                return new ProjectDistributionOperationResult(false, qualification.Message, qualification.ProjectBeneficiaryId, qualification.ProjectClaimId);
            }

            var program = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == ayudaProgramId);

            var membership = await _context.AyudaProjectBeneficiaries
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
                BeneficiaryStagingId = beneficiaryStagingId,
                ProjectBeneficiaryId = membership.Id,
                HouseholdId = membership.HouseholdId,
                HouseholdMemberId = membership.HouseholdMemberId,
                BeneficiaryId = membership.BeneficiaryId,
                CivilRegistryId = membership.CivilRegistryId,
                FullName = membership.FullName,
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

            if (program?.UnitAmount is > 0)
            {
                var releaseResult = await _budgetService.RecordReleaseAsync(
                    new BudgetReleaseRequest(
                        ayudaProgramId,
                        BudgetLedgerFeatureSource.ProjectDistribution,
                        BuildClaimSourceRecordId(ayudaProgramId, beneficiaryStagingId),
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

            await _auditService.LogActivityAsync(
                actedByUserId,
                "ProjectBeneficiaryClaimed",
                nameof(AyudaProjectClaim),
                claim.Id,
                $"Recorded project claim for '{claim.FullName}' under project/program #{ayudaProgramId}.");

            return new ProjectDistributionOperationResult(true, "Project claim recorded.", membership.Id, claim.Id);
        }

        public async Task<IReadOnlyList<AyudaProjectBeneficiary>> GetBeneficiariesAsync(int ayudaProgramId)
        {
            return await _context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .Where(item => item.AyudaProgramId == ayudaProgramId)
                .OrderBy(item => item.FullName)
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

        private static string BuildDisplayName(BeneficiaryStaging beneficiary)
        {
            if (!string.IsNullOrWhiteSpace(beneficiary.FullName))
            {
                return beneficiary.FullName.Trim();
            }

            return string.Join(" ", new[] { beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string BuildClaimSourceRecordId(int ayudaProgramId, int beneficiaryStagingId)
        {
            return $"project-claim:{ayudaProgramId}:{beneficiaryStagingId}";
        }

        private static AssistanceReleaseKind ResolveReleaseKind(AyudaProgram program)
        {
            return string.IsNullOrWhiteSpace(program.ItemDescription)
                ? AssistanceReleaseKind.Cash
                : AssistanceReleaseKind.Goods;
        }
    }
}

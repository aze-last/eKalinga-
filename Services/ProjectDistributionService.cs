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

    /// <summary>UI-only household verification snapshot (no EF entities cross to the ViewModel).</summary>
    public sealed record HouseholdVerificationContext(
        bool HasHousehold,
        string HouseholdCode,
        string HeadName,
        IReadOnlyList<HouseholdMemberVerificationItem> Members,
        bool AnyMemberAlreadyReceived,
        string? WarningMessage);

    public sealed record HouseholdMemberVerificationItem(
        string FullName,
        string RelationshipToHead,
        bool IsScannedBeneficiary,
        bool AlreadyReceivedSameAssistanceType);

    public sealed class ProjectDistributionService
    {
        private readonly LocalDbContext _context;
        private readonly AuditService _auditService;
        private readonly BudgetManagementService _budgetService;
        private readonly IGgmsConsolidatedTransactionService _ggmsConsolidatedTransactionService;

        public ProjectDistributionService(LocalDbContext context)
            : this(context, null)
        {
        }

        public ProjectDistributionService(
            LocalDbContext context,
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
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            var remoteService = new ProjectDistributionService(remoteContext, null, _ggmsConsolidatedTransactionService);
                            return await remoteService.AddBeneficiaryAsync(ayudaProgramId, beneficiaryStagingId, actedByUserId);
                        });

                    if (!remoteResult.IsSuccess)
                    {
                        ScanDiagnosticLogger.Log("AddBeneficiaryAsync", _context, $"Remote enrollment failed: {remoteResult.Message}. Proceeding with local execution.");
                    }
                }
                catch (Exception ex)
                {
                    ScanDiagnosticLogger.Log("AddBeneficiaryAsync", _context, $"Remote enrollment error: {ex.Message}. Proceeding with local execution.");
                }
            }

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
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            var remoteService = new ProjectDistributionService(remoteContext, null, _ggmsConsolidatedTransactionService);
                            return await remoteService.BulkAddBeneficiariesAsync(ayudaProgramId, beneficiaryStagingIds, actedByUserId);
                        });

                    if (!remoteResult.IsSuccess)
                    {
                        ScanDiagnosticLogger.Log("BulkAddBeneficiariesAsync", _context, $"Remote enrollment failed: {remoteResult.Message}. Proceeding with local execution.");
                    }
                }
                catch (Exception ex)
                {
                    ScanDiagnosticLogger.Log("BulkAddBeneficiariesAsync", _context, $"Remote enrollment error: {ex.Message}. Proceeding with local execution.");
                }
            }

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
                .Where(item => newStagingIds.Contains(item.StagingID))
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
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            var remoteService = new ProjectDistributionService(remoteContext, null, _ggmsConsolidatedTransactionService);
                            return await remoteService.BulkRecordClaimsAsync(ayudaProgramId, beneficiaryStagingIds, actedByUserId, remarks);
                        });

                    if (!remoteResult.IsSuccess)
                    {
                        ScanDiagnosticLogger.Log("BulkRecordClaimsAsync", _context, $"Remote claim recording failed: {remoteResult.Message}. Proceeding with local execution.");
                    }
                }
                catch (Exception ex)
                {
                    ScanDiagnosticLogger.Log("BulkRecordClaimsAsync", _context, $"Remote claim recording error: {ex.Message}. Proceeding with local execution.");
                }
            }

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
                    var releaseResult = await _budgetService.RecordWaterfallReleaseAsync(
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
            ScanDiagnosticLogger.Log("EvaluateQualificationAsync", _context, $"START | ProgramId={ayudaProgramId} | StagingId={beneficiaryStagingId}");

            var membership = await ResolveMembershipAsync(ayudaProgramId, beneficiaryStagingId);

            if (membership == null)
            {
                ScanDiagnosticLogger.Log("EvaluateQualificationAsync", _context, $"RESULT=NOT_INCLUDED | ProgramId={ayudaProgramId} | StagingId={beneficiaryStagingId}");
                return new ProjectDistributionQualificationResult(false, false, "Beneficiary is not included in the selected project.");
            }

            ScanDiagnosticLogger.Log("EvaluateQualificationAsync", _context, $"MEMBERSHIP_FOUND | MembershipId={membership.Id} | MembershipStagingId={membership.BeneficiaryStagingId} | MembershipBeneficiaryId={membership.BeneficiaryId} | Status={membership.Status}");

            if (membership.Status == DistributionBeneficiaryStatus.Rejected)
            {
                ScanDiagnosticLogger.Log("EvaluateQualificationAsync", _context, "RESULT=REJECTED");
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
                ScanDiagnosticLogger.Log("EvaluateQualificationAsync", _context, $"RESULT=ALREADY_CLAIMED | ClaimId={claim.Id} | ClaimedAt={claim.ClaimedAt}");
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

            ScanDiagnosticLogger.Log("EvaluateQualificationAsync", _context, $"RESULT=QUALIFIED | CanRelease={membership.Status == DistributionBeneficiaryStatus.Pending} | Status={membership.Status}");
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
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            var remoteService = new ProjectDistributionService(remoteContext, null, _ggmsConsolidatedTransactionService);
                            return await remoteService.UpdateBeneficiaryStatusAsync(ayudaProgramId, beneficiaryStagingId, targetStatus, actedByUserId, reason);
                        });

                    if (!remoteResult.IsSuccess)
                    {
                        ScanDiagnosticLogger.Log("UpdateBeneficiaryStatusAsync", _context, $"Remote status update failed: {remoteResult.Message}. Proceeding with local execution.");
                    }
                }
                catch (Exception ex)
                {
                    ScanDiagnosticLogger.Log("UpdateBeneficiaryStatusAsync", _context, $"Remote status update error: {ex.Message}. Proceeding with local execution.");
                }
            }

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
            ScanDiagnosticLogger.Log("RecordClaimAsync", _context, $"START | ProgramId={ayudaProgramId} | StagingId={beneficiaryStagingId} | QrPayload={qrPayload}");

            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                ScanDiagnosticLogger.Log("RecordClaimAsync", _context, "ROUTING=REMOTE (ShouldRouteToRemote=true)");
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            ScanDiagnosticLogger.Log("RecordClaimAsync", remoteContext, $"REMOTE_CONTEXT_ENTERED | ProgramId={ayudaProgramId} | StagingId={beneficiaryStagingId}");
                            var remoteService = new ProjectDistributionService(remoteContext, null, _ggmsConsolidatedTransactionService);
                            return await remoteService.RecordClaimAsync(ayudaProgramId, beneficiaryStagingId, actedByUserId, qrPayload, remarks);
                        });

                    ScanDiagnosticLogger.Log("RecordClaimAsync", _context, $"REMOTE_RESULT | IsSuccess={remoteResult.IsSuccess} | Msg={remoteResult.Message}");
                    if (!remoteResult.IsSuccess)
                    {
                        ScanDiagnosticLogger.Log("RecordClaimAsync", _context, $"Remote recording failed: {remoteResult.Message}. Proceeding with local execution.");
                    }
                }
                catch (Exception ex)
                {
                    ScanDiagnosticLogger.Log("RecordClaimAsync", _context, $"Remote recording error: {ex.Message}. Proceeding with local execution.");
                }
            }
            else
            {
                ScanDiagnosticLogger.Log("RecordClaimAsync", _context, "ROUTING=LOCAL (ShouldRouteToRemote=false)");
            }

            // Verify identity via QR payload if provided
            if (!string.IsNullOrWhiteSpace(qrPayload))
            {
                var digitalIdService = new BeneficiaryDigitalIdService(_context);
                var lookupResult = await digitalIdService.LookupByQrPayloadAsync(qrPayload);

                if (lookupResult == null)
                {
                    var existingDigitalId = await digitalIdService.GetByStagingIdAsync(beneficiaryStagingId);
                    var isAcceptedFormat = qrPayload.StartsWith("ASM-BID|", StringComparison.OrdinalIgnoreCase)
                        || qrPayload.StartsWith("ASM-BID?", StringComparison.OrdinalIgnoreCase)
                        || qrPayload.StartsWith("ASMBID", StringComparison.OrdinalIgnoreCase)
                        || qrPayload.Length >= 6;

                    if (!isAcceptedFormat)
                    {
                        return new ProjectDistributionOperationResult(false, "Beneficiary identity mismatch. The scanned ID does not match the selected record.");
                    }
                }
                else
                {
                    // Use the QR lookup result to find the correct membership
                    beneficiaryStagingId = lookupResult.BeneficiaryStagingId;
                }
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
                var releaseResult = await _budgetService.RecordWaterfallReleaseAsync(
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

        /// <summary>
        /// Builds a UI-only household snapshot for the confirm panel: the household roster plus a
        /// cross-project "already received the same assistance type" flag per member. Entities are
        /// mapped to DTOs inside the service so the ViewModel never sees EF types.
        /// </summary>
        public async Task<HouseholdVerificationContext> GetHouseholdVerificationContextAsync(int ayudaProgramId, int beneficiaryStagingId)
        {
            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.StagingID == beneficiaryStagingId);

            var householdId = staging?.LinkedHouseholdId;
            if (householdId == null)
            {
                return new HouseholdVerificationContext(false, string.Empty, string.Empty, Array.Empty<HouseholdMemberVerificationItem>(), false, null);
            }

            var program = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == ayudaProgramId);
            var assistanceType = NormalizeNullable(program?.AssistanceType);

            var household = await _context.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == householdId.Value);

            var members = await _context.HouseholdMembers
                .AsNoTracking()
                .Where(item => item.HouseholdId == householdId.Value)
                .OrderBy(item => item.FullName)
                .Select(item => new { item.Id, item.FullName, item.RelationshipToHead })
                .ToListAsync();

            // Cross-project: any claim by this household for the SAME assistance type (single server-side query).
            var priorClaims = await (
                from claim in _context.AyudaProjectClaims.AsNoTracking()
                join prog in _context.AyudaPrograms.AsNoTracking() on claim.AyudaProgramId equals prog.Id
                where claim.HouseholdId == householdId.Value
                      && (assistanceType == null || prog.AssistanceType == assistanceType)
                select new { claim.HouseholdMemberId, claim.FullName })
                .ToListAsync();

            var scannedMemberId = staging?.LinkedHouseholdMemberId;

            var memberItems = members
                .Select(member =>
                {
                    var alreadyReceived = priorClaims.Any(claim =>
                        (claim.HouseholdMemberId != null && claim.HouseholdMemberId == member.Id) ||
                        string.Equals(claim.FullName, member.FullName, StringComparison.OrdinalIgnoreCase));

                    return new HouseholdMemberVerificationItem(
                        member.FullName,
                        member.RelationshipToHead ?? string.Empty,
                        scannedMemberId != null && scannedMemberId == member.Id,
                        alreadyReceived);
                })
                .ToList();

            var anyReceived = priorClaims.Count > 0;
            var assistanceLabel = assistanceType ?? program?.ProgramName ?? "this assistance";
            var warning = anyReceived
                ? $"A member of this household has already received '{assistanceLabel}' assistance ({priorClaims.Count} claim(s) in this household)."
                : null;

            return new HouseholdVerificationContext(
                true,
                household?.HouseholdCode ?? string.Empty,
                household?.HeadName ?? string.Empty,
                memberItems,
                anyReceived,
                warning);
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
            ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, $"START | ProgramId={ayudaProgramId} | StagingId={beneficiaryStagingId}");

            var exactMembership = await _context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AyudaProgramId == ayudaProgramId &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (exactMembership != null)
            {
                ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, $"EXACT_MATCH | MembershipId={exactMembership.Id} | StagingId={exactMembership.BeneficiaryStagingId} | BeneficiaryId={exactMembership.BeneficiaryId} | Status={exactMembership.Status}");
                return exactMembership;
            }

            ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, "EXACT_MATCH=NONE, trying staging fallback...");

            var staging = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == beneficiaryStagingId);

            if (staging == null)
            {
                ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, $"STAGING_NOT_FOUND | StagingId={beneficiaryStagingId}");
                return null;
            }

            ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, $"STAGING_FOUND | StagingId={staging.StagingID} | BeneficiaryId={NormalizeNullable(staging.BeneficiaryId)} | CivilRegistryId={NormalizeNullable(staging.CivilRegistryId)}");

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
                    ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, $"BENEFICIARY_ID_MATCH | MembershipId={beneficiaryMatch.Id} | StagingId={beneficiaryMatch.BeneficiaryStagingId} | BeneficiaryId={beneficiaryMatch.BeneficiaryId} | Status={beneficiaryMatch.Status}");
                    return beneficiaryMatch;
                }
            }

            var civilRegistryId = NormalizeNullable(staging.CivilRegistryId);
            if (civilRegistryId != null)
            {
                var civilMatch = await _context.AyudaProjectBeneficiaries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.AyudaProgramId == ayudaProgramId &&
                        item.CivilRegistryId == civilRegistryId);

                if (civilMatch != null)
                {
                    ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, $"CIVIL_REGISTRY_MATCH | MembershipId={civilMatch.Id} | StagingId={civilMatch.BeneficiaryStagingId} | CivilRegistryId={civilMatch.CivilRegistryId} | Status={civilMatch.Status}");
                    return civilMatch;
                }
            }

            ScanDiagnosticLogger.Log("ResolveMembershipAsync", _context, "RESULT=NO_MEMBERSHIP_FOUND");
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

        public async Task<ProjectDistributionOperationResult> EnrollBeneficiaryAsync(int ayudaProgramId, int beneficiaryStagingId, int actedByUserId)
        {
            var alreadyEnrolled = await _context.AyudaProjectBeneficiaries
                .AnyAsync(b => b.AyudaProgramId == ayudaProgramId && b.BeneficiaryStagingId == beneficiaryStagingId);

            if (alreadyEnrolled)
            {
                return new ProjectDistributionOperationResult(false, "Beneficiary is already enrolled in this project.");
            }

            var entry = new AyudaProjectBeneficiary
            {
                AyudaProgramId = ayudaProgramId,
                BeneficiaryStagingId = beneficiaryStagingId,
                AddedAt = DateTime.Now,
                AddedByUserId = actedByUserId,
                Status = DistributionBeneficiaryStatus.Pending
            };

            _context.AyudaProjectBeneficiaries.Add(entry);
            await _context.SaveChangesAsync();

            return new ProjectDistributionOperationResult(true, "Beneficiary enrolled successfully.");
        }
    }
}

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
        private readonly IGgmsConsolidatedTransactionService _ggmsConsolidatedTransactionService;

        public AssistanceCaseManagementService(
            AppDbContext context,
            AuditService? auditService = null,
            IGgmsConsolidatedTransactionService? ggmsConsolidatedTransactionService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
            _ggmsConsolidatedTransactionService = ggmsConsolidatedTransactionService ?? NullGgmsConsolidatedTransactionService.Instance;
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

            var resolvedBudgetId = await ResolveAssistanceCaseBudgetIdAsync();

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
                ApprovedAmount = null, // Approved amount is set during approval, not creation
                RequestedOn = request.RequestedOn,
                ScheduledReleaseDate = request.ScheduledReleaseDate,
                Summary = NormalizeNullable(request.Summary),
                Notes = null,
                AyudaProgramId = null,
                AssistanceCaseBudgetId = resolvedBudgetId,
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

            if (assistanceCase.Status is AssistanceCaseStatus.Released or AssistanceCaseStatus.Closed or AssistanceCaseStatus.Cancelled or AssistanceCaseStatus.Rejected)
            {
                return new AssistanceCaseOperationResult(false, "Released, closed, or terminal aid requests can no longer be edited.");
            }

            var validatedBeneficiaryName = NormalizeNullable(request.ValidatedBeneficiaryName);
            var validation = await ValidateReferencesAsync(
                validatedBeneficiaryName,
                request.AyudaProgramId);
            if (validation is not null)
            {
                return validation;
            }

            var resolvedBudgetId = await ResolveAssistanceCaseBudgetIdAsync();

            assistanceCase.HouseholdId = null;
            assistanceCase.HouseholdMemberId = null;
            assistanceCase.ValidatedBeneficiaryName = validatedBeneficiaryName;
            assistanceCase.ValidatedBeneficiaryId = NormalizeNullable(request.ValidatedBeneficiaryId);
            assistanceCase.ValidatedCivilRegistryId = NormalizeNullable(request.ValidatedCivilRegistryId);
            assistanceCase.AssistanceType = NormalizeRequired(request.AssistanceType);
            assistanceCase.ReleaseKind = request.ReleaseKind;
            assistanceCase.Priority = request.Priority;
            assistanceCase.RequestedAmount = request.AssistanceAmount;
            // ApprovedAmount is NOT updated here; it must be set via status change (Approve)
            assistanceCase.RequestedOn = request.RequestedOn;
            assistanceCase.ScheduledReleaseDate = request.ScheduledReleaseDate;
            assistanceCase.Summary = NormalizeNullable(request.Summary);
            assistanceCase.Notes = null;
            assistanceCase.AyudaProgramId = null;
            assistanceCase.AssistanceCaseBudgetId = resolvedBudgetId;
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
            if (targetStatus == AssistanceCaseStatus.Released && RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                var localCase = await _context.AssistanceCases
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

                if (localCase == null)
                {
                    return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists locally.");
                }

                var localBudget = await _context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_AID_BUDGET" && item.IsActive);

                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            if (localBudget != null)
                            {
                                var remoteBudget = await remoteContext.AssistanceCaseBudgets
                                    .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_AID_BUDGET");

                                if (remoteBudget == null)
                                {
                                    remoteBudget = new AssistanceCaseBudget
                                    {
                                        BudgetCode = localBudget.BudgetCode,
                                        BudgetName = localBudget.BudgetName,
                                        Description = localBudget.Description,
                                        AssistanceType = localBudget.AssistanceType,
                                        BudgetCap = localBudget.BudgetCap,
                                        IsActive = localBudget.IsActive,
                                        CreatedByUserId = actedByUserId,
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    remoteContext.AssistanceCaseBudgets.Add(remoteBudget);
                                    await remoteContext.SaveChangesAsync();
                                }
                            }

                            var remoteCase = await remoteContext.AssistanceCases
                                .FirstOrDefaultAsync(item => item.CaseNumber == localCase.CaseNumber);

                            if (remoteCase == null)
                            {
                                remoteCase = new AssistanceCase
                                {
                                    CaseNumber = localCase.CaseNumber,
                                    ValidatedBeneficiaryName = localCase.ValidatedBeneficiaryName,
                                    ValidatedBeneficiaryId = localCase.ValidatedBeneficiaryId,
                                    ValidatedCivilRegistryId = localCase.ValidatedCivilRegistryId,
                                    AssistanceType = localCase.AssistanceType,
                                    ReleaseKind = localCase.ReleaseKind,
                                    Priority = localCase.Priority,
                                    Status = localCase.Status,
                                    RequestedAmount = localCase.RequestedAmount,
                                    ApprovedAmount = localCase.ApprovedAmount,
                                    RequestedOn = localCase.RequestedOn,
                                    ScheduledReleaseDate = localCase.ScheduledReleaseDate,
                                    Summary = localCase.Summary,
                                    Notes = localCase.Notes,
                                    CreatedByUserId = actedByUserId,
                                    CreatedAt = localCase.CreatedAt,
                                    UpdatedAt = DateTime.Now
                                };

                                remoteContext.AssistanceCases.Add(remoteCase);
                                await remoteContext.SaveChangesAsync();
                            }

                            var remoteService = new AssistanceCaseManagementService(
                                remoteContext,
                                auditService: null,
                                ggmsConsolidatedTransactionService: _ggmsConsolidatedTransactionService);
                            return await remoteService.ChangeStatusAsync(remoteCase.Id, targetStatus, actedByUserId, resolutionNotes);
                        });

                    if (!remoteResult.IsSuccess)
                    {
                        // Recovery Logic: If remote says it's already released, we should treat it as a success 
                        // and continue to ensure our local database is also marked as Released.
                        if (remoteResult.Message != null && remoteResult.Message.Contains("already has a recorded budget release", StringComparison.OrdinalIgnoreCase))
                        {
                            // Continue to local sync
                        }
                        else
                        {
                            return remoteResult;
                        }
                    }

                    // If remote succeeded (or was already released), we continue to local update below.
                }
                catch (Exception ex)
                {
                    return new AssistanceCaseOperationResult(false, $"Remote release failed. {ex.Message}");
                }
            }

            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            // Recovery Logic: If budget was already released locally but status stayed 'Approved', 
            // we should allow it to transition to 'Released' even if the state machine is grumpy.
            if (targetStatus == AssistanceCaseStatus.Released && assistanceCase.BudgetLedgerEntryId.HasValue)
            {
                assistanceCase.Status = AssistanceCaseStatus.Released;
                assistanceCase.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return new AssistanceCaseOperationResult(true, "Aid request was already released. Local status synchronized.");
            }

            // Enforce State Machine
            var currentStatus = assistanceCase.Status;
            bool isValidTransition = (currentStatus, targetStatus) switch
            {
                (AssistanceCaseStatus.Pending, AssistanceCaseStatus.UnderReview) => true,
                (AssistanceCaseStatus.Pending, AssistanceCaseStatus.Rejected) => true,
                (AssistanceCaseStatus.Pending, AssistanceCaseStatus.Cancelled) => true,

                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Approved) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Rejected) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Cancelled) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Pending) => true,

                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.Released) => true,
                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.UnderReview) => true,
                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.Cancelled) => true,

                (AssistanceCaseStatus.Released, AssistanceCaseStatus.Closed) => true,
                (AssistanceCaseStatus.Rejected, AssistanceCaseStatus.Pending) => true,
                (AssistanceCaseStatus.Cancelled, AssistanceCaseStatus.Pending) => true,

                // Allow resetting terminal states for admin corrections if needed, 
                // but generally they are terminal.
                _ => false
            };

            if (!isValidTransition && currentStatus != targetStatus)
            {
                return new AssistanceCaseOperationResult(false, $"Invalid status transition from {currentStatus} to {targetStatus}.");
            }

            if (targetStatus == AssistanceCaseStatus.Approved)
            {
                if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    assistanceCase.AssistanceCaseBudgetId = await ResolveAssistanceCaseBudgetIdAsync();
                }

                if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    return new AssistanceCaseOperationResult(false, "A global aid request budget must be set before approving requests.");
                }

                var approvedBudget = await _context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == assistanceCase.AssistanceCaseBudgetId.Value && item.IsActive);

                if (approvedBudget == null)
                {
                    return new AssistanceCaseOperationResult(false, "The global assistance case budget is not active or no longer exists.");
                }
                
                // If moving to Approved, set the ApprovedAmount from RequestedAmount if it's currently null
                assistanceCase.ApprovedAmount ??= assistanceCase.RequestedAmount;

                if (!assistanceCase.ApprovedAmount.HasValue || assistanceCase.ApprovedAmount.Value <= 0)
                {
                    return new AssistanceCaseOperationResult(false, "Set an approved amount before approving this request.");
                }
            }

            var shouldWriteGgmsRelease = false;
            if (targetStatus == AssistanceCaseStatus.Released)
            {
                if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    assistanceCase.AssistanceCaseBudgetId = await ResolveAssistanceCaseBudgetIdAsync();
                }

                if (!assistanceCase.ApprovedAmount.HasValue || assistanceCase.ApprovedAmount.Value <= 0)
                {
                    return new AssistanceCaseOperationResult(false, "Assistance amount is required before releasing this aid request.");
                }

                if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    return new AssistanceCaseOperationResult(false, "No active global aid request budget found. Please set one in the Budget module first.");
                }

                var releaseBudget = await _context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == assistanceCase.AssistanceCaseBudgetId.Value && item.IsActive);

                if (releaseBudget == null)
                {
                    return new AssistanceCaseOperationResult(false, "The global assistance case budget is no longer active.");
                }

                if (assistanceCase.BudgetLedgerEntryId.HasValue)
                {
                    assistanceCase.Status = AssistanceCaseStatus.Released;
                    assistanceCase.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return new AssistanceCaseOperationResult(true, "Aid request was already released. Local status synchronized.");
                }

                var budgetService = new BudgetManagementService(_context, _auditService);
                var budgetResult = await budgetService.RecordReleaseAsync(
                    new BudgetReleaseRequest(
                        null,
                        BudgetLedgerFeatureSource.AssistanceCase,
                        $"assistance:{assistanceCase.Id}",
                        1,
                        assistanceCase.ReleaseKind,
                        assistanceCase.ApprovedAmount.Value,
                        DateTime.Now,
                        assistanceCase.Summary ?? assistanceCase.AssistanceType,
                        assistanceCase.AssistanceCaseBudgetId),
                    actedByUserId);

                if (!budgetResult.IsSuccess)
                {
                    if (budgetResult.Message != null && budgetResult.Message.Contains("already has a budget ledger entry", StringComparison.OrdinalIgnoreCase))
                    {
                        assistanceCase.Status = AssistanceCaseStatus.Released;
                        assistanceCase.UpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                        return new AssistanceCaseOperationResult(true, "Aid request was already released. Local status synchronized.");
                    }
                    return new AssistanceCaseOperationResult(false, budgetResult.Message ?? "Budget recording failed.");
                }

                assistanceCase.BudgetLedgerEntryId = budgetResult.LedgerEntryId;

                // Sync with beneficiary assistance history
                var historyService = new BeneficiaryAssistanceLedgerService(_context, _auditService);
                await historyService.RecordEntryAsync(
                    assistanceCase.ValidatedCivilRegistryId,
                    assistanceCase.ValidatedBeneficiaryId,
                    BeneficiaryAssistanceSourceModule.AssistanceCase,
                    $"assistance:{assistanceCase.Id}",
                    DateTime.Now,
                    assistanceCase.ApprovedAmount!.Value,
                    assistanceCase.Summary ?? assistanceCase.AssistanceType,
                    actedByUserId);

                shouldWriteGgmsRelease = true;
            }

            assistanceCase.Status = targetStatus;
            assistanceCase.ResolutionNotes = resolutionNotes;
            assistanceCase.ReviewedByUserId = actedByUserId;
            assistanceCase.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            string? ggmsWarningMessage = null;
            if (shouldWriteGgmsRelease)
            {
                ggmsWarningMessage = await _ggmsConsolidatedTransactionService.TryWriteAssistanceCaseReleaseAsync(_context, assistanceCase);
            }

            await _auditService.LogActivityAsync(
                actedByUserId,
                "AssistanceCaseStatusChanged",
                "AssistanceCase",
                assistanceCase.Id,
                $"Changed aid request '{assistanceCase.CaseNumber}' to {targetStatus}.");

            var successMessage = $"Updated {assistanceCase.CaseNumber} to {targetStatus}.";
            if (!string.IsNullOrWhiteSpace(ggmsWarningMessage))
            {
                successMessage = $"{successMessage} GGMS sync warning: {ggmsWarningMessage}";
            }

            return new AssistanceCaseOperationResult(
                true,
                successMessage,
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

        private async Task<AssistanceCaseOperationResult?> ValidateReferencesAsync(
            string? validatedBeneficiaryName,
            int? ayudaProgramId)
        {
            if (string.IsNullOrWhiteSpace(validatedBeneficiaryName))
            {
                return new AssistanceCaseOperationResult(false, "Select a validated beneficiary before saving this aid request.");
            }

            if (!ayudaProgramId.HasValue)
            {
                return null;
            }

            var program = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == ayudaProgramId.Value && item.IsActive);

            if (program == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected ayuda program no longer exists.");
            }

            if (program.ProgramType == AyudaProgramType.Seminar)
            {
                return new AssistanceCaseOperationResult(false, "Seminar programs are no longer allowed in Aid Request.");
            }

            return null;
        }

        private async Task<int?> ResolveAssistanceCaseBudgetIdAsync()
        {
            var globalBudget = await _context.AssistanceCaseBudgets
                .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_AID_BUDGET");
            
            return globalBudget?.Id;
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

        public async Task<AssistanceCaseOperationResult> RejectCaseAsync(int assistanceCaseId, string reason, int actedByUserId)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return new AssistanceCaseOperationResult(false, "Resolution notes (reason) are required when rejecting a case.");
            }
            return await ChangeStatusAsync(assistanceCaseId, AssistanceCaseStatus.Rejected, actedByUserId, reason);
        }

        public async Task<AssistanceCaseOperationResult> CancelCaseAsync(int assistanceCaseId, string reason, int actedByUserId)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return new AssistanceCaseOperationResult(false, "Resolution notes (reason) are required when cancelling a case.");
            }
            return await ChangeStatusAsync(assistanceCaseId, AssistanceCaseStatus.Cancelled, actedByUserId, reason);
        }

        public async Task<AssistanceCaseOperationResult> FastTrackReleaseAsync(int assistanceCaseId, decimal approvedAmount, int actedByUserId, string? summary = null)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            if (assistanceCase.Status is not AssistanceCaseStatus.Pending && assistanceCase.Status is not AssistanceCaseStatus.UnderReview)
            {
                return new AssistanceCaseOperationResult(false, "Only Pending or Under Review requests can be fast-tracked.");
            }

            if (approvedAmount <= 0)
            {
                return new AssistanceCaseOperationResult(false, "A valid approved amount is required for fast-track release.");
            }

            // Apply Approval State locally
            assistanceCase.ApprovedAmount = approvedAmount;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                assistanceCase.Summary = summary;
            }
            
            // Bypass state machine to go directly to Released
            assistanceCase.Status = AssistanceCaseStatus.Approved; 
            await _context.SaveChangesAsync();

            // Delegate to the main release pipeline now that it's "Approved" and has an amount
            return await ChangeStatusAsync(assistanceCaseId, AssistanceCaseStatus.Released, actedByUserId, null);
        }
    }
}

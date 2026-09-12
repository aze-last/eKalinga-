using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record AssistanceCaseOperationResult(bool IsSuccess, string Message, int? AssistanceCaseId = null, bool IsConcurrencyConflict = false);

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
        private readonly LocalDbContext _context;
        private readonly AuditService _auditService;
        private readonly IGgmsConsolidatedTransactionService _ggmsConsolidatedTransactionService;

        public AssistanceCaseManagementService(
            LocalDbContext context,
            AuditService? auditService = null,
            IGgmsConsolidatedTransactionService? ggmsConsolidatedTransactionService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
            _ggmsConsolidatedTransactionService = ggmsConsolidatedTransactionService ?? NullGgmsConsolidatedTransactionService.Instance;
        }

        public async Task<AssistanceCaseOperationResult> CreateAsync(AssistanceCaseUpsertRequest request, int actedByUserId, int? targetBudgetId = null)
        {
            var validatedBeneficiaryName = NormalizeNullable(request.ValidatedBeneficiaryName);
            var validation = await ValidateReferencesAsync(
                validatedBeneficiaryName,
                request.AyudaProgramId);
            if (validation is not null)
            {
                return validation;
            }

            var resolvedBudgetId = targetBudgetId ?? await ResolveAssistanceCaseBudgetIdAsync();
            if (!resolvedBudgetId.HasValue)
            {
                var anyActive = await _context.AssistanceCaseBudgets.FirstOrDefaultAsync(b => b.IsActive);
                resolvedBudgetId = anyActive?.Id;
            }

            // Guardrail: two PCs intaking at the same moment can generate the same
            // CaseNumber (Max + 1 race). case_number is UNIQUE in the DB, so
            // retry with a freshly generated number instead of crashing.
            const int maxCaseNumberAttempts = 3;
            AssistanceCase? assistanceCase = null;
            for (var attempt = 0; attempt < maxCaseNumberAttempts; attempt++)
            {
                var candidate = new AssistanceCase
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
                    AyudaProgramId = request.AyudaProgramId,
                    AssistanceCaseBudgetId = resolvedBudgetId,
                    CreatedByUserId = actedByUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.AssistanceCases.Add(candidate);
                try
                {
                    await _context.SaveChangesAsync();
                    assistanceCase = candidate;
                    break;
                }
                catch (DbUpdateException ex) when (IsCaseNumberConflict(ex) && attempt < maxCaseNumberAttempts - 1)
                {
                    _context.Entry(candidate).State = EntityState.Detached;
                }
            }

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "Could not assign a unique case number after 3 attempts. Please retry.");
            }

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

        public async Task<AssistanceCaseOperationResult> UpdateAsync(int assistanceCaseId, AssistanceCaseUpsertRequest request, int actedByUserId, DateTime? expectedUpdatedAt = null)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            // Guardrail: another PC may have changed this row since the UI loaded it.
            if (expectedUpdatedAt.HasValue && assistanceCase.UpdatedAt != expectedUpdatedAt.Value)
            {
                return StaleConflictResult();
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
            assistanceCase.AyudaProgramId = request.AyudaProgramId;
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

        public async Task<AssistanceCaseOperationResult> ChangeStatusAsync(int assistanceCaseId, AssistanceCaseStatus targetStatus, int actedByUserId, string? resolutionNotes, int? targetBudgetId = null, DateTime? expectedUpdatedAt = null)
        {
            // Guardrail: check BEFORE the remote-release branch below, so a stale
            // local view never triggers a remote write that the local then rejects.
            if (expectedUpdatedAt.HasValue)
            {
                var currentStamp = await _context.AssistanceCases
                    .AsNoTracking()
                    .Where(item => item.Id == assistanceCaseId)
                    .Select(item => (DateTime?)item.UpdatedAt)
                    .FirstOrDefaultAsync();

                if (currentStamp == null)
                {
                    return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
                }

                if (currentStamp.Value != expectedUpdatedAt.Value)
                {
                    return StaleConflictResult();
                }
            }

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
                                ggmsConsolidatedTransactionService: NullGgmsConsolidatedTransactionService.Instance);
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
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Closed) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Rejected) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Cancelled) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Pending) => true,

                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.Released) => true,
                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.Closed) => true,
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
                if (targetBudgetId.HasValue)
                {
                    assistanceCase.AssistanceCaseBudgetId = targetBudgetId.Value;
                }
                else if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    assistanceCase.AssistanceCaseBudgetId = await ResolveAssistanceCaseBudgetIdAsync();
                }

                if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    var anyActive = await _context.AssistanceCaseBudgets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => item.IsActive);
                    if (anyActive != null)
                    {
                        assistanceCase.AssistanceCaseBudgetId = anyActive.Id;
                    }
                    else
                    {
                        var defaultBudget = new AssistanceCaseBudget
                        {
                            BudgetCode = "GLOBAL_AID_BUDGET",
                            BudgetName = "Global Aid Request Budget",
                            Description = "Default municipal assistance fund pool",
                            AssistanceType = "General Aid",
                            BudgetCap = null,
                            IsActive = true,
                            CreatedByUserId = actedByUserId,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(defaultBudget);
                        await _context.SaveChangesAsync();
                        assistanceCase.AssistanceCaseBudgetId = defaultBudget.Id;
                    }
                }

                var approvedBudget = await _context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == assistanceCase.AssistanceCaseBudgetId.Value && item.IsActive);

                if (approvedBudget == null)
                {
                    var anyActive = await _context.AssistanceCaseBudgets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => item.IsActive);
                    if (anyActive != null)
                    {
                        assistanceCase.AssistanceCaseBudgetId = anyActive.Id;
                    }
                }
                
                // If moving to Approved, set the ApprovedAmount from RequestedAmount if it's currently null
                assistanceCase.ApprovedAmount ??= assistanceCase.RequestedAmount;

                if (!assistanceCase.ApprovedAmount.HasValue || assistanceCase.ApprovedAmount.Value <= 0)
                {
                    return new AssistanceCaseOperationResult(false, "Set an approved amount before approving this request.");
                }

                // NEW: Check "pondo" (Budget Pool) availability during Approval
                var budgetService = new BudgetManagementService(_context, _auditService);
                var overview = await budgetService.GetOverviewAsync();
                if (assistanceCase.ApprovedAmount.Value > overview.CombinedAvailable)
                {
                    var shortfall = assistanceCase.ApprovedAmount.Value - overview.CombinedAvailable;
                    return new AssistanceCaseOperationResult(false, $"Approval denied. The total budget pool (pondo) is insufficient by {shortfall:N2}.");
                }
            }

            var shouldWriteGgmsRelease = false;
            if (targetStatus == AssistanceCaseStatus.Released)
            {
                if (targetBudgetId.HasValue)
                {
                    assistanceCase.AssistanceCaseBudgetId = targetBudgetId.Value;
                }
                else if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    assistanceCase.AssistanceCaseBudgetId = await ResolveAssistanceCaseBudgetIdAsync();
                }

                if (!assistanceCase.ApprovedAmount.HasValue || assistanceCase.ApprovedAmount.Value <= 0)
                {
                    return new AssistanceCaseOperationResult(false, "Assistance amount is required before releasing this aid request.");
                }

                if (!assistanceCase.AssistanceCaseBudgetId.HasValue)
                {
                    var anyActive = await _context.AssistanceCaseBudgets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => item.IsActive);
                    if (anyActive != null)
                    {
                        assistanceCase.AssistanceCaseBudgetId = anyActive.Id;
                    }
                    else
                    {
                        var defaultBudget = new AssistanceCaseBudget
                        {
                            BudgetCode = "GLOBAL_AID_BUDGET",
                            BudgetName = "Global Aid Request Budget",
                            Description = "Default municipal assistance fund pool",
                            AssistanceType = "General Aid",
                            BudgetCap = null,
                            IsActive = true,
                            CreatedByUserId = actedByUserId,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(defaultBudget);
                        await _context.SaveChangesAsync();
                        assistanceCase.AssistanceCaseBudgetId = defaultBudget.Id;
                    }
                }

                var releaseBudget = await _context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == assistanceCase.AssistanceCaseBudgetId.Value && item.IsActive);

                if (releaseBudget == null)
                {
                    var anyActive = await _context.AssistanceCaseBudgets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => item.IsActive);
                    if (anyActive != null)
                    {
                        assistanceCase.AssistanceCaseBudgetId = anyActive.Id;
                    }
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
                    if (budgetResult.IsDuplicate)
                    {
                        if (budgetResult.LedgerEntryId.HasValue)
                        {
                            assistanceCase.BudgetLedgerEntryId = budgetResult.LedgerEntryId;
                        }

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
            if (globalBudget != null) return globalBudget.Id;

            var activeBudget = await _context.AssistanceCaseBudgets
                .Where(item => item.IsActive)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();
            return activeBudget?.Id;
        }

        private async Task<string> GenerateCaseNumberAsync()
        {
            var prefix = $"AR-{DateTime.Now:yyyyMMdd}";
            // Max-suffix (not Count + 1): hard-deleted rows would otherwise make
            // Count reuse an existing number. Still racy across PCs — the caller
            // retries on UNIQUE violation.
            var existingNumbers = await _context.AssistanceCases
                .Where(item => item.CaseNumber.StartsWith(prefix))
                .Select(item => item.CaseNumber)
                .ToListAsync();

            var nextSequence = 1;
            foreach (var caseNumber in existingNumbers)
            {
                var separator = caseNumber.LastIndexOf('-');
                if (separator >= 0
                    && int.TryParse(caseNumber[(separator + 1)..], out var sequence)
                    && sequence >= nextSequence)
                {
                    nextSequence = sequence + 1;
                }
            }

            return $"{prefix}-{nextSequence:0000}";
        }

        private static bool IsCaseNumberConflict(DbUpdateException ex)
        {
            var message = $"{ex.Message} {ex.InnerException?.Message}";
            return message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
                || message.Contains("1062", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("IX_assistance_cases_case_number", StringComparison.OrdinalIgnoreCase)
                || message.Contains("case_number", StringComparison.OrdinalIgnoreCase);
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

        public async Task<AssistanceCaseOperationResult> RejectCaseAsync(int assistanceCaseId, string reason, int actedByUserId, DateTime? expectedUpdatedAt = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return new AssistanceCaseOperationResult(false, "Resolution notes (reason) are required when rejecting a case.");
            }
            return await ChangeStatusAsync(assistanceCaseId, AssistanceCaseStatus.Rejected, actedByUserId, reason, null, expectedUpdatedAt);
        }

        public async Task<AssistanceCaseOperationResult> CancelCaseAsync(int assistanceCaseId, string reason, int actedByUserId, DateTime? expectedUpdatedAt = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return new AssistanceCaseOperationResult(false, "Resolution notes (reason) are required when cancelling a case.");
            }
            return await ChangeStatusAsync(assistanceCaseId, AssistanceCaseStatus.Cancelled, actedByUserId, reason, null, expectedUpdatedAt);
        }

        public async Task<AssistanceCaseOperationResult> FastTrackReleaseAsync(int assistanceCaseId, decimal approvedAmount, int actedByUserId, string? summary = null, int? targetBudgetId = null, DateTime? expectedUpdatedAt = null)
        {
            var assistanceCase = await _context.AssistanceCases
                .FirstOrDefaultAsync(item => item.Id == assistanceCaseId);

            if (assistanceCase == null)
            {
                return new AssistanceCaseOperationResult(false, "The selected aid request no longer exists.");
            }

            // Guardrail: fail before mutating anything locally.
            if (expectedUpdatedAt.HasValue && assistanceCase.UpdatedAt != expectedUpdatedAt.Value)
            {
                return StaleConflictResult();
            }

            if (targetBudgetId.HasValue)
            {
                assistanceCase.AssistanceCaseBudgetId = targetBudgetId.Value;
            }

            if (assistanceCase.Status is AssistanceCaseStatus.Released or AssistanceCaseStatus.Closed or AssistanceCaseStatus.Cancelled or AssistanceCaseStatus.Rejected)
            {
                return new AssistanceCaseOperationResult(false, "Released, closed, or terminal aid requests cannot be disbursed.");
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
            return await ChangeStatusAsync(assistanceCaseId, AssistanceCaseStatus.Released, actedByUserId, null, targetBudgetId, expectedUpdatedAt);
        }

        private static AssistanceCaseOperationResult StaleConflictResult()
        {
            return new AssistanceCaseOperationResult(
                false,
                "Another user updated this request after you opened it. Please review the latest details and try again.",
                null,
                true);
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record ProjectBudgetSourceRequestDto(
        int BudgetBucketId,
        string BudgetBucketType,
        int Priority);

    public sealed record AyudaProgramRequest(
        string ProgramCode,
        string ProgramName,
        AyudaProgramType ProgramType,
        string? Description,
        string? AssistanceType,
        AssistanceReleaseKind ReleaseKind,
        decimal? UnitAmount,
        string? ItemDescription,
        string? ItemName,
        decimal? QuantityPerBeneficiary,
        string? UnitOfMeasure,
        DateTime? StartDate,
        DateTime? EndDate,
        decimal? BudgetCap,
        AyudaProgramDistributionStatus DistributionStatus,
        int? SourceDonationId = null,
        int? SourceGGMSBudgetId = null,
        string? SourceProjectDetailsId = null);

    public sealed record AyudaProgramOperationResult(bool IsSuccess, string Message, int? ProgramId = null);

    public sealed record AssistanceCaseBudgetRequest(
        string BudgetCode,
        string BudgetName,
        string? Description,
        string? AssistanceType,
        decimal? BudgetCap);

    public sealed record AssistanceCaseBudgetOperationResult(bool IsSuccess, string Message, int? AssistanceCaseBudgetId = null);

    public sealed record CashForWorkBudgetRequest(
        string BudgetCode,
        string BudgetName,
        string? Description,
        decimal? BudgetCap);

    public sealed record CashForWorkBudgetOperationResult(bool IsSuccess, string Message, int? CashForWorkBudgetId = null);

    public sealed record CashForWorkProjectRequest(
        string BudgetCode,
        string BudgetName,
        string? Description,
        decimal? DailyRate,
        decimal? BudgetCap,
        DateTime StartDate,
        DateTime EndDate,
        int? SourceDonationId = null,
        int? SourceGGMSBudgetId = null,
        string? SourceProjectDetailsId = null);

    public sealed record CashForWorkProjectOperationResult(
        bool Success,
        string Message = "",
        int BudgetId = 0,
        string BudgetName = "");

    public sealed record GovernmentBudgetSnapshotRequest(
        string OfficeCode,
        string OfficeName,
        int YearlyBudgetId,
        decimal AllocatedAmount,
        decimal SpentAmount,
        string? SourceRowId,
        GovernmentBudgetSyncStatus SyncStatus,
        DateTime SyncedAt,
        int? TargetProgramId = null,
        int? TargetAssistanceCaseBudgetId = null,
        int? TargetCashForWorkBudgetId = null);

    public sealed record GovernmentBudgetSnapshotOperationResult(bool IsSuccess, string Message, int? SnapshotId = null);

    public sealed record PrivateDonationRequest(
        PrivateDonationDonorType DonorType,
        string DonorName,
        DonationType DonationType,
        decimal Amount,
        string? ItemName,
        decimal? Quantity,
        string? UnitOfMeasure,
        DateTime DateReceived,
        string? ReferenceNumber,
        string? Remarks,
        DonationProofType ProofType,
        string? ProofReferenceNumber,
        string? ProofFilePath,
        int? TargetProgramId = null,
        int? TargetAssistanceCaseBudgetId = null,
        int? TargetCashForWorkBudgetId = null)
    {
        public PrivateDonationRequest(
            PrivateDonationDonorType donorType,
            string donorName,
            decimal amount,
            DateTime dateReceived,
            string? referenceNumber,
            string? remarks,
            DonationProofType proofType,
            string? proofReferenceNumber,
            string? proofFilePath,
            int? targetProgramId = null,
            int? targetAssistanceCaseBudgetId = null,
            int? targetCashForWorkBudgetId = null)
            : this(
                donorType,
                donorName,
                DonationType.Cash,
                amount,
                null,
                null,
                null,
                dateReceived,
                referenceNumber,
                remarks,
                proofType,
                proofReferenceNumber,
                proofFilePath,
                targetProgramId,
                targetAssistanceCaseBudgetId,
                targetCashForWorkBudgetId)
        {
        }
    }

    public sealed record PrivateDonationOperationResult(bool IsSuccess, string Message, int? DonationId = null, int? LedgerEntryId = null);

    public sealed record BudgetReleaseRequest(
        int? AyudaProgramId,
        BudgetLedgerFeatureSource FeatureSource,
        string SourceRecordId,
        int RecipientCount,
        AssistanceReleaseKind ReleaseKind,
        decimal TotalAmount,
        DateTime ReleasedAt,
        string? Remarks,
        int? AssistanceCaseBudgetId = null,
        int? CashForWorkBudgetId = null,
        string? ForcedBudgetBucketType = null);

    public sealed record BudgetReleaseOperationResult(bool IsSuccess, string Message, int? LedgerEntryId = null);

    public sealed record BudgetOverviewSnapshot(
        decimal GovernmentAllocated,
        decimal GovernmentSpentReference,
        decimal GovernmentAvailable,
        decimal GovernmentUnrestrictedAvailable,
        decimal GovernmentLockedAvailable,
        decimal PrivateAvailable,
        decimal PrivateUnrestrictedAvailable,
        decimal PrivateLockedAvailable,
        decimal CombinedAvailable,
        decimal ReleasedTotal,
        decimal GovernmentReleasedTotal,
        decimal PrivateReleasedTotal,
        decimal WeeklySpent,
        decimal MonthlySpent,
        DateTime? LastGovernmentSyncAt,
        string? OfficeCode,
        string? OfficeName,
        decimal GovernmentProjectEarmarkTotal = 0m);

    public sealed record EarmarkedBudgetStatus(
        int TargetId,
        string TargetType,
        string TargetName,
        decimal AllocatedAmount,
        decimal ConsumedAmount,
        decimal RemainingAmount,
        bool IsDormant);

    public sealed record ReallocationOperationResult(bool IsSuccess, string Message, int? LedgerEntryId = null);

    public sealed class BudgetManagementService
    {
        private readonly LocalDbContext _context;
        private readonly AuditService _auditService;

        public BudgetManagementService(LocalDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        public async Task<IReadOnlyList<AyudaProgram>> GetProgramsAsync()
        {
            return await _context.AyudaPrograms
                .AsNoTracking()
                .Where(p => p.IsActive)
                .Include(p => p.SourceDonation)
                .Include(p => p.SourceGGMSBudget)
                .OrderBy(program => program.ProgramName)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AssistanceCaseBudget>> GetAssistanceCaseBudgetsAsync()
        {
            return await _context.AssistanceCaseBudgets
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.BudgetName)
                .ThenBy(item => item.BudgetCode)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<CashForWorkBudget>> GetCashForWorkBudgetsAsync()
        {
            return await _context.CashForWorkBudgets
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.BudgetName)
                .ThenBy(item => item.BudgetCode)
                .ToListAsync();
        }

        public async Task<AssistanceCaseBudget?> GetGlobalAssistanceCaseBudgetAsync()
        {
            return await _context.AssistanceCaseBudgets
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_AID_BUDGET");
        }

        public async Task<AssistanceCaseBudgetOperationResult> CreateAssistanceCaseBudgetAsync(AssistanceCaseBudgetRequest request, int createdByUserId)
        {
            var budgetCap = request.BudgetCap ?? 0m;
            if (budgetCap < 0)
            {
                return new AssistanceCaseBudgetOperationResult(false, "Aid request budget cap cannot be negative.");
            }

            var budget = await _context.AssistanceCaseBudgets
                .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_AID_BUDGET");

            if (budget == null)
            {
                budget = new AssistanceCaseBudget
                {
                    BudgetCode = "GLOBAL_AID_BUDGET",
                    BudgetName = "Global Aid Request Budget",
                    Description = "The unified budget cap for all walk-in and individual assistance cases.",
                    AssistanceType = "General Assistance",
                    BudgetCap = budgetCap,
                    IsActive = true,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.AssistanceCaseBudgets.Add(budget);
            }
            else
            {
                budget.BudgetCap = budgetCap;
                budget.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                createdByUserId,
                "AssistanceCaseBudgetUpdated",
                nameof(AssistanceCaseBudget),
                budget.Id,
                $"Updated global assistance case budget cap to {budgetCap:N2}.");

            return new AssistanceCaseBudgetOperationResult(true, "Updated global aid request budget.", budget.Id);
        }

        public async Task<CashForWorkBudget?> GetGlobalCashForWorkBudgetAsync()
        {
            return await _context.CashForWorkBudgets
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_CFW_BUDGET");
        }

        public async Task<CashForWorkBudgetOperationResult> CreateCashForWorkBudgetAsync(CashForWorkBudgetRequest request, int createdByUserId)
        {
            var budgetCap = request.BudgetCap ?? 0m;
            if (budgetCap < 0)
            {
                return new CashForWorkBudgetOperationResult(false, "Cash-for-work budget cap cannot be negative.");
            }

            var budget = await _context.CashForWorkBudgets
                .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_CFW_BUDGET");

            if (budget == null)
            {
                budget = new CashForWorkBudget
                {
                    BudgetCode = "GLOBAL_CFW_BUDGET",
                    BudgetName = "Global Cash-for-Work Budget",
                    Description = "The unified budget cap for all cash-for-work events and activities.",
                    BudgetCap = budgetCap,
                    IsActive = true,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.CashForWorkBudgets.Add(budget);
            }
            else
            {
                budget.BudgetCap = budgetCap;
                budget.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                createdByUserId,
                "CashForWorkBudgetUpdated",
                nameof(CashForWorkBudget),
                budget.Id,
                $"Updated global cash-for-work budget cap to {budgetCap:N2}.");

            return new CashForWorkBudgetOperationResult(true, "Updated global cash-for-work budget.", budget.Id);
        }

        public async Task<CashForWorkProjectOperationResult> CreateCashForWorkProjectAsync(
            CashForWorkProjectRequest request, int createdByUserId)
        {
            int? remoteGeneratedId = null;
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            var remoteService = new BudgetManagementService(remoteContext, _auditService);
                            return await remoteService.CreateCashForWorkProjectAsync(request, createdByUserId);
                        });

                    if (!remoteResult.Success) return remoteResult;
                    remoteGeneratedId = remoteResult.BudgetId;
                }
                catch (Exception ex)
                {
                    return new(false, $"Remote creation failed. {ex.Message}");
                }
            }

            var budgetCode = NormalizeRequired(request.BudgetCode);
            var budgetName = NormalizeRequired(request.BudgetName);

            // Validate
            if (string.IsNullOrWhiteSpace(budgetName))
                return new(false, "Project name is required");
            if (string.IsNullOrWhiteSpace(budgetCode))
                return new(false, "Project code is required");
            if (budgetCode.Length > 40)
                return new(false, "Project code is too long (40 characters max including the CFW- prefix)");
            if (request.BudgetCap.HasValue && request.BudgetCap < 0)
                return new(false, "Budget cap cannot be negative");
            if (request.DailyRate.HasValue && request.DailyRate <= 0)
                return new(false, "Daily rate must be positive");
            if (request.StartDate > request.EndDate)
                return new(false, "Start date cannot be after end date");

            // budget_code has a unique index; fail with a friendly message instead of a DbUpdateException
            var codeExists = await _context.CashForWorkBudgets.AsNoTracking()
                .AnyAsync(b => b.BudgetCode == budgetCode);
            if (codeExists)
                return new(false, $"A cash-for-work project with code '{budgetCode}' already exists");

            // Verify funding source exists
            if (request.SourceDonationId.HasValue)
            {
                var donationExists = await _context.PrivateDonations.AsNoTracking()
                    .AnyAsync(d => d.Id == request.SourceDonationId.Value);
                if (!donationExists)
                    return new(false, "Selected donation not found");
            }
            if (request.SourceGGMSBudgetId.HasValue)
            {
                var ggmsBudgetExists = await _context.GovernmentBudgetSnapshots.AsNoTracking()
                    .AnyAsync(g => g.Id == request.SourceGGMSBudgetId.Value);
                if (!ggmsBudgetExists)
                    return new(false, "Selected GGMS budget not found");
            }

            var sourceProjectDetailsId = NormalizeNullable(request.SourceProjectDetailsId);
            var budgetCap = request.BudgetCap;

            // GGMS-linked projects: the sub-allocation is the spending envelope — cap defaults
            // to it and can never exceed it (same rule as CreateProgramAsync). The cache is a
            // local-only SQLite mirror, so this validation is skipped on the MySQL provider.
            if (sourceProjectDetailsId != null && _context.Database.ProviderName != "Pomelo.EntityFrameworkCore.MySql")
            {
                var linkedGgmsProject = await _context.GgmsProjectCache.AsNoTracking()
                    .FirstOrDefaultAsync(cache => cache.ProjectDetailsId == sourceProjectDetailsId);

                if (linkedGgmsProject == null)
                    return new(false, $"GGMS project '{sourceProjectDetailsId}' was not found in the local mirror. Run Sync GGMS first.");

                if (budgetCap.HasValue && budgetCap.Value > linkedGgmsProject.TotalBudget)
                    return new(false, $"Budget cap cannot exceed the GGMS project budget of {linkedGgmsProject.TotalBudget:N2}.");
                budgetCap ??= linkedGgmsProject.TotalBudget;
            }

            // Create per-project CFW budget
            var cfwBudget = new CashForWorkBudget
            {
                BudgetCode = budgetCode,
                BudgetName = budgetName,
                Description = NormalizeNullable(request.Description),
                BudgetCap = budgetCap,
                DailyRate = request.DailyRate,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                SourceDonationId = request.SourceDonationId,
                SourceGGMSBudgetId = request.SourceGGMSBudgetId,
                SourceProjectDetailsId = sourceProjectDetailsId,
                IsActive = true,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (remoteGeneratedId.HasValue)
            {
                cfwBudget.Id = remoteGeneratedId.Value;
            }

            _context.CashForWorkBudgets.Add(cfwBudget);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                createdByUserId,
                "CashForWorkProjectCreated",
                nameof(CashForWorkBudget),
                cfwBudget.Id,
                $"Created cash-for-work project '{budgetName}' with budget cap {budgetCap:N2}");

            return new(true, "Project created successfully", cfwBudget.Id, cfwBudget.BudgetName);
        }

        public async Task<IReadOnlyList<PrivateDonation>> GetPrivateDonationsAsync(int take = 50)
        {
            return await _context.PrivateDonations
                .AsNoTracking()
                .OrderByDescending(donation => donation.DateReceived)
                .ThenByDescending(donation => donation.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<BudgetLedgerEntry>> GetRecentLedgerEntriesAsync(int skip = 0, int take = 50, string? search = null, string? sourceFilter = null)
        {
            IQueryable<BudgetLedgerEntry> query = _context.BudgetLedgerEntries
                .AsNoTracking()
                .Include(e => e.Program)
                .Include(e => e.AssistanceCaseBudget)
                .Include(e => e.CashForWorkBudget);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.Remarks != null && EF.Functions.Like(e.Remarks, $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(sourceFilter) && sourceFilter != "All Sources")
            {
                query = query.Where(e => e.FeatureSource.ToString() == sourceFilter);
            }

            return await query
                .OrderByDescending(entry => entry.EntryDate)
                .ThenByDescending(entry => entry.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetLedgerCountAsync(string? search = null, string? sourceFilter = null)
        {
            var query = _context.BudgetLedgerEntries.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.Remarks != null && EF.Functions.Like(e.Remarks, $"%{search}%"));
            }

            if (!string.IsNullOrWhiteSpace(sourceFilter) && sourceFilter != "All Sources")
            {
                query = query.Where(e => e.FeatureSource.ToString() == sourceFilter);
            }

            return await query.CountAsync();
        }

        public async Task<AyudaProgramOperationResult> CreateProgramAsync(AyudaProgramRequest request, int createdByUserId)
        {
            int? remoteGeneratedId = null;
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            var remoteService = new BudgetManagementService(remoteContext, _auditService);
                            return await remoteService.CreateProgramAsync(request, createdByUserId);
                        });

                    if (!remoteResult.IsSuccess) return remoteResult;
                    remoteGeneratedId = remoteResult.ProgramId;
                }
                catch (Exception ex)
                {
                    return new AyudaProgramOperationResult(false, $"Remote creation failed. {ex.Message}");
                }
            }

            var programCode = NormalizeRequired(request.ProgramCode);
            var programName = NormalizeRequired(request.ProgramName);
            var assistanceType = NormalizeNullable(request.AssistanceType);
            var itemDescription = NormalizeNullable(request.ItemDescription);
            var existingProgram = await _context.AyudaPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(program => program.ProgramCode == programCode);

            if (existingProgram != null)
            {
                return new AyudaProgramOperationResult(false, $"Program code '{programCode}' already exists.");
            }

            if (request.UnitAmount is < 0)
            {
                return new AyudaProgramOperationResult(false, "Unit amount cannot be negative.");
            }

            if (request.BudgetCap is < 0)
            {
                return new AyudaProgramOperationResult(false, "Project budget cap cannot be negative.");
            }

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value.Date < request.StartDate.Value.Date)
            {
                return new AyudaProgramOperationResult(false, "Project end date cannot be earlier than the start date.");
            }

            if (request.ReleaseKind == AssistanceReleaseKind.Cash &&
                request.UnitAmount is not > 0)
            {
                return new AyudaProgramOperationResult(false, "Cash distribution projects require a unit amount greater than zero.");
            }

            if (request.ReleaseKind == AssistanceReleaseKind.Goods &&
                string.IsNullOrWhiteSpace(request.ItemName))
            {
                return new AyudaProgramOperationResult(false, "Goods distribution projects require an item name.");
            }

            if (request.SourceDonationId.HasValue)
            {
                var donation = await _context.PrivateDonations.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == request.SourceDonationId.Value);

                if (donation != null && donation.DonationType == DonationType.Goods)
                {
                    if (request.ReleaseKind != AssistanceReleaseKind.Goods)
                    {
                        return new AyudaProgramOperationResult(false, "Projects linked to a Goods donation must use the Goods release kind.");
                    }
                }
            }

            var sourceProjectDetailsId = NormalizeNullable(request.SourceProjectDetailsId);
            GgmsProjectCache? linkedGgmsProject = null;
            var budgetCap = request.BudgetCap;

            // ggms_project_cache is a local-only SQLite mirror, so this validation is skipped on
            // the remote (MySQL) pass — the caller resolves the cap before building the request.
            if (sourceProjectDetailsId != null && _context.Database.ProviderName != "Pomelo.EntityFrameworkCore.MySql")
            {
                linkedGgmsProject = await _context.GgmsProjectCache
                    .FirstOrDefaultAsync(cache => cache.ProjectDetailsId == sourceProjectDetailsId);

                if (linkedGgmsProject == null)
                {
                    return new AyudaProgramOperationResult(false, $"GGMS project '{sourceProjectDetailsId}' was not found in the local mirror. Run Sync GGMS first.");
                }

                var alreadyLinked = await _context.AyudaPrograms.AsNoTracking()
                    .AnyAsync(p => p.SourceProjectDetailsId == sourceProjectDetailsId && p.IsActive);
                if (alreadyLinked)
                {
                    return new AyudaProgramOperationResult(false, $"GGMS project '{sourceProjectDetailsId}' is already linked to an active project.");
                }

                // The GGMS sub-allocation is the spending envelope: cap defaults to it and can never exceed it.
                if (budgetCap.HasValue && budgetCap.Value > linkedGgmsProject.TotalBudget)
                {
                    return new AyudaProgramOperationResult(false, $"Budget cap cannot exceed the GGMS project budget of {linkedGgmsProject.TotalBudget:N2}.");
                }
                budgetCap ??= linkedGgmsProject.TotalBudget;
            }

            var ayudaProgram = new AyudaProgram
            {
                ProgramCode = programCode,
                ProgramName = programName,
                ProgramType = request.ProgramType,
                Description = NormalizeNullable(request.Description),
                AssistanceType = assistanceType,
                ReleaseKind = request.ReleaseKind,
                UnitAmount = request.ReleaseKind == AssistanceReleaseKind.Goods ? null : request.UnitAmount,
                ItemDescription = itemDescription,
                ItemName = request.ReleaseKind == AssistanceReleaseKind.Goods ? NormalizeRequired(request.ItemName) : NormalizeNullable(request.ItemName),
                QuantityPerBeneficiary = request.QuantityPerBeneficiary,
                UnitOfMeasure = request.ReleaseKind == AssistanceReleaseKind.Goods ? NormalizeRequired(request.UnitOfMeasure) : NormalizeNullable(request.UnitOfMeasure),
                StartDate = request.StartDate?.Date,
                EndDate = request.EndDate?.Date,
                BudgetCap = budgetCap,
                DistributionStatus = request.DistributionStatus,
                SourceDonationId = request.SourceDonationId,
                SourceGGMSBudgetId = request.SourceGGMSBudgetId,
                SourceProjectDetailsId = sourceProjectDetailsId,
                CreatedByUserId = createdByUserId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (remoteGeneratedId.HasValue)
            {
                ayudaProgram.Id = remoteGeneratedId.Value;
            }

            _context.AyudaPrograms.Add(ayudaProgram);

            if (linkedGgmsProject != null)
            {
                linkedGgmsProject.IsLinked = true;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                createdByUserId,
                "AyudaProgramCreated",
                "AyudaProgram",
                ayudaProgram.Id,
                $"Created ayuda program '{ayudaProgram.ProgramCode} - {ayudaProgram.ProgramName}'.");

            var message = "Created ayuda program.";
            if (!ayudaProgram.StartDate.HasValue && !ayudaProgram.EndDate.HasValue)
            {
                message += " Project dates are still blank; dates are optional but should be added later.";
            }

            return new AyudaProgramOperationResult(true, message, ayudaProgram.Id);
        }

        public async Task<GovernmentBudgetSnapshotOperationResult> RecordGovernmentSnapshotAsync(GovernmentBudgetSnapshotRequest request, int recordedByUserId)
        {
            if (request.AllocatedAmount < 0 || request.SpentAmount < 0)
            {
                return new GovernmentBudgetSnapshotOperationResult(false, "Budget amounts cannot be negative.");
            }

            if ((request.TargetProgramId.HasValue ? 1 : 0) +
                (request.TargetAssistanceCaseBudgetId.HasValue ? 1 : 0) +
                (request.TargetCashForWorkBudgetId.HasValue ? 1 : 0) > 1)
            {
                return new GovernmentBudgetSnapshotOperationResult(false, "An allocation can only have a single target.");
            }

            var snapshot = new GovernmentBudgetSnapshot
            {
                OfficeCode = NormalizeRequired(request.OfficeCode),
                OfficeName = NormalizeRequired(request.OfficeName),
                YearlyBudgetId = request.YearlyBudgetId,
                AllocatedAmount = request.AllocatedAmount,
                SpentAmount = request.SpentAmount,
                SourceRowId = NormalizeNullable(request.SourceRowId),
                SyncStatus = request.SyncStatus,
                SyncedAt = request.SyncedAt,
                TargetProgramId = request.TargetProgramId,
                TargetAssistanceCaseBudgetId = request.TargetAssistanceCaseBudgetId,
                TargetCashForWorkBudgetId = request.TargetCashForWorkBudgetId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.GovernmentBudgetSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                recordedByUserId,
                "GovernmentBudgetSnapshotRecorded",
                "GovernmentBudgetSnapshot",
                snapshot.Id,
                $"Recorded government budget snapshot for office '{snapshot.OfficeCode}'.");

            return new GovernmentBudgetSnapshotOperationResult(true, "Recorded government budget snapshot.", snapshot.Id);
        }

        public async Task<PrivateDonationOperationResult> RecordPrivateDonationAsync(PrivateDonationRequest request, int recordedByUserId)
        {
            if (request.DonationType == DonationType.Cash && request.Amount <= 0)
            {
                return new PrivateDonationOperationResult(false, "Cash donation amount must be greater than zero.");
            }

            if (request.DonationType == DonationType.Goods)
            {
                if (string.IsNullOrWhiteSpace(request.ItemName)) return new PrivateDonationOperationResult(false, "Goods donation requires an item name.");
                if (request.Quantity is null or <= 0) return new PrivateDonationOperationResult(false, "Goods donation requires a quantity greater than zero.");
                if (string.IsNullOrWhiteSpace(request.UnitOfMeasure)) return new PrivateDonationOperationResult(false, "Goods donation requires a unit of measure.");
            }

            if ((request.TargetProgramId.HasValue ? 1 : 0) +
                (request.TargetAssistanceCaseBudgetId.HasValue ? 1 : 0) +
                (request.TargetCashForWorkBudgetId.HasValue ? 1 : 0) > 1)
            {
                return new PrivateDonationOperationResult(false, "A donation can only have a single target.");
            }

            var donorName = NormalizeRequired(request.DonorName);
            var donation = new PrivateDonation
            {
                DonorType = request.DonorType,
                DonorName = donorName,
                DonationType = request.DonationType,
                Amount = request.DonationType == DonationType.Cash ? request.Amount : 0,
                ItemName = request.DonationType == DonationType.Goods ? NormalizeRequired(request.ItemName) : null,
                Quantity = request.DonationType == DonationType.Goods ? request.Quantity : null,
                UnitOfMeasure = request.DonationType == DonationType.Goods ? NormalizeRequired(request.UnitOfMeasure) : null,
                DateReceived = request.DateReceived.Date,
                ReferenceNumber = NormalizeNullable(request.ReferenceNumber),
                Remarks = NormalizeNullable(request.Remarks),
                ProofType = request.ProofType,
                ProofReferenceNumber = NormalizeNullable(request.ProofReferenceNumber),
                ProofFilePath = NormalizeNullable(request.ProofFilePath),
                ReceivedByUserId = recordedByUserId,
                TargetProgramId = request.TargetProgramId,
                TargetAssistanceCaseBudgetId = request.TargetAssistanceCaseBudgetId,
                TargetCashForWorkBudgetId = request.TargetCashForWorkBudgetId,
                CreatedAt = DateTime.Now
            };

            _context.PrivateDonations.Add(donation);
            await _context.SaveChangesAsync();

            var ledgerEntry = new BudgetLedgerEntry
            {
                EntryType = request.DonationType == DonationType.Goods ? BudgetLedgerEntryType.GoodsDonation : BudgetLedgerEntryType.Donation,
                FeatureSource = BudgetLedgerFeatureSource.BudgetModule,
                SourceRecordId = $"donation:{donation.Id}",
                ProgramId = null,
                RecipientCount = 0,
                TotalAmount = donation.Amount,
                GovernmentPortion = 0m,
                PrivatePortion = request.DonationType == DonationType.Goods ? 0m : donation.Amount,
                EntryDate = donation.DateReceived.Date,
                Remarks = donation.Remarks ?? $"Private donation from {donorName}.",
                ReleaseKind = request.DonationType == DonationType.Goods ? AssistanceReleaseKind.Goods : null,
                RecordedByUserId = recordedByUserId,
                CreatedAt = DateTime.Now
            };

            _context.BudgetLedgerEntries.Add(ledgerEntry);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                recordedByUserId,
                "PrivateDonationRecorded",
                "PrivateDonation",
                donation.Id,
                $"Recorded private donation of {donation.Amount:N2} from '{donorName}'.");

            return new PrivateDonationOperationResult(true, "Recorded private donation.", donation.Id, ledgerEntry.Id);
        }

        public async Task<BudgetReleaseOperationResult> RecordReleaseAsync(BudgetReleaseRequest request, int recordedByUserId)
        {
            if (request.TotalAmount <= 0)
            {
                return new BudgetReleaseOperationResult(false, "Release amount must be greater than zero.");
            }

            if (request.RecipientCount <= 0)
            {
                return new BudgetReleaseOperationResult(false, "Recipient count must be greater than zero.");
            }

            var sourceRecordId = NormalizeRequired(request.SourceRecordId);
            var existingRelease = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(entry =>
                    entry.EntryType == BudgetLedgerEntryType.Release &&
                    entry.FeatureSource == request.FeatureSource &&
                    entry.SourceRecordId == sourceRecordId);

            if (existingRelease != null)
            {
                return new BudgetReleaseOperationResult(false, "This release already has a budget ledger entry.", existingRelease.Id);
            }

            AyudaProgram? ayudaProgram = null;
            AssistanceCaseBudget? assistanceCaseBudget = null;
            CashForWorkBudget? cashForWorkBudget = null;
            string budgetOwnerLabel;
            decimal? budgetCap;
            int? sourceDonationId = null;
            int? sourceGgmsBudgetId = null;

            if (request.CashForWorkBudgetId.HasValue)
            {
                cashForWorkBudget = await _context.CashForWorkBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == request.CashForWorkBudgetId.Value);

                if (cashForWorkBudget == null || !cashForWorkBudget.IsActive)
                {
                    return new BudgetReleaseOperationResult(false, "Select an active cash-for-work budget before releasing funds.");
                }

                budgetOwnerLabel = cashForWorkBudget.BudgetName;
                budgetCap = cashForWorkBudget.BudgetCap;

                // Read funding source from the CFW budget (Phase 3 enhancement)
                sourceDonationId = cashForWorkBudget.SourceDonationId;
                sourceGgmsBudgetId = cashForWorkBudget.SourceGGMSBudgetId;
            }
            else if (request.AyudaProgramId.HasValue)
            {
                ayudaProgram = await _context.AyudaPrograms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(program => program.Id == request.AyudaProgramId.Value);

                if (ayudaProgram == null || !ayudaProgram.IsActive)
                {
                    return new BudgetReleaseOperationResult(false, "Select an active ayuda program before releasing funds.");
                }

                budgetOwnerLabel = ayudaProgram.ProgramName;
                budgetCap = ayudaProgram.BudgetCap;
                sourceDonationId = ayudaProgram.SourceDonationId;
                sourceGgmsBudgetId = ayudaProgram.SourceGGMSBudgetId;
            }
            else if (request.AssistanceCaseBudgetId.HasValue)
            {
                assistanceCaseBudget = await _context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == request.AssistanceCaseBudgetId.Value);

                if (assistanceCaseBudget == null || !assistanceCaseBudget.IsActive)
                {
                    return new BudgetReleaseOperationResult(false, "Select an active assistance case budget before releasing funds.");
                }

                budgetOwnerLabel = assistanceCaseBudget.BudgetName;
                budgetCap = assistanceCaseBudget.BudgetCap;
            }
            else if (request.FeatureSource == BudgetLedgerFeatureSource.ManualAssistance)
            {
                // Manual Assistance pulls directly from the unified waterfall.
                // No specific budget bucket required — fall through to waterfall logic below.
                budgetOwnerLabel = "Manual Assistance";
                budgetCap = null;
            }
            else
            {
                return new BudgetReleaseOperationResult(false, "A release must be assigned to a distribution program, cash-for-work budget, or assistance case budget.");
            }

            decimal governmentPortion = 0m;
            decimal privatePortion = 0m;

            if (sourceDonationId.HasValue)
            {
                var donation = await _context.PrivateDonations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == sourceDonationId.Value);
                if (donation == null) return new BudgetReleaseOperationResult(false, "Source donation not found.");

                var sharingProgramIds = await _context.AyudaPrograms.AsNoTracking()
                    .Where(p => p.SourceDonationId == sourceDonationId.Value)
                    .Select(p => p.Id)
                    .ToListAsync();

                // CFW budgets funded by the same donation also draw from this envelope;
                // their ledger entries carry CashForWorkBudgetId with a null ProgramId.
                var sharingCfwBudgetIds = await _context.CashForWorkBudgets.AsNoTracking()
                    .Where(b => b.SourceDonationId == sourceDonationId.Value)
                    .Select(b => b.Id)
                    .ToListAsync();

                var spent = await _context.BudgetLedgerEntries.AsNoTracking()
                    .Where(e => e.EntryType == BudgetLedgerEntryType.Release &&
                        ((e.ProgramId.HasValue && sharingProgramIds.Contains(e.ProgramId.Value)) ||
                         (e.CashForWorkBudgetId.HasValue && sharingCfwBudgetIds.Contains(e.CashForWorkBudgetId.Value))))
                    .SumAsync(e => (decimal?)e.TotalAmount) ?? 0m;

                var remaining = donation.Amount - spent;
                if (request.TotalAmount > remaining)
                {
                    return new BudgetReleaseOperationResult(false, $"Release cannot continue. Source donation budget short by {request.TotalAmount - remaining:N2}.");
                }

                privatePortion = request.TotalAmount;
            }
            else if (sourceGgmsBudgetId.HasValue)
            {
                var ggms = await _context.GovernmentBudgetSnapshots.AsNoTracking().FirstOrDefaultAsync(g => g.Id == sourceGgmsBudgetId.Value);
                if (ggms == null) return new BudgetReleaseOperationResult(false, "Source GGMS budget not found.");

                var sharingProgramIds = await _context.AyudaPrograms.AsNoTracking()
                    .Where(p => p.SourceGGMSBudgetId == sourceGgmsBudgetId.Value)
                    .Select(p => p.Id)
                    .ToListAsync();

                var sharingCfwBudgetIds = await _context.CashForWorkBudgets.AsNoTracking()
                    .Where(b => b.SourceGGMSBudgetId == sourceGgmsBudgetId.Value)
                    .Select(b => b.Id)
                    .ToListAsync();

                var spent = await _context.BudgetLedgerEntries.AsNoTracking()
                    .Where(e => e.EntryType == BudgetLedgerEntryType.Release &&
                        ((e.ProgramId.HasValue && sharingProgramIds.Contains(e.ProgramId.Value)) ||
                         (e.CashForWorkBudgetId.HasValue && sharingCfwBudgetIds.Contains(e.CashForWorkBudgetId.Value))))
                    .SumAsync(e => (decimal?)e.TotalAmount) ?? 0m;

                var remaining = ggms.AllocatedAmount - spent;
                if (request.TotalAmount > remaining)
                {
                    return new BudgetReleaseOperationResult(false, $"Release cannot continue. Source GGMS budget short by {request.TotalAmount - remaining:N2}.");
                }

                governmentPortion = request.TotalAmount;
            }
            else if (ayudaProgram?.SourceProjectDetailsId != null || cashForWorkBudget?.SourceProjectDetailsId != null)
            {
                // GGMS-linked projects spend their own earmarked envelope: BudgetCap equals the
                // GGMS project budget and GetOverviewAsync already subtracts that envelope from
                // the government pool, so the general-pool check below must not apply — the
                // budget-cap check after this block enforces the envelope instead.
                governmentPortion = request.TotalAmount;
            }
            else
            {
                var overview = await GetOverviewAsync();
                if (request.ForcedBudgetBucketType == "Government")
                {
                    if (request.TotalAmount > overview.GovernmentAvailable)
                    {
                        return new BudgetReleaseOperationResult(false, $"Release cannot continue. Government budget short by {request.TotalAmount - overview.GovernmentAvailable:N2}.");
                    }
                    governmentPortion = request.TotalAmount;
                }
                else if (request.ForcedBudgetBucketType == "Private")
                {
                    if (request.TotalAmount > overview.PrivateAvailable)
                    {
                        return new BudgetReleaseOperationResult(false, $"Release cannot continue. Private donation budget short by {request.TotalAmount - overview.PrivateAvailable:N2}.");
                    }
                    privatePortion = request.TotalAmount;
                }
                else
                {
                    if (request.TotalAmount > overview.CombinedAvailable)
                    {
                        var shortfall = request.TotalAmount - overview.CombinedAvailable;
                        return new BudgetReleaseOperationResult(false, $"Release cannot continue. Budget short by {shortfall:N2}.");
                    }
                    governmentPortion = Math.Min(request.TotalAmount, overview.GovernmentAvailable);
                    privatePortion = request.TotalAmount - governmentPortion;
                }
            }

            if (budgetCap.HasValue)
            {
                var existingBudgetSpend = await _context.BudgetLedgerEntries
                    .AsNoTracking()
                    .Where(entry =>
                        entry.EntryType == BudgetLedgerEntryType.Release &&
                        entry.ProgramId == request.AyudaProgramId &&
                        entry.AssistanceCaseBudgetId == request.AssistanceCaseBudgetId &&
                        entry.CashForWorkBudgetId == request.CashForWorkBudgetId)
                    .SumAsync(entry => (decimal?)entry.TotalAmount) ?? 0m;

                if (existingBudgetSpend + request.TotalAmount > budgetCap.Value)
                {
                    return new BudgetReleaseOperationResult(false, "Budget cap would be exceeded by this release.");
                }
            }

            var ledgerEntry = new BudgetLedgerEntry
            {
                EntryType = BudgetLedgerEntryType.Release,
                FeatureSource = request.FeatureSource,
                SourceRecordId = sourceRecordId,
                ProgramId = ayudaProgram?.Id,
                AssistanceCaseBudgetId = assistanceCaseBudget?.Id,
                CashForWorkBudgetId = cashForWorkBudget?.Id,
                RecipientCount = request.RecipientCount,
                TotalAmount = request.TotalAmount,
                GovernmentPortion = governmentPortion,
                PrivatePortion = privatePortion,
                EntryDate = request.ReleasedAt,
                Remarks = NormalizeNullable(request.Remarks) ?? $"Released for {budgetOwnerLabel}.",
                ReleaseKind = request.ReleaseKind,
                RecordedByUserId = recordedByUserId,
                CreatedAt = DateTime.Now
            };

            _context.BudgetLedgerEntries.Add(ledgerEntry);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                recordedByUserId,
                "BudgetReleaseRecorded",
                "BudgetLedgerEntry",
                ledgerEntry.Id,
                $"Recorded release of {ledgerEntry.TotalAmount:N2} for '{budgetOwnerLabel}' from {request.FeatureSource}.");

            return new BudgetReleaseOperationResult(true, "Recorded release budget entry.", ledgerEntry.Id);
        }

        public async Task<BudgetReleaseOperationResult> RecordWaterfallReleaseAsync(BudgetReleaseRequest request, int recordedByUserId)
        {
            if (!request.AyudaProgramId.HasValue)
            {
                return new BudgetReleaseOperationResult(false, "Waterfall release requires an Ayuda Program ID.");
            }

            // Under the new funding-first architecture, projects tied to explicit sources do not use waterfalls.
            // All project releases are routed to the central RecordReleaseAsync which handles 1:1 fund derivation.
            return await RecordReleaseAsync(request, recordedByUserId);
        }

        public async Task<BudgetOverviewSnapshot> GetOverviewAsync()
        {
            var latestSnapshot = await _context.GovernmentBudgetSnapshots
                .AsNoTracking()
                .OrderByDescending(snapshot => snapshot.SyncedAt)
                .ThenByDescending(snapshot => snapshot.Id)
                .FirstOrDefaultAsync();

            var donationTotal = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.EntryType == BudgetLedgerEntryType.Donation)
                .SumAsync(entry => (decimal?)entry.PrivatePortion) ?? 0m;

            var releaseEntries = _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release);

            var governmentReleasedTotal = await releaseEntries
                .SumAsync(entry => (decimal?)entry.GovernmentPortion) ?? 0m;

            var privateReleasedTotal = await releaseEntries
                .SumAsync(entry => (decimal?)entry.PrivatePortion) ?? 0m;

            var now = DateTime.Today;
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var weeklySpent = await releaseEntries
                .Where(entry => entry.EntryDate >= startOfWeek)
                .SumAsync(entry => (decimal?)entry.TotalAmount) ?? 0m;

            var monthlySpent = await releaseEntries
                .Where(entry => entry.EntryDate >= startOfMonth)
                .SumAsync(entry => (decimal?)entry.TotalAmount) ?? 0m;

            var governmentAllocated = latestSnapshot?.AllocatedAmount ?? 0m;
            var governmentSpentReference = latestSnapshot?.SpentAmount ?? 0m;
            var governmentBaseAvailable = Math.Max(0m, governmentAllocated - governmentSpentReference);
            var governmentReleasedSinceLastSync = 0m;

            // GGMS project sub-allocations are carved out of the office allocation on the GGMS
            // side, so active mirrored envelopes are earmarks against the unearmarked pool.
            // ggms_project_cache is a local-only SQLite mirror — skipped on a remote MySQL pass.
            var governmentProjectEarmarkTotal = 0m;
            var projectLinkedProgramIds = new List<int>();
            if (_context.Database.ProviderName != "Pomelo.EntityFrameworkCore.MySql")
            {
                governmentProjectEarmarkTotal = await _context.GgmsProjectCache
                    .AsNoTracking()
                    .Where(cache => cache.Status.ToLower() != "archived")
                    .SumAsync(cache => (decimal?)cache.TotalBudget) ?? 0m;

                // Releases under a program linked to a GGMS project consume that project's envelope
                // (already counted via the earmark), so only unattributed releases drain the pool.
                projectLinkedProgramIds = await _context.AyudaPrograms
                    .AsNoTracking()
                    .Where(program => program.SourceProjectDetailsId != null)
                    .Select(program => program.Id)
                    .ToListAsync();
            }

            if (latestSnapshot != null)
            {
                governmentReleasedSinceLastSync = await _context.BudgetLedgerEntries
                    .AsNoTracking()
                    .Where(entry =>
                        entry.EntryType == BudgetLedgerEntryType.Release &&
                        entry.CreatedAt >= latestSnapshot.SyncedAt &&
                        (entry.ProgramId == null || !projectLinkedProgramIds.Contains(entry.ProgramId.Value)))
                    .SumAsync(entry => (decimal?)entry.GovernmentPortion) ?? 0m;
            }

            var governmentAvailable = Math.Max(0m, governmentBaseAvailable - governmentProjectEarmarkTotal - governmentReleasedSinceLastSync);
            var privateAvailable = Math.Max(0m, donationTotal - privateReleasedTotal);

            var privateLockedAllocated = await _context.PrivateDonations
                .AsNoTracking()
                .Where(d => d.TargetProgramId != null || d.TargetAssistanceCaseBudgetId != null || d.TargetCashForWorkBudgetId != null)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            var privateLockedReleased = await releaseEntries
                .Where(e => e.ProgramId != null || e.AssistanceCaseBudgetId != null || e.CashForWorkBudgetId != null)
                .SumAsync(e => (decimal?)e.PrivatePortion) ?? 0m;
                
            var privateLockedReallocated = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(e => e.EntryType == BudgetLedgerEntryType.Reallocation)
                .SumAsync(e => (decimal?)e.PrivatePortion) ?? 0m;

            var privateLockedAvailable = Math.Max(0m, privateLockedAllocated - privateLockedReleased - privateLockedReallocated);
            var privateUnrestrictedAvailable = Math.Max(0m, privateAvailable - privateLockedAvailable);

            var governmentLockedAllocated = await _context.GovernmentBudgetSnapshots
                .AsNoTracking()
                .Where(s => s.TargetProgramId != null || s.TargetAssistanceCaseBudgetId != null || s.TargetCashForWorkBudgetId != null)
                .SumAsync(s => (decimal?)s.AllocatedAmount) ?? 0m;

            var governmentLockedReleased = await releaseEntries
                .Where(e => e.ProgramId != null || e.AssistanceCaseBudgetId != null || e.CashForWorkBudgetId != null)
                .SumAsync(e => (decimal?)e.GovernmentPortion) ?? 0m;
                
            var governmentLockedReallocated = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(e => e.EntryType == BudgetLedgerEntryType.Reallocation)
                .SumAsync(e => (decimal?)e.GovernmentPortion) ?? 0m;

            var governmentLockedAvailable = Math.Max(0m, governmentLockedAllocated - governmentLockedReleased - governmentLockedReallocated);
            var governmentUnrestrictedAvailable = Math.Max(0m, governmentAvailable - governmentLockedAvailable);

            return new BudgetOverviewSnapshot(
                governmentAllocated,
                governmentSpentReference,
                governmentAvailable,
                governmentUnrestrictedAvailable,
                governmentLockedAvailable,
                privateAvailable,
                privateUnrestrictedAvailable,
                privateLockedAvailable,
                governmentAvailable + privateAvailable,
                governmentReleasedTotal + privateReleasedTotal,
                governmentReleasedTotal,
                privateReleasedTotal,
                weeklySpent,
                monthlySpent,
                latestSnapshot?.SyncedAt,
                latestSnapshot?.OfficeCode,
                latestSnapshot?.OfficeName,
                governmentProjectEarmarkTotal);
        }

        public async Task<ReallocationOperationResult> ReallocateEarmarkAsync(int targetId, string targetType, string remarks, int recordedByUserId)
        {
            var isProgram = targetType == "AyudaProgram";
            var isCase = targetType == "AssistanceCaseBudget";
            var isCfw = targetType == "CashForWorkBudget";

            var privateAllocated = await _context.PrivateDonations
                .AsNoTracking()
                .Where(d => (isProgram && d.TargetProgramId == targetId) ||
                            (isCase && d.TargetAssistanceCaseBudgetId == targetId) ||
                            (isCfw && d.TargetCashForWorkBudgetId == targetId))
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            var govAllocated = await _context.GovernmentBudgetSnapshots
                .AsNoTracking()
                .Where(s => (isProgram && s.TargetProgramId == targetId) ||
                            (isCase && s.TargetAssistanceCaseBudgetId == targetId) ||
                            (isCfw && s.TargetCashForWorkBudgetId == targetId))
                .SumAsync(s => (decimal?)s.AllocatedAmount) ?? 0m;

            var privateReleased = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(e => e.EntryType == BudgetLedgerEntryType.Release &&
                            ((isProgram && e.ProgramId == targetId) ||
                             (isCase && e.AssistanceCaseBudgetId == targetId) ||
                             (isCfw && e.CashForWorkBudgetId == targetId)))
                .SumAsync(e => (decimal?)e.PrivatePortion) ?? 0m;

            var govReleased = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(e => e.EntryType == BudgetLedgerEntryType.Release &&
                            ((isProgram && e.ProgramId == targetId) ||
                             (isCase && e.AssistanceCaseBudgetId == targetId) ||
                             (isCfw && e.CashForWorkBudgetId == targetId)))
                .SumAsync(e => (decimal?)e.GovernmentPortion) ?? 0m;

            var privateReallocated = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(e => e.EntryType == BudgetLedgerEntryType.Reallocation &&
                            ((isProgram && e.ProgramId == targetId) ||
                             (isCase && e.AssistanceCaseBudgetId == targetId) ||
                             (isCfw && e.CashForWorkBudgetId == targetId)))
                .SumAsync(e => (decimal?)e.PrivatePortion) ?? 0m;

            var govReallocated = await _context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(e => e.EntryType == BudgetLedgerEntryType.Reallocation &&
                            ((isProgram && e.ProgramId == targetId) ||
                             (isCase && e.AssistanceCaseBudgetId == targetId) ||
                             (isCfw && e.CashForWorkBudgetId == targetId)))
                .SumAsync(e => (decimal?)e.GovernmentPortion) ?? 0m;

            var privateRemaining = Math.Max(0m, privateAllocated - privateReleased - privateReallocated);
            var govRemaining = Math.Max(0m, govAllocated - govReleased - govReallocated);
            var totalRemaining = privateRemaining + govRemaining;

            if (totalRemaining <= 0)
            {
                return new ReallocationOperationResult(false, "No remaining earmarked funds to reallocate.");
            }

            var ledgerEntry = new BudgetLedgerEntry
            {
                EntryType = BudgetLedgerEntryType.Reallocation,
                FeatureSource = BudgetLedgerFeatureSource.BudgetModule,
                SourceRecordId = $"reallocation:{targetType}:{targetId}",
                ProgramId = isProgram ? targetId : null,
                AssistanceCaseBudgetId = isCase ? targetId : null,
                CashForWorkBudgetId = isCfw ? targetId : null,
                RecipientCount = 0,
                TotalAmount = totalRemaining,
                GovernmentPortion = govRemaining,
                PrivatePortion = privateRemaining,
                EntryDate = DateTime.Today,
                Remarks = NormalizeNullable(remarks) ?? "Reallocated dormant/unused earmarked funds to general pool.",
                RecordedByUserId = recordedByUserId,
                CreatedAt = DateTime.Now
            };

            _context.BudgetLedgerEntries.Add(ledgerEntry);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                recordedByUserId,
                "BudgetReallocated",
                "BudgetLedgerEntry",
                ledgerEntry.Id,
                $"Reallocated {totalRemaining:N2} from {targetType} {targetId} back to general funds.");

            return new ReallocationOperationResult(true, "Funds successfully reallocated.", ledgerEntry.Id);
        }

        private static string NormalizeRequired(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException("A required budget value is missing.")
                : value.Trim();
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

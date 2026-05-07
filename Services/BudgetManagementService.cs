using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record AyudaProgramRequest(
        string ProgramCode,
        string ProgramName,
        AyudaProgramType ProgramType,
        string? Description,
        string? AssistanceType,
        AssistanceReleaseKind ReleaseKind,
        decimal? UnitAmount,
        string? ItemDescription,
        DateTime? StartDate,
        DateTime? EndDate,
        decimal? BudgetCap,
        AyudaProgramDistributionStatus DistributionStatus);

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

    public sealed record GovernmentBudgetSnapshotRequest(
        string OfficeCode,
        string OfficeName,
        int YearlyBudgetId,
        decimal AllocatedAmount,
        decimal SpentAmount,
        string? SourceRowId,
        GovernmentBudgetSyncStatus SyncStatus,
        DateTime SyncedAt);

    public sealed record GovernmentBudgetSnapshotOperationResult(bool IsSuccess, string Message, int? SnapshotId = null);

    public sealed record PrivateDonationRequest(
        PrivateDonationDonorType DonorType,
        string DonorName,
        decimal Amount,
        DateTime DateReceived,
        string? ReferenceNumber,
        string? Remarks,
        DonationProofType ProofType,
        string? ProofReferenceNumber,
        string? ProofFilePath);

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
        int? CashForWorkBudgetId = null);

    public sealed record BudgetReleaseOperationResult(bool IsSuccess, string Message, int? LedgerEntryId = null);

    public sealed record BudgetOverviewSnapshot(
        decimal GovernmentAllocated,
        decimal GovernmentSpentReference,
        decimal GovernmentAvailable,
        decimal PrivateAvailable,
        decimal CombinedAvailable,
        decimal ReleasedTotal,
        decimal GovernmentReleasedTotal,
        decimal PrivateReleasedTotal,
        DateTime? LastGovernmentSyncAt,
        string? OfficeCode,
        string? OfficeName);

    public sealed class BudgetManagementService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public BudgetManagementService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
        }

        public async Task<IReadOnlyList<AyudaProgram>> GetProgramsAsync()
        {
            return await _context.AyudaPrograms
                .AsNoTracking()
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

        public async Task<IReadOnlyList<PrivateDonation>> GetPrivateDonationsAsync(int take = 50)
        {
            return await _context.PrivateDonations
                .AsNoTracking()
                .OrderByDescending(donation => donation.DateReceived)
                .ThenByDescending(donation => donation.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<BudgetLedgerEntry>> GetRecentLedgerEntriesAsync(int take = 100)
        {
            return await _context.BudgetLedgerEntries
                .AsNoTracking()
                .OrderByDescending(entry => entry.EntryDate)
                .ThenByDescending(entry => entry.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<AyudaProgramOperationResult> CreateProgramAsync(AyudaProgramRequest request, int createdByUserId)
        {
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
                string.IsNullOrWhiteSpace(itemDescription))
            {
                return new AyudaProgramOperationResult(false, "Goods distribution projects require an item description or package detail.");
            }

            var ayudaProgram = new AyudaProgram
            {
                ProgramCode = programCode,
                ProgramName = programName,
                ProgramType = request.ProgramType,
                Description = NormalizeNullable(request.Description),
                AssistanceType = assistanceType,
                ReleaseKind = request.ReleaseKind,
                UnitAmount = request.UnitAmount,
                ItemDescription = itemDescription,
                StartDate = request.StartDate?.Date,
                EndDate = request.EndDate?.Date,
                BudgetCap = request.BudgetCap,
                DistributionStatus = request.DistributionStatus,
                CreatedByUserId = createdByUserId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.AyudaPrograms.Add(ayudaProgram);
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
            if (request.Amount <= 0)
            {
                return new PrivateDonationOperationResult(false, "Donation amount must be greater than zero.");
            }

            var donorName = NormalizeRequired(request.DonorName);
            var donation = new PrivateDonation
            {
                DonorType = request.DonorType,
                DonorName = donorName,
                Amount = request.Amount,
                DateReceived = request.DateReceived.Date,
                ReferenceNumber = NormalizeNullable(request.ReferenceNumber),
                Remarks = NormalizeNullable(request.Remarks),
                ProofType = request.ProofType,
                ProofReferenceNumber = NormalizeNullable(request.ProofReferenceNumber),
                ProofFilePath = NormalizeNullable(request.ProofFilePath),
                ReceivedByUserId = recordedByUserId,
                CreatedAt = DateTime.Now
            };

            _context.PrivateDonations.Add(donation);
            await _context.SaveChangesAsync();

            var ledgerEntry = new BudgetLedgerEntry
            {
                EntryType = BudgetLedgerEntryType.Donation,
                FeatureSource = BudgetLedgerFeatureSource.BudgetModule,
                SourceRecordId = $"donation:{donation.Id}",
                ProgramId = null,
                RecipientCount = 0,
                TotalAmount = donation.Amount,
                GovernmentPortion = 0m,
                PrivatePortion = donation.Amount,
                EntryDate = donation.DateReceived.Date,
                Remarks = donation.Remarks ?? $"Private donation from {donorName}.",
                ReleaseKind = null,
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
            else
            {
                return new BudgetReleaseOperationResult(false, "A release must be assigned to a distribution program, cash-for-work budget, or assistance case budget.");
            }

            var overview = await GetOverviewAsync();
            if (request.TotalAmount > overview.CombinedAvailable)
            {
                var shortfall = request.TotalAmount - overview.CombinedAvailable;
                return new BudgetReleaseOperationResult(false, $"Release cannot continue. Budget short by {shortfall:N2}.");
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

            var governmentPortion = Math.Min(request.TotalAmount, overview.GovernmentAvailable);
            var privatePortion = request.TotalAmount - governmentPortion;
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

            var governmentAllocated = latestSnapshot?.AllocatedAmount ?? 0m;
            var governmentSpentReference = latestSnapshot?.SpentAmount ?? 0m;
            var governmentBaseAvailable = Math.Max(0m, governmentAllocated - governmentSpentReference);
            var governmentReleasedSinceLastSync = 0m;

            if (latestSnapshot != null)
            {
                governmentReleasedSinceLastSync = await _context.BudgetLedgerEntries
                    .AsNoTracking()
                    .Where(entry =>
                        entry.EntryType == BudgetLedgerEntryType.Release &&
                        entry.CreatedAt >= latestSnapshot.SyncedAt)
                    .SumAsync(entry => (decimal?)entry.GovernmentPortion) ?? 0m;
            }

            var governmentAvailable = Math.Max(0m, governmentBaseAvailable - governmentReleasedSinceLastSync);
            var privateAvailable = Math.Max(0m, donationTotal - privateReleasedTotal);

            return new BudgetOverviewSnapshot(
                governmentAllocated,
                governmentSpentReference,
                governmentAvailable,
                privateAvailable,
                governmentAvailable + privateAvailable,
                governmentReleasedTotal + privateReleasedTotal,
                governmentReleasedTotal,
                privateReleasedTotal,
                latestSnapshot?.SyncedAt,
                latestSnapshot?.OfficeCode,
                latestSnapshot?.OfficeName);
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

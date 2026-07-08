using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public sealed class BeneficiaryHistoryService
    {
        private readonly LocalDbContext _context;

        public BeneficiaryHistoryService(LocalDbContext context)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<BeneficiarySearchResultDto> Items, int TotalCount)> SearchBeneficiariesAsync(string query, int page, int pageSize)
        {
            var dbQuery = _context.BeneficiaryStaging.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim().ToLower();
                dbQuery = dbQuery.Where(b => 
                    (b.BeneficiaryId != null && b.BeneficiaryId.ToLower().Contains(q)) ||
                    (b.CivilRegistryId != null && b.CivilRegistryId.ToLower().Contains(q)) ||
                    (b.LastName != null && b.LastName.ToLower().Contains(q)) ||
                    (b.FirstName != null && b.FirstName.ToLower().Contains(q)) ||
                    (b.FullName != null && b.FullName.ToLower().Contains(q)));
            }

            var totalCount = await dbQuery.CountAsync();

            var pagedEntities = await dbQuery
                .OrderBy(b => b.LastName)
                .ThenBy(b => b.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var beneficiaries = pagedEntities
                .Select(b => new BeneficiarySearchResultDto
                {
                    StagingId = b.StagingID,
                    BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = b.CivilRegistryId ?? string.Empty,
                    FullName = b.FullName ?? string.Empty,
                    FirstName = b.FirstName ?? string.Empty,
                    LastName = b.LastName ?? string.Empty,
                    Address = b.Address ?? string.Empty,
                    PhotoPath = b.PhotoPath ?? string.Empty,
                    SexAgeSummary = (string.IsNullOrWhiteSpace(b.Sex) ? "" : b.Sex + " / ") + (string.IsNullOrWhiteSpace(b.Age) ? "" : b.Age),
                    FlagsSummary = (b.IsSenior ? "Senior" : "") + (b.IsSenior && b.IsPwd ? " | PWD" : (b.IsPwd ? "PWD" : (!b.IsSenior ? "Standard" : "")))
                })
                .ToList();

            // Handle client-side properties (DisplayName, Initials)
            foreach(var dto in beneficiaries)
            {
                dto.DisplayName = !string.IsNullOrWhiteSpace(dto.FullName) 
                    ? dto.FullName 
                    : $"{dto.FirstName} {dto.LastName}".Trim();

                dto.Initials = "";
                if (!string.IsNullOrWhiteSpace(dto.FirstName)) dto.Initials += dto.FirstName[..1].ToUpper();
                if (!string.IsNullOrWhiteSpace(dto.LastName)) dto.Initials += dto.LastName[..1].ToUpper();
            }

            return (beneficiaries, totalCount);
        }

        public async Task<BeneficiarySummaryDto> GetBeneficiarySummaryAsync(string civilRegistryId, string beneficiaryId)
        {
            if (string.IsNullOrWhiteSpace(civilRegistryId) && string.IsNullOrWhiteSpace(beneficiaryId))
                return new BeneficiarySummaryDto();

            // We only look at supported beneficiary assistance history source modules.
            var ledgerEntries = await _context.BeneficiaryAssistanceLedgerEntries
                .AsNoTracking()
                .Where(e =>
                    ((!string.IsNullOrEmpty(civilRegistryId) && e.CivilRegistryId == civilRegistryId) ||
                     (!string.IsNullOrEmpty(beneficiaryId) && e.BeneficiaryId == beneficiaryId)) &&
                    (e.SourceModule == BeneficiaryAssistanceSourceModule.AssistanceCase ||
                     e.SourceModule == BeneficiaryAssistanceSourceModule.ManualHistory ||
                     e.SourceModule == BeneficiaryAssistanceSourceModule.CashForWork ||
                     e.SourceModule == BeneficiaryAssistanceSourceModule.ProjectDistribution))
                .ToListAsync();

            // We need to resolve Cash vs Goods. For accuracy, we can do a quick check per entry if needed,
            // or assume we can approximate. Actually, let's join with source models to be precise about cash vs goods,
            // or we could have stored it in the ledger. Since the user said don't duplicate, we will query accurately.

            // Since we need to calculate totals from enriched history metadata, use the same enrichment path.
            var history = await EnrichLedgerEntriesAsync(ledgerEntries);

            var dto = new BeneficiarySummaryDto
            {
                BeneficiaryId = beneficiaryId,
                CivilRegistryId = ledgerEntries.FirstOrDefault()?.CivilRegistryId ?? string.Empty,
                TotalAssistanceRecords = history.Count,
                TotalCashReleased = history.Where(h => h.ReleaseKind == "Cash").Sum(h => h.Amount),
                TotalGoodsReleased = history.Where(h => h.ReleaseKind == "Goods").Sum(h => h.Amount),
                AssistanceThisMonth = history.Where(h => h.ReleaseDate.Year == DateTime.Now.Year && h.ReleaseDate.Month == DateTime.Now.Month).Sum(h => h.Amount),
                LatestAssistance = history.OrderByDescending(h => h.ReleaseDate).FirstOrDefault()?.ReleaseDate
            };

            return dto;
        }

        public async Task<(IReadOnlyList<BeneficiaryHistoryDto> Items, int TotalCount)> GetBeneficiaryHistoryAsync(string civilRegistryId, string beneficiaryId, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(civilRegistryId) && string.IsNullOrWhiteSpace(beneficiaryId))
            {
                return (Array.Empty<BeneficiaryHistoryDto>(), 0);
            }

            var ledgerQuery = _context.BeneficiaryAssistanceLedgerEntries
                .AsNoTracking()
                .Where(e =>
                    ((!string.IsNullOrEmpty(civilRegistryId) && e.CivilRegistryId == civilRegistryId) ||
                     (!string.IsNullOrEmpty(beneficiaryId) && e.BeneficiaryId == beneficiaryId)) &&
                    (e.SourceModule == BeneficiaryAssistanceSourceModule.AssistanceCase ||
                     e.SourceModule == BeneficiaryAssistanceSourceModule.ManualHistory ||
                     e.SourceModule == BeneficiaryAssistanceSourceModule.CashForWork ||
                     e.SourceModule == BeneficiaryAssistanceSourceModule.ProjectDistribution));

            var totalCount = await ledgerQuery.CountAsync();

            if (totalCount == 0)
            {
                return (Array.Empty<BeneficiaryHistoryDto>(), 0);
            }

            var entries = await ledgerQuery
                .OrderByDescending(e => e.ReleaseDate)
                .ThenByDescending(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedHistory = await EnrichLedgerEntriesAsync(entries);
            return (pagedHistory, totalCount);
        }

        private async Task<List<BeneficiaryHistoryDto>> EnrichLedgerEntriesAsync(IReadOnlyList<BeneficiaryAssistanceLedgerEntry> ledgerEntries)
        {
            if (ledgerEntries == null || ledgerEntries.Count == 0)
            {
                return new List<BeneficiaryHistoryDto>();
            }

            var acIds = ledgerEntries
                .Where(e => e.SourceModule == BeneficiaryAssistanceSourceModule.AssistanceCase || e.SourceModule == BeneficiaryAssistanceSourceModule.ManualHistory)
                .Select(e => ParsePrimaryRecordId(e.SourceRecordId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var cwIds = ledgerEntries
                .Where(e => e.SourceModule == BeneficiaryAssistanceSourceModule.CashForWork)
                .Select(e => ParsePrimaryRecordId(e.SourceRecordId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var pdIds = ledgerEntries
                .Where(e => e.SourceModule == BeneficiaryAssistanceSourceModule.ProjectDistribution)
                .Select(e => ParsePrimaryRecordId(e.SourceRecordId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var assistanceCases = acIds.Any()
                ? await _context.AssistanceCases.AsNoTracking()
                    .Include(a => a.AyudaProgram)
                    .Where(a => acIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id)
                : new Dictionary<int, AssistanceCase>();

            var cfwEvents = cwIds.Any()
                ? await _context.CashForWorkEvents.AsNoTracking()
                    .Include(c => c.AyudaProgram)
                    .Where(c => cwIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id)
                : new Dictionary<int, CashForWorkEvent>();

            var programs = pdIds.Any()
                ? await _context.AyudaPrograms.AsNoTracking()
                    .Where(p => pdIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id)
                : new Dictionary<int, AyudaProgram>();

            var results = new List<BeneficiaryHistoryDto>(ledgerEntries.Count);

            foreach (var entry in ledgerEntries)
            {
                var dto = new BeneficiaryHistoryDto
                {
                    Id = entry.Id,
                    ReleaseDate = entry.ReleaseDate,
                    Amount = entry.Amount,
                    Remarks = entry.Remarks
                };

                var recordId = ParsePrimaryRecordId(entry.SourceRecordId);
                if (!recordId.HasValue)
                {
                    continue;
                }

                var isValid = false;
                switch (entry.SourceModule)
                {
                    case BeneficiaryAssistanceSourceModule.AssistanceCase:
                        if (assistanceCases.TryGetValue(recordId.Value, out var ac))
                        {
                            dto.SourceModule = "Assistance Case";
                            dto.ProgramName = ac.AyudaProgram?.ProgramName ?? ac.AssistanceType;
                            dto.ReferenceNumber = ac.CaseNumber;
                            dto.ReleaseKind = ac.ReleaseKind.ToString();
                            isValid = true;
                        }
                        break;
                    case BeneficiaryAssistanceSourceModule.ManualHistory:
                        if (assistanceCases.TryGetValue(recordId.Value, out var manualCase))
                        {
                            dto.SourceModule = "Manual Assistance";
                            dto.ProgramName = manualCase.AyudaProgram?.ProgramName ?? manualCase.AssistanceType;
                            dto.ReferenceNumber = manualCase.CaseNumber;
                            dto.ReleaseKind = manualCase.ReleaseKind.ToString();
                            isValid = true;
                        }
                        break;
                    case BeneficiaryAssistanceSourceModule.CashForWork:
                        if (cfwEvents.TryGetValue(recordId.Value, out var cfw))
                        {
                            dto.SourceModule = "Cash For Work";
                            dto.ProgramName = cfw.Title;
                            dto.ReferenceNumber = $"CFW-{cfw.Id}";
                            dto.ReleaseKind = cfw.BenefitType == CashForWorkBenefitType.Goods ? "Goods" : "Cash";
                            isValid = true;
                        }
                        break;
                    case BeneficiaryAssistanceSourceModule.ProjectDistribution:
                        if (programs.TryGetValue(recordId.Value, out var prog))
                        {
                            dto.SourceModule = "Project Distribution";
                            dto.ProgramName = prog.ProgramName;
                            dto.ReferenceNumber = $"PD-{prog.Id}";
                            dto.ReleaseKind = prog.ReleaseKind.ToString();
                            isValid = true;
                        }
                        break;
                }

                if (isValid)
                {
                    results.Add(dto);
                }
            }

            return results;
        }

        private static int? ParsePrimaryRecordId(string? sourceRecordId)
        {
            if (string.IsNullOrWhiteSpace(sourceRecordId))
            {
                return null;
            }

            var match = Regex.Match(sourceRecordId, @"\d+");
            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : null;
        }
        
        public async Task<(bool Success, string? ErrorMessage)> RecordManualAssistanceAsync(
            string beneficiaryId, 
            string civilRegistryId, 
            decimal amount, 
            AssistanceReleaseKind releaseKind, 
            string programName,
            string remarks,
            int userId)
        {
            // Transaction: Create AssistanceCase → Deduct Budget → Create Ledger Entry → Commit
            // Rollback everything if any step fails.
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Step 1: Create AssistanceCase record
                var newCase = new AssistanceCase
                {
                    CaseNumber = $"MAC-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(100,999)}",
                    ValidatedBeneficiaryId = beneficiaryId,
                    ValidatedCivilRegistryId = civilRegistryId,
                    AssistanceType = programName,
                    ReleaseKind = releaseKind,
                    Priority = AssistanceCasePriority.Medium,
                    Status = AssistanceCaseStatus.Released,
                    RequestedAmount = amount,
                    ApprovedAmount = amount,
                    RequestedOn = DateTime.Now,
                    ScheduledReleaseDate = DateTime.Now,
                    Summary = remarks,
                    CreatedByUserId = userId
                };

                _context.AssistanceCases.Add(newCase);
                await _context.SaveChangesAsync();

                // Step 2: Deduct from Budget Waterfall (no AssistanceCaseBudgetId — pulls from unified pool)
                var budgetService = new BudgetManagementService(_context);
                var budgetResult = await budgetService.RecordReleaseAsync(
                    new BudgetReleaseRequest(
                        null, // No AyudaProgramId
                        BudgetLedgerFeatureSource.ManualAssistance,
                        $"manual-assistance:{newCase.Id}",
                        1,
                        releaseKind,
                        amount,
                        DateTime.Now,
                        remarks),
                    userId);

                if (!budgetResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return (false, budgetResult.Message);
                }

                newCase.BudgetLedgerEntryId = budgetResult.LedgerEntryId;
                await _context.SaveChangesAsync();

                // Step 3: Create exactly one Beneficiary Assistance Ledger entry
                var ledgerEntry = new BeneficiaryAssistanceLedgerEntry
                {
                    CivilRegistryId = civilRegistryId,
                    BeneficiaryId = beneficiaryId,
                    SourceModule = BeneficiaryAssistanceSourceModule.ManualHistory,
                    SourceRecordId = newCase.Id.ToString(),
                    ReleaseDate = DateTime.Now.Date,
                    Amount = amount,
                    Remarks = remarks,
                    RecordedByUserId = userId,
                    CreatedAt = DateTime.Now
                };

                _context.BeneficiaryAssistanceLedgerEntries.Add(ledgerEntry);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}

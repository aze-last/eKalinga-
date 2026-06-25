using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace AttendanceShiftingManagement.Services
{
    public enum ReportsReportType
    {
        AidRequests,
        ValidatedBeneficiaries,
        BudgetUtilization,
        DistributionClaims,
        AdminActivityAudit
    }

    public sealed class ReportsQueryOptions
    {
        public ReportsReportType ReportType { get; init; } = ReportsReportType.AidRequests;
        public DateTime DateFrom { get; init; } = DateTime.Today.AddDays(-30);
        public DateTime DateTo { get; init; } = DateTime.Today;
        public int? AyudaProgramId { get; init; }
    }

    public sealed class ReportsMetricItem
    {
        public string Label { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
    }

    public sealed class ReportsSnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string ExportFilePrefix { get; init; } = "report";
        public string RangeLabel { get; init; } = string.Empty;
        public string ProgramLabel { get; init; } = "All Programs";
        public string SuggestedOrientation { get; init; } = "Portrait";
        public DataTable Table { get; init; } = new();
        public IReadOnlyList<ReportsMetricItem> Metrics { get; init; } = Array.Empty<ReportsMetricItem>();
        public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
    }

    public sealed class ReportsService
    {
        public async Task<ReportsSnapshot> BuildSnapshotAsync(ReportsQueryOptions options, CancellationToken cancellationToken = default)
        {
            var normalizedOptions = Normalize(options);

            await using var context = new LocalDbContext();
            var programLabel = await ResolveProgramLabelAsync(context, normalizedOptions.ReportType, normalizedOptions.AyudaProgramId, cancellationToken);

            return normalizedOptions.ReportType switch
            {
                ReportsReportType.ValidatedBeneficiaries => await BuildValidatedBeneficiariesSnapshotAsync(context, normalizedOptions, programLabel, cancellationToken),
                ReportsReportType.BudgetUtilization => await BuildBudgetUtilizationSnapshotAsync(context, normalizedOptions, programLabel, cancellationToken),
                ReportsReportType.DistributionClaims => await BuildDistributionClaimsSnapshotAsync(context, normalizedOptions, programLabel, cancellationToken),
                _ => await BuildAidRequestsSnapshotAsync(context, normalizedOptions, programLabel, cancellationToken)
            };
        }

        public async Task<ReportsSnapshot> BuildCashForWorkAttendanceSheetSnapshotAsync(
            int eventId,
            CancellationToken cancellationToken = default)
        {
            await using var context = new LocalDbContext();

            var cashForWorkEvent = await context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            var participants = await context.CashForWorkParticipants
                .AsNoTracking()
                .Include(participant => participant.Beneficiary)
                .Where(participant => participant.EventId == eventId)
                .OrderBy(participant => participant.Beneficiary!.FullName ?? participant.Beneficiary!.LastName)
                .ThenBy(participant => participant.Beneficiary!.FirstName)
                .ToListAsync(cancellationToken);

            var participantIds = participants
                .Select(participant => participant.Id)
                .ToList();

            var attendanceLookup = await context.CashForWorkAttendances
                .AsNoTracking()
                .Where(attendance =>
                    participantIds.Contains(attendance.ParticipantId) &&
                    attendance.AttendanceDate.Date == cashForWorkEvent.EventDate.Date)
                .GroupBy(attendance => attendance.ParticipantId)
                .Select(group => group
                    .OrderByDescending(attendance => attendance.RecordedAt)
                    .First())
                .ToListAsync(cancellationToken);

            var attendanceByParticipantId = attendanceLookup.ToDictionary(attendance => attendance.ParticipantId);

            var table = CreateTable(
                ("Full Name", typeof(string)),
                ("Beneficiary ID", typeof(string)),
                ("Civil Registry ID", typeof(string)),
                ("Event", typeof(string)),
                ("Kind", typeof(string)),
                ("Attendance Date", typeof(string)),
                ("Status", typeof(string)),
                ("Source", typeof(string)),
                ("Recorded At", typeof(string)));

            foreach (var participant in participants)
            {
                attendanceByParticipantId.TryGetValue(participant.Id, out var attendance);
                var beneficiary = participant.Beneficiary;
                var fullName = string.IsNullOrWhiteSpace(beneficiary?.FullName)
                    ? string.Join(
                        " ",
                        new[] { beneficiary?.FirstName, beneficiary?.MiddleName, beneficiary?.LastName }
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Select(value => value!.Trim()))
                    : beneficiary!.FullName!.Trim();

                table.Rows.Add(
                    string.IsNullOrWhiteSpace(fullName) ? $"Participant #{participant.Id}" : fullName,
                    beneficiary?.BeneficiaryId ?? "--",
                    beneficiary?.CivilRegistryId ?? "--",
                    cashForWorkEvent.Title,
                    cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar ? "Seminar" : "Cash-for-Work",
                    cashForWorkEvent.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    attendance?.Status.ToString() ?? "Not Recorded",
                    attendance?.Source.ToString() ?? "--",
                    attendance == null
                        ? "--"
                        : attendance.RecordedAt.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture));
            }

            var presentCount = attendanceLookup.Count(attendance => attendance.Status == CashForWorkAttendanceStatus.Present);
            var absentCount = attendanceLookup.Count(attendance => attendance.Status == CashForWorkAttendanceStatus.Absent);
            var pendingCount = Math.Max(0, participants.Count - attendanceLookup.Count);

            return new ReportsSnapshot
            {
                Title = "Attendance Sheet",
                Subtitle = $"Printable attendance history for {cashForWorkEvent.Title}.",
                ExportFilePrefix = "cash-for-work-attendance-sheet",
                RangeLabel = $"{cashForWorkEvent.EventDate:MMM dd, yyyy}",
                ProgramLabel = cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar ? "Seminar Event" : "Cash-for-Work Event",
                SuggestedOrientation = "Landscape",
                Table = table,
                Metrics = new[]
                {
                    CreateMetric("Assigned", participants.Count.ToString("N0", CultureInfo.InvariantCulture), "Beneficiaries assigned to the selected event"),
                    CreateMetric("Present", presentCount.ToString("N0", CultureInfo.InvariantCulture), "Attendance records marked present"),
                    CreateMetric("Absent", absentCount.ToString("N0", CultureInfo.InvariantCulture), "Attendance records marked absent"),
                    CreateMetric("Not Recorded", pendingCount.ToString("N0", CultureInfo.InvariantCulture), "Assigned beneficiaries without an attendance record")
                },
                Highlights = BuildHighlights(
                    $"{cashForWorkEvent.Title} is scheduled for {cashForWorkEvent.EventDate:MMMM dd, yyyy} at {cashForWorkEvent.Location}.",
                    pendingCount == 0
                        ? "All assigned beneficiaries already have attendance records for this event."
                        : $"{pendingCount:N0} assigned beneficiary row(s) still do not have an attendance record.")
            };
        }

        private static ReportsQueryOptions Normalize(ReportsQueryOptions options)
        {
            var dateFrom = options.DateFrom.Date;
            var dateTo = options.DateTo.Date;
            if (dateTo < dateFrom)
            {
                (dateFrom, dateTo) = (dateTo, dateFrom);
            }

            return new ReportsQueryOptions
            {
                ReportType = options.ReportType,
                DateFrom = dateFrom,
                DateTo = dateTo,
                AyudaProgramId = options.AyudaProgramId
            };
        }

        private static async Task<string> ResolveProgramLabelAsync(LocalDbContext context, ReportsReportType reportType, int? ayudaProgramId, CancellationToken cancellationToken)
        {
            if (!ayudaProgramId.HasValue)
            {
                return reportType == ReportsReportType.BudgetUtilization ? "All Budgets" : "All Programs";
            }

            var program = await context.AyudaPrograms
                .AsNoTracking()
                .Where(item => item.Id == ayudaProgramId.Value)
                .Select(item => item.ProgramName)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(program) ? "Selected Program" : program;
        }

        private static async Task<ReportsSnapshot> BuildAidRequestsSnapshotAsync(
            LocalDbContext context,
            ReportsQueryOptions options,
            string programLabel,
            CancellationToken cancellationToken)
        {
            var rangeEndExclusive = options.DateTo.AddDays(1);

            var query = context.AssistanceCases
                .AsNoTracking()
                .Where(item => item.RequestedOn >= options.DateFrom && item.RequestedOn < rangeEndExclusive);

            if (options.AyudaProgramId.HasValue)
            {
                query = query.Where(item => item.AyudaProgramId == options.AyudaProgramId.Value);
            }

            var rows = await query
                .OrderByDescending(item => item.RequestedOn)
                .ThenByDescending(item => item.CreatedAt)
                .Select(item => new
                {
                    item.CaseNumber,
                    item.RequestedOn,
                    Beneficiary = item.ValidatedBeneficiaryName ?? "--",
                    item.AssistanceType,
                    Status = item.Status.ToString(),
                Program = item.AssistanceCaseBudget != null
                    ? item.AssistanceCaseBudget.BudgetName
                    : item.AyudaProgram != null
                        ? item.AyudaProgram.ProgramName
                        : "--",
                    Amount = item.ApprovedAmount ?? item.RequestedAmount ?? 0m
                })
                .ToListAsync(cancellationToken);

            var table = CreateTable(("Case Number", typeof(string)), ("Requested On", typeof(string)), ("Beneficiary", typeof(string)), ("Assistance Type", typeof(string)), ("Status", typeof(string)), ("Budget", typeof(string)), ("Amount", typeof(string)));
            foreach (var row in rows)
            {
                table.Rows.Add(row.CaseNumber, row.RequestedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.Beneficiary, row.AssistanceType, row.Status, row.Program, row.Amount.ToString("N2", CultureInfo.InvariantCulture));
            }

            var pendingCount = rows.Count(item => string.Equals(item.Status, AssistanceCaseStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase));
            var approvedCount = rows.Count(item => string.Equals(item.Status, AssistanceCaseStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase));
            var releasedCount = rows.Count(item => string.Equals(item.Status, AssistanceCaseStatus.Released.ToString(), StringComparison.OrdinalIgnoreCase));

            return new ReportsSnapshot
            {
                Title = "Aid Request Summary",
                Subtitle = "Case volume, status mix, and requested or approved assistance totals.",
                ExportFilePrefix = "aid-request-summary",
                RangeLabel = BuildRangeLabel(options),
                ProgramLabel = programLabel,
                SuggestedOrientation = "Landscape",
                Table = table,
                Metrics = new[]
                {
                    CreateMetric("Total Requests", rows.Count.ToString("N0", CultureInfo.InvariantCulture), "Aid requests inside the selected period"),
                    CreateMetric("Pending", pendingCount.ToString("N0", CultureInfo.InvariantCulture), "Still awaiting review or action"),
                    CreateMetric("Approved", approvedCount.ToString("N0", CultureInfo.InvariantCulture), "Approved inside the selected date window"),
                    CreateMetric("Released", releasedCount.ToString("N0", CultureInfo.InvariantCulture), "Requests already released")
                },
                Highlights = BuildHighlights(
                    $"{pendingCount:N0} request(s) remain pending inside the selected range.",
                    rows.Count == 0 ? "No aid requests matched the selected filters." : $"The latest request in scope is {rows[0].CaseNumber} dated {rows[0].RequestedOn:MMM dd, yyyy}.")
            };
        }

        private static async Task<ReportsSnapshot> BuildValidatedBeneficiariesSnapshotAsync(
            LocalDbContext context,
            ReportsQueryOptions options,
            string programLabel,
            CancellationToken cancellationToken)
        {
            var rangeEndExclusive = options.DateTo.AddDays(1);

            var rows = await context.BeneficiaryStaging
                .AsNoTracking()
                .Where(item => item.ImportedAt >= options.DateFrom && item.ImportedAt < rangeEndExclusive)
                .OrderByDescending(item => item.ImportedAt)
                .ThenBy(item => item.FullName)
                .Select(item => new
                {
                    BeneficiaryId = item.BeneficiaryId ?? "--",
                    CivilRegistryId = item.CivilRegistryId ?? "--",
                    FullName = item.FullName ?? $"{item.LastName}, {item.FirstName}".Trim(' ', ','),
                    Sex = item.Sex ?? "--",
                    Age = item.Age ?? "--",
                    Status = item.VerificationStatus.ToString(),
                    item.ImportedAt
                })
                .ToListAsync(cancellationToken);

            var table = CreateTable(("Beneficiary ID", typeof(string)), ("Civil Registry", typeof(string)), ("Full Name", typeof(string)), ("Sex", typeof(string)), ("Age", typeof(string)), ("Status", typeof(string)), ("Imported At", typeof(string)));
            foreach (var row in rows)
            {
                table.Rows.Add(row.BeneficiaryId, row.CivilRegistryId, string.IsNullOrWhiteSpace(row.FullName) ? "--" : row.FullName, row.Sex, row.Age, row.Status, row.ImportedAt.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture));
            }

            var approvedCount = rows.Count(item => string.Equals(item.Status, VerificationStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase));
            var pendingCount = rows.Count(item => string.Equals(item.Status, VerificationStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase));
            var rejectedCount = rows.Count(item => string.Equals(item.Status, VerificationStatus.Rejected.ToString(), StringComparison.OrdinalIgnoreCase));

            return new ReportsSnapshot
            {
                Title = "Validated Beneficiaries",
                Subtitle = "Imported beneficiary rows with verification state and registry identifiers.",
                ExportFilePrefix = "validated-beneficiaries",
                RangeLabel = BuildRangeLabel(options),
                ProgramLabel = programLabel,
                SuggestedOrientation = "Landscape",
                Table = table,
                Metrics = new[]
                {
                    CreateMetric("Imported Rows", rows.Count.ToString("N0", CultureInfo.InvariantCulture), "Beneficiary staging rows imported in the selected period"),
                    CreateMetric("Approved", approvedCount.ToString("N0", CultureInfo.InvariantCulture), "Ready for downstream aid workflows"),
                    CreateMetric("Pending", pendingCount.ToString("N0", CultureInfo.InvariantCulture), "Still waiting for review"),
                    CreateMetric("Rejected", rejectedCount.ToString("N0", CultureInfo.InvariantCulture), "Rejected during verification")
                },
                Highlights = BuildHighlights(
                    approvedCount == 0 ? "No approved beneficiaries were found in the selected range." : $"{approvedCount:N0} beneficiary row(s) are already approved and usable downstream.",
                    rows.Count == 0 ? "No validated-beneficiary rows matched the selected filters." : $"Latest import in scope was recorded at {rows[0].ImportedAt:MMM dd, yyyy hh:mm tt}.")
            };
        }

        private static async Task<ReportsSnapshot> BuildBudgetUtilizationSnapshotAsync(
            LocalDbContext context,
            ReportsQueryOptions options,
            string programLabel,
            CancellationToken cancellationToken)
        {
            var rangeEndExclusive = options.DateTo.AddDays(1);
            var programs = await context.AyudaPrograms
                .AsNoTracking()
                .Where(item => item.IsActive && item.BudgetCap.HasValue && item.BudgetCap.Value > 0)
                .OrderBy(item => item.ProgramName)
                .ToListAsync(cancellationToken);
            var assistanceBudgets = await context.AssistanceCaseBudgets
                .AsNoTracking()
                .Where(item => item.IsActive && item.BudgetCap.HasValue && item.BudgetCap.Value > 0)
                .OrderBy(item => item.BudgetName)
                .ToListAsync(cancellationToken);
            var cashForWorkBudgets = await context.CashForWorkBudgets
                .AsNoTracking()
                .Where(item => item.IsActive && item.BudgetCap.HasValue && item.BudgetCap.Value > 0)
                .OrderBy(item => item.BudgetName)
                .ToListAsync(cancellationToken);

            var releasedByProgram = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item => item.EntryDate >= options.DateFrom && item.EntryDate < rangeEndExclusive)
                .Where(item => item.ProgramId != null)
                .GroupBy(item => item.ProgramId)
                .Select(group => new { ProgramId = group.Key!.Value, Released = group.Sum(item => item.TotalAmount) })
                .ToDictionaryAsync(item => item.ProgramId, item => item.Released, cancellationToken);
            var releasedByAssistanceBudget = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item => item.EntryDate >= options.DateFrom && item.EntryDate < rangeEndExclusive)
                .Where(item => item.AssistanceCaseBudgetId != null)
                .GroupBy(item => item.AssistanceCaseBudgetId)
                .Select(group => new { BudgetId = group.Key!.Value, Released = group.Sum(item => item.TotalAmount) })
                .ToDictionaryAsync(item => item.BudgetId, item => item.Released, cancellationToken);
            var releasedByCashForWorkBudget = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item => item.EntryDate >= options.DateFrom && item.EntryDate < rangeEndExclusive)
                .Where(item => item.CashForWorkBudgetId != null)
                .GroupBy(item => item.CashForWorkBudgetId)
                .Select(group => new { BudgetId = group.Key!.Value, Released = group.Sum(item => item.TotalAmount) })
                .ToDictionaryAsync(item => item.BudgetId, item => item.Released, cancellationToken);

            var table = CreateTable(("Bucket", typeof(string)), ("Type", typeof(string)), ("Status", typeof(string)), ("Budget Cap", typeof(string)), ("Released", typeof(string)), ("Balance", typeof(string)), ("Utilization", typeof(string)));
            decimal totalBudgetCap = 0m;
            decimal totalReleased = 0m;
            var alertCount = 0;

            foreach (var program in programs)
            {
                var released = releasedByProgram.TryGetValue(program.Id, out var releasedValue) ? releasedValue : 0m;
                var budgetCap = program.BudgetCap ?? 0m;
                var utilization = budgetCap <= 0m ? 0m : released / budgetCap;
                if (budgetCap > 0m && utilization >= 0.80m)
                {
                    alertCount++;
                }

                totalBudgetCap += budgetCap;
                totalReleased += released;
                table.Rows.Add(program.ProgramName, program.ProgramType.ToString(), program.DistributionStatus.ToString(), budgetCap.ToString("N2", CultureInfo.InvariantCulture), released.ToString("N2", CultureInfo.InvariantCulture), (budgetCap - released).ToString("N2", CultureInfo.InvariantCulture), budgetCap <= 0m ? "--" : $"{utilization:P0}");
            }

            foreach (var budget in assistanceBudgets)
            {
                var released = releasedByAssistanceBudget.TryGetValue(budget.Id, out var releasedValue) ? releasedValue : 0m;
                var budgetCap = budget.BudgetCap ?? 0m;
                var utilization = budgetCap <= 0m ? 0m : released / budgetCap;
                if (budgetCap > 0m && utilization >= 0.80m)
                {
                    alertCount++;
                }

                totalBudgetCap += budgetCap;
                totalReleased += released;
                table.Rows.Add(budget.BudgetName, "Aid Request Budget", budget.AssistanceType ?? "--", budgetCap.ToString("N2", CultureInfo.InvariantCulture), released.ToString("N2", CultureInfo.InvariantCulture), (budgetCap - released).ToString("N2", CultureInfo.InvariantCulture), budgetCap <= 0m ? "--" : $"{utilization:P0}");
            }

            foreach (var budget in cashForWorkBudgets)
            {
                var released = releasedByCashForWorkBudget.TryGetValue(budget.Id, out var releasedValue) ? releasedValue : 0m;
                var budgetCap = budget.BudgetCap ?? 0m;
                var utilization = budgetCap <= 0m ? 0m : released / budgetCap;
                if (budgetCap > 0m && utilization >= 0.80m)
                {
                    alertCount++;
                }

                totalBudgetCap += budgetCap;
                totalReleased += released;
                table.Rows.Add(budget.BudgetName, "Cash-for-Work Budget", "--", budgetCap.ToString("N2", CultureInfo.InvariantCulture), released.ToString("N2", CultureInfo.InvariantCulture), (budgetCap - released).ToString("N2", CultureInfo.InvariantCulture), budgetCap <= 0m ? "--" : $"{utilization:P0}");
            }

            return new ReportsSnapshot
            {
                Title = "Budget Utilization",
                Subtitle = "Program, aid request, and cash-for-work budget caps versus released amounts recorded in the ledger.",
                ExportFilePrefix = "budget-utilization",
                RangeLabel = BuildRangeLabel(options),
                ProgramLabel = programLabel,
                SuggestedOrientation = "Landscape",
                Table = table,
                Metrics = new[]
                {
                    CreateMetric("Buckets", (programs.Count + assistanceBudgets.Count + cashForWorkBudgets.Count).ToString("N0", CultureInfo.InvariantCulture), "Budget buckets visible in this utilization report"),
                    CreateMetric("Budget Cap", totalBudgetCap.ToString("N2", CultureInfo.InvariantCulture), "Configured budget caps across selected buckets"),
                    CreateMetric("Released", totalReleased.ToString("N2", CultureInfo.InvariantCulture), "Ledger releases recorded inside the selected range"),
                    CreateMetric("Alerts", alertCount.ToString("N0", CultureInfo.InvariantCulture), "Buckets at or above 80% utilization")
                },
                Highlights = BuildHighlights(
                    (programs.Count + assistanceBudgets.Count + cashForWorkBudgets.Count) == 0 ? "No budget buckets matched the selected filters." : $"{(programs.Count + assistanceBudgets.Count + cashForWorkBudgets.Count):N0} bucket(s) are included in the utilization table.",
                    alertCount == 0 ? "No budget-cap alerts were triggered in the selected range." : $"{alertCount:N0} bucket(s) are already at or above the alert threshold.")
            };
        }

        private static async Task<ReportsSnapshot> BuildDistributionClaimsSnapshotAsync(
            LocalDbContext context,
            ReportsQueryOptions options,
            string programLabel,
            CancellationToken cancellationToken)
        {
            var rangeEndExclusive = options.DateTo.AddDays(1);
            var query = context.AyudaProjectClaims
                .AsNoTracking()
                .Where(item => item.ClaimedAt >= options.DateFrom && item.ClaimedAt < rangeEndExclusive);

            if (options.AyudaProgramId.HasValue)
            {
                query = query.Where(item => item.AyudaProgramId == options.AyudaProgramId.Value);
            }

            var rows = await query
                .OrderByDescending(item => item.ClaimedAt)
                .Select(item => new
                {
                    item.ClaimedAt,
                    Program = item.AyudaProgram != null ? item.AyudaProgram.ProgramName : "--",
                    item.FullName,
                    BeneficiaryId = item.BeneficiaryId ?? "--",
                    AssistanceType = item.AssistanceTypeSnapshot ?? "--",
                    ItemDetail = item.ItemDescriptionSnapshot ?? "--",
                    Amount = item.UnitAmountSnapshot ?? 0m
                })
                .ToListAsync(cancellationToken);

            var table = CreateTable(("Claimed At", typeof(string)), ("Program", typeof(string)), ("Full Name", typeof(string)), ("Beneficiary ID", typeof(string)), ("Assistance", typeof(string)), ("Item / Detail", typeof(string)), ("Amount", typeof(string)));
            foreach (var row in rows)
            {
                table.Rows.Add(row.ClaimedAt.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture), row.Program, row.FullName, row.BeneficiaryId, row.AssistanceType, row.ItemDetail, row.Amount.ToString("N2", CultureInfo.InvariantCulture));
            }

            var uniqueBeneficiaries = rows.Select(item => $"{item.BeneficiaryId}|{item.FullName}").Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var distinctPrograms = rows.Select(item => item.Program).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            return new ReportsSnapshot
            {
                Title = "Distribution Claims",
                Subtitle = "One-claim-per-project distribution records captured through the project workflow.",
                ExportFilePrefix = "distribution-claims",
                RangeLabel = BuildRangeLabel(options),
                ProgramLabel = programLabel,
                SuggestedOrientation = "Landscape",
                Table = table,
                Metrics = new[]
                {
                    CreateMetric("Claims", rows.Count.ToString("N0", CultureInfo.InvariantCulture), "Claim log rows captured in the selected period"),
                    CreateMetric("Beneficiaries", uniqueBeneficiaries.ToString("N0", CultureInfo.InvariantCulture), "Unique beneficiaries with claims in scope"),
                    CreateMetric("Programs", distinctPrograms.ToString("N0", CultureInfo.InvariantCulture), "Projects represented by the claims log"),
                    CreateMetric("Latest", rows.Count == 0 ? "--" : rows[0].ClaimedAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture), "Latest claim date in scope")
                },
                Highlights = BuildHighlights(
                    rows.Count == 0 ? "No distribution claims matched the selected filters." : $"Latest claim in scope was recorded at {rows[0].ClaimedAt:MMM dd, yyyy hh:mm tt}.",
                    distinctPrograms == 0 ? "No distribution programs are represented in the current report." : $"{distinctPrograms:N0} program(s) are represented in the claims table.")
            };
        }

        private static async Task<ReportsSnapshot> BuildAdminActivityAuditSnapshotAsync(
            LocalDbContext context,
            ReportsQueryOptions options,
            string programLabel,
            CancellationToken cancellationToken)
        {
            var rangeEndExclusive = options.DateTo.AddDays(1);
            
            // Only include logs from Admin and SuperAdmin
            var adminLogs = await context.ActivityLogs
                .Include(al => al.User)
                .Where(al => al.Timestamp >= options.DateFrom && al.Timestamp < rangeEndExclusive)
                .Where(al => al.User != null && (al.User.Role == UserRole.Admin || al.User.Role == UserRole.SuperAdmin))
                .OrderByDescending(al => al.Timestamp)
                .ToListAsync(cancellationToken);

            var table = CreateTable(
                ("Timestamp", typeof(string)),
                ("Admin", typeof(string)),
                ("Role", typeof(string)),
                ("Action", typeof(string)),
                ("Module / Entity", typeof(string)),
                ("Specific Details", typeof(string)));

            foreach (var log in adminLogs)
            {
                table.Rows.Add(
                    log.Timestamp.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture),
                    log.User?.Username ?? "System",
                    log.User?.Role.ToString() ?? "--",
                    log.Action,
                    log.Entity,
                    log.Details);
            }

            var superAdminCount = adminLogs.Count(al => al.User?.Role == UserRole.SuperAdmin);
            var adminCount = adminLogs.Count(al => al.User?.Role == UserRole.Admin);

            return new ReportsSnapshot
            {
                Title = "Admin Activity Audit",
                Subtitle = "Comprehensive trail of actions performed by Admin and SuperAdmin accounts across system modules.",
                ExportFilePrefix = "admin-activity-audit",
                RangeLabel = BuildRangeLabel(options),
                ProgramLabel = programLabel,
                SuggestedOrientation = "Landscape",
                Table = table,
                Metrics = new[]
                {
                    CreateMetric("Total Activities", adminLogs.Count.ToString("N0", CultureInfo.InvariantCulture), "Total actions recorded by admins in this period"),
                    CreateMetric("SuperAdmin Logs", superAdminCount.ToString("N0", CultureInfo.InvariantCulture), "Actions performed by SuperAdmins"),
                    CreateMetric("Admin Logs", adminCount.ToString("N0", CultureInfo.InvariantCulture), "Actions performed by Admins"),
                    CreateMetric("Unique Admins", adminLogs.Select(al => al.UserId).Distinct().Count().ToString("N0", CultureInfo.InvariantCulture), "Number of distinct admins active in this range")
                },
                Highlights = BuildHighlights(
                    adminLogs.Count == 0 ? "No admin activities were recorded in the selected date range." : $"Found {adminLogs.Count:N0} admin action(s) for the selected period.",
                    $"This audit specifically filters for accounts with Admin or SuperAdmin roles.")
            };
        }

        private static ReportsMetricItem CreateMetric(string label, string value, string note)
        {
            return new ReportsMetricItem { Label = label, Value = value, Note = note };
        }

        private static string[] BuildHighlights(params string[] items)
        {
            return items.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        }

        private static string BuildRangeLabel(ReportsQueryOptions options)
        {
            return $"{options.DateFrom:MMM dd, yyyy} to {options.DateTo:MMM dd, yyyy}";
        }

        private static DataTable CreateTable(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable();
            foreach (var (name, type) in columns)
            {
                table.Columns.Add(name, type);
            }

            return table;
        }
    }
}

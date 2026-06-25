using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    public sealed class BarangayDashboardSnapshot
    {
        public string ActiveDatabaseLabel { get; init; } = string.Empty;
        public DateTime RetrievedAt { get; init; }
        public int AidRequestCount { get; init; }
        public int AidRequestsToday { get; init; }
        public int HouseholdCount { get; init; }
        public bool MasterListAvailable { get; init; }
        public int MasterListCount { get; init; }
        public DateTime? MasterListUpdatedAt { get; init; }
        public string MasterListStatusText { get; init; } = string.Empty;
        public int PendingBeneficiaries { get; init; }
        public int ApprovedBeneficiaries { get; init; }
        public int RejectedBeneficiaries { get; init; }
        public int BudgetAlertCount { get; init; }
        public int DistributionCount { get; init; }
        public int DistributionsToday { get; init; }
        public decimal ReleasedAmountToday { get; init; }
        public int CashForWorkBeneficiaryCount { get; init; }
        public int OpenCashForWorkEvents { get; init; }
        public int CompletedEventsThisMonth { get; init; }
        public int TodayAttendanceCount { get; init; }
        public int OverdueBorrowingCount { get; init; }
        public IReadOnlyList<DashboardRecentActivitySnapshot> RecentActivities { get; init; } = Array.Empty<DashboardRecentActivitySnapshot>();
        public IReadOnlyList<DashboardUpcomingEventSnapshot> UpcomingEvents { get; init; } = Array.Empty<DashboardUpcomingEventSnapshot>();
        public IReadOnlyList<DashboardRecentImportSnapshot> RecentImports { get; init; } = Array.Empty<DashboardRecentImportSnapshot>();
    }

    public sealed class DashboardRecentActivitySnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public DateTime OccurredAt { get; init; }
    }

    public sealed class DashboardUpcomingEventSnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public DateTime EventDate { get; init; }
        public TimeSpan StartTime { get; init; }
        public CashForWorkEventStatus Status { get; init; }
        public int ParticipantCount { get; init; }
    }

    public sealed class DashboardRecentImportSnapshot
    {
        public string FullName { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public DateTime ImportedAt { get; init; }
        public VerificationStatus Status { get; init; }
    }

    public sealed class BarangayDashboardService
    {
        private const string MasterListTableName = "val_beneficiaries";

        public async Task<BarangayDashboardSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            var settings = ConnectionSettingsService.Load();
            var activePreset = settings.GetPreset(settings.SelectedPreset);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var masterListMetrics = await LoadMasterListMetricsAsync(activePreset, cancellationToken);

            await using var context = new LocalDbContext();

            var aidRequestCount = await context.AssistanceCases
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var aidRequestsToday = await context.AssistanceCases
                .AsNoTracking()
                .CountAsync(item => item.CreatedAt >= today && item.CreatedAt < tomorrow, cancellationToken);

            var pendingBeneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .CountAsync(row => row.VerificationStatus == VerificationStatus.Pending, cancellationToken);

            var approvedBeneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .CountAsync(row => row.VerificationStatus == VerificationStatus.Approved, cancellationToken);

            var rejectedBeneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .CountAsync(row => row.VerificationStatus == VerificationStatus.Rejected, cancellationToken);

            var openCashForWorkEvents = await context.CashForWorkEvents
                .AsNoTracking()
                .CountAsync(cashForWorkEvent => cashForWorkEvent.Status == CashForWorkEventStatus.Open, cancellationToken);

            var completedEventsThisMonth = await context.CashForWorkEvents
                .AsNoTracking()
                .CountAsync(cashForWorkEvent =>
                    cashForWorkEvent.Status == CashForWorkEventStatus.Completed &&
                    cashForWorkEvent.EventDate >= monthStart &&
                    cashForWorkEvent.EventDate < monthEnd,
                    cancellationToken);

            var todayAttendanceCount = await context.CashForWorkAttendances
                .AsNoTracking()
                .CountAsync(attendance =>
                    attendance.Status == CashForWorkAttendanceStatus.Present &&
                    attendance.AttendanceDate >= today &&
                    attendance.AttendanceDate < tomorrow,
                    cancellationToken);

            var overdueBorrowingCount = await context.EquipmentBorrowings
                .AsNoTracking()
                .CountAsync(b => b.ReturnDate == null && b.DueDate < DateTime.Now, cancellationToken);

            var distributionCount = await context.AyudaProjectClaims
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var householdCount = await context.Households
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var distributionsToday = await context.AyudaProjectClaims
                .AsNoTracking()
                .CountAsync(item => item.ClaimedAt >= today && item.ClaimedAt < tomorrow, cancellationToken);

            var releasedAmountToday = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item =>
                    item.EntryType == BudgetLedgerEntryType.Release &&
                    item.EntryDate >= today &&
                    item.EntryDate < tomorrow)
                .SumAsync(item => (decimal?)item.TotalAmount, cancellationToken) ?? 0m;

            var cappedPrograms = await context.AyudaPrograms
                .AsNoTracking()
                .Where(item => item.IsActive && item.BudgetCap.HasValue && item.BudgetCap.Value > 0)
                .Select(item => new
                {
                    item.Id,
                    BudgetCap = item.BudgetCap!.Value
                })
                .ToListAsync(cancellationToken);

            var cappedAssistanceBudgets = await context.AssistanceCaseBudgets
                .AsNoTracking()
                .Where(item => item.IsActive && item.BudgetCap.HasValue && item.BudgetCap.Value > 0)
                .Select(item => new
                {
                    item.Id,
                    BudgetCap = item.BudgetCap!.Value
                })
                .ToListAsync(cancellationToken);

            var cappedCashForWorkBudgets = await context.CashForWorkBudgets
                .AsNoTracking()
                .Where(item => item.IsActive && item.BudgetCap.HasValue && item.BudgetCap.Value > 0)
                .Select(item => new
                {
                    item.Id,
                    BudgetCap = item.BudgetCap!.Value
                })
                .ToListAsync(cancellationToken);

            var releasedByProgram = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item =>
                    item.EntryType == BudgetLedgerEntryType.Release &&
                    item.ProgramId.HasValue)
                .GroupBy(item => item.ProgramId!.Value)
                .Select(group => new
                {
                    ProgramId = group.Key,
                    ReleasedTotal = group.Sum(entry => entry.TotalAmount)
                })
                .ToDictionaryAsync(item => item.ProgramId, item => item.ReleasedTotal, cancellationToken);

            var releasedByAssistanceBudget = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item =>
                    item.EntryType == BudgetLedgerEntryType.Release &&
                    item.AssistanceCaseBudgetId.HasValue)
                .GroupBy(item => item.AssistanceCaseBudgetId!.Value)
                .Select(group => new
                {
                    BudgetId = group.Key,
                    ReleasedTotal = group.Sum(entry => entry.TotalAmount)
                })
                .ToDictionaryAsync(item => item.BudgetId, item => item.ReleasedTotal, cancellationToken);

            var releasedByCashForWorkBudget = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item =>
                    item.EntryType == BudgetLedgerEntryType.Release &&
                    item.CashForWorkBudgetId.HasValue)
                .GroupBy(item => item.CashForWorkBudgetId!.Value)
                .Select(group => new
                {
                    BudgetId = group.Key,
                    ReleasedTotal = group.Sum(entry => entry.TotalAmount)
                })
                .ToDictionaryAsync(item => item.BudgetId, item => item.ReleasedTotal, cancellationToken);

            var budgetAlertCount = cappedPrograms.Count(item =>
            {
                releasedByProgram.TryGetValue(item.Id, out var releasedTotal);
                return releasedTotal >= item.BudgetCap * 0.8m;
            }) + cappedAssistanceBudgets.Count(item =>
            {
                releasedByAssistanceBudget.TryGetValue(item.Id, out var releasedTotal);
                return releasedTotal >= item.BudgetCap * 0.8m;
            }) + cappedCashForWorkBudgets.Count(item =>
            {
                releasedByCashForWorkBudget.TryGetValue(item.Id, out var releasedTotal);
                return releasedTotal >= item.BudgetCap * 0.8m;
            });

            var upcomingEventsData = await context.CashForWorkEvents
                .AsNoTracking()
                .Where(cashForWorkEvent => cashForWorkEvent.EventDate >= today)
                .OrderBy(cashForWorkEvent => cashForWorkEvent.EventDate)
                .Select(cashForWorkEvent => new DashboardUpcomingEventSnapshot
                {
                    Title = cashForWorkEvent.Title,
                    Location = cashForWorkEvent.Location,
                    EventDate = cashForWorkEvent.EventDate,
                    StartTime = cashForWorkEvent.StartTime,
                    Status = cashForWorkEvent.Status,
                    ParticipantCount = cashForWorkEvent.Participants.Count
                })
                .Take(20)
                .ToListAsync(cancellationToken);

            var upcomingEvents = upcomingEventsData
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.StartTime)
                .Take(5)
                .ToList();

            var recentImports = await context.BeneficiaryStaging
                .AsNoTracking()
                .OrderByDescending(row => row.ImportedAt)
                .Select(row => new
                {
                    row.FullName,
                    row.FirstName,
                    row.MiddleName,
                    row.LastName,
                    row.Address,
                    row.ImportedAt,
                    row.VerificationStatus
                })
                .Take(6)
                .ToListAsync(cancellationToken);

            var recentActivities = await context.ActivityLogs
                .AsNoTracking()
                .OrderByDescending(item => item.Timestamp)
                .Take(4)
                .Select(item => new DashboardRecentActivitySnapshot
                {
                    Title = BuildActivityTitle(item.Action, item.Entity),
                    Detail = string.IsNullOrWhiteSpace(item.Details)
                        ? $"{HumanizeToken(item.Entity)} activity recorded."
                        : item.Details.Trim(),
                    OccurredAt = item.Timestamp
                })
                .ToListAsync(cancellationToken);

            return new BarangayDashboardSnapshot
            {
                ActiveDatabaseLabel = $"{activePreset.DisplayName}: {activePreset.Server}:{activePreset.Port} / {activePreset.Database}",
                RetrievedAt = DateTime.Now,
                AidRequestCount = aidRequestCount,
                AidRequestsToday = aidRequestsToday,
                HouseholdCount = householdCount,
                MasterListAvailable = masterListMetrics.IsAvailable,
                MasterListCount = masterListMetrics.Count,
                MasterListUpdatedAt = masterListMetrics.LastUpdatedAt,
                MasterListStatusText = masterListMetrics.StatusText,
                PendingBeneficiaries = pendingBeneficiaries,
                ApprovedBeneficiaries = approvedBeneficiaries,
                RejectedBeneficiaries = rejectedBeneficiaries,
                BudgetAlertCount = budgetAlertCount,
                DistributionCount = distributionCount,
                DistributionsToday = distributionsToday,
                ReleasedAmountToday = releasedAmountToday,
                CashForWorkBeneficiaryCount = approvedBeneficiaries,
                OpenCashForWorkEvents = openCashForWorkEvents,
                CompletedEventsThisMonth = completedEventsThisMonth,
                TodayAttendanceCount = todayAttendanceCount,
                OverdueBorrowingCount = overdueBorrowingCount,
                RecentActivities = recentActivities,
                UpcomingEvents = upcomingEvents,
                RecentImports = recentImports
                    .Select(row => new DashboardRecentImportSnapshot
                    {
                        FullName = BuildDisplayName(row.FullName, row.FirstName, row.MiddleName, row.LastName),
                        Address = row.Address ?? string.Empty,
                        ImportedAt = row.ImportedAt,
                        Status = row.VerificationStatus
                    })
                    .ToList()
            };
        }

        private static async Task<MasterListMetrics> LoadMasterListMetricsAsync(DatabaseConnectionPreset preset, CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(preset));
                await connection.OpenAsync(cancellationToken);

                if (!await TableExistsAsync(connection, preset.Database, cancellationToken))
                {
                    return new MasterListMetrics(false, 0, null, "Validated beneficiaries snapshot is not available yet.");
                }

                const string sql =
                    """
                    SELECT COUNT(*) AS total_count, MAX(updated_at) AS last_updated
                    FROM val_beneficiaries;
                    """;

                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    return new MasterListMetrics(true, 0, null, "Validated beneficiaries snapshot is ready.");
                }

                var count = reader.IsDBNull(reader.GetOrdinal("total_count"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("total_count"));

                var lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated"))
                    ? null
                    : (DateTime?)reader.GetDateTime(reader.GetOrdinal("last_updated"));

                return new MasterListMetrics(true, count, lastUpdatedAt, "Validated beneficiaries snapshot is ready.");
            }
            catch (Exception ex)
            {
                return new MasterListMetrics(false, 0, null, $"Validated beneficiaries snapshot unavailable: {ex.Message}");
            }
        }

        private static async Task<bool> TableExistsAsync(MySqlConnection connection, string databaseName, CancellationToken cancellationToken)
        {
            const string sql =
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @databaseName
                  AND table_name = @tableName
                  AND table_type = 'BASE TABLE';
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@databaseName", databaseName);
            command.Parameters.AddWithValue("@tableName", MasterListTableName);

            var count = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(count) > 0;
        }

        private static string BuildDisplayName(string? fullName, string? firstName, string? middleName, string? lastName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Trim();
            }

            return string.Join(
                " ",
                new[] { firstName, middleName, lastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
        }

        private static string BuildActivityTitle(string? action, string? entity)
        {
            var actionText = HumanizeToken(action);
            var entityText = HumanizeToken(entity);

            if (string.IsNullOrWhiteSpace(entityText))
            {
                return actionText;
            }

            if (actionText.Contains(entityText, StringComparison.OrdinalIgnoreCase))
            {
                return actionText;
            }

            return $"{entityText} {actionText}".Trim();
        }

        private static string HumanizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Recent activity";
            }

            var source = value.Trim().Replace('_', ' ').Replace('-', ' ');
            var builder = new StringBuilder(source.Length + 8);

            for (var index = 0; index < source.Length; index++)
            {
                var current = source[index];
                if (index > 0
                    && char.IsUpper(current)
                    && source[index - 1] != ' '
                    && !char.IsUpper(source[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString().Trim();
        }

        private sealed record MasterListMetrics(bool IsAvailable, int Count, DateTime? LastUpdatedAt, string StatusText);
    }
}

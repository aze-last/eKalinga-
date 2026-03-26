using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public sealed class BarangayDashboardSnapshot
    {
        public string ActiveDatabaseLabel { get; init; } = string.Empty;
        public DateTime RetrievedAt { get; init; }
        public bool MasterListAvailable { get; init; }
        public int MasterListCount { get; init; }
        public DateTime? MasterListUpdatedAt { get; init; }
        public string MasterListStatusText { get; init; } = string.Empty;
        public int PendingBeneficiaries { get; init; }
        public int ApprovedBeneficiaries { get; init; }
        public int RejectedBeneficiaries { get; init; }
        public int ActiveHouseholds { get; init; }
        public int TotalMembers { get; init; }
        public int EligibleWorkers { get; init; }
        public int OpenCashForWorkEvents { get; init; }
        public int CompletedEventsThisMonth { get; init; }
        public int TodayAttendanceCount { get; init; }
        public IReadOnlyList<DashboardUpcomingEventSnapshot> UpcomingEvents { get; init; } = Array.Empty<DashboardUpcomingEventSnapshot>();
        public IReadOnlyList<DashboardRecentImportSnapshot> RecentImports { get; init; } = Array.Empty<DashboardRecentImportSnapshot>();
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

            await using var context = new AppDbContext();

            var pendingBeneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .CountAsync(row => row.VerificationStatus == VerificationStatus.Pending, cancellationToken);

            var approvedBeneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .CountAsync(row => row.VerificationStatus == VerificationStatus.Approved, cancellationToken);

            var rejectedBeneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .CountAsync(row => row.VerificationStatus == VerificationStatus.Rejected, cancellationToken);

            var activeHouseholds = await context.Households
                .AsNoTracking()
                .CountAsync(household => household.Status == HouseholdStatus.Active, cancellationToken);

            var totalMembers = await context.HouseholdMembers
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var eligibleWorkers = await context.HouseholdMembers
                .AsNoTracking()
                .CountAsync(member => member.IsCashForWorkEligible && member.Household.Status == HouseholdStatus.Active, cancellationToken);

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

            var upcomingEvents = await context.CashForWorkEvents
                .AsNoTracking()
                .Where(cashForWorkEvent => cashForWorkEvent.EventDate >= today)
                .OrderBy(cashForWorkEvent => cashForWorkEvent.EventDate)
                .ThenBy(cashForWorkEvent => cashForWorkEvent.StartTime)
                .Select(cashForWorkEvent => new DashboardUpcomingEventSnapshot
                {
                    Title = cashForWorkEvent.Title,
                    Location = cashForWorkEvent.Location,
                    EventDate = cashForWorkEvent.EventDate,
                    StartTime = cashForWorkEvent.StartTime,
                    Status = cashForWorkEvent.Status,
                    ParticipantCount = cashForWorkEvent.Participants.Count
                })
                .Take(5)
                .ToListAsync(cancellationToken);

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

            return new BarangayDashboardSnapshot
            {
                ActiveDatabaseLabel = $"{activePreset.DisplayName}: {activePreset.Server}:{activePreset.Port} / {activePreset.Database}",
                RetrievedAt = DateTime.Now,
                MasterListAvailable = masterListMetrics.IsAvailable,
                MasterListCount = masterListMetrics.Count,
                MasterListUpdatedAt = masterListMetrics.LastUpdatedAt,
                MasterListStatusText = masterListMetrics.StatusText,
                PendingBeneficiaries = pendingBeneficiaries,
                ApprovedBeneficiaries = approvedBeneficiaries,
                RejectedBeneficiaries = rejectedBeneficiaries,
                ActiveHouseholds = activeHouseholds,
                TotalMembers = totalMembers,
                EligibleWorkers = eligibleWorkers,
                OpenCashForWorkEvents = openCashForWorkEvents,
                CompletedEventsThisMonth = completedEventsThisMonth,
                TodayAttendanceCount = todayAttendanceCount,
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

        private sealed record MasterListMetrics(bool IsAvailable, int Count, DateTime? LastUpdatedAt, string StatusText);
    }
}

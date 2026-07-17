using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AttendanceShiftingManagement.Services
{
    public sealed record GgmsProjectSyncResult(
        bool IsSuccess,
        string Message,
        int NewProjectCount = 0,
        int UpdatedProjectCount = 0,
        int ArchivedProjectCount = 0);

    /// <summary>
    /// Read-only mirror of the GGMS project_details table into the local ggms_project_cache.
    /// GGMS admins split the office-level allocation into per-project sub-budgets; this service
    /// pulls those rows for the configured Ayuda office code so the Budget module can list them
    /// and treat each as its own spending envelope. Never writes to GGMS.
    /// </summary>
    public sealed class GgmsProjectSyncService
    {
        private readonly BudgetRuntimeOptions _options;

        public GgmsProjectSyncService(BudgetRuntimeOptions? options = null)
        {
            _options = options ?? BudgetRuntimeOptions.Load();
        }

        public async Task<GgmsProjectSyncResult> RefreshProjectCacheAsync(LocalDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!HasConnectionDetails(_options.GgmsConnection))
            {
                return new GgmsProjectSyncResult(false, "GGMS source settings are incomplete. Open Settings and complete the GGMS Budget Source connection details.");
            }

            if (string.IsNullOrWhiteSpace(_options.AyudaOfficeCode))
            {
                return new GgmsProjectSyncResult(false, "The Ayuda office code is not configured, so GGMS projects cannot be filtered.");
            }

            List<GgmsProjectRow> remoteProjects;
            try
            {
                remoteProjects = await ReadProjectsForOfficeAsync();
            }
            catch (Exception ex)
            {
                return new GgmsProjectSyncResult(false, $"Unable to read GGMS project details: {ex.Message}");
            }

            var officeCode = _options.AyudaOfficeCode.Trim();
            var cachedProjects = await context.GgmsProjectCache
                .Where(cache => cache.OfficeCode == officeCode)
                .ToListAsync();
            var cacheByProjectDetailsId = cachedProjects.ToDictionary(cache => cache.ProjectDetailsId, StringComparer.OrdinalIgnoreCase);

            var now = DateTime.Now;
            var newCount = 0;
            var updatedCount = 0;
            var seenProjectDetailsIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var remote in remoteProjects)
            {
                if (string.IsNullOrWhiteSpace(remote.ProjectDetailsId))
                {
                    continue;
                }

                seenProjectDetailsIds.Add(remote.ProjectDetailsId);

                if (cacheByProjectDetailsId.TryGetValue(remote.ProjectDetailsId, out var cached))
                {
                    var changed = cached.TotalBudget != remote.TotalBudget
                        || !string.Equals(cached.ProjectName, remote.ProjectName, StringComparison.Ordinal)
                        || !string.Equals(cached.Description, remote.Description, StringComparison.Ordinal)
                        || !string.Equals(cached.Status, remote.Status, StringComparison.OrdinalIgnoreCase)
                        || cached.SourceUpdatedAt != remote.SourceUpdatedAt;

                    if (changed)
                    {
                        cached.YearlyBudgetId = remote.YearlyBudgetId;
                        cached.ProjectName = remote.ProjectName;
                        cached.Description = remote.Description;
                        cached.SystemName = remote.SystemName;
                        cached.TotalBudget = remote.TotalBudget;
                        cached.Status = remote.Status;
                        cached.VoucherCode = remote.VoucherCode;
                        cached.SourceCreatedAt = remote.SourceCreatedAt;
                        cached.SourceUpdatedAt = remote.SourceUpdatedAt;
                        cached.CachedAt = now;
                        updatedCount++;
                    }
                }
                else
                {
                    context.GgmsProjectCache.Add(new GgmsProjectCache
                    {
                        ProjectDetailsId = remote.ProjectDetailsId,
                        YearlyBudgetId = remote.YearlyBudgetId,
                        OfficeCode = officeCode,
                        ProjectName = remote.ProjectName,
                        Description = remote.Description,
                        SystemName = remote.SystemName,
                        TotalBudget = remote.TotalBudget,
                        Status = remote.Status,
                        VoucherCode = remote.VoucherCode,
                        SourceCreatedAt = remote.SourceCreatedAt,
                        SourceUpdatedAt = remote.SourceUpdatedAt,
                        CachedAt = now,
                        IsLinked = false
                    });
                    newCount++;
                }
            }

            // Never delete cache rows: projects that vanished from GGMS are soft-archived.
            var archivedCount = 0;
            foreach (var cached in cachedProjects)
            {
                if (!seenProjectDetailsIds.Contains(cached.ProjectDetailsId)
                    && !string.Equals(cached.Status, "archived", StringComparison.OrdinalIgnoreCase))
                {
                    cached.Status = "archived";
                    cached.CachedAt = now;
                    archivedCount++;
                }
            }

            await context.SaveChangesAsync();
            await FollowGgmsBudgetOnLinkedProgramsAsync(context);

            var message = newCount > 0
                ? $"{newCount} new GGMS project(s) found for {officeCode}."
                : $"No new GGMS projects for {officeCode}.";
            return new GgmsProjectSyncResult(true, message, newCount, updatedCount, archivedCount);
        }

        /// <summary>
        /// Linked programs follow the GGMS project budget: when a GGMS admin edits a project's
        /// total_budget, the local program cap is updated to match — but never below what the
        /// program has already released.
        /// </summary>
        private static async Task FollowGgmsBudgetOnLinkedProgramsAsync(LocalDbContext context)
        {
            var linkedPrograms = await context.AyudaPrograms
                .Where(program => program.SourceProjectDetailsId != null)
                .ToListAsync();
            if (linkedPrograms.Count == 0)
            {
                return;
            }

            var linkedIds = linkedPrograms
                .Select(program => program.SourceProjectDetailsId!)
                .Distinct()
                .ToList();
            var cachedByProjectDetailsId = (await context.GgmsProjectCache
                    .AsNoTracking()
                    .Where(cache => linkedIds.Contains(cache.ProjectDetailsId))
                    .ToListAsync())
                .ToDictionary(cache => cache.ProjectDetailsId, StringComparer.OrdinalIgnoreCase);

            var anyChanged = false;
            foreach (var program in linkedPrograms)
            {
                if (!cachedByProjectDetailsId.TryGetValue(program.SourceProjectDetailsId!, out var cached))
                {
                    continue;
                }

                var released = await context.BudgetLedgerEntries
                    .AsNoTracking()
                    .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release && entry.ProgramId == program.Id)
                    .SumAsync(entry => (decimal?)entry.TotalAmount) ?? 0m;

                var newCap = Math.Max(cached.TotalBudget, released);
                if (program.BudgetCap != newCap)
                {
                    program.BudgetCap = newCap;
                    program.UpdatedAt = DateTime.Now;
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                await context.SaveChangesAsync();
            }
        }

        private async Task<List<GgmsProjectRow>> ReadProjectsForOfficeAsync()
        {
            var projects = new List<GgmsProjectRow>();

            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(_options.GgmsConnection));
            await connection.OpenAsync();

            var commandText = BuildProjectDetailsQuery(_options.GgmsProjectDetailsTable);
            await using var command = new MySqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@officeCode", _options.AyudaOfficeCode.Trim());

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                projects.Add(new GgmsProjectRow(
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                    reader.IsDBNull(7) ? "active" : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    reader.IsDBNull(10) ? null : reader.GetDateTime(10)));
            }

            return projects;
        }

        /// <summary>
        /// Filters by office_code (never the numeric office id) per the GGMS integration contract.
        /// Exposed for test assertions, mirroring the allocation query builders.
        /// </summary>
        public static string BuildProjectDetailsQuery(string? projectDetailsTable)
        {
            var table = NormalizeTableName(projectDetailsTable, "project_details");

            return $"""
                SELECT
                    details.`project_details_id`,
                    details.`yearly_budget_id`,
                    details.`office_code`,
                    details.`project`,
                    details.`description`,
                    details.`system_name`,
                    details.`total_budget`,
                    details.`status`,
                    details.`voucher_code`,
                    details.`create_at`,
                    details.`updated_at`
                FROM `{table}` AS details
                WHERE details.`office_code` = @officeCode
                ORDER BY details.`create_at` DESC, details.`id` DESC;
                """;
        }

        private static string NormalizeTableName(string? configured, string fallback)
        {
            return !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : fallback;
        }

        private static bool HasConnectionDetails(DatabaseConnectionPreset preset)
        {
            return !string.IsNullOrWhiteSpace(preset.Server)
                && !string.IsNullOrWhiteSpace(preset.Database)
                && !string.IsNullOrWhiteSpace(preset.Username);
        }

        private sealed record GgmsProjectRow(
            string ProjectDetailsId,
            int YearlyBudgetId,
            string OfficeCode,
            string ProjectName,
            string? Description,
            string? SystemName,
            decimal TotalBudget,
            string Status,
            string? VoucherCode,
            DateTime? SourceCreatedAt,
            DateTime? SourceUpdatedAt);
    }
}

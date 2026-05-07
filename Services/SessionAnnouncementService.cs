using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class SessionAnnouncementSnapshot
    {
        public DateTime? PreviousLogoutAt { get; init; }
        public DateTime? LastLogoutAt { get; init; }
        public DateTime GeneratedAt { get; init; }
        public IReadOnlyList<SessionAnnouncementItem> Items { get; init; } = Array.Empty<SessionAnnouncementItem>();
        public int ApprovalCount { get; init; }
        public int BudgetCount { get; init; }
        public int DistributionCount { get; init; }
        public int CashForWorkCount { get; init; }
        public int EquipmentCount { get; init; }
        public bool HasUpdates => Items.Count > 0;
    }

    public sealed class SessionAnnouncementItem
    {
        public string CategoryKey { get; init; } = "All";
        public string CategoryLabel { get; init; } = "Activity";
        public string Title { get; init; } = string.Empty;
        public string Module { get; init; } = string.Empty;
        public string UpdatedBy { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public string TimestampLabel => Timestamp.ToString("MMM dd, yyyy hh:mm tt");
    }

    internal sealed class SessionAnnouncementCheckpoint
    {
        public DateTime? PreviousLogoutAt { get; set; }
        public int? PreviousLogoutUserId { get; set; }
        public DateTime? LastLogoutAt { get; set; }
        public int? LastLogoutUserId { get; set; }
    }

    public sealed class SessionAnnouncementService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly Func<AppDbContext> _contextFactory;
        private readonly string _runtimePath;

        public SessionAnnouncementService(
            Func<AppDbContext>? contextFactory = null,
            string? runtimePath = null)
        {
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _runtimePath = runtimePath ?? GetRuntimePath();
        }

        public void RecordLogoutCheckpoint(int? userId)
        {
            RecordLogoutCheckpoint(userId, DateTime.Now);
        }

        public void RecordLogoutCheckpoint(int? userId, DateTime checkpointAt)
        {
            var previousCheckpoint = LoadCheckpoint();
            var payload = new SessionAnnouncementCheckpoint
            {
                PreviousLogoutAt = previousCheckpoint.LastLogoutAt,
                PreviousLogoutUserId = previousCheckpoint.LastLogoutUserId,
                LastLogoutAt = checkpointAt,
                LastLogoutUserId = userId
            };

            var directory = Path.GetDirectoryName(_runtimePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_runtimePath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        public async Task<SessionAnnouncementSnapshot> BuildSnapshotAsync(int currentUserId, int maxItems = 24)
        {
            var checkpoint = LoadCheckpoint();
            if (!checkpoint.PreviousLogoutAt.HasValue || !checkpoint.LastLogoutAt.HasValue)
            {
                return new SessionAnnouncementSnapshot
                {
                    GeneratedAt = DateTime.Now
                };
            }

            var windowStart = checkpoint.PreviousLogoutAt.Value;
            var windowEnd = checkpoint.LastLogoutAt.Value;

            await using var context = _contextFactory();
            
            // Focus on all significant actions within the previous session window.
            var logs = await context.ActivityLogs
                .Include(item => item.User)
                .Where(item =>
                    item.Timestamp > windowStart &&
                    item.Timestamp <= windowEnd &&
                    item.Action != "Login" &&
                    item.Action != "Logout")
                .OrderByDescending(item => item.Timestamp)
                .Take(maxItems)
                .ToListAsync();

            var items = logs
                .Select(BuildItem)
                .ToList();

            return new SessionAnnouncementSnapshot
            {
                PreviousLogoutAt = windowStart,
                LastLogoutAt = windowEnd,
                GeneratedAt = windowEnd,
                Items = items,
                ApprovalCount = items.Count(item => item.CategoryKey == "Approvals"),
                BudgetCount = items.Count(item => item.CategoryKey == "Budget"),
                DistributionCount = items.Count(item => item.CategoryKey == "Distribution"),
                CashForWorkCount = items.Count(item => item.CategoryKey == "CashForWork"),
                EquipmentCount = items.Count(item => item.CategoryKey == "Equipment")
            };
        }

        private SessionAnnouncementCheckpoint LoadCheckpoint()
        {
            if (!File.Exists(_runtimePath))
            {
                return new SessionAnnouncementCheckpoint();
            }

            try
            {
                var json = File.ReadAllText(_runtimePath);
                return JsonSerializer.Deserialize<SessionAnnouncementCheckpoint>(json, JsonOptions) ?? new SessionAnnouncementCheckpoint();
            }
            catch
            {
                return new SessionAnnouncementCheckpoint();
            }
        }

        private static SessionAnnouncementItem BuildItem(ActivityLog log)
        {
            var categoryKey = GetCategoryKey(log.Action, log.Entity);
            return new SessionAnnouncementItem
            {
                CategoryKey = categoryKey,
                CategoryLabel = GetCategoryLabel(categoryKey),
                Title = BuildTitle(log.Action, log.Entity),
                Module = BuildModule(log.Entity),
                UpdatedBy = BuildUpdatedBy(log),
                Details = NormalizeDetails(log.Details),
                Timestamp = log.Timestamp
            };
        }

        private static string GetCategoryKey(string? action, string? entity)
        {
            var actionValue = action ?? string.Empty;
            var entityValue = entity ?? string.Empty;

            if (actionValue.Contains("Approve", StringComparison.OrdinalIgnoreCase) ||
                actionValue.Contains("Reject", StringComparison.OrdinalIgnoreCase) ||
                entityValue.Contains("Beneficiary", StringComparison.OrdinalIgnoreCase))
            {
                return "Approvals";
            }

            if (entityValue.Contains("Budget", StringComparison.OrdinalIgnoreCase) ||
                entityValue.Contains("Donation", StringComparison.OrdinalIgnoreCase))
            {
                return "Budget";
            }

            if (entityValue.Contains("Distribution", StringComparison.OrdinalIgnoreCase) ||
                entityValue.Contains("Project", StringComparison.OrdinalIgnoreCase) ||
                actionValue.Contains("Claim", StringComparison.OrdinalIgnoreCase))
            {
                return "Distribution";
            }

            if (entityValue.Contains("CashForWork", StringComparison.OrdinalIgnoreCase) ||
                entityValue.Contains("Attendance", StringComparison.OrdinalIgnoreCase))
            {
                return "CashForWork";
            }

            if (entityValue.Contains("Equipment", StringComparison.OrdinalIgnoreCase) ||
                entityValue.Contains("Asset", StringComparison.OrdinalIgnoreCase))
            {
                return "Equipment";
            }

            if (entityValue.Contains("Report", StringComparison.OrdinalIgnoreCase))
            {
                return "Reports";
            }

            return "General";
        }

        private static string GetCategoryLabel(string categoryKey)
        {
            return categoryKey switch
            {
                "Approvals" => "Approvals",
                "Budget" => "Budget",
                "Distribution" => "Distribution",
                "CashForWork" => "Cash-for-Work",
                "Reports" => "Reports",
                "Equipment" => "Equipment Borrowing",
                _ => "General"
            };
        }

        private static string BuildTitle(string? action, string? entity)
        {
            var actionLabel = string.IsNullOrWhiteSpace(action) ? "Updated" : SplitWords(action!);
            var entityLabel = BuildModule(entity);
            return string.IsNullOrWhiteSpace(entityLabel)
                ? actionLabel
                : $"{actionLabel} {entityLabel}";
        }

        private static string BuildModule(string? entity)
        {
            if (string.IsNullOrWhiteSpace(entity))
            {
                return "General";
            }

            return entity.Trim() switch
            {
                "BeneficiaryStaging" => "Validated Beneficiaries",
                "AssistanceCase" => "Aid Request",
                "AyudaProgram" => "Distribution",
                "AyudaProjectClaim" => "Distribution",
                "AyudaProjectBeneficiary" => "Distribution",
                "BudgetLedgerEntry" => "Budget",
                "GovernmentBudgetSnapshot" => "Budget",
                "PrivateDonation" => "Budget",
                "CashForWorkEvent" => "Cash-for-Work",
                "CashForWorkAttendance" => "Cash-for-Work",
                "CashForWorkParticipant" => "Cash-for-Work",
                "EquipmentBorrowing" => "Equipment Borrowing",
                "BarangayAsset" => "Asset Registry",
                "Report" => "Reports",
                "User" => "User Account",
                _ => SplitWords(entity.Trim())
            };
        }

        private static string BuildUpdatedBy(ActivityLog log)
        {
            if (!string.IsNullOrWhiteSpace(log.User?.Username))
            {
                return log.User.Username;
            }

            if (!string.IsNullOrWhiteSpace(log.User?.Email))
            {
                return log.User.Email;
            }

            return "System";
        }

        private static string NormalizeDetails(string? details)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                return "Recent activity was recorded for this module.";
            }

            var normalized = string.Join(" ",
                details
                    .Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim()));

            return normalized.Length <= 180
                ? normalized
                : $"{normalized[..177]}...";
        }

        private static string SplitWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var buffer = new List<char>(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (index > 0 && char.IsUpper(current) && !char.IsWhiteSpace(value[index - 1]))
                {
                    buffer.Add(' ');
                }

                buffer.Add(current);
            }

            return new string(buffer.ToArray());
        }

        private static string GetRuntimePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "session-announcement-state.json");
        }
    }
}

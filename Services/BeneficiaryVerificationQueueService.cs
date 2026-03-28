using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    internal static class BeneficiaryVerificationStatusFilters
    {
        public const string Pending = "Pending";
        public const string Verified = "Verified";
        public const string Approved = "Approved";
        public const string Duplicate = "Duplicate";
        public const string Inactive = "Inactive";
        public const string Rejected = "Rejected";
        public const string All = "All";

        public static IReadOnlyList<string> Options { get; } = new[]
        {
            Pending,
            Verified,
            Approved,
            Duplicate,
            Inactive,
            Rejected,
            All
        };
    }

    internal sealed class BeneficiaryVerificationQueuePageRequest
    {
        public string SearchText { get; init; } = string.Empty;
        public string StatusFilter { get; init; } = BeneficiaryVerificationStatusFilters.Pending;
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 100;
    }

    internal sealed class BeneficiaryVerificationQueueRow
    {
        public BeneficiaryStaging Staging { get; init; } = new();
        public BeneficiaryDigitalId? DigitalId { get; init; }
    }

    internal sealed class BeneficiaryVerificationQueuePageResult
    {
        public IReadOnlyList<BeneficiaryVerificationQueueRow> Rows { get; init; } = Array.Empty<BeneficiaryVerificationQueueRow>();
        public int PageNumber { get; init; } = 1;
        public int FilteredRecordCount { get; init; }
        public int TotalCount { get; init; }
        public int PendingCount { get; init; }
        public int VerifiedCount { get; init; }
        public int ApprovedCount { get; init; }
        public int DuplicateCount { get; init; }
        public int InactiveCount { get; init; }
        public int RejectedCount { get; init; }
    }

    internal interface IBeneficiaryVerificationQueueService
    {
        Task<BeneficiaryVerificationQueuePageResult> LoadPageAsync(
            BeneficiaryVerificationQueuePageRequest request,
            CancellationToken cancellationToken = default);
    }

    internal sealed class BeneficiaryVerificationQueueService : IBeneficiaryVerificationQueueService
    {
        private const int MaxPageSize = 500;

        public async Task<BeneficiaryVerificationQueuePageResult> LoadPageAsync(
            BeneficiaryVerificationQueuePageRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var requestedPageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

            await using var context = new AppDbContext();

            var summaryCounts = await context.BeneficiaryStaging
                .AsNoTracking()
                .GroupBy(row => row.VerificationStatus)
                .Select(group => new
                {
                    Status = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var summaryCountMap = summaryCounts.ToDictionary(item => item.Status, item => item.Count);

            var filteredQuery = ApplyFilters(
                context.BeneficiaryStaging.AsNoTracking(),
                request.SearchText,
                request.StatusFilter);

            var filteredRecordCount = await filteredQuery.CountAsync(cancellationToken);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(filteredRecordCount, 1) / pageSize));
            var actualPageNumber = Math.Min(requestedPageNumber, totalPages);

            var stagingRows = filteredRecordCount == 0
                ? new List<BeneficiaryStaging>()
                : await filteredQuery
                    .OrderByDescending(row => row.ImportedAt)
                    .ThenByDescending(row => row.StagingID)
                    .Skip((actualPageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

            var stagingIds = stagingRows
                .Select(row => row.StagingID)
                .ToList();

            var digitalIdsByStagingId = stagingIds.Count == 0
                ? new Dictionary<int, BeneficiaryDigitalId>()
                : await context.BeneficiaryDigitalIds
                    .AsNoTracking()
                    .Where(item => stagingIds.Contains(item.BeneficiaryStagingId))
                    .ToDictionaryAsync(item => item.BeneficiaryStagingId, cancellationToken);

            return new BeneficiaryVerificationQueuePageResult
            {
                Rows = stagingRows
                    .Select(row => new BeneficiaryVerificationQueueRow
                    {
                        Staging = row,
                        DigitalId = digitalIdsByStagingId.GetValueOrDefault(row.StagingID)
                })
                    .ToList(),
                PageNumber = actualPageNumber,
                FilteredRecordCount = filteredRecordCount,
                TotalCount = summaryCounts.Sum(item => item.Count),
                PendingCount = CountStatus(summaryCountMap, VerificationStatus.Pending),
                VerifiedCount = CountStatus(summaryCountMap, VerificationStatus.Verified),
                ApprovedCount = CountStatus(summaryCountMap, VerificationStatus.Approved),
                DuplicateCount = CountStatus(summaryCountMap, VerificationStatus.Duplicate),
                InactiveCount = CountStatus(summaryCountMap, VerificationStatus.Inactive),
                RejectedCount = CountStatus(summaryCountMap, VerificationStatus.Rejected)
            };
        }

        private static IQueryable<BeneficiaryStaging> ApplyFilters(
            IQueryable<BeneficiaryStaging> query,
            string? searchText,
            string? statusFilter)
        {
            if (!string.IsNullOrWhiteSpace(statusFilter)
                && !string.Equals(statusFilter, BeneficiaryVerificationStatusFilters.All, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<VerificationStatus>(statusFilter, true, out var parsedStatus))
            {
                query = query.Where(row => row.VerificationStatus == parsedStatus);
            }

            var normalizedSearchText = searchText?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                return query;
            }

            var pattern = $"%{normalizedSearchText}%";
            return query.Where(row =>
                EF.Functions.Like(row.FullName ?? string.Empty, pattern)
                || EF.Functions.Like(row.FirstName ?? string.Empty, pattern)
                || EF.Functions.Like(row.MiddleName ?? string.Empty, pattern)
                || EF.Functions.Like(row.LastName ?? string.Empty, pattern)
                || EF.Functions.Like(row.BeneficiaryId ?? string.Empty, pattern)
                || EF.Functions.Like(row.CivilRegistryId ?? string.Empty, pattern)
                || EF.Functions.Like(row.Address ?? string.Empty, pattern)
                || EF.Functions.Like(row.ReviewNotes ?? string.Empty, pattern));
        }

        private static int CountStatus(
            IReadOnlyDictionary<VerificationStatus, int> summaryCounts,
            VerificationStatus status)
        {
            if (summaryCounts.TryGetValue(status, out var count))
            {
                return count;
            }

            return 0;
        }
    }
}

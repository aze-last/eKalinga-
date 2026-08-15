using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    internal static class MasterListQuickFilters
    {
        public const string AllBeneficiaries = "All beneficiaries";
        public const string SeniorCitizens = "Senior citizens";
        public const string PersonsWithDisability = "PWD";
        public const string WithCivilRegistryId = "With civil registry ID";
        public const string MissingCivilRegistryId = "Missing civil registry ID";
        public const string Approved = "Approved";
        public const string Pending = "Pending";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            AllBeneficiaries,
            SeniorCitizens,
            PersonsWithDisability,
            WithCivilRegistryId,
            MissingCivilRegistryId,
            Approved,
            Pending
        };
    }

    internal sealed class MasterListPageRequest
    {
        public string SearchText { get; init; } = string.Empty;
        public IReadOnlyList<string> QuickFilters { get; init; } = Array.Empty<string>();
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 100;
    }

    internal sealed class MasterListPageResult
    {
        public IReadOnlyList<MasterListBeneficiary> Beneficiaries { get; init; } = Array.Empty<MasterListBeneficiary>();
        public int TotalBeneficiaries { get; init; }
        public int ApprovedCount { get; init; }
        public int PendingCount { get; init; }
        public int LinkedCivilRegistryCount { get; init; }
        public int SeniorCount { get; init; }
        public int PwdCount { get; init; }
        public int FilteredBeneficiaryCount { get; init; }
        public string SourceDatabase { get; init; } = string.Empty;
        public string SourceServer { get; init; } = string.Empty;
        public DateTime? LastUpdatedAt { get; init; }
    }

    internal interface IMasterListQueryService
    {
        Task<MasterListPageResult> LoadPageAsync(MasterListPageRequest request, CancellationToken cancellationToken = default);
    }

    internal sealed class MasterListService : IMasterListQueryService
    {
        private const int MaxPageSize = 500;

        public async Task<MasterListPageResult> LoadPageAsync(MasterListPageRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

            await using var context = new LocalDbContext();

            // Compute summary metrics across local staging registry
            var totalCount = await context.BeneficiaryStaging.CountAsync(cancellationToken);
            var seniorCount = await context.BeneficiaryStaging.CountAsync(b => b.IsSenior, cancellationToken);
            var pwdCount = await context.BeneficiaryStaging.CountAsync(b => b.IsPwd, cancellationToken);
            var linkedCivilCount = await context.BeneficiaryStaging.CountAsync(b => !string.IsNullOrWhiteSpace(b.CivilRegistryId), cancellationToken);

            IQueryable<BeneficiaryStaging> query = context.BeneficiaryStaging.AsNoTracking();

            // Quick filters
            foreach (var filter in request.QuickFilters)
            {
                switch (filter)
                {
                    case MasterListQuickFilters.SeniorCitizens:
                        query = query.Where(b => b.IsSenior);
                        break;
                    case MasterListQuickFilters.PersonsWithDisability:
                        query = query.Where(b => b.IsPwd);
                        break;
                    case MasterListQuickFilters.WithCivilRegistryId:
                        query = query.Where(b => b.CivilRegistryId != null && b.CivilRegistryId != "");
                        break;
                    case MasterListQuickFilters.MissingCivilRegistryId:
                        query = query.Where(b => b.CivilRegistryId == null || b.CivilRegistryId == "");
                        break;
                }
            }

            // Search filter
            var searchText = request.SearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lower = searchText.ToLower();
                query = query.Where(b =>
                    (b.FullName != null && b.FullName.ToLower().Contains(lower)) ||
                    (b.BeneficiaryId != null && b.BeneficiaryId.ToLower().Contains(lower)) ||
                    (b.CivilRegistryId != null && b.CivilRegistryId.ToLower().Contains(lower)) ||
                    (b.LastName != null && b.LastName.ToLower().Contains(lower)) ||
                    (b.FirstName != null && b.FirstName.ToLower().Contains(lower)) ||
                    (b.Address != null && b.Address.ToLower().Contains(lower)));
            }

            var filteredCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(b => b.FullName)
                .ThenBy(b => b.BeneficiaryId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new MasterListBeneficiary
                {
                    Id = b.StagingID,
                    ResidentsId = b.ResidentsId ?? 0,
                    BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = b.CivilRegistryId ?? string.Empty,
                    LastName = b.LastName ?? string.Empty,
                    FirstName = b.FirstName ?? string.Empty,
                    MiddleName = b.MiddleName ?? string.Empty,
                    FullName = b.FullName ?? string.Empty,
                    Sex = b.Sex ?? string.Empty,
                    DateOfBirth = b.DateOfBirth ?? string.Empty,
                    Age = b.Age ?? string.Empty,
                    MaritalStatus = b.MaritalStatus ?? string.Empty,
                    Address = b.Address ?? string.Empty,
                    IsPwd = b.IsPwd,
                    PwdIdNo = b.PwdIdNo ?? string.Empty,
                    IsSenior = b.IsSenior,
                    SeniorIdNo = b.SeniorIdNo ?? string.Empty,
                    DisabilityType = b.DisabilityType ?? string.Empty,
                    CauseOfDisability = b.CauseOfDisability ?? string.Empty,
                    VerificationStatus = VerificationStatus.Approved,
                    CreatedAt = b.ImportedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return new MasterListPageResult
            {
                Beneficiaries = items,
                TotalBeneficiaries = totalCount,
                ApprovedCount = totalCount,
                PendingCount = 0,
                LinkedCivilRegistryCount = linkedCivilCount,
                SeniorCount = seniorCount,
                PwdCount = pwdCount,
                FilteredBeneficiaryCount = filteredCount,
                SourceDatabase = "ams.db (Local Masterlist)",
                SourceServer = "eKalinga Local Registry",
                LastUpdatedAt = DateTime.Now
            };
        }
    }
}

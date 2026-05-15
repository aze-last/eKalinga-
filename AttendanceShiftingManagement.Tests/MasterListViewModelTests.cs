using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class MasterListViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsFirstPageAndUpdatesPaginationState()
    {
        var queryService = new FakeMasterListQueryService();
        
        // Result for Pending
        queryService.Enqueue(new MasterListPageResult
        {
            Beneficiaries = BuildBeneficiaries(1, 10),
            TotalBeneficiaries = 40000,
            LinkedCivilRegistryCount = 32000,
            SeniorCount = 8000,
            PwdCount = 1200,
            FilteredBeneficiaryCount = 10,
            SourceDatabase = "ayuda_local",
            SourceServer = "127.0.0.1",
            LastUpdatedAt = new DateTime(2026, 3, 26, 9, 30, 0)
        });

        // Result for Approved
        queryService.Enqueue(new MasterListPageResult
        {
            Beneficiaries = BuildBeneficiaries(11, 90),
            TotalBeneficiaries = 40000,
            LinkedCivilRegistryCount = 32000,
            SeniorCount = 8000,
            PwdCount = 1200,
            FilteredBeneficiaryCount = 230,
            SourceDatabase = "ayuda_local",
            SourceServer = "127.0.0.1",
            LastUpdatedAt = new DateTime(2026, 3, 26, 9, 30, 0)
        });

        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        await viewModel.RefreshAsync();

        Assert.Equal(2, queryService.Requests.Count);
        
        // Check Pending Request
        var pendingRequest = queryService.Requests[0];
        Assert.Equal(1, pendingRequest.PageNumber);
        Assert.Contains(MasterListQuickFilters.Pending, pendingRequest.QuickFilters);

        // Check Approved Request
        var approvedRequest = queryService.Requests[1];
        Assert.Equal(1, approvedRequest.PageNumber);
        Assert.Contains(MasterListQuickFilters.Approved, approvedRequest.QuickFilters);

        Assert.Equal(10, viewModel.PendingBeneficiaries.Count);
        Assert.Equal(90, viewModel.ApprovedBeneficiaries.Count);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(3, viewModel.TotalPages); // (10 + 230) / 100 = 2.4 -> 3 pages
        Assert.Equal("Page 1 of 3", viewModel.PageIndicator);
        Assert.Equal("Showing 1-100 of 240 total records", viewModel.PageSummary);
        Assert.Equal(40000, viewModel.TotalBeneficiaries);
    }

    [Fact]
    public async Task GoToNextPageAsync_RequestsNextPageUsingCurrentFilters()
    {
        var queryService = new FakeMasterListQueryService();
        
        // Refresh: Pending
        queryService.Enqueue(new MasterListPageResult { FilteredBeneficiaryCount = 20, Beneficiaries = BuildBeneficiaries(1, 10) });
        // Refresh: Approved
        queryService.Enqueue(new MasterListPageResult { FilteredBeneficiaryCount = 50, Beneficiaries = BuildBeneficiaries(11, 40) });
        
        // NextPage: Pending
        queryService.Enqueue(new MasterListPageResult { FilteredBeneficiaryCount = 20, Beneficiaries = BuildBeneficiaries(21, 10) });
        // NextPage: Approved
        queryService.Enqueue(new MasterListPageResult { FilteredBeneficiaryCount = 50, Beneficiaries = BuildBeneficiaries(51, 10) });

        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false)
        {
            SearchText = "ana",
            SelectedPageSize = 50
        };
        
        var seniorFilter = viewModel.FilterOptions.First(o => o.Label == MasterListQuickFilters.SeniorCitizens);
        seniorFilter.IsSelected = true;

        await viewModel.RefreshAsync();
        await viewModel.GoToNextPageAsync();

        Assert.Equal(4, queryService.Requests.Count);

        // Check requests use ana and senior
        foreach (var req in queryService.Requests)
        {
            Assert.Equal("ana", req.SearchText);
            Assert.Contains(MasterListQuickFilters.SeniorCitizens, req.QuickFilters);
        }

        Assert.Equal(2, queryService.Requests[2].PageNumber); // NextPage call
        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(2, viewModel.TotalPages); // (20+50) / 50 = 1.4 -> 2 pages
    }

    [Fact]
    public void ProcessScanCommand_PopulatesSearchTextAndClearsScannerInput()
    {
        var queryService = new FakeMasterListQueryService();
        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        viewModel.ScannerInput = "BEN-00123 ";
        
        viewModel.ProcessScanCommand.Execute(null);

        Assert.Equal("BEN-00123", viewModel.SearchText);
        Assert.Equal(string.Empty, viewModel.ScannerInput);
    }

    private static IReadOnlyList<MasterListBeneficiary> BuildBeneficiaries(int start, int count)
    {
        return Enumerable.Range(start, count)
            .Select(index => new MasterListBeneficiary
            {
                Id = index,
                ResidentsId = index,
                BeneficiaryId = $"BEN-{index:00000}",
                FullName = $"Beneficiary {index}",
                Address = $"Address {index}"
            })
            .ToList();
    }

    private sealed class FakeMasterListQueryService : IMasterListQueryService
    {
        private readonly Queue<MasterListPageResult> _results = new();

        public List<MasterListPageRequest> Requests { get; } = new();

        public void Enqueue(MasterListPageResult result)
        {
            _results.Enqueue(result);
        }

        public Task<MasterListPageResult> LoadPageAsync(MasterListPageRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (_results.Count == 0)
            {
                // Return empty result instead of throwing to avoid crashing tests prematurely
                return Task.FromResult(new MasterListPageResult { Beneficiaries = new List<MasterListBeneficiary>() });
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}

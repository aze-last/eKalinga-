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
        queryService.Enqueue(new MasterListPageResult
        {
            Beneficiaries = BuildBeneficiaries(1, 100),
            TotalBeneficiaries = 40000,
            LinkedCivilRegistryCount = 32000,
            SeniorCount = 8000,
            PwdCount = 1200,
            FilteredBeneficiaryCount = 240,
            SourceDatabase = "ayuda_local",
            SourceServer = "127.0.0.1",
            LastUpdatedAt = new DateTime(2026, 3, 26, 9, 30, 0)
        });

        var viewModel = new MasterListViewModel(queryService, autoLoad: false, autoRefresh: false);

        await viewModel.RefreshAsync();

        var request = Assert.Single(queryService.Requests);
        Assert.Equal(1, request.PageNumber);
        Assert.Equal(100, request.PageSize);
        Assert.Equal(MasterListQuickFilters.AllBeneficiaries, request.QuickFilter);
        Assert.Equal(string.Empty, request.SearchText);

        Assert.Equal(100, viewModel.Beneficiaries.Count);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(3, viewModel.TotalPages);
        Assert.Equal("Page 1 of 3", viewModel.PageIndicator);
        Assert.Equal("Showing 1-100 of 240 validated beneficiaries", viewModel.PageSummary);
        Assert.Equal("Beneficiary 1", viewModel.SelectedBeneficiary?.DisplayName);
        Assert.Equal(40000, viewModel.TotalBeneficiaries);
        Assert.Equal(32000, viewModel.LinkedCivilRegistryCount);
        Assert.Equal(8000, viewModel.SeniorCount);
        Assert.Equal(1200, viewModel.PwdCount);
    }

    [Fact]
    public async Task GoToNextPageAsync_RequestsNextPageUsingCurrentFilters()
    {
        var queryService = new FakeMasterListQueryService();
        queryService.Enqueue(new MasterListPageResult
        {
            Beneficiaries = BuildBeneficiaries(1, 50),
            TotalBeneficiaries = 40000,
            LinkedCivilRegistryCount = 32000,
            SeniorCount = 8000,
            PwdCount = 1200,
            FilteredBeneficiaryCount = 70,
            SourceDatabase = "ayuda_local",
            SourceServer = "127.0.0.1",
            LastUpdatedAt = new DateTime(2026, 3, 26, 9, 30, 0)
        });
        queryService.Enqueue(new MasterListPageResult
        {
            Beneficiaries = BuildBeneficiaries(51, 20),
            TotalBeneficiaries = 40000,
            LinkedCivilRegistryCount = 32000,
            SeniorCount = 8000,
            PwdCount = 1200,
            FilteredBeneficiaryCount = 70,
            SourceDatabase = "ayuda_local",
            SourceServer = "127.0.0.1",
            LastUpdatedAt = new DateTime(2026, 3, 26, 9, 30, 0)
        });

        var viewModel = new MasterListViewModel(queryService, autoLoad: false, autoRefresh: false)
        {
            SearchText = "ana",
            SelectedQuickFilter = MasterListQuickFilters.SeniorCitizens,
            SelectedPageSize = 50
        };

        await viewModel.RefreshAsync();
        await viewModel.GoToNextPageAsync();

        Assert.Equal(2, queryService.Requests.Count);

        Assert.Equal(1, queryService.Requests[0].PageNumber);
        Assert.Equal(50, queryService.Requests[0].PageSize);
        Assert.Equal("ana", queryService.Requests[0].SearchText);
        Assert.Equal(MasterListQuickFilters.SeniorCitizens, queryService.Requests[0].QuickFilter);

        Assert.Equal(2, queryService.Requests[1].PageNumber);
        Assert.Equal(50, queryService.Requests[1].PageSize);
        Assert.Equal("ana", queryService.Requests[1].SearchText);
        Assert.Equal(MasterListQuickFilters.SeniorCitizens, queryService.Requests[1].QuickFilter);

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(2, viewModel.TotalPages);
        Assert.Equal("Page 2 of 2", viewModel.PageIndicator);
        Assert.Equal("Showing 51-70 of 70 validated beneficiaries", viewModel.PageSummary);
        Assert.False(viewModel.NextPageCommand.CanExecute(null));
        Assert.True(viewModel.PreviousPageCommand.CanExecute(null));
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
                throw new InvalidOperationException("No fake master list page result was queued.");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}

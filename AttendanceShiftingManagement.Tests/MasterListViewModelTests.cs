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
            Beneficiaries = BuildBeneficiaries(1, 25),
            TotalBeneficiaries = 40000,
            PendingCount = 0,
            ApprovedCount = 40000,
            LinkedCivilRegistryCount = 32000,
            SeniorCount = 8000,
            PwdCount = 1200,
            FilteredBeneficiaryCount = 40000,
            SourceDatabase = "ams.db",
            SourceServer = "eKalinga Local Registry",
            LastUpdatedAt = new DateTime(2026, 3, 26, 9, 30, 0)
        });

        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        await viewModel.RefreshAsync();

        Assert.Single(queryService.Requests);
        
        var request = queryService.Requests[0];
        Assert.Equal(1, request.PageNumber);

        Assert.Equal(25, viewModel.ApprovedBeneficiaries.Count);
        Assert.Equal(1, viewModel.ApprovedCurrentPage);
        Assert.Equal(400, viewModel.ApprovedTotalPages); // 40000 / 100 = 400
        Assert.Equal("Page 1 of 400", viewModel.ApprovedPageIndicator);
        
        Assert.Equal(40000, viewModel.TotalApprovedBeneficiaries);
    }

    [Fact]
    public void HouseholdContext_StartsEmptyUntilABeneficiaryIsSelected()
    {
        var queryService = new FakeMasterListQueryService();
        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        Assert.False(viewModel.HasHouseholdContext);
        Assert.Empty(viewModel.SelectedHouseholdMembers);
        Assert.Equal(string.Empty, viewModel.HouseholdCode);
        Assert.Equal(string.Empty, viewModel.HouseholdHeadName);
        Assert.Equal(string.Empty, viewModel.HouseholdAddressSummary);
    }

    [Fact]
    public async Task GoToNextApprovedPageAsync_RequestsNextPageUsingCurrentFilters()
    {
        var queryService = new FakeMasterListQueryService();
        
        queryService.Enqueue(new MasterListPageResult { TotalBeneficiaries = 200, FilteredBeneficiaryCount = 200, ApprovedCount = 200, Beneficiaries = BuildBeneficiaries(1, 10) });
        queryService.Enqueue(new MasterListPageResult { TotalBeneficiaries = 200, FilteredBeneficiaryCount = 200, ApprovedCount = 200, Beneficiaries = BuildBeneficiaries(21, 10) });

        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false)
        {
            SearchText = "ana",
            SelectedPageSize = 50
        };
        
        var seniorFilter = viewModel.FilterOptions.First(o => o.Label == MasterListQuickFilters.SeniorCitizens);
        seniorFilter.IsSelected = true;

        await viewModel.RefreshAsync();
        await viewModel.GoToNextApprovedPageAsync();

        Assert.Equal(2, queryService.Requests.Count);

        foreach (var req in queryService.Requests)
        {
            Assert.Equal("ana", req.SearchText);
            Assert.Contains(MasterListQuickFilters.SeniorCitizens, req.QuickFilters);
        }

        Assert.Equal(2, queryService.Requests[1].PageNumber);
        Assert.Equal(2, viewModel.ApprovedCurrentPage);
        Assert.Equal(4, viewModel.ApprovedTotalPages); // 200 / 50 = 4
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

    [Fact]
    public void OnboardingTour_NavigatesThroughStepsAndClosesCorrectly()
    {
        var queryService = new FakeMasterListQueryService();
        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        Assert.False(viewModel.IsOnboardingOpen);
        Assert.False(viewModel.IsAnyOverlayOpen);

        // Open Tour
        viewModel.OpenOnboardingCommand.Execute(null);
        Assert.True(viewModel.IsOnboardingOpen);
        Assert.True(viewModel.IsAnyOverlayOpen);
        Assert.False(viewModel.IsStandardModalOpen);
        Assert.Equal(1, viewModel.OnboardingStep);
        Assert.Equal("Search & Filter Registry", viewModel.OnboardingTitle);
        Assert.Equal("SidebarSearchBorder", viewModel.OnboardingTargetName);

        // Next Step: 1 -> 2
        viewModel.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(2, viewModel.OnboardingStep);
        Assert.Equal("Select Resident Profile", viewModel.OnboardingTitle);
        Assert.Equal("ApprovedDataGrid", viewModel.OnboardingTargetName);

        // Next Step: 2 -> 3
        viewModel.NextOnboardingStepCommand.Execute(null);
        Assert.Equal(3, viewModel.OnboardingStep);
        Assert.Equal("Review & Issue Digital ID", viewModel.OnboardingTitle);
        Assert.Equal("QuickActionsPanel", viewModel.OnboardingTargetName);

        // Previous Step: 3 -> 2
        viewModel.PreviousOnboardingStepCommand.Execute(null);
        Assert.Equal(2, viewModel.OnboardingStep);

        // Jump to Step 4
        viewModel.SetOnboardingStepCommand.Execute(4);
        Assert.Equal(4, viewModel.OnboardingStep);
        Assert.Equal("Direct Project Enrollment", viewModel.OnboardingTitle);
        Assert.Equal("EnrollmentButton", viewModel.OnboardingTargetName);

        // Step 4 Next -> Closes Tour
        viewModel.NextOnboardingStepCommand.Execute(null);
        Assert.False(viewModel.IsOnboardingOpen);
        Assert.False(viewModel.IsAnyOverlayOpen);
    }

    [Fact]
    public void OnboardingTour_UpdatesActionHintWhenBeneficiarySelected()
    {
        var queryService = new FakeMasterListQueryService();
        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        viewModel.OpenOnboarding();
        viewModel.OnboardingStep = 2;

        Assert.Contains("Click a resident row", viewModel.OnboardingActionHint);

        var ben = new MasterListBeneficiary
        {
            Id = 1,
            FullName = "Maria Santos",
            BeneficiaryId = "BEN-00001"
        };
        viewModel.SelectedBeneficiary = ben;

        // Auto-advances to step 3 on selection
        Assert.Equal(3, viewModel.OnboardingStep);
        Assert.Equal("QuickActionsPanel", viewModel.OnboardingTargetName);
    }

    [Fact]
    public void OnboardingTour_AutoAdvancesOnFullProfileAndEnrollmentActions()
    {
        var queryService = new FakeMasterListQueryService();
        var viewModel = new MasterListViewModel(null, queryService, autoLoad: false, autoRefresh: false);

        viewModel.OpenOnboarding();
        viewModel.OnboardingStep = 3;
        viewModel.SelectedBeneficiary = new MasterListBeneficiary { Id = 1, FullName = "Juan Dela Cruz" };

        // Step 3: Triggering OpenFullProfileCommand auto-advances to Step 4
        viewModel.OpenFullProfileCommand.Execute(null);
        Assert.Equal(4, viewModel.OnboardingStep);
        Assert.Equal("EnrollmentButton", viewModel.OnboardingTargetName);

        // Step 4: Triggering OpenEnrollmentPanelCommand completes/closes tour
        viewModel.OpenEnrollmentPanelCommand.Execute(null);
        Assert.False(viewModel.IsOnboardingOpen);
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
                return Task.FromResult(new MasterListPageResult { Beneficiaries = new List<MasterListBeneficiary>() });
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}

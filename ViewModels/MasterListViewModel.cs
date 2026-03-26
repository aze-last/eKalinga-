using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class MasterListViewModel : ObservableObject
    {
        private readonly ObservableCollection<MasterListBeneficiary> _beneficiaries = new();
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _previousPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly IMasterListQueryService _queryService;
        private readonly bool _autoRefresh;
        private CancellationTokenSource? _loadCts;
        private MasterListBeneficiary? _selectedBeneficiary;
        private string _searchText = string.Empty;
        private string _selectedQuickFilter = MasterListQuickFilters.AllBeneficiaries;
        private int _selectedPageSize = 100;
        private bool _isBusy;
        private string _statusMessage = "Loading validated beneficiaries...";
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private int _totalBeneficiaries;
        private int _linkedCivilRegistryCount;
        private int _seniorCount;
        private int _pwdCount;
        private int _filteredBeneficiaryCount;
        private int _currentPage = 1;
        private string _snapshotSourceSummary = "Validated beneficiaries snapshot";
        private string _lastUpdatedSummary = "Last refresh: --";

        public MasterListViewModel()
            : this(new MasterListService(), autoLoad: true, autoRefresh: true)
        {
        }

        internal MasterListViewModel(IMasterListQueryService queryService, bool autoLoad, bool autoRefresh)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _autoRefresh = autoRefresh;

            QuickFilters = new ObservableCollection<string>(MasterListQuickFilters.All);
            PageSizeOptions = new ObservableCollection<int> { 50, 100, 250, 500 };
            _refreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
            _previousPageCommand = new RelayCommand(async _ => await GoToPreviousPageAsync(), _ => !IsBusy && CurrentPage > 1);
            _nextPageCommand = new RelayCommand(async _ => await GoToNextPageAsync(), _ => !IsBusy && CurrentPage < TotalPages);

            if (autoLoad)
            {
                _ = RefreshAsync();
            }
        }

        public ObservableCollection<string> QuickFilters { get; }

        public ObservableCollection<int> PageSizeOptions { get; }

        public ObservableCollection<MasterListBeneficiary> Beneficiaries => _beneficiaries;

        public MasterListBeneficiary? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set => SetProperty(ref _selectedBeneficiary, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    QueueReloadFromFirstPage();
                }
            }
        }

        public string SelectedQuickFilter
        {
            get => _selectedQuickFilter;
            set
            {
                if (SetProperty(ref _selectedQuickFilter, value))
                {
                    QueueReloadFromFirstPage();
                }
            }
        }

        public int SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (SetProperty(ref _selectedPageSize, value))
                {
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageIndicator));
                    OnPropertyChanged(nameof(PageSummary));
                    QueueReloadFromFirstPage();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _refreshCommand.RaiseCanExecuteChanged();
                    _previousPageCommand.RaiseCanExecuteChanged();
                    _nextPageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        public int TotalBeneficiaries
        {
            get => _totalBeneficiaries;
            private set => SetProperty(ref _totalBeneficiaries, value);
        }

        public int LinkedCivilRegistryCount
        {
            get => _linkedCivilRegistryCount;
            private set => SetProperty(ref _linkedCivilRegistryCount, value);
        }

        public int SeniorCount
        {
            get => _seniorCount;
            private set => SetProperty(ref _seniorCount, value);
        }

        public int PwdCount
        {
            get => _pwdCount;
            private set => SetProperty(ref _pwdCount, value);
        }

        public int FilteredBeneficiaryCount
        {
            get => _filteredBeneficiaryCount;
            private set
            {
                if (SetProperty(ref _filteredBeneficiaryCount, value))
                {
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageIndicator));
                    OnPropertyChanged(nameof(PageSummary));
                    _previousPageCommand.RaiseCanExecuteChanged();
                    _nextPageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageIndicator));
                    OnPropertyChanged(nameof(PageSummary));
                    _previousPageCommand.RaiseCanExecuteChanged();
                    _nextPageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)Math.Max(FilteredBeneficiaryCount, 1) / SelectedPageSize));

        public string PageIndicator => $"Page {CurrentPage} of {TotalPages}";

        public string PageSummary
        {
            get
            {
                if (FilteredBeneficiaryCount == 0 || Beneficiaries.Count == 0)
                {
                    return "Showing 0 validated beneficiaries";
                }

                var start = ((CurrentPage - 1) * SelectedPageSize) + 1;
                var end = start + Beneficiaries.Count - 1;
                return $"Showing {start:N0}-{end:N0} of {FilteredBeneficiaryCount:N0} validated beneficiaries";
            }
        }

        public string SnapshotSourceSummary
        {
            get => _snapshotSourceSummary;
            private set => SetProperty(ref _snapshotSourceSummary, value);
        }

        public string LastUpdatedSummary
        {
            get => _lastUpdatedSummary;
            private set => SetProperty(ref _lastUpdatedSummary, value);
        }

        public ICommand RefreshCommand => _refreshCommand;

        public ICommand PreviousPageCommand => _previousPageCommand;

        public ICommand NextPageCommand => _nextPageCommand;

        internal Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            return LoadPageAsync(CurrentPage, cancellationToken);
        }

        internal Task GoToNextPageAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentPage >= TotalPages)
            {
                return Task.CompletedTask;
            }

            return LoadPageAsync(CurrentPage + 1, cancellationToken);
        }

        internal Task GoToPreviousPageAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentPage <= 1)
            {
                return Task.CompletedTask;
            }

            return LoadPageAsync(CurrentPage - 1, cancellationToken);
        }

        private async Task LoadPageAsync(int targetPage, CancellationToken cancellationToken)
        {
            _loadCts?.Cancel();
            var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadCts = loadCts;

            IsBusy = true;
            SetNeutralStatus($"Loading validated beneficiaries page {Math.Max(1, targetPage):N0}...");

            try
            {
                var result = await _queryService.LoadPageAsync(
                    new MasterListPageRequest
                    {
                        SearchText = SearchText.Trim(),
                        QuickFilter = SelectedQuickFilter,
                        PageNumber = Math.Max(1, targetPage),
                        PageSize = SelectedPageSize
                    },
                    loadCts.Token);

                if (loadCts.IsCancellationRequested)
                {
                    return;
                }

                _beneficiaries.Clear();
                foreach (var beneficiary in result.Beneficiaries)
                {
                    _beneficiaries.Add(beneficiary);
                }

                TotalBeneficiaries = result.TotalBeneficiaries;
                LinkedCivilRegistryCount = result.LinkedCivilRegistryCount;
                SeniorCount = result.SeniorCount;
                PwdCount = result.PwdCount;
                FilteredBeneficiaryCount = result.FilteredBeneficiaryCount;
                CurrentPage = Math.Max(1, targetPage);

                SnapshotSourceSummary = $"Validated beneficiaries snapshot from {result.SourceDatabase} on {result.SourceServer}";
                LastUpdatedSummary = result.LastUpdatedAt.HasValue
                    ? $"Last synced data: {result.LastUpdatedAt.Value:MMMM dd, yyyy hh:mm tt}"
                    : "Last synced data: unavailable";

                SelectedBeneficiary = _beneficiaries.FirstOrDefault();

                SetSuccessStatus(
                    FilteredBeneficiaryCount == 0
                        ? "No validated beneficiaries matched the current filters."
                        : $"Loaded {PageSummary.ToLowerInvariant()}.");
            }
            catch (OperationCanceledException) when (loadCts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (loadCts.IsCancellationRequested)
                {
                    return;
                }

                ClearResults();
                SetErrorStatus(ex.Message);
            }
            finally
            {
                if (ReferenceEquals(_loadCts, loadCts))
                {
                    _loadCts = null;
                    IsBusy = false;
                }

                loadCts.Dispose();
            }
        }

        private void QueueReloadFromFirstPage()
        {
            if (!_autoRefresh)
            {
                return;
            }

            _ = LoadPageAsync(1, CancellationToken.None);
        }

        private void ClearResults()
        {
            _beneficiaries.Clear();
            SelectedBeneficiary = null;
            TotalBeneficiaries = 0;
            LinkedCivilRegistryCount = 0;
            SeniorCount = 0;
            PwdCount = 0;
            FilteredBeneficiaryCount = 0;
            CurrentPage = 1;
            SnapshotSourceSummary = "Validated beneficiaries snapshot unavailable";
            LastUpdatedSummary = "Last synced data: unavailable";
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7A4A"));
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Models.DTOs;
using AttendanceShiftingManagement.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class AssistanceCaseManagementViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly BeneficiaryHistoryService _historyService;

        private string _searchQuery = string.Empty;
        private BeneficiarySearchResultDto? _selectedBeneficiary;
        private BeneficiarySummaryDto? _beneficiarySummary;
        
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalHistoryRecords;
        private int _totalSearchRecords;
        private bool _isLoading;
        private bool _isSearchLoading;

        // Status Bar
        private string _statusMessage = string.Empty;
        private string _statusType = string.Empty; // "Success", "Error", "Warning"
        private bool _isStatusVisible;

        // Manual Record Assistance
        private bool _isRecordAssistancePanelOpen;
        private string _recordAmount = string.Empty;
        private string _recordProgramName = string.Empty;
        private string _recordRemarks = string.Empty;
        private AssistanceReleaseKind _recordReleaseKind = AssistanceReleaseKind.Cash;

        public ObservableCollection<BeneficiarySearchResultDto> SearchResults { get; } = new();
        public ObservableCollection<BeneficiaryHistoryDto> HistoryRecords { get; } = new();

        public RelayCommand SearchCommand { get; }
        public RelayCommand LoadNextPageCommand { get; }
        public RelayCommand LoadPreviousPageCommand { get; }
        public RelayCommand LoadSearchNextPageCommand { get; }
        public RelayCommand LoadSearchPreviousPageCommand { get; }
        public RelayCommand OpenRecordAssistanceCommand { get; }
        public RelayCommand CloseRecordAssistanceCommand { get; }
        public RelayCommand SaveRecordAssistanceCommand { get; }
        public RelayCommand DismissStatusCommand { get; }

        public AssistanceCaseManagementViewModel(User currentUser, LocalDbContext context)
        {
            _currentUser = currentUser;
            _historyService = new BeneficiaryHistoryService(context);

            SearchCommand = new RelayCommand(async _ => await PerformSearchAsync());
            LoadNextPageCommand = new RelayCommand(async _ => await LoadHistoryPageAsync(_currentPage + 1), _ => CanLoadNextPage);
            LoadPreviousPageCommand = new RelayCommand(async _ => await LoadHistoryPageAsync(_currentPage - 1), _ => CanLoadPreviousPage);
            
            LoadSearchNextPageCommand = new RelayCommand(async _ => await LoadSearchPageAsync(SearchCurrentPage + 1), _ => CanLoadSearchNextPage);
            LoadSearchPreviousPageCommand = new RelayCommand(async _ => await LoadSearchPageAsync(SearchCurrentPage - 1), _ => CanLoadSearchPreviousPage);

            OpenRecordAssistanceCommand = new RelayCommand(_ => IsRecordAssistancePanelOpen = true, _ => SelectedBeneficiary != null);
            CloseRecordAssistanceCommand = new RelayCommand(_ => IsRecordAssistancePanelOpen = false);
            SaveRecordAssistanceCommand = new RelayCommand(async _ => await SaveRecordAssistanceAsync(), _ => CanSaveRecordAssistance());
            DismissStatusCommand = new RelayCommand(_ => IsStatusVisible = false);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsSearchLoading
        {
            get => _isSearchLoading;
            set => SetProperty(ref _isSearchLoading, value);
        }

        public BeneficiarySearchResultDto? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedBeneficiary, value))
                {
                    _ = LoadBeneficiaryDetailsAsync();
                    OpenRecordAssistanceCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public BeneficiarySummaryDto? BeneficiarySummary
        {
            get => _beneficiarySummary;
            private set => SetProperty(ref _beneficiarySummary, value);
        }

        public bool IsRecordAssistancePanelOpen
        {
            get => _isRecordAssistancePanelOpen;
            set => SetProperty(ref _isRecordAssistancePanelOpen, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string StatusType
        {
            get => _statusType;
            set => SetProperty(ref _statusType, value);
        }

        public bool IsStatusVisible
        {
            get => _isStatusVisible;
            set => SetProperty(ref _isStatusVisible, value);
        }

        public string RecordAmount
        {
            get => _recordAmount;
            set
            {
                SetProperty(ref _recordAmount, value);
                SaveRecordAssistanceCommand.RaiseCanExecuteChanged();
            }
        }

        public string RecordProgramName
        {
            get => _recordProgramName;
            set
            {
                SetProperty(ref _recordProgramName, value);
                SaveRecordAssistanceCommand.RaiseCanExecuteChanged();
            }
        }

        public string RecordRemarks
        {
            get => _recordRemarks;
            set
            {
                SetProperty(ref _recordRemarks, value);
                SaveRecordAssistanceCommand.RaiseCanExecuteChanged();
            }
        }

        public AssistanceReleaseKind RecordReleaseKind
        {
            get => _recordReleaseKind;
            set => SetProperty(ref _recordReleaseKind, value);
        }

        public IEnumerable<AssistanceReleaseKind> RecordReleaseKinds => Enum.GetValues<AssistanceReleaseKind>();

        // Pagination Properties
        public int TotalHistoryRecords
        {
            get => _totalHistoryRecords;
            set
            {
                SetProperty(ref _totalHistoryRecords, value);
                OnPropertyChanged(nameof(CanLoadNextPage));
                OnPropertyChanged(nameof(CanLoadPreviousPage));
                OnPropertyChanged(nameof(HistoryPageInfo));
            }
        }

        public string HistoryPageInfo => $"Page {_currentPage} of {Math.Max(1, (int)Math.Ceiling(TotalHistoryRecords / (double)_pageSize))}";
        public bool CanLoadNextPage => _currentPage * _pageSize < TotalHistoryRecords;
        public bool CanLoadPreviousPage => _currentPage > 1;

        private int _searchCurrentPage = 1;
        public int SearchCurrentPage
        {
            get => _searchCurrentPage;
            set => SetProperty(ref _searchCurrentPage, value);
        }

        public int TotalSearchRecords
        {
            get => _totalSearchRecords;
            set
            {
                SetProperty(ref _totalSearchRecords, value);
                OnPropertyChanged(nameof(CanLoadSearchNextPage));
                OnPropertyChanged(nameof(CanLoadSearchPreviousPage));
                OnPropertyChanged(nameof(SearchPageInfo));
            }
        }

        public string SearchPageInfo => $"Page {SearchCurrentPage} of {Math.Max(1, (int)Math.Ceiling(TotalSearchRecords / (double)_pageSize))}";
        public bool CanLoadSearchNextPage => SearchCurrentPage * _pageSize < TotalSearchRecords;
        public bool CanLoadSearchPreviousPage => SearchCurrentPage > 1;

        private async Task PerformSearchAsync()
        {
            try
            {
                await LoadSearchPageAsync(1);
            }
            catch (Exception ex)
            {
                ShowStatus($"Search failed: {ex.Message}", "Error");
            }
        }

        private async Task LoadSearchPageAsync(int page)
        {
            if (IsSearchLoading) return;
            IsSearchLoading = true;

            try
            {
                SearchCurrentPage = page;
                var result = await _historyService.SearchBeneficiariesAsync(SearchQuery, SearchCurrentPage, _pageSize);
                
                SearchResults.Clear();
                foreach (var item in result.Items)
                {
                    SearchResults.Add(item);
                }

                TotalSearchRecords = result.TotalCount;
            }
            catch (Exception ex)
            {
                ShowStatus($"Search error: {ex.Message}", "Error");
            }
            finally
            {
                IsSearchLoading = false;
            }
        }

        private async Task LoadBeneficiaryDetailsAsync()
        {
            if (SelectedBeneficiary == null)
            {
                BeneficiarySummary = null;
                HistoryRecords.Clear();
                return;
            }

            IsLoading = true;
            try
            {
                // Load summary
                BeneficiarySummary = await _historyService.GetBeneficiarySummaryAsync(SelectedBeneficiary.CivilRegistryId, SelectedBeneficiary.BeneficiaryId);

                // Load first page of history
                await LoadHistoryPageAsync(1);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load beneficiary details: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadHistoryPageAsync(int page)
        {
            if (SelectedBeneficiary == null) return;
            
            _currentPage = page;
            var result = await _historyService.GetBeneficiaryHistoryAsync(SelectedBeneficiary.CivilRegistryId, SelectedBeneficiary.BeneficiaryId, _currentPage, _pageSize);
            
            HistoryRecords.Clear();
            foreach (var item in result.Items)
            {
                HistoryRecords.Add(item);
            }

            TotalHistoryRecords = result.TotalCount;
        }

        private bool CanSaveRecordAssistance()
        {
            return !string.IsNullOrWhiteSpace(RecordAmount) && 
                   decimal.TryParse(RecordAmount, out var amt) && amt > 0 &&
                   !string.IsNullOrWhiteSpace(RecordProgramName) &&
                   !string.IsNullOrWhiteSpace(RecordRemarks);
        }

        private void ShowStatus(string message, string type)
        {
            StatusMessage = message;
            StatusType = type;
            IsStatusVisible = true;
        }

        private async Task SaveRecordAssistanceAsync()
        {
            if (SelectedBeneficiary == null || !CanSaveRecordAssistance()) return;

            IsLoading = true;
            try
            {
                var (success, errorMessage) = await _historyService.RecordManualAssistanceAsync(
                    SelectedBeneficiary.BeneficiaryId,
                    SelectedBeneficiary.CivilRegistryId,
                    decimal.Parse(RecordAmount),
                    RecordReleaseKind,
                    RecordProgramName,
                    RecordRemarks,
                    _currentUser.Id
                );

                if (success)
                {
                    IsRecordAssistancePanelOpen = false;
                    RecordAmount = string.Empty;
                    RecordProgramName = string.Empty;
                    RecordRemarks = string.Empty;

                    ShowStatus($"Assistance recorded successfully for {SelectedBeneficiary.DisplayName}.", "Success");

                    // Reload to reflect new record
                    await LoadBeneficiaryDetailsAsync();
                }
                else
                {
                    ShowStatus(errorMessage ?? "Failed to record assistance. Please try again.", "Error");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Unexpected error: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

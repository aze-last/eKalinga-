using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class AssistanceAnalyticsItem : ObservableObject
    {
        private string _keyword = string.Empty;
        private int _caseCount;
        private decimal _totalSpent;

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public int CaseCount
        {
            get => _caseCount;
            set => SetProperty(ref _caseCount, value);
        }

        public decimal TotalSpent
        {
            get => _totalSpent;
            set => SetProperty(ref _totalSpent, value);
        }
    }

    public sealed class AssistanceCaseManagementViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly ObservableCollection<AssistanceCaseListItem> _cases = new();
        private readonly ObservableCollection<AssistanceValidatedBeneficiaryOption> _validatedBeneficiaries = new();
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _newCaseCommand;
        private readonly RelayCommand _saveCaseCommand;
        private readonly RelayCommand _markUnderReviewCommand;
        private readonly RelayCommand _approveCommand;
        private readonly RelayCommand _releaseCommand;
        private readonly RelayCommand _closeCommand;
        private readonly RelayCommand _rejectCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _fastTrackReleaseCommand;
        private readonly RelayCommand _deleteCommand;
        private readonly RelayCommand _showListCommand;
        private readonly RelayCommand _showAllCasesCommand;
        private readonly RelayCommand _showPendingCasesCommand;
        private readonly RelayCommand _showUnderReviewCasesCommand;
        private readonly RelayCommand _showApprovedCasesCommand;
        private readonly RelayCommand _showReleasedCasesCommand;
        private readonly RelayCommand _showRejectedCasesCommand;
        private readonly RelayCommand _selectCaseCommand;
        private readonly RelayCommand _exportCasesCommand;
        private readonly RelayCommand _openCasePanelCommand;
        private readonly RelayCommand _closeCasePanelCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _previousPageCommand;
        private readonly RelayCommand _toggleAnalyticsCommand;
        private readonly RelayCommand _navigatePreviousCommand;
        private readonly RelayCommand _navigateNextCommand;
        private ICollectionView _casesView;
        private AssistanceCaseListItem? _selectedCase;
        private AssistanceValidatedBeneficiaryOption? _selectedValidatedBeneficiary;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "All";
        private bool _isBusy;
        private string _statusMessage = "Loading aid requests...";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private int _totalCases;
        private int _pendingCount;
        private int _underReviewCount;
        private int _approvedCount;
        private int _releasedCount;
        private int _closedCount;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        private int _totalFilteredCount;
        private int _currentIndex = -1;
        private decimal _budgetCapRemaining;
        private bool _isAnalyticsVisible;
        private string _editableCaseNumber = "New aid request";
        private AssistanceReleaseKind _selectedReleaseKind = AssistanceReleaseKind.Cash;
        private AssistanceCasePriority _selectedPriority = AssistanceCasePriority.Medium;
        private string _editableAssistanceType = string.Empty;
        private string _editableAssistanceAmount = string.Empty;
        private DateTime _requestedOnDate = DateTime.Today;
        private DateTime? _scheduledReleaseDate;
        private string _editableSummary = string.Empty;
        private string _editableResolutionNotes = string.Empty;
        private bool _isCasePanelOpen;
        private string _validatedBeneficiaryErrorMessage = string.Empty;
        private string _assistanceAmountErrorMessage = string.Empty;
        private string? _lookupScannerSessionUrl;
        private string? _lookupScannerSessionPin;
        private string? _lookupScannerSessionExpiresAtText;
        private ImageSource? _lookupScannerQrImage;
        private readonly RelayCommand _createLookupScannerSessionCommand;

        private bool _isPcScannerOpen;
        private AssistanceValidatedBeneficiaryOption? _scannedBeneficiary;
        private string? _scannedBeneficiaryStatus;
        private BitmapSource? _scannedBeneficiaryPhoto;
        private bool _isScannedResultVisible;
        private string? _lastScannedPayload;
        private string _scannerActionLabel = "LOAD BENEFICIARY";

        private readonly RelayCommand _openPcScannerCommand;
        private readonly RelayCommand _processPcScanCommand;
        private readonly RelayCommand _confirmScannedClaimCommand;
        private readonly RelayCommand _cancelScannedClaimCommand;

        public AssistanceCaseManagementViewModel(User currentUser)
        {
            _currentUser = currentUser;
            ScannerActionLabel = "LOAD BENEFICIARY";
            StatusFilters = new ObservableCollection<string>
            {
                "All",
                "Pending",
                "Under review",
                "Approved",
                "Released",
                "Closed",
                "Rejected",
                "Cancelled"
            };

            AssistanceKeywords = new ObservableCollection<string>
            {
                "Medical Assistance",
                "Burial Assistance",
                "Educational Assistance",
                "Food Assistance",
                "Financial Assistance",
                "Emergency Shelter Assistance",
                "Transportation Assistance",
                "Livelihood Assistance"
            };

            PriorityOptions = new ObservableCollection<AssistanceCasePriority>(Enum.GetValues<AssistanceCasePriority>());
            ReleaseKindOptions = new ObservableCollection<AssistanceReleaseKind>(Enum.GetValues<AssistanceReleaseKind>());
            AnalyticsList = new ObservableCollection<AssistanceAnalyticsItem>();

            _casesView = CollectionViewSource.GetDefaultView(_cases);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _newCaseCommand = new RelayCommand(_ => BeginNewCaseAndOpen(), _ => !IsBusy);
            _saveCaseCommand = new RelayCommand(async _ => await SaveCaseAsync(), _ => CanSaveCase());
            _markUnderReviewCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.UnderReview, "under review"), _ => CanChangeStatus(AssistanceCaseStatus.UnderReview));
            _approveCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Approved, "approved"), _ => CanChangeStatus(AssistanceCaseStatus.Approved));
            _releaseCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Released, "released"), _ => CanChangeStatus(AssistanceCaseStatus.Released));
            _closeCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Closed, "closed"), _ => CanChangeStatus(AssistanceCaseStatus.Closed));
            _rejectCommand = new RelayCommand(async _ => await RejectCaseAsync(), _ => CanChangeStatus(AssistanceCaseStatus.Rejected));
            _cancelCommand = new RelayCommand(async _ => await CancelCaseAsync(), _ => CanChangeStatus(AssistanceCaseStatus.Cancelled));
            _fastTrackReleaseCommand = new RelayCommand(async _ => await FastTrackReleaseAsync(), _ => CanFastTrackRelease());
            _deleteCommand = new RelayCommand(async _ => await DeleteCaseAsync(), _ => CanDeleteCase());
            _showListCommand = new RelayCommand(_ => ShowList(), _ => !IsBusy);
            _showAllCasesCommand = new RelayCommand(_ => SetStatusFilter("All"), _ => !IsBusy);
            _showPendingCasesCommand = new RelayCommand(_ => SetStatusFilter("Pending"), _ => !IsBusy);
            _showUnderReviewCasesCommand = new RelayCommand(_ => SetStatusFilter("Under review"), _ => !IsBusy);
            _showApprovedCasesCommand = new RelayCommand(_ => SetStatusFilter("Approved"), _ => !IsBusy);
            _showReleasedCasesCommand = new RelayCommand(_ => SetStatusFilter("Released"), _ => !IsBusy);
            _showRejectedCasesCommand = new RelayCommand(_ => SetStatusFilter("Rejected"), _ => !IsBusy);
            _selectCaseCommand = new RelayCommand(parameter => SelectCase(parameter as AssistanceCaseListItem), _ => !IsBusy);
            _exportCasesCommand = new RelayCommand(_ => ExportCases(), _ => CanExportCases());
            _openCasePanelCommand = new RelayCommand(parameter => OpenCasePanel(parameter as AssistanceCaseListItem), _ => CanOpenCasePanel());
            _closeCasePanelCommand = new RelayCommand(_ => CloseCasePanel(), _ => IsCasePanelOpen);
            _nextPageCommand = new RelayCommand(_ => NextPage(), _ => CanNextPage());
            _previousPageCommand = new RelayCommand(_ => PreviousPage(), _ => CanPreviousPage());
            _toggleAnalyticsCommand = new RelayCommand(async _ => await ToggleAnalyticsAsync());
            _createLookupScannerSessionCommand = new RelayCommand(async _ => await CreateLookupScannerSessionAsync(), _ => !IsBusy);
            _navigatePreviousCommand = new RelayCommand(_ => NavigatePrevious(), _ => CanNavigatePrevious());
            _navigateNextCommand = new RelayCommand(_ => NavigateNext(), _ => CanNavigateNext());

            _openPcScannerCommand = new RelayCommand(_ => IsPcScannerOpen = true, _ => !IsBusy && CanEditSelectedCase);
            _processPcScanCommand = new RelayCommand(payload => _ = ExecuteProcessPcScan(payload as string));
            _confirmScannedClaimCommand = new RelayCommand(_ => ExecuteConfirmScannedLookup(), _ => !IsBusy && ScannedBeneficiary != null);
            _cancelScannedClaimCommand = new RelayCommand(_ => ResetScannedResult());

            ApplyFilter();
            _ = LoadAsync();
            _ = LoadValidatedBeneficiariesAsync();
        }

        public ObservableCollection<string> StatusFilters { get; }

        public ObservableCollection<AssistanceCasePriority> PriorityOptions { get; }

        public ObservableCollection<AssistanceReleaseKind> ReleaseKindOptions { get; }

        public ObservableCollection<AssistanceValidatedBeneficiaryOption> ValidatedBeneficiaries => _validatedBeneficiaries;

        public ObservableCollection<AssistanceAnalyticsItem> AnalyticsList { get; }

        public string? LookupScannerSessionUrl
        {
            get => _lookupScannerSessionUrl;
            private set => SetProperty(ref _lookupScannerSessionUrl, value);
        }

        public string? LookupScannerSessionPin
        {
            get => _lookupScannerSessionPin;
            private set => SetProperty(ref _lookupScannerSessionPin, value);
        }

        public string? LookupScannerSessionExpiresAtText
        {
            get => _lookupScannerSessionExpiresAtText;
            private set => SetProperty(ref _lookupScannerSessionExpiresAtText, value);
        }

        public ImageSource? LookupScannerQrImage
        {
            get => _lookupScannerQrImage;
            private set => SetProperty(ref _lookupScannerQrImage, value);
        }

        public ICommand CreateLookupScannerSessionCommand => _createLookupScannerSessionCommand;

        public ICollectionView CasesView
        {
            get => _casesView;
            private set => SetProperty(ref _casesView, value);
        }

        public AssistanceCaseListItem? SelectedCase
        {
            get => _selectedCase;
            set
            {
                if (SetProperty(ref _selectedCase, value))
                {
                    SyncEditorFromSelection();
                    UpdateCurrentIndex();
                    OnPropertyChanged(nameof(IsSelectedCaseLocked));
                    OnPropertyChanged(nameof(CanEditSelectedCase));
                    OnPropertyChanged(nameof(SelectedCaseLockMessage));
                    OnPropertyChanged(nameof(SelectedCaseNoticeBrush));
                    OnPropertyChanged(nameof(SelectedCaseNoticeBackgroundBrush));
                    OnPropertyChanged(nameof(CurrentPosition));
                    RaiseCommandStates();
                }
            }
        }

        private void UpdateCurrentIndex()
        {
            if (SelectedCase == null || CasesView == null)
            {
                _currentIndex = -1;
                return;
            }

            var list = CasesView.Cast<AssistanceCaseListItem>().ToList();
            _currentIndex = list.FindIndex(item => item.Id == SelectedCase.Id);
        }

        private bool CanNavigatePrevious() => _currentIndex > 0;

        private void NavigatePrevious()
        {
            if (!CanNavigatePrevious()) return;
            var list = CasesView.Cast<AssistanceCaseListItem>().ToList();
            SelectedCase = list[_currentIndex - 1];
        }

        private bool CanNavigateNext() => CasesView != null && _currentIndex >= 0 && _currentIndex < CasesView.Cast<AssistanceCaseListItem>().Count() - 1;

        private void NavigateNext()
        {
            if (!CanNavigateNext()) return;
            var list = CasesView.Cast<AssistanceCaseListItem>().ToList();
            SelectedCase = list[_currentIndex + 1];
        }

        public AssistanceValidatedBeneficiaryOption? SelectedValidatedBeneficiary
        {
            get => _selectedValidatedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedValidatedBeneficiary, value))
                {
                    ClearValidatedBeneficiaryError();
                    OnPropertyChanged(nameof(ApplicantSummary));
                    RaiseCommandStates();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (SetProperty(ref _selectedStatusFilter, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
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
                    RaiseCommandStates();
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

        public int TotalCases
        {
            get => _totalCases;
            private set => SetProperty(ref _totalCases, value);
        }

        public int PendingCount
        {
            get => _pendingCount;
            private set => SetProperty(ref _pendingCount, value);
        }

        public int UnderReviewCount
        {
            get => _underReviewCount;
            private set => SetProperty(ref _underReviewCount, value);
        }

        public int ApprovedCount
        {
            get => _approvedCount;
            private set => SetProperty(ref _approvedCount, value);
        }

        public int ReleasedCount
        {
            get => _releasedCount;
            private set => SetProperty(ref _releasedCount, value);
        }

        public int ClosedCount
        {
            get => _closedCount;
            private set => SetProperty(ref _closedCount, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    ApplyFilter();
                    RaiseCommandStates();
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set => SetProperty(ref _totalPages, value);
        }

        public int TotalFilteredCount
        {
            get => _totalFilteredCount;
            private set => SetProperty(ref _totalFilteredCount, value);
        }

        public decimal BudgetCapRemaining
        {
            get => _budgetCapRemaining;
            private set => SetProperty(ref _budgetCapRemaining, value);
        }

        public bool IsAnalyticsVisible
        {
            get => _isAnalyticsVisible;
            private set
            {
                if (SetProperty(ref _isAnalyticsVisible, value))
                {
                    OnPropertyChanged(nameof(ListVisibility));
                    OnPropertyChanged(nameof(AnalyticsVisibility));
                }
            }
        }

        public Visibility ListVisibility => IsAnalyticsVisible ? Visibility.Collapsed : Visibility.Visible;
        public Visibility AnalyticsVisibility => IsAnalyticsVisible ? Visibility.Visible : Visibility.Collapsed;

        public string EditableCaseNumber
        {
            get => _editableCaseNumber;
            private set => SetProperty(ref _editableCaseNumber, value);
        }

        public string EditableAssistanceType
        {
            get => _editableAssistanceType;
            set
            {
                if (SetProperty(ref _editableAssistanceType, value))
                {
                    OnPropertyChanged(nameof(ResolvedAssistanceType));
                }
            }
        }

        public AssistanceReleaseKind SelectedReleaseKind
        {
            get => _selectedReleaseKind;
            set
            {
                if (SetProperty(ref _selectedReleaseKind, value))
                {
                    OnPropertyChanged(nameof(ReleaseKindSummary));
                    
                    // If the user hasn't typed a custom keyword yet, we can provide a smart default
                    // but it remains editable as requested.
                    if (string.IsNullOrWhiteSpace(EditableAssistanceType))
                    {
                        OnPropertyChanged(nameof(ResolvedAssistanceType));
                    }
                }
            }
        }

        public AssistanceCasePriority SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        public string EditableAssistanceAmount
        {
            get => _editableAssistanceAmount;
            set
            {
                if (SetProperty(ref _editableAssistanceAmount, value))
                {
                    ValidateAssistanceAmount(requireValue: false);
                    OnPropertyChanged(nameof(SelectedCase));
                }
            }
        }

        public DateTime RequestedOnDate
        {
            get => _requestedOnDate;
            set => SetProperty(ref _requestedOnDate, value);
        }

        public DateTime? ScheduledReleaseDate
        {
            get => _scheduledReleaseDate;
            set => SetProperty(ref _scheduledReleaseDate, value);
        }

        public string EditableSummary
        {
            get => _editableSummary;
            set => SetProperty(ref _editableSummary, value);
        }

        public string EditableResolutionNotes
        {
            get => _editableResolutionNotes;
            set => SetProperty(ref _editableResolutionNotes, value);
        }

        private bool _isBrowsePanelOpen;

        public bool IsAnyOverlayOpen => IsCasePanelOpen || IsBrowsePanelOpen;

        public bool IsBrowsePanelOpen
        {
            get => _isBrowsePanelOpen;
            set
            {
                if (SetProperty(ref _isBrowsePanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public bool IsCasePanelOpen
        {
            get => _isCasePanelOpen;
            private set
            {
                if (SetProperty(ref _isCasePanelOpen, value))
                {
                    _openCasePanelCommand.RaiseCanExecuteChanged();
                    _closeCasePanelCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public string ApplicantSummary =>
            SelectedValidatedBeneficiary?.DisplayLabel
            ?? "Select a validated beneficiary";

        public string ReleaseKindSummary => $"Release kind: {SelectedReleaseKind}";

        public string ResolvedAssistanceType => ResolveAssistanceType();

        public string ValidatedBeneficiaryErrorMessage => _validatedBeneficiaryErrorMessage;

        public bool HasValidatedBeneficiaryError => !string.IsNullOrWhiteSpace(ValidatedBeneficiaryErrorMessage);

        public Brush ValidatedBeneficiaryBorderBrush => GetFieldBorderBrush(HasValidatedBeneficiaryError);

        public Brush ValidatedBeneficiaryBackgroundBrush => GetFieldBackgroundBrush(HasValidatedBeneficiaryError);

        public string AssistanceAmountErrorMessage => _assistanceAmountErrorMessage;

        public bool HasAssistanceAmountError => !string.IsNullOrWhiteSpace(AssistanceAmountErrorMessage);

        public Brush AssistanceAmountBorderBrush => GetFieldBorderBrush(HasAssistanceAmountError);

        public Brush AssistanceAmountBackgroundBrush => GetFieldBackgroundBrush(HasAssistanceAmountError);

        public bool IsSelectedCaseLocked =>
            SelectedCase?.Status is AssistanceCaseStatus.Released
                or AssistanceCaseStatus.Rejected
                or AssistanceCaseStatus.Closed
                or AssistanceCaseStatus.Cancelled;

        public bool CanEditSelectedCase => !IsSelectedCaseLocked;

        public string SelectedCaseLockMessage => SelectedCase switch
        {
            null => "Select or create a request to continue.",
            { Status: AssistanceCaseStatus.Released } => "This request can no longer be edited because it is already Released.",
            { Status: AssistanceCaseStatus.Rejected } => "This request can no longer be edited because it is already Rejected.",
            { Status: AssistanceCaseStatus.Closed } => "This request can no longer be edited because it is already Closed.",
            { Status: AssistanceCaseStatus.Cancelled } => "This request can no longer be edited because it is already Cancelled.",
            _ => "Update the form and continue the request workflow from this panel."
        };

        public Brush SelectedCaseNoticeBrush =>
            IsSelectedCaseLocked
                ? CreateBrush("#991B1B")
                : CreateBrush("#475569");

        public Brush SelectedCaseNoticeBackgroundBrush =>
            IsSelectedCaseLocked
                ? CreateBrush("#FEE2E2")
                : CreateBrush("#F8FAFC");

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand NewCaseCommand => _newCaseCommand;
        public ICommand SaveCaseCommand => _saveCaseCommand;
        public ICommand MarkUnderReviewCommand => _markUnderReviewCommand;
        public ICommand ApproveCommand => _approveCommand;
        public ICommand ReleaseCommand => _releaseCommand;
        public ICommand CloseCommand => _closeCommand;
        public ICommand RejectCommand => _rejectCommand;
        public ICommand CancelCommand => _cancelCommand;
        public ICommand FastTrackReleaseCommand => _fastTrackReleaseCommand;
        public ICommand DeleteCommand => _deleteCommand;
        public ICommand ShowListCommand => _showListCommand;
        public ICommand ShowAllCasesCommand => _showAllCasesCommand;
        public ICommand ShowPendingCasesCommand => _showPendingCasesCommand;
        public ICommand ShowUnderReviewCasesCommand => _showUnderReviewCasesCommand;
        public ICommand ShowApprovedCasesCommand => _showApprovedCasesCommand;
        public ICommand ShowReleasedCasesCommand => _showReleasedCasesCommand;
        public ICommand ShowRejectedCasesCommand => _showRejectedCasesCommand;
        public ICommand SelectCaseCommand => _selectCaseCommand;
        public ICommand ExportCasesCommand => _exportCasesCommand;
        public ICommand OpenCasePanelCommand => _openCasePanelCommand;
        public ICommand CloseCasePanelCommand => _closeCasePanelCommand;
        public ICommand NextPageCommand => _nextPageCommand;
        public ICommand PreviousPageCommand => _previousPageCommand;
        public ICommand ToggleAnalyticsCommand => _toggleAnalyticsCommand;
        public ICommand NavigatePreviousCommand => _navigatePreviousCommand;
        public ICommand NavigateNextCommand => _navigateNextCommand;
        public ICommand OpenPcScannerCommand => _openPcScannerCommand;
        public ICommand ProcessPcScanCommand => _processPcScanCommand;
        public ICommand ConfirmScannedClaimCommand => _confirmScannedClaimCommand;
        public ICommand CancelScannedClaimCommand => _cancelScannedClaimCommand;

        public bool IsPcScannerOpen
        {
            get => _isPcScannerOpen;
            set
            {
                if (SetProperty(ref _isPcScannerOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public AssistanceValidatedBeneficiaryOption? ScannedBeneficiary
        {
            get => _scannedBeneficiary;
            private set
            {
                if (SetProperty(ref _scannedBeneficiary, value))
                {
                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? ScannedBeneficiaryStatus
        {
            get => _scannedBeneficiaryStatus;
            private set => SetProperty(ref _scannedBeneficiaryStatus, value);
        }

        public BitmapSource? ScannedBeneficiaryPhoto
        {
            get => _scannedBeneficiaryPhoto;
            private set => SetProperty(ref _scannedBeneficiaryPhoto, value);
        }

        public bool IsScannedResultVisible
        {
            get => _isScannedResultVisible;
            private set => SetProperty(ref _isScannedResultVisible, value);
        }

        public string ScannerActionLabel
        {
            get => _scannerActionLabel;
            set => SetProperty(ref _scannerActionLabel, value);
        }

        public string ScannerHeader => "Beneficiary Search";
        public string ScannerDescription => "Scan ID to find and load beneficiary details.";

        public ObservableCollection<string> AssistanceKeywords { get; }

        public string CurrentPosition
        {
            get
            {
                if (_currentIndex < 0 || TotalFilteredCount == 0) return "0 / 0";
                return $"{_currentIndex + 1} / {TotalFilteredCount}";
            }
        }

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading aid requests...");

            try
            {
                await LoadCoreAsync(SelectedCase?.Id);
                if (IsAnalyticsVisible)
                {
                    await LoadAnalyticsAsync();
                }
                SetSuccessStatus($"Loaded {TotalCases:N0} aid request(s).");
            }
            catch (Exception ex)
            {
                ClearLoadedState();
                SetErrorStatus($"Unable to load aid requests: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadCoreAsync(int? preferredCaseId)
        {
            var preferredValidatedBeneficiary = SelectedValidatedBeneficiary;

            await using var context = new AppDbContext();

            var assistanceCases = await context.AssistanceCases
                .AsNoTracking()
                .Include(item => item.AssistanceCaseBudget)
                .OrderByDescending(item => item.RequestedOn)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();

            _cases.Clear();
            foreach (var assistanceCase in assistanceCases)
            {
                _cases.Add(AssistanceCaseListItem.FromEntity(assistanceCase));
            }

            UpdateCounts();

            CasesView = CollectionViewSource.GetDefaultView(_cases);
            ApplyFilter();

            SelectedCase = _cases.FirstOrDefault(item => item.Id == preferredCaseId);

            if (!string.IsNullOrWhiteSpace(SelectedCase?.ValidatedBeneficiaryName))
            {
                var validatedBeneficiary = new AssistanceValidatedBeneficiaryOption(
                    SelectedCase.ValidatedBeneficiaryName,
                    SelectedCase.ValidatedBeneficiaryId,
                    SelectedCase.ValidatedCivilRegistryId);

                if (!_validatedBeneficiaries.Any(item => item.Matches(validatedBeneficiary)))
                {
                    _validatedBeneficiaries.Insert(0, validatedBeneficiary);
                }

                SelectedValidatedBeneficiary = _validatedBeneficiaries.FirstOrDefault(item => item.Matches(validatedBeneficiary))
                    ?? validatedBeneficiary;
            }
            else if (SelectedCase != null)
            {
                SelectedValidatedBeneficiary = null;
            }
            else
            {
                SelectedValidatedBeneficiary = preferredValidatedBeneficiary != null
                    ? _validatedBeneficiaries.FirstOrDefault(item => item.Matches(preferredValidatedBeneficiary))
                        ?? preferredValidatedBeneficiary
                    : null;
            }

            if (SelectedCase == null)
            {
                BeginNewCase();
            }
        }

        private void UpdateCounts()
        {
            TotalCases = _cases.Count;
            PendingCount = _cases.Count(item => item.Status == AssistanceCaseStatus.Pending);
            UnderReviewCount = _cases.Count(item => item.Status == AssistanceCaseStatus.UnderReview);
            ApprovedCount = _cases.Count(item => item.Status == AssistanceCaseStatus.Approved);
            ReleasedCount = _cases.Count(item => item.Status == AssistanceCaseStatus.Released);
            ClosedCount = _cases.Count(item => item.Status is AssistanceCaseStatus.Closed or AssistanceCaseStatus.Rejected or AssistanceCaseStatus.Cancelled);
            
            _ = UpdateBudgetCapRemainingAsync();
        }

        private async Task UpdateBudgetCapRemainingAsync()
        {
            try
            {
                await using var context = new AppDbContext();
                
                var globalBudget = await context.AssistanceCaseBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BudgetCode == "GLOBAL_AID_BUDGET" && b.IsActive);

                if (globalBudget == null)
                {
                    BudgetCapRemaining = 0m;
                    return;
                }

                var totalCap = globalBudget.BudgetCap ?? 0m;

                var totalSpent = await context.BudgetLedgerEntries
                    .AsNoTracking()
                    .Where(e => e.EntryType == BudgetLedgerEntryType.Release && e.AssistanceCaseBudgetId == globalBudget.Id)
                    .SumAsync(e => (decimal?)e.TotalAmount) ?? 0m;

                BudgetCapRemaining = Math.Max(0m, totalCap - totalSpent);
            }
            catch
            {
                BudgetCapRemaining = 0m;
            }
        }

        private async Task ToggleAnalyticsAsync()
        {
            IsAnalyticsVisible = !IsAnalyticsVisible;
            if (IsAnalyticsVisible)
            {
                await LoadAnalyticsAsync();
            }
        }

        private async Task ExecuteProcessPcScan(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return;
            
            IsBusy = true;
            SetNeutralStatus("Analyzing ID card...");

            try
            {
                await using var context = new AppDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);
                var lookup = await digitalIdService.LookupByQrPayloadAsync(payload);

                if (lookup == null)
                {
                    SetErrorStatus("Invalid QR code or beneficiary not found.");
                    return;
                }

                ScannedBeneficiary = new AssistanceValidatedBeneficiaryOption(
                    lookup.FullName,
                    lookup.BeneficiaryId,
                    lookup.CivilRegistryId);

                ScannedBeneficiaryStatus = "Found in Validated Masterlist. Click to load into current request.";
                ScannedBeneficiaryPhoto = string.IsNullOrWhiteSpace(lookup.PhotoPath) ? null : LocalImageLoader.Load(lookup.PhotoPath) as BitmapSource;
                
                _lastScannedPayload = payload;
                IsScannedResultVisible = true;
                SetNeutralStatus($"ID analyzed: {lookup.FullName}. Please review and confirm.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Scan analysis error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteConfirmScannedLookup()
        {
            if (ScannedBeneficiary == null) return;

            // 1. Reset/Prepare a new case first to ensure a clean form
            BeginNewCase();

            // 2. Ensure the beneficiary is in the lookup list
            if (!_validatedBeneficiaries.Any(item => item.Matches(ScannedBeneficiary)))
            {
                _validatedBeneficiaries.Insert(0, ScannedBeneficiary);
            }

            // 3. Select the scanned beneficiary in the form
            SelectedValidatedBeneficiary = _validatedBeneficiaries.FirstOrDefault(item => item.Matches(ScannedBeneficiary))
                ?? ScannedBeneficiary;

            // 4. Open the panel so the user can immediately see the pre-filled form
            OpenCasePanel(force: true);

            ResetScannedResult();
            IsPcScannerOpen = false;
            SetSuccessStatus($"Beneficiary assigned: {SelectedValidatedBeneficiary.FullName}. Form ready.");
        }

        private void ResetScannedResult()
        {
            ScannedBeneficiary = null;
            ScannedBeneficiaryStatus = null;
            ScannedBeneficiaryPhoto = null;
            _lastScannedPayload = null;
            IsScannedResultVisible = false;
        }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                await using var context = new AppDbContext();
                var releasedCases = await context.AssistanceCases
                    .AsNoTracking()
                    .Where(c => c.Status == AssistanceCaseStatus.Released)
                    .ToListAsync();

                var groupedData = releasedCases
                    .GroupBy(c => c.AssistanceType.Trim().ToUpperInvariant())
                    .Select(g => new AssistanceAnalyticsItem
                    {
                        Keyword = g.Key,
                        CaseCount = g.Count(),
                        TotalSpent = g.Sum(c => c.ApprovedAmount ?? 0m)
                    })
                    .OrderByDescending(i => i.TotalSpent)
                    .ToList();

                AnalyticsList.Clear();
                foreach (var item in groupedData)
                {
                    AnalyticsList.Add(item);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load analytics: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var filteredItems = _cases.Where(assistanceCase =>
            {
                if (SelectedStatusFilter != "All" &&
                    !string.Equals(assistanceCase.StatusText, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                return Contains(assistanceCase.CaseNumber, SearchText)
                    || Contains(assistanceCase.AssistanceType, SearchText)
                    || Contains(assistanceCase.ReleaseKindText, SearchText)
                    || Contains(assistanceCase.RecipientLabel, SearchText)
                    || Contains(assistanceCase.Summary, SearchText);
            }).ToList();

            TotalFilteredCount = filteredItems.Count;
            TotalPages = (int)Math.Max(1, Math.Ceiling((double)TotalFilteredCount / PageSize));

            if (CurrentPage > TotalPages)
            {
                _currentPage = TotalPages;
                OnPropertyChanged(nameof(CurrentPage));
            }

            var pagedItems = filteredItems
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            CasesView = CollectionViewSource.GetDefaultView(pagedItems);
            _exportCasesCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
            _previousPageCommand.RaiseCanExecuteChanged();
            _navigatePreviousCommand.RaiseCanExecuteChanged();
            _navigateNextCommand.RaiseCanExecuteChanged();
        }

        private bool CanNextPage() => CurrentPage < TotalPages;
        private void NextPage() => CurrentPage++;

        private bool CanPreviousPage() => CurrentPage > 1;
        private void PreviousPage() => CurrentPage--;
        private void BeginNewCase()
        {
            SelectedCase = null;
            EditableCaseNumber = "New aid request";
            SelectedReleaseKind = AssistanceReleaseKind.Cash;
            SelectedPriority = AssistanceCasePriority.Medium;
            EditableAssistanceType = string.Empty;
            EditableAssistanceAmount = string.Empty;
            RequestedOnDate = DateTime.Today;
            ScheduledReleaseDate = null;
            EditableSummary = string.Empty;
            EditableResolutionNotes = string.Empty;
            SelectedValidatedBeneficiary = null;
            ClearFieldErrors();
            OnPropertyChanged(nameof(ResolvedAssistanceType));
            RaiseCommandStates();
            OnPropertyChanged(nameof(ApplicantSummary));
        }

        private void BeginNewCaseAndOpen()
        {
            BeginNewCase();
            OpenCasePanel(force: true);
        }

        private void SyncEditorFromSelection()
        {
            if (SelectedCase == null)
            {
                OnPropertyChanged(nameof(ApplicantSummary));
                return;
            }

            EditableCaseNumber = SelectedCase.CaseNumber;
            SelectedReleaseKind = SelectedCase.ReleaseKind;
            SelectedPriority = SelectedCase.Priority;
            EditableAssistanceType = SelectedCase.AssistanceType;
            EditableAssistanceAmount = SelectedCase.AssistanceAmountText;
            RequestedOnDate = SelectedCase.RequestedOn;
            ScheduledReleaseDate = SelectedCase.ScheduledReleaseDate;
            EditableSummary = SelectedCase.Summary;
            EditableResolutionNotes = SelectedCase.ResolutionNotes;

            if (!string.IsNullOrWhiteSpace(SelectedCase.ValidatedBeneficiaryName))
            {
                var validatedBeneficiary = new AssistanceValidatedBeneficiaryOption(
                    SelectedCase.ValidatedBeneficiaryName,
                    SelectedCase.ValidatedBeneficiaryId,
                    SelectedCase.ValidatedCivilRegistryId);

                if (!_validatedBeneficiaries.Any(item => item.Matches(validatedBeneficiary)))
                {
                    _validatedBeneficiaries.Insert(0, validatedBeneficiary);
                }

                SelectedValidatedBeneficiary = _validatedBeneficiaries.First(item => item.Matches(validatedBeneficiary));
            }
            else
            {
                SelectedValidatedBeneficiary = null;
            }

            ClearFieldErrors();
            OnPropertyChanged(nameof(ResolvedAssistanceType));
            OnPropertyChanged(nameof(ApplicantSummary));
        }

        private bool CanSaveCase()
        {
            if (IsBusy)
            {
                return false;
            }

            // Block editing for Released or terminal cases
            if (SelectedCase != null && SelectedCase.Status is AssistanceCaseStatus.Released or AssistanceCaseStatus.Closed or AssistanceCaseStatus.Rejected or AssistanceCaseStatus.Cancelled)
            {
                return false;
            }

            return true;
        }

        private bool CanChangeStatus(AssistanceCaseStatus targetStatus)
        {
            if (IsBusy || SelectedCase == null || SelectedCase.Status == targetStatus)
            {
                return false;
            }

            var currentStatus = SelectedCase.Status;

            // Enforcement of state machine in UI
            bool isValidTransition = (currentStatus, targetStatus) switch
            {
                (AssistanceCaseStatus.Pending, AssistanceCaseStatus.UnderReview) => true,
                (AssistanceCaseStatus.Pending, AssistanceCaseStatus.Rejected) => true,
                (AssistanceCaseStatus.Pending, AssistanceCaseStatus.Cancelled) => true,

                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Approved) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Rejected) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Cancelled) => true,
                (AssistanceCaseStatus.UnderReview, AssistanceCaseStatus.Pending) => true,

                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.Released) => true,
                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.UnderReview) => true,
                (AssistanceCaseStatus.Approved, AssistanceCaseStatus.Cancelled) => true,

                (AssistanceCaseStatus.Released, AssistanceCaseStatus.Closed) => true,

                _ => false
            };

            if (!isValidTransition)
            {
                return false;
            }

            // Global Budget verification
            if (targetStatus is AssistanceCaseStatus.Approved or AssistanceCaseStatus.Released)
            {
                if (BudgetCapRemaining <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task SaveCaseAsync()
        {
            if (!CanSaveCase())
            {
                return;
            }

            ClearFieldErrors();
            if (!ValidateCurrentForm(out var assistanceAmount))
            {
                return;
            }

            var editingCaseId = SelectedCase?.Id;
            IsBusy = true;
            SetNeutralStatus(editingCaseId.HasValue ? $"Saving {EditableCaseNumber}..." : "Creating aid request...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(
                    context,
                    ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var request = new AssistanceCaseUpsertRequest(
                    null,
                    null,
                    SelectedValidatedBeneficiary?.FullName,
                    SelectedValidatedBeneficiary?.BeneficiaryId,
                    SelectedValidatedBeneficiary?.CivilRegistryId,
                    ResolveAssistanceType(),
                    SelectedPriority,
                    SelectedReleaseKind,
                    assistanceAmount,
                    RequestedOnDate,
                    ScheduledReleaseDate,
                    NormalizeNullable(EditableSummary));

                AssistanceCaseOperationResult result = editingCaseId.HasValue
                    ? await service.UpdateAsync(editingCaseId.Value, request, _currentUser.Id)
                    : await service.CreateAsync(request, _currentUser.Id);

                if (!result.IsSuccess)
                {
                    if (!TryApplyInlineError(result.Message))
                    {
                        SetErrorStatus(result.Message);
                    }

                    return;
                }

                await LoadCoreAsync(result.AssistanceCaseId);
                IsCasePanelOpen = false; // Close the panel to show the updated summary in the center
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not save aid request: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ChangeStatusAsync(AssistanceCaseStatus targetStatus, string actionLabel)
        {
            if (!CanChangeStatus(targetStatus) || SelectedCase == null)
            {
                return;
            }

            if (MessageBox.Show(
                    $"Mark {SelectedCase.CaseNumber} as {actionLabel}?",
                    "Update Aid Request",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var assistanceCaseId = SelectedCase.Id;
            IsBusy = true;
            SetNeutralStatus($"Updating {SelectedCase.CaseNumber}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(
                    context,
                    ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.ChangeStatusAsync(
                    assistanceCaseId,
                    targetStatus,
                    _currentUser.Id,
                    null);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(assistanceCaseId);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not update aid request status: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RejectCaseAsync()
        {
            if (SelectedCase == null || !CanChangeStatus(AssistanceCaseStatus.Rejected)) return;

            if (string.IsNullOrWhiteSpace(EditableResolutionNotes))
            {
                SetErrorStatus("Resolution notes are required when rejecting a case.");
                return;
            }

            if (MessageBox.Show($"Reject {SelectedCase.CaseNumber}?", "Reject Aid Request", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            var assistanceCaseId = SelectedCase.Id;
            IsBusy = true;
            SetNeutralStatus($"Rejecting {SelectedCase.CaseNumber}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.RejectCaseAsync(assistanceCaseId, EditableResolutionNotes, _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(assistanceCaseId);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not reject aid request: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CancelCaseAsync()
        {
            if (SelectedCase == null || !CanChangeStatus(AssistanceCaseStatus.Cancelled)) return;

            if (string.IsNullOrWhiteSpace(EditableResolutionNotes))
            {
                SetErrorStatus("Resolution notes are required when cancelling a case.");
                return;
            }

            if (MessageBox.Show($"Cancel {SelectedCase.CaseNumber}?", "Cancel Aid Request", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            var assistanceCaseId = SelectedCase.Id;
            IsBusy = true;
            SetNeutralStatus($"Cancelling {SelectedCase.CaseNumber}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.CancelCaseAsync(assistanceCaseId, EditableResolutionNotes, _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(assistanceCaseId);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not cancel aid request: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanFastTrackRelease()
        {
            if (IsBusy || SelectedCase == null || (SelectedCase.Status != AssistanceCaseStatus.Pending && SelectedCase.Status != AssistanceCaseStatus.UnderReview))
            {
                return false;
            }

            if (BudgetCapRemaining <= 0)
            {
                return false;
            }

            return true;
        }

        private async Task FastTrackReleaseAsync()
        {
            if (!CanFastTrackRelease() || SelectedCase == null) return;

            ClearFieldErrors();
            if (!ValidateCurrentForm(out var assistanceAmount) || !assistanceAmount.HasValue)
            {
                return;
            }

            if (MessageBox.Show($"Fast-track release for {SelectedCase.CaseNumber}?\nThis will automatically approve and release the request.", "Fast-Track Payout", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var assistanceCaseId = SelectedCase.Id;
            IsBusy = true;
            SetNeutralStatus($"Fast-tracking {SelectedCase.CaseNumber}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.FastTrackReleaseAsync(assistanceCaseId, assistanceAmount.Value, _currentUser.Id, EditableSummary);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(assistanceCaseId);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not fast-track aid request: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteCaseAsync()
        {
            if (!CanDeleteCase() || SelectedCase == null)
            {
                return;
            }

            if (MessageBox.Show(
                    $"Delete {SelectedCase.CaseNumber}?",
                    "Delete Aid Request",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            var caseId = SelectedCase.Id;
            IsBusy = true;
            SetNeutralStatus($"Deleting {SelectedCase.CaseNumber}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(
                    context,
                    ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.DeleteAsync(caseId, _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(null);
                CloseCasePanel();
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not delete aid request: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanDeleteCase()
        {
            return !IsBusy
                && SelectedCase != null
                && !SelectedCase.BudgetLedgerEntryId.HasValue;
        }

        private async Task LoadValidatedBeneficiariesAsync()
        {
            try
            {
                var preferredMatch = SelectedValidatedBeneficiary;

                await using var context = new AppDbContext();
                var query = context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(item => item.VerificationStatus == VerificationStatus.Approved);

                var beneficiaries = await query
                    .OrderBy(item => item.FullName ?? item.LastName)
                    .ThenBy(item => item.FirstName)
                    .ToListAsync();

                _validatedBeneficiaries.Clear();
                foreach (var beneficiary in beneficiaries)
                {
                    _validatedBeneficiaries.Add(AssistanceValidatedBeneficiaryOption.FromApprovedStaging(beneficiary));
                }

                if (preferredMatch != null)
                {
                    SelectedValidatedBeneficiary = _validatedBeneficiaries.FirstOrDefault(item => item.Matches(preferredMatch))
                        ?? preferredMatch;
                }
            }
            catch
            {
                _validatedBeneficiaries.Clear();
            }
        }

        private void RaiseCommandStates()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _newCaseCommand.RaiseCanExecuteChanged();
            _saveCaseCommand.RaiseCanExecuteChanged();
            _markUnderReviewCommand.RaiseCanExecuteChanged();
            _approveCommand.RaiseCanExecuteChanged();
            _releaseCommand.RaiseCanExecuteChanged();
            _closeCommand.RaiseCanExecuteChanged();
            _rejectCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
            _fastTrackReleaseCommand.RaiseCanExecuteChanged();
            _deleteCommand.RaiseCanExecuteChanged();
            _showListCommand.RaiseCanExecuteChanged();
            _showAllCasesCommand.RaiseCanExecuteChanged();
            _showPendingCasesCommand.RaiseCanExecuteChanged();
            _showUnderReviewCasesCommand.RaiseCanExecuteChanged();
            _showApprovedCasesCommand.RaiseCanExecuteChanged();
            _showReleasedCasesCommand.RaiseCanExecuteChanged();
            _showRejectedCasesCommand.RaiseCanExecuteChanged();
            _selectCaseCommand.RaiseCanExecuteChanged();
            _exportCasesCommand.RaiseCanExecuteChanged();
            _openCasePanelCommand.RaiseCanExecuteChanged();
            _closeCasePanelCommand.RaiseCanExecuteChanged();
        }

        private void ShowList()
        {
            if (IsBusy)
            {
                return;
            }

            SearchText = string.Empty;
            SetStatusFilter("All");
            CloseCasePanel();
        }

        private async Task CreateLookupScannerSessionAsync()
        {
            IsBusy = true;
            SetNeutralStatus("Creating mobile scanner session...");

            try
            {
                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                await using var context = new AppDbContext();
                var sessionService = new ScannerSessionService(context);
                var session = await sessionService.CreateLookupSessionAsync(_currentUser?.Id ?? 0, TimeSpan.FromMinutes(15));

                var sessionUrl = $"{baseUrl}/scanner?session={Uri.EscapeDataString(session.SessionToken)}";

                LookupScannerSessionUrl = sessionUrl;
                LookupScannerSessionPin = session.Pin;
                LookupScannerSessionExpiresAtText = $"Expires at {session.ExpiresAt:hh:mm tt}";
                LookupScannerQrImage = QrCodeToolkitService.GenerateQrImage(sessionUrl, 8);

                SetSuccessStatus("Mobile scanner session created. Open the URL on your phone to start scanning.");

                _ = MonitorScannerSessionAsync(session.SessionToken);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to create scanner session: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task MonitorScannerSessionAsync(string sessionToken)
        {
            while (!string.IsNullOrEmpty(LookupScannerSessionUrl))
            {
                try
                {
                    await using var context = new AppDbContext();
                    var sessionService = new ScannerSessionService(context);
                    var scanPayload = await sessionService.TryPopScanAsync(sessionToken);

                    if (scanPayload != null)
                    {
                        SearchText = scanPayload.Trim();
                        SetSuccessStatus($"Mobile Scan Received: {scanPayload}");

                        var match = _cases.FirstOrDefault(c => 
                            c.ValidatedBeneficiaryId == scanPayload || 
                            c.ValidatedCivilRegistryId == scanPayload || 
                            c.ValidatedBeneficiaryName == scanPayload);

                        if (match != null)
                        {
                            SelectedCase = match;
                            OpenCasePanel(match);
                        }
                    }
                }
                catch
                {
                    // Ignore transient errors
                }

                await Task.Delay(2000);
            }
        }

        private void SetStatusFilter(string filter)
        {
            if (IsBusy)
            {
                return;
            }

            SelectedStatusFilter = filter;
        }

        private void ClearLoadedState()
        {
            _cases.Clear();
            _validatedBeneficiaries.Clear();
            CasesView = CollectionViewSource.GetDefaultView(_cases);
            TotalCases = 0;
            PendingCount = 0;
            UnderReviewCount = 0;
            ApprovedCount = 0;
            ReleasedCount = 0;
            ClosedCount = 0;
            IsCasePanelOpen = false;
            BeginNewCase();
        }

        private bool CanOpenCasePanel()
        {
            return !IsBusy && !IsCasePanelOpen && SelectedCase != null;
        }

        private void OpenCasePanel(AssistanceCaseListItem? caseItem = null, bool force = false)
        {
            if (caseItem != null)
            {
                SelectedCase = caseItem;
            }

            if (!force && !CanOpenCasePanel())
            {
                return;
            }

            IsCasePanelOpen = true;
        }

        private void SelectCase(AssistanceCaseListItem? caseItem)
        {
            if (IsBusy || caseItem == null)
            {
                return;
            }

            SelectedCase = caseItem;
        }

        private bool CanExportCases()
        {
            return !IsBusy
                && CasesView != null
                && CasesView.Cast<object>().Any();
        }

        private void ExportCases()
        {
            if (!CanExportCases())
            {
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"aid-requests-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                DefaultExt = ".csv"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            var rows = CasesView.Cast<AssistanceCaseListItem>().ToList();
            var lines = new List<string>
            {
                "Case Number,Status,Priority,Assistance Type,Release Kind,Beneficiary,Program,Amount,Requested On,Scheduled Release,Summary"
            };

            lines.AddRange(rows.Select(item => string.Join(",",
                EscapeCsv(item.CaseNumber),
                EscapeCsv(item.StatusText),
                EscapeCsv(item.PriorityText),
                EscapeCsv(item.AssistanceType),
                EscapeCsv(item.ReleaseKindText),
                EscapeCsv(item.RecipientLabel),
                EscapeCsv(item.AssistanceCaseBudgetLabel),
                EscapeCsv(item.AssistanceAmountText),
                EscapeCsv(item.RequestedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                EscapeCsv(item.ScheduledReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty),
                EscapeCsv(item.Summary))));

            File.WriteAllLines(saveFileDialog.FileName, lines);
            SetSuccessStatus($"Exported {rows.Count:N0} aid request(s) to {Path.GetFileName(saveFileDialog.FileName)}.");
        }

        private void CloseCasePanel()
        {
            IsCasePanelOpen = false;
        }

        private bool ValidateCurrentForm(out decimal? assistanceAmount)
        {
            assistanceAmount = null;
            var isValid = true;

            if (SelectedValidatedBeneficiary == null)
            {
                SetValidatedBeneficiaryError("Select a validated beneficiary.");
                isValid = false;
            }

            if (!ValidateAssistanceAmount(requireValue: true))
            {
                isValid = false;
            }
            else if (TryParseAmount(EditableAssistanceAmount, out assistanceAmount, out _))
            {
                assistanceAmount ??= null;
            }

            return isValid;
        }

        private bool ValidateAssistanceAmount(bool requireValue)
        {
            var text = EditableAssistanceAmount.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                if (requireValue)
                {
                    SetAssistanceAmountError("Enter the assistance amount.");
                    return false;
                }

                ClearAssistanceAmountError();
                return true;
            }

            if (!TryParseAmount(text, out var amount, out _))
            {
                SetAssistanceAmountError("Enter a valid numeric amount.");
                return false;
            }

            if (!amount.HasValue || amount.Value <= 0)
            {
                SetAssistanceAmountError("Amount must be greater than 0.");
                return false;
            }

            ClearAssistanceAmountError();
            return true;
        }

        private bool TryApplyInlineError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            if (message.Contains("validated beneficiary", StringComparison.OrdinalIgnoreCase))
            {
                SetValidatedBeneficiaryError(message);
                return true;
            }

            if (message.Contains("amount", StringComparison.OrdinalIgnoreCase))
            {
                SetAssistanceAmountError(message);
                return true;
            }

            return false;
        }

        private string ResolveAssistanceType()
        {
            if (!string.IsNullOrWhiteSpace(EditableAssistanceType))
            {
                return EditableAssistanceType.Trim();
            }

            return SelectedReleaseKind == AssistanceReleaseKind.Cash
                ? "Cash"
                : "Goods";
        }

        private void ClearFieldErrors()
        {
            ClearValidatedBeneficiaryError();
            ClearAssistanceAmountError();
        }

        private void SetValidatedBeneficiaryError(string message)
        {
            if (SetProperty(ref _validatedBeneficiaryErrorMessage, message, nameof(ValidatedBeneficiaryErrorMessage)))
            {
                OnPropertyChanged(nameof(HasValidatedBeneficiaryError));
                OnPropertyChanged(nameof(ValidatedBeneficiaryBorderBrush));
                OnPropertyChanged(nameof(ValidatedBeneficiaryBackgroundBrush));
            }
        }

        private void ClearValidatedBeneficiaryError()
        {
            SetValidatedBeneficiaryError(string.Empty);
        }

        private void SetAssistanceAmountError(string message)
        {
            if (SetProperty(ref _assistanceAmountErrorMessage, message, nameof(AssistanceAmountErrorMessage)))
            {
                OnPropertyChanged(nameof(HasAssistanceAmountError));
                OnPropertyChanged(nameof(AssistanceAmountBorderBrush));
                OnPropertyChanged(nameof(AssistanceAmountBackgroundBrush));
            }
        }

        private void ClearAssistanceAmountError()
        {
            SetAssistanceAmountError(string.Empty);
        }

        private static bool TryParseAmount(string text, out decimal? amount, out string errorMessage)
        {
            errorMessage = string.Empty;
            amount = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedAmount))
            {
                amount = parsedAmount;
                return true;
            }

            errorMessage = $"Invalid amount value: '{text}'.";
            return false;
        }

        private static bool Contains(string? source, string searchText)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }

            return $"\"{value}\"";
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#6B7280");
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#991B1B");
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static Brush GetFieldBorderBrush(bool hasError)
        {
            return hasError
                ? CreateBrush("#DC2626")
                : CreateBrush("#CCD6E3");
        }

        private static Brush GetFieldBackgroundBrush(bool hasError)
        {
            return hasError
                ? CreateBrush("#FEF2F2")
                : Brushes.White;
        }

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }
    }

    public sealed class AssistanceCaseListItem
    {
        public int Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public int? AssistanceCaseBudgetId { get; init; }
        public int? BudgetLedgerEntryId { get; init; }
        public string ValidatedBeneficiaryName { get; init; } = string.Empty;
        public string ValidatedBeneficiaryId { get; init; } = string.Empty;
        public string ValidatedCivilRegistryId { get; init; } = string.Empty;
        public string AssistanceType { get; init; } = string.Empty;
        public AssistanceReleaseKind ReleaseKind { get; init; }
        public AssistanceCasePriority Priority { get; init; }
        public AssistanceCaseStatus Status { get; init; }
        public decimal? RequestedAmount { get; init; }
        public decimal? ApprovedAmount { get; init; }
        public DateTime RequestedOn { get; init; }
        public DateTime? ScheduledReleaseDate { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string ResolutionNotes { get; init; } = string.Empty;
        public string RecipientLabel { get; init; } = string.Empty;
        public string AssistanceCaseBudgetLabel { get; init; } = string.Empty;

        public string PriorityText => Priority.ToString();
        public string StatusText => Status switch
        {
            AssistanceCaseStatus.UnderReview => "Under review",
            _ => Status.ToString()
        };

        public decimal? AssistanceAmount => ApprovedAmount ?? RequestedAmount;
        public string AssistanceAmountText => AssistanceAmount?.ToString("N2") ?? string.Empty;
        public string ReleaseKindText => ReleaseKind.ToString();
        public string AmountsSummary => $"Amount: {(AssistanceAmount?.ToString("N2") ?? "--")} | Release: {ReleaseKindText}";

        public Brush StatusBrush => Status switch
        {
            AssistanceCaseStatus.Pending => CreateBrush("#FEF3C7"),
            AssistanceCaseStatus.UnderReview => CreateBrush("#DBEAFE"),
            AssistanceCaseStatus.Approved => CreateBrush("#DCFCE7"),
            AssistanceCaseStatus.Released => CreateBrush("#CCFBF1"),
            AssistanceCaseStatus.Closed => CreateBrush("#E5E7EB"),
            AssistanceCaseStatus.Rejected => CreateBrush("#FEE2E2"),
            AssistanceCaseStatus.Cancelled => CreateBrush("#F3E8FF"),
            _ => CreateBrush("#E5E7EB")
        };

        public Brush StatusTextBrush => Status switch
        {
            AssistanceCaseStatus.Pending => CreateBrush("#92400E"),
            AssistanceCaseStatus.UnderReview => CreateBrush("#1D4ED8"),
            AssistanceCaseStatus.Approved => CreateBrush("#166534"),
            AssistanceCaseStatus.Released => CreateBrush("#0F766E"),
            AssistanceCaseStatus.Closed => CreateBrush("#374151"),
            AssistanceCaseStatus.Rejected => CreateBrush("#991B1B"),
            AssistanceCaseStatus.Cancelled => CreateBrush("#7C3AED"),
            _ => CreateBrush("#374151")
        };

        public static AssistanceCaseListItem FromEntity(AssistanceCase assistanceCase)
        {
            return new AssistanceCaseListItem
            {
                Id = assistanceCase.Id,
                CaseNumber = assistanceCase.CaseNumber,
                AssistanceCaseBudgetId = assistanceCase.AssistanceCaseBudgetId,
                BudgetLedgerEntryId = assistanceCase.BudgetLedgerEntryId,
                ValidatedBeneficiaryName = assistanceCase.ValidatedBeneficiaryName ?? string.Empty,
                ValidatedBeneficiaryId = assistanceCase.ValidatedBeneficiaryId ?? string.Empty,
                ValidatedCivilRegistryId = assistanceCase.ValidatedCivilRegistryId ?? string.Empty,
                AssistanceType = assistanceCase.AssistanceType,
                ReleaseKind = assistanceCase.ReleaseKind,
                Priority = assistanceCase.Priority,
                Status = assistanceCase.Status,
                RequestedAmount = assistanceCase.RequestedAmount,
                ApprovedAmount = assistanceCase.ApprovedAmount,
                RequestedOn = assistanceCase.RequestedOn,
                ScheduledReleaseDate = assistanceCase.ScheduledReleaseDate,
                Summary = assistanceCase.Summary ?? string.Empty,
                ResolutionNotes = assistanceCase.ResolutionNotes ?? string.Empty,
                RecipientLabel = !string.IsNullOrWhiteSpace(assistanceCase.ValidatedBeneficiaryName)
                    ? assistanceCase.ValidatedBeneficiaryName
                    : "Legacy household-linked record",
                AssistanceCaseBudgetLabel = assistanceCase.AssistanceCaseBudget?.BudgetName ?? "Budget not set"
            };
        }

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }
    }

    public sealed class AssistanceCaseBudgetOption
    {
        public int Id { get; init; }
        public string BudgetCode { get; init; } = string.Empty;
        public string BudgetName { get; init; } = string.Empty;
        public string? AssistanceType { get; init; }
        public string DisplayLabel => $"{BudgetCode} - {BudgetName}";

        public static AssistanceCaseBudgetOption FromEntity(AssistanceCaseBudget budget)
        {
            return new AssistanceCaseBudgetOption
            {
                Id = budget.Id,
                BudgetCode = budget.BudgetCode,
                BudgetName = budget.BudgetName,
                AssistanceType = budget.AssistanceType
            };
        }
    }

    public sealed class AssistanceValidatedBeneficiaryOption
    {
        public AssistanceValidatedBeneficiaryOption(string fullName, string? beneficiaryId, string? civilRegistryId)
        {
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Unnamed beneficiary" : fullName.Trim();
            BeneficiaryId = string.IsNullOrWhiteSpace(beneficiaryId) ? string.Empty : beneficiaryId.Trim();
            CivilRegistryId = string.IsNullOrWhiteSpace(civilRegistryId) ? string.Empty : civilRegistryId.Trim();
        }

        public string FullName { get; }
        public string BeneficiaryId { get; }
        public string CivilRegistryId { get; }

        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(BeneficiaryId))
                {
                    return $"{FullName} [{BeneficiaryId}]";
                }

                if (!string.IsNullOrWhiteSpace(CivilRegistryId))
                {
                    return $"{FullName} [CR: {CivilRegistryId}]";
                }

                return FullName;
            }
        }

        public bool Matches(AssistanceValidatedBeneficiaryOption other)
        {
            if (!string.IsNullOrWhiteSpace(BeneficiaryId) &&
                !string.IsNullOrWhiteSpace(other.BeneficiaryId))
            {
                return string.Equals(BeneficiaryId, other.BeneficiaryId, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(CivilRegistryId) &&
                !string.IsNullOrWhiteSpace(other.CivilRegistryId))
            {
                return string.Equals(CivilRegistryId, other.CivilRegistryId, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(FullName, other.FullName, StringComparison.OrdinalIgnoreCase);
        }

        public static AssistanceValidatedBeneficiaryOption FromApprovedStaging(BeneficiaryStaging beneficiary)
        {
            return new AssistanceValidatedBeneficiaryOption(
                !string.IsNullOrWhiteSpace(beneficiary.FullName)
                    ? beneficiary.FullName
                    : string.Join(" ", new[] { beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())),
                beneficiary.BeneficiaryId,
                beneficiary.CivilRegistryId);
        }
    }
}

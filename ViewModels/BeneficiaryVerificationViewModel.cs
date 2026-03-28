using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BeneficiaryVerificationViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly IBeneficiaryVerificationQueueService _queueService;
        private readonly ObservableCollection<StagedBeneficiaryItem> _records = new();
        private readonly ObservableCollection<HouseholdOption> _households = new();
        private readonly ObservableCollection<HouseholdMemberOption> _availableHouseholdMembers = new();
        private readonly RelayCommand _previousPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _saveCorrectionsCommand;
        private readonly RelayCommand _verifyCommand;
        private readonly RelayCommand _approveCommand;
        private readonly RelayCommand _markDuplicateCommand;
        private readonly RelayCommand _markInactiveCommand;
        private readonly RelayCommand _rejectCommand;
        private readonly RelayCommand _uploadDigitalIdPhotoCommand;
        private readonly RelayCommand _printDigitalIdCommand;
        private readonly RelayCommand _createLookupScannerSessionCommand;
        private readonly bool _autoRefresh;
        private CancellationTokenSource? _loadCts;
        private ICollectionView _recordsView;
        private StagedBeneficiaryItem? _selectedBeneficiary;
        private HouseholdOption? _selectedHousehold;
        private HouseholdMemberOption? _selectedHouseholdMember;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "Pending";
        private int _selectedPageSize = 100;
        private bool _isBusy;
        private string _statusMessage = "Loading staged beneficiaries...";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private int _totalCount;
        private int _pendingCount;
        private int _verifiedCount;
        private int _approvedCount;
        private int _duplicateCount;
        private int _inactiveCount;
        private int _rejectedCount;
        private int _filteredRecordCount;
        private int _currentPage = 1;
        private string _editableBeneficiaryId = string.Empty;
        private string _editableCivilRegistryId = string.Empty;
        private string _editableFirstName = string.Empty;
        private string _editableMiddleName = string.Empty;
        private string _editableLastName = string.Empty;
        private string _editableFullName = string.Empty;
        private string _editableSex = string.Empty;
        private string _editableDateOfBirth = string.Empty;
        private string _editableAge = string.Empty;
        private string _editableMaritalStatus = string.Empty;
        private string _editableAddress = string.Empty;
        private string _editablePwdIdNo = string.Empty;
        private string _editableSeniorIdNo = string.Empty;
        private string _editableDisabilityType = string.Empty;
        private string _editableReviewNotes = string.Empty;
        private string _digitalIdCardNumber = "No digital ID issued yet.";
        private string _digitalIdIssuedAtText = "Approve a beneficiary to generate a digital ID.";
        private BitmapSource? _digitalIdPhotoImage;
        private BitmapSource? _digitalIdQrImage;
        private string _lookupScannerSessionUrl = string.Empty;
        private string _lookupScannerSessionPin = string.Empty;
        private string _lookupScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _lookupScannerQrImage;

        public BeneficiaryVerificationViewModel(User currentUser)
            : this(currentUser, new BeneficiaryVerificationQueueService(), autoLoad: true, autoRefresh: true)
        {
        }

        internal BeneficiaryVerificationViewModel(
            User currentUser,
            IBeneficiaryVerificationQueueService queueService,
            bool autoLoad,
            bool autoRefresh)
        {
            _currentUser = currentUser;
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _autoRefresh = autoRefresh;
            StatusFilters = new ObservableCollection<string>(BeneficiaryVerificationStatusFilters.Options);
            PageSizeOptions = new ObservableCollection<int> { 50, 100, 250, 500 };

            _recordsView = CollectionViewSource.GetDefaultView(_records);
            _previousPageCommand = new RelayCommand(async _ => await GoToPreviousPageAsync(), _ => !IsBusy && CurrentPage > 1);
            _nextPageCommand = new RelayCommand(async _ => await GoToNextPageAsync(), _ => !IsBusy && CurrentPage < TotalPages);
            _refreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
            _saveCorrectionsCommand = new RelayCommand(async _ => await SaveCorrectionsAsync(), _ => CanSaveCorrectionsSelected());
            _verifyCommand = new RelayCommand(async _ => await VerifySelectedAsync(), _ => CanVerifySelected());
            _approveCommand = new RelayCommand(async _ => await ApproveSelectedAsync(), _ => CanApproveSelected());
            _markDuplicateCommand = new RelayCommand(async _ => await MarkDuplicateSelectedAsync(), _ => CanMarkDuplicateSelected());
            _markInactiveCommand = new RelayCommand(async _ => await MarkInactiveSelectedAsync(), _ => CanMarkInactiveSelected());
            _rejectCommand = new RelayCommand(async _ => await RejectSelectedAsync(), _ => CanRejectSelected());
            _uploadDigitalIdPhotoCommand = new RelayCommand(async _ => await UploadDigitalIdPhotoAsync(), _ => CanUseDigitalId());
            _printDigitalIdCommand = new RelayCommand(async _ => await PrintDigitalIdAsync(), _ => CanUseDigitalId());
            _createLookupScannerSessionCommand = new RelayCommand(async _ => await CreateLookupScannerSessionAsync(), _ => CanCreateLookupScannerSession());

            if (autoLoad)
            {
                _ = LoadPageAsync(1, null, syncValidatedSnapshot: false, CancellationToken.None);
            }
        }

        public ObservableCollection<string> StatusFilters { get; }

        public ObservableCollection<int> PageSizeOptions { get; }

        public ObservableCollection<HouseholdOption> Households => _households;

        public ObservableCollection<HouseholdMemberOption> AvailableHouseholdMembers => _availableHouseholdMembers;

        public ICollectionView RecordsView
        {
            get => _recordsView;
            private set => SetProperty(ref _recordsView, value);
        }

        public StagedBeneficiaryItem? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedBeneficiary, value))
                {
                    SyncEditableFieldsFromSelection();
                    RaiseActionCanExecuteChanged();
                }
            }
        }

        public HouseholdOption? SelectedHousehold
        {
            get => _selectedHousehold;
            set
            {
                if (SetProperty(ref _selectedHousehold, value))
                {
                    RefreshAvailableHouseholdMembers();
                    RaiseActionCanExecuteChanged();
                }
            }
        }

        public HouseholdMemberOption? SelectedHouseholdMember
        {
            get => _selectedHouseholdMember;
            set
            {
                if (SetProperty(ref _selectedHouseholdMember, value))
                {
                    RaiseActionCanExecuteChanged();
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
                    QueueReloadFromFirstPage();
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
                    RaiseActionCanExecuteChanged();
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

        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        public int PendingCount
        {
            get => _pendingCount;
            private set => SetProperty(ref _pendingCount, value);
        }

        public int VerifiedCount
        {
            get => _verifiedCount;
            private set => SetProperty(ref _verifiedCount, value);
        }

        public int ApprovedCount
        {
            get => _approvedCount;
            private set => SetProperty(ref _approvedCount, value);
        }

        public int DuplicateCount
        {
            get => _duplicateCount;
            private set => SetProperty(ref _duplicateCount, value);
        }

        public int InactiveCount
        {
            get => _inactiveCount;
            private set => SetProperty(ref _inactiveCount, value);
        }

        public int RejectedCount
        {
            get => _rejectedCount;
            private set => SetProperty(ref _rejectedCount, value);
        }

        public int FilteredRecordCount
        {
            get => _filteredRecordCount;
            private set
            {
                if (SetProperty(ref _filteredRecordCount, value))
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

        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)Math.Max(FilteredRecordCount, 1) / SelectedPageSize));

        public string PageIndicator => $"Page {CurrentPage} of {TotalPages}";

        public string PageSummary
        {
            get
            {
                if (FilteredRecordCount == 0 || _records.Count == 0)
                {
                    return "Showing 0 beneficiary approval records";
                }

                var start = ((CurrentPage - 1) * SelectedPageSize) + 1;
                var end = start + _records.Count - 1;
                return $"Showing {start:N0}-{end:N0} of {FilteredRecordCount:N0} beneficiary approval records";
            }
        }

        public string EditableBeneficiaryId
        {
            get => _editableBeneficiaryId;
            set => SetProperty(ref _editableBeneficiaryId, value);
        }

        public string EditableCivilRegistryId
        {
            get => _editableCivilRegistryId;
            set => SetProperty(ref _editableCivilRegistryId, value);
        }

        public string EditableFirstName
        {
            get => _editableFirstName;
            set => SetProperty(ref _editableFirstName, value);
        }

        public string EditableMiddleName
        {
            get => _editableMiddleName;
            set => SetProperty(ref _editableMiddleName, value);
        }

        public string EditableLastName
        {
            get => _editableLastName;
            set => SetProperty(ref _editableLastName, value);
        }

        public string EditableFullName
        {
            get => _editableFullName;
            set => SetProperty(ref _editableFullName, value);
        }

        public string EditableSex
        {
            get => _editableSex;
            set => SetProperty(ref _editableSex, value);
        }

        public string EditableDateOfBirth
        {
            get => _editableDateOfBirth;
            set => SetProperty(ref _editableDateOfBirth, value);
        }

        public string EditableAge
        {
            get => _editableAge;
            set => SetProperty(ref _editableAge, value);
        }

        public string EditableMaritalStatus
        {
            get => _editableMaritalStatus;
            set => SetProperty(ref _editableMaritalStatus, value);
        }

        public string EditableAddress
        {
            get => _editableAddress;
            set => SetProperty(ref _editableAddress, value);
        }

        public string EditablePwdIdNo
        {
            get => _editablePwdIdNo;
            set => SetProperty(ref _editablePwdIdNo, value);
        }

        public string EditableSeniorIdNo
        {
            get => _editableSeniorIdNo;
            set => SetProperty(ref _editableSeniorIdNo, value);
        }

        public string EditableDisabilityType
        {
            get => _editableDisabilityType;
            set => SetProperty(ref _editableDisabilityType, value);
        }

        public string EditableReviewNotes
        {
            get => _editableReviewNotes;
            set
            {
                if (SetProperty(ref _editableReviewNotes, value))
                {
                    RaiseActionCanExecuteChanged();
                }
            }
        }

        public string DigitalIdCardNumber
        {
            get => _digitalIdCardNumber;
            private set => SetProperty(ref _digitalIdCardNumber, value);
        }

        public string DigitalIdIssuedAtText
        {
            get => _digitalIdIssuedAtText;
            private set => SetProperty(ref _digitalIdIssuedAtText, value);
        }

        public BitmapSource? DigitalIdPhotoImage
        {
            get => _digitalIdPhotoImage;
            private set => SetProperty(ref _digitalIdPhotoImage, value);
        }

        public BitmapSource? DigitalIdQrImage
        {
            get => _digitalIdQrImage;
            private set => SetProperty(ref _digitalIdQrImage, value);
        }

        public string LookupScannerSessionUrl
        {
            get => _lookupScannerSessionUrl;
            private set => SetProperty(ref _lookupScannerSessionUrl, value);
        }

        public string LookupScannerSessionPin
        {
            get => _lookupScannerSessionPin;
            private set => SetProperty(ref _lookupScannerSessionPin, value);
        }

        public string LookupScannerSessionExpiresAtText
        {
            get => _lookupScannerSessionExpiresAtText;
            private set => SetProperty(ref _lookupScannerSessionExpiresAtText, value);
        }

        public BitmapSource? LookupScannerQrImage
        {
            get => _lookupScannerQrImage;
            private set => SetProperty(ref _lookupScannerQrImage, value);
        }

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand PreviousPageCommand => _previousPageCommand;
        public ICommand NextPageCommand => _nextPageCommand;
        public ICommand SaveCorrectionsCommand => _saveCorrectionsCommand;
        public ICommand VerifyCommand => _verifyCommand;
        public ICommand ApproveCommand => _approveCommand;
        public ICommand MarkDuplicateCommand => _markDuplicateCommand;
        public ICommand MarkInactiveCommand => _markInactiveCommand;
        public ICommand RejectCommand => _rejectCommand;
        public ICommand UploadDigitalIdPhotoCommand => _uploadDigitalIdPhotoCommand;
        public ICommand PrintDigitalIdCommand => _printDigitalIdCommand;
        public ICommand CreateLookupScannerSessionCommand => _createLookupScannerSessionCommand;

        internal Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            return LoadPageAsync(CurrentPage, SelectedBeneficiary?.StagingId, syncValidatedSnapshot: true, cancellationToken);
        }

        internal Task LoadCurrentPageAsync(CancellationToken cancellationToken = default)
        {
            return LoadPageAsync(CurrentPage, SelectedBeneficiary?.StagingId, syncValidatedSnapshot: false, cancellationToken);
        }

        internal Task GoToNextPageAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentPage >= TotalPages)
            {
                return Task.CompletedTask;
            }

            return LoadPageAsync(CurrentPage + 1, null, syncValidatedSnapshot: false, cancellationToken);
        }

        internal Task GoToPreviousPageAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentPage <= 1)
            {
                return Task.CompletedTask;
            }

            return LoadPageAsync(CurrentPage - 1, null, syncValidatedSnapshot: false, cancellationToken);
        }

        private async Task LoadPageAsync(
            int targetPage,
            int? preferredStagingId,
            bool syncValidatedSnapshot,
            CancellationToken cancellationToken)
        {
            _loadCts?.Cancel();
            var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadCts = loadCts;

            var previouslySelectedHouseholdId = SelectedHousehold?.Id;
            var previouslySelectedHouseholdMemberId = SelectedHouseholdMember?.Id;
            var normalizedTargetPage = Math.Max(1, targetPage);

            IsBusy = true;
            SetNeutralStatus(
                syncValidatedSnapshot
                    ? "Syncing validated snapshot into the approval queue..."
                    : $"Loading beneficiary approval queue page {normalizedTargetPage:N0}...");

            try
            {
                CrsBeneficiaryImportResult? syncResult = null;
                if (syncValidatedSnapshot)
                {
                    syncResult = await SyncPendingFromValidatedSnapshotAsync(loadCts.Token);
                    if (loadCts.IsCancellationRequested)
                    {
                        return;
                    }
                }

                var result = await _queueService.LoadPageAsync(
                    new BeneficiaryVerificationQueuePageRequest
                    {
                        SearchText = SearchText.Trim(),
                        StatusFilter = SelectedStatusFilter,
                        PageNumber = normalizedTargetPage,
                        PageSize = SelectedPageSize
                    },
                    loadCts.Token);

                if (loadCts.IsCancellationRequested)
                {
                    return;
                }

                await LoadHouseholdsAsync(loadCts.Token);
                if (loadCts.IsCancellationRequested)
                {
                    return;
                }

                _records.Clear();
                foreach (var row in result.Rows)
                {
                    _records.Add(StagedBeneficiaryItem.FromEntity(row.Staging, row.DigitalId));
                }

                RecordsView = CollectionViewSource.GetDefaultView(_records);
                UpdateCounts(result);
                FilteredRecordCount = result.FilteredRecordCount;
                CurrentPage = result.PageNumber;

                SelectedBeneficiary = _records.FirstOrDefault(row => row.StagingId == preferredStagingId)
                    ?? _records.FirstOrDefault(row => row.VerificationStatus == VerificationStatus.Pending)
                    ?? _records.FirstOrDefault(row => row.VerificationStatus == VerificationStatus.Verified)
                    ?? _records.FirstOrDefault();

                var preferredHouseholdId = SelectedBeneficiary?.LinkedHouseholdId ?? previouslySelectedHouseholdId;
                SelectedHousehold = _households.FirstOrDefault(household => household.Id == preferredHouseholdId)
                    ?? _households.FirstOrDefault();

                var preferredHouseholdMemberId = SelectedBeneficiary?.LinkedHouseholdMemberId ?? previouslySelectedHouseholdMemberId;
                if (preferredHouseholdMemberId.HasValue)
                {
                    SelectedHouseholdMember = _availableHouseholdMembers
                        .FirstOrDefault(member => member.Id == preferredHouseholdMemberId.Value);
                }

                if (syncResult != null)
                {
                    if (syncResult.IsSuccess)
                    {
                        SetSuccessStatus($"{syncResult.Message} {PageSummary}.");
                    }
                    else
                    {
                        SetWarningStatus($"Validated snapshot sync warning: {syncResult.Message} {PageSummary}.");
                    }
                }
                else
                {
                    SetSuccessStatus(
                        FilteredRecordCount == 0
                            ? "No beneficiary approval records matched the current filters."
                            : $"{PageSummary}.");
                }
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

                ClearLoadedState();
                SetErrorStatus($"Unable to load validated beneficiaries approval queue: {ex.Message}");
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

        private static Task<CrsBeneficiaryImportResult> SyncPendingFromValidatedSnapshotAsync(CancellationToken cancellationToken)
        {
            var settings = ConnectionSettingsService.Load();
            var activePreset = settings.GetPreset(settings.SelectedPreset);
            return CrsBeneficiaryImportService.ImportPendingAsync(activePreset, cancellationToken);
        }

        private async Task LoadHouseholdsAsync(CancellationToken cancellationToken)
        {
            await using var context = new AppDbContext();
            var households = await context.Households
                .AsNoTracking()
                .Include(household => household.Members)
                .OrderBy(household => household.HouseholdCode)
                .ThenBy(household => household.HeadName)
                .ToListAsync(cancellationToken);

            _households.Clear();
            foreach (var household in households)
            {
                _households.Add(HouseholdOption.FromEntity(household));
            }
        }

        private void QueueReloadFromFirstPage()
        {
            if (!_autoRefresh)
            {
                return;
            }

            _ = LoadPageAsync(1, null, syncValidatedSnapshot: false, CancellationToken.None);
        }

        private void UpdateCounts(BeneficiaryVerificationQueuePageResult result)
        {
            TotalCount = result.TotalCount;
            PendingCount = result.PendingCount;
            VerifiedCount = result.VerifiedCount;
            ApprovedCount = result.ApprovedCount;
            DuplicateCount = result.DuplicateCount;
            InactiveCount = result.InactiveCount;
            RejectedCount = result.RejectedCount;
        }

        private void SyncEditableFieldsFromSelection()
        {
            if (SelectedBeneficiary == null)
            {
                EditableBeneficiaryId = string.Empty;
                EditableCivilRegistryId = string.Empty;
                EditableFirstName = string.Empty;
                EditableMiddleName = string.Empty;
                EditableLastName = string.Empty;
                EditableFullName = string.Empty;
                EditableSex = string.Empty;
                EditableDateOfBirth = string.Empty;
                EditableAge = string.Empty;
                EditableMaritalStatus = string.Empty;
                EditableAddress = string.Empty;
                EditablePwdIdNo = string.Empty;
                EditableSeniorIdNo = string.Empty;
                EditableDisabilityType = string.Empty;
                EditableReviewNotes = string.Empty;
                SelectedHouseholdMember = null;
                ResetDigitalIdPreview();
                return;
            }

            EditableBeneficiaryId = SelectedBeneficiary.BeneficiaryId;
            EditableCivilRegistryId = SelectedBeneficiary.CivilRegistryId;
            EditableFirstName = SelectedBeneficiary.FirstName;
            EditableMiddleName = SelectedBeneficiary.MiddleName;
            EditableLastName = SelectedBeneficiary.LastName;
            EditableFullName = SelectedBeneficiary.FullName;
            EditableSex = SelectedBeneficiary.Sex;
            EditableDateOfBirth = SelectedBeneficiary.DateOfBirth;
            EditableAge = SelectedBeneficiary.Age;
            EditableMaritalStatus = SelectedBeneficiary.MaritalStatus;
            EditableAddress = SelectedBeneficiary.Address;
            EditablePwdIdNo = SelectedBeneficiary.PwdIdNo;
            EditableSeniorIdNo = SelectedBeneficiary.SeniorIdNo;
            EditableDisabilityType = SelectedBeneficiary.DisabilityType;
            EditableReviewNotes = SelectedBeneficiary.ReviewNotes;

            if (SelectedBeneficiary.LinkedHouseholdId.HasValue)
            {
                SelectedHousehold = _households.FirstOrDefault(household => household.Id == SelectedBeneficiary.LinkedHouseholdId.Value)
                    ?? SelectedHousehold
                    ?? _households.FirstOrDefault();
            }
            else if (SelectedHousehold == null)
            {
                SelectedHousehold = _households.FirstOrDefault();
            }

            if (SelectedBeneficiary.LinkedHouseholdMemberId.HasValue)
            {
                SelectedHouseholdMember = _availableHouseholdMembers
                    .FirstOrDefault(member => member.Id == SelectedBeneficiary.LinkedHouseholdMemberId.Value);
            }
            else if (SelectedBeneficiary.LinkedHouseholdId != SelectedHousehold?.Id)
            {
                SelectedHouseholdMember = null;
            }

            SyncDigitalIdPreviewFromSelection();
        }

        private void RefreshAvailableHouseholdMembers()
        {
            var preferredMemberId = SelectedHouseholdMember?.Id;
            var selectedBeneficiary = SelectedBeneficiary;

            if (selectedBeneficiary is { LinkedHouseholdMemberId: not null } &&
                selectedBeneficiary.LinkedHouseholdId == SelectedHousehold?.Id)
            {
                preferredMemberId = selectedBeneficiary.LinkedHouseholdMemberId;
            }

            _availableHouseholdMembers.Clear();

            if (SelectedHousehold != null)
            {
                foreach (var member in SelectedHousehold.Members)
                {
                    _availableHouseholdMembers.Add(member);
                }
            }

            if (preferredMemberId.HasValue)
            {
                SelectedHouseholdMember = _availableHouseholdMembers
                    .FirstOrDefault(member => member.Id == preferredMemberId.Value);
            }
            else
            {
                SelectedHouseholdMember = null;
            }
        }

        private void SyncDigitalIdPreviewFromSelection()
        {
            if (SelectedBeneficiary == null || !SelectedBeneficiary.HasDigitalId)
            {
                ResetDigitalIdPreview();
                return;
            }

            DigitalIdCardNumber = SelectedBeneficiary.DigitalIdCardNumber;
            DigitalIdIssuedAtText = SelectedBeneficiary.DigitalIdIssuedAt.HasValue
                ? $"Issued on {SelectedBeneficiary.DigitalIdIssuedAt.Value:MMMM dd, yyyy hh:mm tt}"
                : "Digital ID issued.";
            DigitalIdPhotoImage = BuildImage(SelectedBeneficiary.DigitalIdPhotoPath);
            DigitalIdQrImage = QrCodeToolkitService.GenerateQrImage(SelectedBeneficiary.DigitalIdQrPayload, 10);

            LookupScannerSessionUrl = string.Empty;
            LookupScannerSessionPin = string.Empty;
            LookupScannerSessionExpiresAtText = string.Empty;
            LookupScannerQrImage = null;
        }

        private void ResetDigitalIdPreview()
        {
            DigitalIdCardNumber = "No digital ID issued yet.";
            DigitalIdIssuedAtText = "Approve a beneficiary to generate a digital ID.";
            DigitalIdPhotoImage = null;
            DigitalIdQrImage = null;
            LookupScannerSessionUrl = string.Empty;
            LookupScannerSessionPin = string.Empty;
            LookupScannerSessionExpiresAtText = string.Empty;
            LookupScannerQrImage = null;
        }

        private bool CanUseDigitalId()
        {
            return !IsBusy
                && SelectedBeneficiary?.HasDigitalId == true;
        }

        private bool CanCreateLookupScannerSession()
        {
            return CanUseDigitalId();
        }

        private async Task UploadDigitalIdPhotoAsync()
        {
            if (!CanUseDigitalId() || SelectedBeneficiary == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                CheckFileExists = true,
                Title = "Select Beneficiary ID Photo"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var storedPhotoPath = StoreBeneficiaryPhoto(dialog.FileName, SelectedBeneficiary.StagingId);
                await using var context = new AppDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);
                var wasSaved = await digitalIdService.UpdatePhotoAsync(SelectedBeneficiary.StagingId, storedPhotoPath, _currentUser.Id);

                if (!wasSaved)
                {
                    SetErrorStatus("Digital ID was not found for the selected beneficiary.");
                    return;
                }

                await LoadPageAsync(CurrentPage, SelectedBeneficiary.StagingId, syncValidatedSnapshot: false, CancellationToken.None);
                SetSuccessStatus("Beneficiary digital ID photo updated.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to update the beneficiary photo: {ex.Message}");
            }
        }

        private async Task PrintDigitalIdAsync()
        {
            if (!CanUseDigitalId() || SelectedBeneficiary == null)
            {
                return;
            }

            var printService = new DigitalIdPrintService();
            var wasPrinted = printService.PrintCard(new DigitalIdPrintRequest(
                SelectedBeneficiary.FullName,
                SelectedBeneficiary.DigitalIdCardNumber,
                SelectedBeneficiary.BeneficiaryId,
                SelectedBeneficiary.CivilRegistryId,
                DigitalIdPhotoImage,
                DigitalIdQrImage));

            if (!wasPrinted)
            {
                return;
            }

            await using var context = new AppDbContext();
            var digitalIdService = new BeneficiaryDigitalIdService(context);
            await digitalIdService.MarkPrintedAsync(SelectedBeneficiary.StagingId, _currentUser.Id);
            await LoadPageAsync(CurrentPage, SelectedBeneficiary.StagingId, syncValidatedSnapshot: false, CancellationToken.None);
            SetSuccessStatus("Beneficiary digital ID sent to printer.");
        }

        private async Task CreateLookupScannerSessionAsync()
        {
            if (!CanCreateLookupScannerSession())
            {
                return;
            }

            try
            {
                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                await using var context = new AppDbContext();
                var sessionService = new ScannerSessionService(context);
                var session = await sessionService.CreateLookupSessionAsync(_currentUser.Id, TimeSpan.FromMinutes(15));
                var sessionUrl = $"{baseUrl}/scanner?session={Uri.EscapeDataString(session.SessionToken)}";

                LookupScannerSessionUrl = sessionUrl;
                LookupScannerSessionPin = session.Pin;
                LookupScannerSessionExpiresAtText = $"Expires {session.ExpiresAt:MMMM dd, yyyy hh:mm tt}";
                LookupScannerQrImage = QrCodeToolkitService.GenerateQrImage(sessionUrl, 8);
                SetSuccessStatus("Lookup scanner session is ready for the employee phone.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to start the lookup scanner session: {ex.Message}");
            }
        }

        private bool CanSaveCorrectionsSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Approved;
        }

        private bool CanVerifySelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Approved
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Verified;
        }

        private bool CanApproveSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedHousehold != null
                && SelectedBeneficiary.VerificationStatus is VerificationStatus.Pending or VerificationStatus.Verified;
        }

        private bool CanMarkDuplicateSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Approved
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Duplicate
                && HasReviewNotes();
        }

        private bool CanMarkInactiveSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Approved
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Inactive
                && HasReviewNotes();
        }

        private bool CanRejectSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Approved
                && SelectedBeneficiary.VerificationStatus != VerificationStatus.Rejected
                && HasReviewNotes();
        }

        private bool HasReviewNotes()
        {
            return !string.IsNullOrWhiteSpace(EditableReviewNotes);
        }

        private async Task SaveCorrectionsAsync()
        {
            if (!CanSaveCorrectionsSelected() || SelectedBeneficiary == null)
            {
                return;
            }

            var stagingId = SelectedBeneficiary.StagingId;
            var displayName = SelectedBeneficiary.FullName;

            IsBusy = true;
            SetNeutralStatus($"Saving corrections for {displayName}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new BeneficiaryVerificationService(context);
                var result = await service.SaveCorrectionsAsync(
                    new BeneficiaryCorrectionRequest(
                        stagingId,
                        NormalizeNullable(EditableBeneficiaryId),
                        NormalizeNullable(EditableCivilRegistryId),
                        NormalizeNullable(EditableFirstName),
                        NormalizeNullable(EditableMiddleName),
                        NormalizeNullable(EditableLastName),
                        NormalizeNullable(EditableFullName),
                        NormalizeNullable(EditableSex),
                        NormalizeNullable(EditableDateOfBirth),
                        NormalizeNullable(EditableAge),
                        NormalizeNullable(EditableMaritalStatus),
                        NormalizeNullable(EditableAddress),
                        NormalizeNullable(EditablePwdIdNo),
                        NormalizeNullable(EditableSeniorIdNo),
                        NormalizeNullable(EditableDisabilityType),
                        NormalizeNullable(EditableReviewNotes)),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadPageAsync(CurrentPage, stagingId, syncValidatedSnapshot: false, CancellationToken.None);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not save beneficiary corrections: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task VerifySelectedAsync()
        {
            if (!CanVerifySelected() || SelectedBeneficiary == null)
            {
                return;
            }

            var stagingId = SelectedBeneficiary.StagingId;
            var displayName = SelectedBeneficiary.FullName;

            IsBusy = true;
            SetNeutralStatus($"Marking {displayName} as verified...");

            try
            {
                await using var context = new AppDbContext();
                var service = new BeneficiaryVerificationService(context);
                var result = await service.VerifyAsync(stagingId, _currentUser.Id, NormalizeNullable(EditableReviewNotes));

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadPageAsync(CurrentPage, stagingId, syncValidatedSnapshot: false, CancellationToken.None);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Verification failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApproveSelectedAsync()
        {
            if (!CanApproveSelected() || SelectedBeneficiary == null || SelectedHousehold == null)
            {
                return;
            }

            var confirmMessage = SelectedHouseholdMember == null
                ? $"Approve {SelectedBeneficiary.FullName} into household {SelectedHousehold.HouseholdCode} as a new member?"
                : $"Approve {SelectedBeneficiary.FullName} and link to {SelectedHouseholdMember.FullName} in household {SelectedHousehold.HouseholdCode}?";

            if (MessageBox.Show(
                    confirmMessage,
                    "Approve Beneficiary",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var stagingId = SelectedBeneficiary.StagingId;
            var displayName = SelectedBeneficiary.FullName;

            IsBusy = true;
            SetNeutralStatus($"Approving {displayName}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new BeneficiaryVerificationService(context);
                var result = await service.ApproveAsync(
                    new BeneficiaryApprovalRequest(
                        stagingId,
                        SelectedHousehold.Id,
                        SelectedHouseholdMember?.Id,
                        NormalizeNullable(EditableReviewNotes),
                        Corrections: new BeneficiaryCorrectionRequest(
                            stagingId,
                            NormalizeNullable(EditableBeneficiaryId),
                            NormalizeNullable(EditableCivilRegistryId),
                            NormalizeNullable(EditableFirstName),
                            NormalizeNullable(EditableMiddleName),
                            NormalizeNullable(EditableLastName),
                            NormalizeNullable(EditableFullName),
                            NormalizeNullable(EditableSex),
                            NormalizeNullable(EditableDateOfBirth),
                            NormalizeNullable(EditableAge),
                            NormalizeNullable(EditableMaritalStatus),
                            NormalizeNullable(EditableAddress),
                            NormalizeNullable(EditablePwdIdNo),
                            NormalizeNullable(EditableSeniorIdNo),
                            NormalizeNullable(EditableDisabilityType),
                            NormalizeNullable(EditableReviewNotes))),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadPageAsync(CurrentPage, stagingId, syncValidatedSnapshot: false, CancellationToken.None);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Approval failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task MarkDuplicateSelectedAsync()
        {
            if (!CanMarkDuplicateSelected() || SelectedBeneficiary == null)
            {
                return;
            }

            if (MessageBox.Show(
                    $"Mark {SelectedBeneficiary.FullName} as a duplicate record?",
                    "Mark Duplicate",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await ChangeChecklistStatusAsync(
                "duplicate",
                (service, stagingId) => service.MarkDuplicateAsync(stagingId, _currentUser.Id, NormalizeNullable(EditableReviewNotes)));
        }

        private async Task MarkInactiveSelectedAsync()
        {
            if (!CanMarkInactiveSelected() || SelectedBeneficiary == null)
            {
                return;
            }

            if (MessageBox.Show(
                    $"Mark {SelectedBeneficiary.FullName} as inactive?",
                    "Mark Inactive",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await ChangeChecklistStatusAsync(
                "inactive",
                (service, stagingId) => service.MarkInactiveAsync(stagingId, _currentUser.Id, NormalizeNullable(EditableReviewNotes)));
        }

        private async Task RejectSelectedAsync()
        {
            if (!CanRejectSelected() || SelectedBeneficiary == null)
            {
                return;
            }

            if (MessageBox.Show(
                    $"Reject {SelectedBeneficiary.FullName} from the registry checklist?",
                    "Reject Beneficiary",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await ChangeChecklistStatusAsync(
                "rejected",
                (service, stagingId) => service.RejectAsync(stagingId, _currentUser.Id, NormalizeNullable(EditableReviewNotes)));
        }

        private async Task ChangeChecklistStatusAsync(
            string actionLabel,
            Func<BeneficiaryVerificationService, int, Task<BeneficiaryVerificationOperationResult>> action)
        {
            if (SelectedBeneficiary == null)
            {
                return;
            }

            var stagingId = SelectedBeneficiary.StagingId;
            var displayName = SelectedBeneficiary.FullName;

            IsBusy = true;
            SetNeutralStatus($"Updating {displayName} as {actionLabel}...");

            try
            {
                await using var context = new AppDbContext();
                var service = new BeneficiaryVerificationService(context);
                var result = await action(service, stagingId);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadPageAsync(CurrentPage, stagingId, syncValidatedSnapshot: false, CancellationToken.None);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Could not update beneficiary status: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseActionCanExecuteChanged()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _saveCorrectionsCommand.RaiseCanExecuteChanged();
            _verifyCommand.RaiseCanExecuteChanged();
            _approveCommand.RaiseCanExecuteChanged();
            _markDuplicateCommand.RaiseCanExecuteChanged();
            _markInactiveCommand.RaiseCanExecuteChanged();
            _rejectCommand.RaiseCanExecuteChanged();
            _uploadDigitalIdPhotoCommand.RaiseCanExecuteChanged();
            _printDigitalIdCommand.RaiseCanExecuteChanged();
            _createLookupScannerSessionCommand.RaiseCanExecuteChanged();
        }

        private void ClearLoadedState()
        {
            _records.Clear();
            _households.Clear();
            _availableHouseholdMembers.Clear();
            RecordsView = CollectionViewSource.GetDefaultView(_records);
            SelectedBeneficiary = null;
            SelectedHousehold = null;
            SelectedHouseholdMember = null;
            TotalCount = 0;
            PendingCount = 0;
            VerifiedCount = 0;
            ApprovedCount = 0;
            DuplicateCount = 0;
            InactiveCount = 0;
            RejectedCount = 0;
            FilteredRecordCount = 0;
            CurrentPage = 1;
            ResetDigitalIdPreview();
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

        private void SetWarningStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#92400E");
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#991B1B");
        }

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }

        private static BitmapSource? BuildImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string StoreBeneficiaryPhoto(string sourcePath, int stagingId)
        {
            var extension = Path.GetExtension(sourcePath);
            var targetDirectory = Path.Combine(AppContext.BaseDirectory, "beneficiary-id-photos");
            Directory.CreateDirectory(targetDirectory);

            var targetPath = Path.Combine(
                targetDirectory,
                $"beneficiary-{stagingId:D6}-{DateTime.Now:yyyyMMddHHmmss}{extension}");

            File.Copy(sourcePath, targetPath, overwrite: true);
            return targetPath;
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

    }

    public sealed class StagedBeneficiaryItem
    {
        public int StagingId { get; init; }
        public string BeneficiaryId { get; init; } = string.Empty;
        public string CivilRegistryId { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string MiddleName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Sex { get; init; } = string.Empty;
        public string Age { get; init; } = string.Empty;
        public bool IsPwd { get; init; }
        public bool IsSenior { get; init; }
        public VerificationStatus VerificationStatus { get; init; }
        public DateTime ImportedAt { get; init; }
        public string DateOfBirth { get; init; } = string.Empty;
        public string MaritalStatus { get; init; } = string.Empty;
        public string DisabilityType { get; init; } = string.Empty;
        public string SeniorIdNo { get; init; } = string.Empty;
        public string PwdIdNo { get; init; } = string.Empty;
        public string ReviewNotes { get; init; } = string.Empty;
        public int? LinkedHouseholdId { get; init; }
        public int? LinkedHouseholdMemberId { get; init; }
        public string DigitalIdCardNumber { get; init; } = string.Empty;
        public string DigitalIdQrPayload { get; init; } = string.Empty;
        public string? DigitalIdPhotoPath { get; init; }
        public DateTime? DigitalIdIssuedAt { get; init; }
        public bool HasDigitalId => !string.IsNullOrWhiteSpace(DigitalIdCardNumber) && !string.IsNullOrWhiteSpace(DigitalIdQrPayload);

        public string StatusText => VerificationStatus.ToString();
        public string PwdLabel => IsPwd ? "Yes" : "No";
        public string SeniorLabel => IsSenior ? "Yes" : "No";
        public string SexAgeSummary => string.IsNullOrWhiteSpace(Age) ? Sex : $"{Sex} / {Age}";
        public string PwdDetails => string.IsNullOrWhiteSpace(PwdIdNo) ? "No PWD ID" : $"PWD ID: {PwdIdNo}";
        public string SeniorDetails => string.IsNullOrWhiteSpace(SeniorIdNo) ? "No Senior ID" : $"Senior ID: {SeniorIdNo}";
        public string LinkSummary =>
            LinkedHouseholdMemberId.HasValue
                ? $"Linked to household member #{LinkedHouseholdMemberId.Value}"
                : LinkedHouseholdId.HasValue
                    ? $"Linked to household #{LinkedHouseholdId.Value}"
                    : "Not linked to a registry record yet";

        public Brush StatusBrush => VerificationStatus switch
        {
            VerificationStatus.Approved => CreateBrush("#DCFCE7"),
            VerificationStatus.Rejected => CreateBrush("#FEE2E2"),
            VerificationStatus.Verified => CreateBrush("#DBEAFE"),
            VerificationStatus.Duplicate => CreateBrush("#FFEDD5"),
            VerificationStatus.Inactive => CreateBrush("#E5E7EB"),
            _ => CreateBrush("#FEF3C7")
        };

        public Brush StatusTextBrush => VerificationStatus switch
        {
            VerificationStatus.Approved => CreateBrush("#166534"),
            VerificationStatus.Rejected => CreateBrush("#991B1B"),
            VerificationStatus.Verified => CreateBrush("#1D4ED8"),
            VerificationStatus.Duplicate => CreateBrush("#9A3412"),
            VerificationStatus.Inactive => CreateBrush("#374151"),
            _ => CreateBrush("#92400E")
        };

        public static StagedBeneficiaryItem FromEntity(BeneficiaryStaging row, BeneficiaryDigitalId? digitalId = null)
        {
            var fullName = string.IsNullOrWhiteSpace(row.FullName)
                ? string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()))
                : row.FullName.Trim();

            return new StagedBeneficiaryItem
            {
                StagingId = row.StagingID,
                BeneficiaryId = row.BeneficiaryId ?? string.Empty,
                CivilRegistryId = row.CivilRegistryId ?? string.Empty,
                FirstName = row.FirstName ?? string.Empty,
                MiddleName = row.MiddleName ?? string.Empty,
                LastName = row.LastName ?? string.Empty,
                FullName = fullName,
                Address = row.Address ?? string.Empty,
                Sex = row.Sex ?? string.Empty,
                Age = row.Age ?? string.Empty,
                IsPwd = row.IsPwd,
                IsSenior = row.IsSenior,
                VerificationStatus = row.VerificationStatus,
                ImportedAt = row.ImportedAt,
                DateOfBirth = row.DateOfBirth ?? string.Empty,
                MaritalStatus = row.MaritalStatus ?? string.Empty,
                DisabilityType = row.DisabilityType ?? string.Empty,
                SeniorIdNo = row.SeniorIdNo ?? string.Empty,
                PwdIdNo = row.PwdIdNo ?? string.Empty,
                ReviewNotes = row.ReviewNotes ?? string.Empty,
                LinkedHouseholdId = row.LinkedHouseholdId,
                LinkedHouseholdMemberId = row.LinkedHouseholdMemberId,
                DigitalIdCardNumber = digitalId?.CardNumber ?? string.Empty,
                DigitalIdQrPayload = digitalId?.QrPayload ?? string.Empty,
                DigitalIdPhotoPath = digitalId?.PhotoPath,
                DigitalIdIssuedAt = digitalId?.IssuedAt
            };
        }

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }
    }

    public sealed class HouseholdOption
    {
        public int Id { get; init; }
        public string HouseholdCode { get; init; } = string.Empty;
        public string HeadName { get; init; } = string.Empty;
        public string AddressLine { get; init; } = string.Empty;
        public IReadOnlyList<HouseholdMemberOption> Members { get; init; } = Array.Empty<HouseholdMemberOption>();
        public string DisplayLabel => $"{HouseholdCode} - {HeadName}";

        public static HouseholdOption FromEntity(Household household)
        {
            return new HouseholdOption
            {
                Id = household.Id,
                HouseholdCode = household.HouseholdCode,
                HeadName = household.HeadName,
                AddressLine = household.AddressLine,
                Members = household.Members
                    .OrderBy(member => member.FullName)
                    .Select(HouseholdMemberOption.FromEntity)
                    .ToList()
            };
        }
    }

    public sealed class HouseholdMemberOption
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string RelationshipToHead { get; init; } = string.Empty;
        public string DisplayLabel =>
            string.IsNullOrWhiteSpace(RelationshipToHead)
                ? FullName
                : $"{FullName} ({RelationshipToHead})";

        public static HouseholdMemberOption FromEntity(HouseholdMember member)
        {
            return new HouseholdMemberOption
            {
                Id = member.Id,
                FullName = member.FullName,
                RelationshipToHead = member.RelationshipToHead
            };
        }
    }
}

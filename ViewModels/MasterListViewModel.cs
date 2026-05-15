using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class MasterListFilterOption : ObservableObject
    {
        private bool _isSelected;
        public string Label { get; init; } = string.Empty;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public sealed class MasterListViewModel : ObservableObject
    {
        private readonly User? _currentUser;
        private readonly ObservableCollection<MasterListBeneficiary> _pendingBeneficiaries = new();
        private readonly ObservableCollection<MasterListBeneficiary> _approvedBeneficiaries = new();
        private readonly ObservableCollection<MasterListFilterOption> _filterOptions = new();
        private readonly ObservableCollection<BeneficiaryAssistanceLedgerEntry> _selectedBeneficiaryHistory = new();
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _previousPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _viewHistoryCommand;
        private readonly RelayCommand _printDigitalIdCommand;
        private readonly RelayCommand _processScanCommand;
        private readonly RelayCommand _saveCorrectionsCommand;
        private readonly RelayCommand _returnToPendingCommand;
        private readonly RelayCommand _approveCommand;
        private readonly RelayCommand _rejectCommand;
        private readonly RelayCommand _uploadDigitalIdPhotoCommand;
        private readonly RelayCommand _cropDigitalIdPhotoCommand;
        private readonly RelayCommand _createLookupScannerSessionCommand;

        private readonly IMasterListQueryService _queryService;
        private readonly bool _autoRefresh;
        private CancellationTokenSource? _loadCts;
        private MasterListBeneficiary? _selectedBeneficiary;
        private MasterListBeneficiary? _selectedPendingBeneficiary;
        private MasterListBeneficiary? _selectedApprovedBeneficiary;
        private string _searchText = string.Empty;
        private string _scannerInput = string.Empty;
        private int _selectedPageSize = 100;
        private bool _isBusy;
        private bool _isHistoryLoading;
        private bool _isFilterPanelOpen;
        private bool _isDetailPanelOpen;
        private string _statusMessage = "Loading validated beneficiaries...";
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private int _totalBeneficiaries;
        private int _linkedCivilRegistryCount;
        private int _seniorCount;
        private int _pwdCount;
        private int _filteredBeneficiaryCount;
        private int _currentPage = 1;
        private bool _isHeaderCollapsed;
        private string _snapshotSourceSummary = "Validated beneficiaries snapshot";
        private string _lastUpdatedSummary = "Last refresh: --";

        // Editable fields
        private int? _selectedStagingId;
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
        private bool _editableIsPwd;
        private string _editablePwdIdNo = string.Empty;
        private bool _editableIsSenior;
        private string _editableSeniorIdNo = string.Empty;
        private string _editableDisabilityType = string.Empty;
        private string _editableCauseOfDisability = string.Empty;
        private string _editableReviewNotes = string.Empty;

        // Digital ID Preview
        private string _digitalIdCardNumber = "No digital ID issued yet.";
        private string _digitalIdIssuedAtText = "Approve a beneficiary to generate a digital ID.";
        private BitmapSource? _digitalIdPhotoImage;
        private BitmapSource? _digitalIdQrImage;

        // Scanner Session
        private string _lookupScannerSessionUrl = string.Empty;
        private string _lookupScannerSessionPin = string.Empty;
        private string _lookupScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _lookupScannerQrImage;

        public MasterListViewModel(User currentUser)
            : this(currentUser, new MasterListService(), autoLoad: true, autoRefresh: true)
        {
        }

        internal MasterListViewModel(User? currentUser, IMasterListQueryService queryService, bool autoLoad, bool autoRefresh)
        {
            _currentUser = currentUser;
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _autoRefresh = autoRefresh;

            InitializeFilterOptions();
            PageSizeOptions = new ObservableCollection<int> { 50, 100, 250, 500 };
            _refreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
            _previousPageCommand = new RelayCommand(async _ => await GoToPreviousPageAsync(), _ => !IsBusy && CurrentPage > 1);
            _nextPageCommand = new RelayCommand(async _ => await GoToNextPageAsync(), _ => !IsBusy && CurrentPage < TotalPages);
            _viewHistoryCommand = new RelayCommand(async _ => await ExecuteViewHistoryAsync(), _ => SelectedBeneficiary != null);
            _printDigitalIdCommand = new RelayCommand(async _ => await ExecutePrintDigitalIdAsync(), _ => CanUseDigitalId());
            _processScanCommand = new RelayCommand(_ => ExecuteProcessScan());
            _toggleHeaderCommand = new RelayCommand(_ => IsHeaderCollapsed = !IsHeaderCollapsed);

            _saveCorrectionsCommand = new RelayCommand(async _ => await SaveCorrectionsAsync(), _ => CanSaveCorrectionsSelected());
            _returnToPendingCommand = new RelayCommand(async _ => await ReturnToPendingAsync(), _ => CanReturnToPendingSelected());
            _approveCommand = new RelayCommand(async _ => await ApproveSelectedAsync(), _ => CanApproveSelected());
            _rejectCommand = new RelayCommand(async _ => await RejectSelectedAsync(), _ => CanRejectSelected());
            _uploadDigitalIdPhotoCommand = new RelayCommand(async _ => await UploadDigitalIdPhotoAsync(), _ => CanUploadDigitalIdPhoto());
            _cropDigitalIdPhotoCommand = new RelayCommand(async _ => await CropDigitalIdPhotoAsync(), _ => CanCropDigitalIdPhoto());
            _createLookupScannerSessionCommand = new RelayCommand(async _ => await CreateLookupScannerSessionAsync(), _ => CanCreateLookupScannerSession());

            if (autoLoad)
            {
                _ = RefreshAsync();
            }
        }

        public ObservableCollection<BeneficiaryAssistanceLedgerEntry> SelectedBeneficiaryHistory => _selectedBeneficiaryHistory;

        public string ScannerInput
        {
            get => _scannerInput;
            set => SetProperty(ref _scannerInput, value);
        }

        private void ExecuteProcessScan()
        {
            if (string.IsNullOrWhiteSpace(ScannerInput)) return;

            SearchText = ScannerInput.Trim();
            ScannerInput = string.Empty;
        }

        public ICommand ProcessScanCommand => _processScanCommand;

        public bool IsHistoryLoading
        {
            get => _isHistoryLoading;
            private set => SetProperty(ref _isHistoryLoading, value);
        }

        public bool IsAnyOverlayOpen => _isFilterPanelOpen || _isDetailPanelOpen;

        public bool IsFilterPanelOpen
        {
            get => _isFilterPanelOpen;
            set
            {
                if (SetProperty(ref _isFilterPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public bool IsDetailPanelOpen
        {
            get => _isDetailPanelOpen;
            set
            {
                if (SetProperty(ref _isDetailPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public Visibility HistoryEmptyStateVisibility => (SelectedBeneficiaryHistory.Count == 0 && !IsHistoryLoading) ? Visibility.Visible : Visibility.Collapsed;

        private readonly RelayCommand _toggleHeaderCommand;
        public ICommand ToggleHeaderCommand => _toggleHeaderCommand;

        public bool IsHeaderCollapsed
        {
            get => _isHeaderCollapsed;
            set
            {
                if (SetProperty(ref _isHeaderCollapsed, value))
                {
                    OnPropertyChanged(nameof(HeaderVisibility));
                    OnPropertyChanged(nameof(HeaderBarVisibility));
                }
            }
        }

        public Visibility HeaderVisibility => IsHeaderCollapsed ? Visibility.Collapsed : Visibility.Visible;
        public Visibility HeaderBarVisibility => IsHeaderCollapsed ? Visibility.Visible : Visibility.Collapsed;

        // Editable fields properties
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

        public bool EditableIsPwd
        {
            get => _editableIsPwd;
            set => SetProperty(ref _editableIsPwd, value);
        }

        public string EditablePwdIdNo
        {
            get => _editablePwdIdNo;
            set => SetProperty(ref _editablePwdIdNo, value);
        }

        public bool EditableIsSenior
        {
            get => _editableIsSenior;
            set => SetProperty(ref _editableIsSenior, value);
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

        public string EditableCauseOfDisability
        {
            get => _editableCauseOfDisability;
            set => SetProperty(ref _editableCauseOfDisability, value);
        }

        public string EditableReviewNotes
        {
            get => _editableReviewNotes;
            set
            {
                if (SetProperty(ref _editableReviewNotes, value))
                {
                    _rejectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // Digital ID Preview properties
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

        // Scanner Session properties
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

        public ICommand SaveCorrectionsCommand => _saveCorrectionsCommand;
        public ICommand ReturnToPendingCommand => _returnToPendingCommand;
        public ICommand ApproveCommand => _approveCommand;
        public ICommand RejectCommand => _rejectCommand;
        public ICommand UploadDigitalIdPhotoCommand => _uploadDigitalIdPhotoCommand;
        public ICommand CreateLookupScannerSessionCommand => _createLookupScannerSessionCommand;

        public string LinkedCivilRegistryTooltip => BuildMetricTooltip("linked civil registry", LinkedCivilRegistryCount);
        public string SeniorTooltip => BuildMetricTooltip("senior citizens", SeniorCount);
        public string PwdTooltip => BuildMetricTooltip("persons with disability", PwdCount);

        private string BuildMetricTooltip(string label, int count)
        {
            if (TotalBeneficiaries <= 0) return $"{count:N0} {label}";
            var percent = (double)count / TotalBeneficiaries * 100;
            return $"{count:N0} {label} ({percent:F1}% of registry)";
        }

        private void InitializeFilterOptions()
        {
            foreach (var filter in MasterListQuickFilters.All)
            {
                if (filter == MasterListQuickFilters.AllBeneficiaries) continue;
                
                var option = new MasterListFilterOption { Label = filter };
                option.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MasterListFilterOption.IsSelected))
                    {
                        HandleFilterConflict(option);
                        QueueReloadFromFirstPage();
                    }
                };
                _filterOptions.Add(option);
            }
        }

        private void HandleFilterConflict(MasterListFilterOption changedOption)
        {
            if (!changedOption.IsSelected) return;

            // Radio-button behavior: deselect all other filters
            foreach (var option in _filterOptions)
            {
                if (option != changedOption && option.IsSelected)
                {
                    option.IsSelected = false;
                }
            }
        }

        public ObservableCollection<MasterListFilterOption> FilterOptions => _filterOptions;

        public ObservableCollection<int> PageSizeOptions { get; }

        public ObservableCollection<MasterListBeneficiary> PendingBeneficiaries => _pendingBeneficiaries;
        public ObservableCollection<MasterListBeneficiary> ApprovedBeneficiaries => _approvedBeneficiaries;

        public MasterListBeneficiary? SelectedPendingBeneficiary
        {
            get => _selectedPendingBeneficiary;
            set
            {
                if (SetProperty(ref _selectedPendingBeneficiary, value))
                {
                    if (value != null)
                    {
                        SelectedApprovedBeneficiary = null;
                        SelectedBeneficiary = value;
                    }
                }
            }
        }

        public MasterListBeneficiary? SelectedApprovedBeneficiary
        {
            get => _selectedApprovedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedApprovedBeneficiary, value))
                {
                    if (value != null)
                    {
                        SelectedPendingBeneficiary = null;
                        SelectedBeneficiary = value;
                    }
                }
            }
        }

        public MasterListBeneficiary? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedBeneficiary, value))
                {
                    _selectedBeneficiaryHistory.Clear();
                    OnPropertyChanged(nameof(HistoryEmptyStateVisibility));
                    _viewHistoryCommand.RaiseCanExecuteChanged();
                    _printDigitalIdCommand.RaiseCanExecuteChanged();
                    
                    if (value != null)
                    {
                        _ = LoadSelectedBeneficiaryHistoryAsync();
                        _ = SyncEditableFieldsFromSelectionAsync(value);
                    }
                    else
                    {
                        ClearEditableFields();
                    }

                    RaiseActionCanExecuteChanged();
                }
            }
        }

        private async Task SyncEditableFieldsFromSelectionAsync(MasterListBeneficiary beneficiary)
        {
            try
            {
                await using var context = new AppDbContext();
                
                // Fetch or Create Staging Record
                var staging = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(s => s.ResidentsId == beneficiary.ResidentsId);

                // Fetch Digital ID
                BeneficiaryDigitalId? digitalId = null;
                if (staging != null)
                {
                    digitalId = await context.BeneficiaryDigitalIds
                        .FirstOrDefaultAsync(d => d.BeneficiaryStagingId == staging.StagingID && d.IsActive);
                    _selectedStagingId = staging.StagingID;
                }
                else
                {
                    _selectedStagingId = null;
                }

                // Populate editable fields (prefer staging, fallback to snapshot)
                EditableBeneficiaryId = staging?.BeneficiaryId ?? beneficiary.BeneficiaryId ?? string.Empty;
                EditableCivilRegistryId = staging?.CivilRegistryId ?? beneficiary.CivilRegistryId ?? string.Empty;
                EditableFirstName = staging?.FirstName ?? beneficiary.FirstName ?? string.Empty;
                EditableMiddleName = staging?.MiddleName ?? beneficiary.MiddleName ?? string.Empty;
                EditableLastName = staging?.LastName ?? beneficiary.LastName ?? string.Empty;
                EditableFullName = staging?.FullName ?? beneficiary.FullName ?? string.Empty;
                EditableSex = staging?.Sex ?? beneficiary.Sex ?? string.Empty;
                EditableDateOfBirth = staging?.DateOfBirth ?? beneficiary.DateOfBirth ?? string.Empty;
                EditableAge = staging?.Age ?? beneficiary.Age ?? string.Empty;
                EditableMaritalStatus = staging?.MaritalStatus ?? beneficiary.MaritalStatus ?? string.Empty;
                EditableAddress = staging?.Address ?? beneficiary.Address ?? string.Empty;
                EditableIsPwd = staging?.IsPwd ?? beneficiary.IsPwd;
                EditablePwdIdNo = staging?.PwdIdNo ?? beneficiary.PwdIdNo ?? string.Empty;
                EditableIsSenior = staging?.IsSenior ?? beneficiary.IsSenior;
                EditableSeniorIdNo = staging?.SeniorIdNo ?? beneficiary.SeniorIdNo ?? string.Empty;
                EditableDisabilityType = staging?.DisabilityType ?? beneficiary.DisabilityType ?? string.Empty;
                EditableCauseOfDisability = staging?.CauseOfDisability ?? beneficiary.CauseOfDisability ?? string.Empty;
                EditableReviewNotes = staging?.ReviewNotes ?? string.Empty;

                SyncDigitalIdPreview(digitalId, staging?.PhotoPath);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to sync staging data: {ex.Message}");
            }
        }

        private void SyncDigitalIdPreview(BeneficiaryDigitalId? digitalId, string? stagingPhotoPath = null)
        {
            if (digitalId == null)
            {
                ResetDigitalIdPreview();
                if (!string.IsNullOrWhiteSpace(stagingPhotoPath))
                {
                    DigitalIdPhotoImage = BuildImage(stagingPhotoPath);
                }
                return;
            }

            DigitalIdCardNumber = digitalId.CardNumber;
            DigitalIdIssuedAtText = $"Issued on {digitalId.IssuedAt:MMMM dd, yyyy hh:mm tt}";
            DigitalIdPhotoImage = BuildImage(digitalId.PhotoPath ?? stagingPhotoPath);
            DigitalIdQrImage = QrCodeToolkitService.GenerateQrImage(digitalId.QrPayload, 14);

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

        private void ClearEditableFields()
        {
            _selectedStagingId = null;
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
            EditableIsPwd = false;
            EditablePwdIdNo = string.Empty;
            EditableIsSenior = false;
            EditableSeniorIdNo = string.Empty;
            EditableDisabilityType = string.Empty;
            EditableCauseOfDisability = string.Empty;
            EditableReviewNotes = string.Empty;
            ResetDigitalIdPreview();
        }

        private void RaiseActionCanExecuteChanged()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _viewHistoryCommand.RaiseCanExecuteChanged();
            _printDigitalIdCommand.RaiseCanExecuteChanged();
            _saveCorrectionsCommand.RaiseCanExecuteChanged();
            _returnToPendingCommand.RaiseCanExecuteChanged();
            _approveCommand.RaiseCanExecuteChanged();
            _rejectCommand.RaiseCanExecuteChanged();
            _uploadDigitalIdPhotoCommand.RaiseCanExecuteChanged();
            _cropDigitalIdPhotoCommand.RaiseCanExecuteChanged();
            _createLookupScannerSessionCommand.RaiseCanExecuteChanged();
        }

        private bool CanUseDigitalId() => SelectedBeneficiary != null && SelectedBeneficiary.IsApproved;
        private bool CanSaveCorrectionsSelected() => SelectedBeneficiary != null && !IsBusy;
        private bool CanReturnToPendingSelected() => SelectedBeneficiary != null && SelectedBeneficiary.VerificationStatus != VerificationStatus.Pending && !IsBusy;
        private bool CanApproveSelected() => SelectedBeneficiary != null && (SelectedBeneficiary.VerificationStatus == VerificationStatus.Pending || SelectedBeneficiary.VerificationStatus == VerificationStatus.Verified) && !IsBusy;
        private bool CanRejectSelected() => SelectedBeneficiary != null && SelectedBeneficiary.VerificationStatus != VerificationStatus.Approved && !string.IsNullOrWhiteSpace(EditableReviewNotes) && !IsBusy;
        private bool CanUploadDigitalIdPhoto() => SelectedBeneficiary != null && !IsBusy;
        private bool CanCropDigitalIdPhoto() => SelectedBeneficiary != null && !IsBusy && _digitalIdPhotoImage != null;
        private bool CanCreateLookupScannerSession() => !IsBusy;

        private async Task SaveCorrectionsAsync()
        {
            if (SelectedBeneficiary == null || _currentUser == null) return;

            IsBusy = true;
            SetNeutralStatus("Saving corrections...");

            try
            {
                await using var context = new AppDbContext();
                var stagingId = await EnsureStagingRecordAsync(context);
                
                var verificationService = new BeneficiaryVerificationService(context);
                var result = await verificationService.SaveCorrectionsAsync(
                    BuildCorrectionRequest(stagingId),
                    _currentUser.Id);

                if (result.IsSuccess)
                {
                    SetSuccessStatus(result.Message);
                    await RefreshCurrentBeneficiaryAsync();
                }
                else
                {
                    SetErrorStatus(result.Message);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to save corrections: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RaiseActionCanExecuteChanged();
            }
        }

        private async Task ReturnToPendingAsync()
        {
            if (SelectedBeneficiary == null || _currentUser == null || _selectedStagingId == null) return;

            IsBusy = true;
            SetNeutralStatus("Returning to pending review...");

            try
            {
                await using var context = new AppDbContext();
                var verificationService = new BeneficiaryVerificationService(context);
                var result = await verificationService.ReturnToPendingAsync(_selectedStagingId.Value, _currentUser.Id, EditableReviewNotes);

                if (result.IsSuccess)
                {
                    SetSuccessStatus(result.Message);
                    await RefreshCurrentBeneficiaryAsync();
                }
                else
                {
                    SetErrorStatus(result.Message);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to return to pending: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RaiseActionCanExecuteChanged();
            }
        }

        private async Task ApproveSelectedAsync()
        {
            if (SelectedBeneficiary == null || _currentUser == null) return;

            // Prompt for photo if missing
            if (_digitalIdPhotoImage == null)
            {
                var promptResult = MessageBox.Show(
                    "This beneficiary doesn't have a digital ID photo yet. Would you like to add one before approving?",
                    "Missing Photo",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (promptResult == MessageBoxResult.Yes)
                {
                    await UploadDigitalIdPhotoAsync();
                    if (_digitalIdPhotoImage == null) return; // Cancelled
                }
                else if (promptResult == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            IsBusy = true;
            SetNeutralStatus("Approving beneficiary...");

            try
            {
                await using var context = new AppDbContext();
                var stagingId = await EnsureStagingRecordAsync(context);

                var verificationService = new BeneficiaryVerificationService(context);
                var result = await verificationService.ApproveAsync(
                    new BeneficiaryApprovalRequest(
                        stagingId,
                        0, // Household logic not implemented in this view
                        null,
                        EditableReviewNotes,
                        BuildCorrectionRequest(stagingId)),
                    _currentUser.Id);

                if (result.IsSuccess)
                {
                    SetSuccessStatus(result.Message);
                    await RefreshCurrentBeneficiaryAsync();
                }
                else
                {
                    SetErrorStatus(result.Message);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to approve: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RaiseActionCanExecuteChanged();
            }
        }

        private async Task RejectSelectedAsync()
        {
            if (SelectedBeneficiary == null || _currentUser == null || string.IsNullOrWhiteSpace(EditableReviewNotes)) return;

            IsBusy = true;
            SetNeutralStatus("Rejecting beneficiary...");

            try
            {
                await using var context = new AppDbContext();
                var stagingId = await EnsureStagingRecordAsync(context);

                var verificationService = new BeneficiaryVerificationService(context);
                var result = await verificationService.RejectAsync(stagingId, _currentUser.Id, EditableReviewNotes);

                if (result.IsSuccess)
                {
                    SetSuccessStatus(result.Message);
                    await RefreshCurrentBeneficiaryAsync();
                }
                else
                {
                    SetErrorStatus(result.Message);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to reject: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RaiseActionCanExecuteChanged();
            }
        }

        private async Task<int> EnsureStagingRecordAsync(AppDbContext context)
        {
            if (_selectedStagingId.HasValue) return _selectedStagingId.Value;
            if (SelectedBeneficiary == null) throw new InvalidOperationException("No beneficiary selected.");

            var staging = new BeneficiaryStaging
            {
                ResidentsId = SelectedBeneficiary.ResidentsId,
                BeneficiaryId = SelectedBeneficiary.BeneficiaryId,
                CivilRegistryId = SelectedBeneficiary.CivilRegistryId,
                FirstName = SelectedBeneficiary.FirstName,
                MiddleName = SelectedBeneficiary.MiddleName,
                LastName = SelectedBeneficiary.LastName,
                FullName = SelectedBeneficiary.FullName,
                Sex = SelectedBeneficiary.Sex,
                DateOfBirth = SelectedBeneficiary.DateOfBirth,
                Age = SelectedBeneficiary.Age,
                MaritalStatus = SelectedBeneficiary.MaritalStatus,
                Address = SelectedBeneficiary.Address,
                IsPwd = SelectedBeneficiary.IsPwd,
                PwdIdNo = SelectedBeneficiary.PwdIdNo,
                IsSenior = SelectedBeneficiary.IsSenior,
                SeniorIdNo = SelectedBeneficiary.SeniorIdNo,
                DisabilityType = SelectedBeneficiary.DisabilityType,
                CauseOfDisability = SelectedBeneficiary.CauseOfDisability,
                VerificationStatus = VerificationStatus.Pending,
                ImportedAt = DateTime.Now
            };

            context.BeneficiaryStaging.Add(staging);
            await context.SaveChangesAsync();
            
            _selectedStagingId = staging.StagingID;
            return staging.StagingID;
        }

        private BeneficiaryCorrectionRequest BuildCorrectionRequest(int stagingId)
        {
            return new BeneficiaryCorrectionRequest(
                stagingId,
                EditableBeneficiaryId,
                EditableCivilRegistryId,
                EditableFirstName,
                EditableMiddleName,
                EditableLastName,
                EditableFullName,
                EditableSex,
                EditableDateOfBirth,
                EditableAge,
                EditableMaritalStatus,
                EditableAddress,
                EditableIsPwd,
                EditablePwdIdNo,
                EditableIsSenior,
                EditableSeniorIdNo,
                EditableDisabilityType,
                EditableCauseOfDisability,
                EditableReviewNotes);
        }

        private async Task RefreshCurrentBeneficiaryAsync()
        {
            if (SelectedBeneficiary == null) return;
            await SyncEditableFieldsFromSelectionAsync(SelectedBeneficiary);
            
            // Also update the list item if possible
            var stagingStatus = await GetStagingStatusAsync(SelectedBeneficiary.ResidentsId);
            SelectedBeneficiary.VerificationStatus = stagingStatus;
            
            // Force property change notification for list UI
            UpdateBeneficiaryInLists(SelectedBeneficiary);
        }

        private void UpdateBeneficiaryInLists(MasterListBeneficiary beneficiary)
        {
            var pIndex = _pendingBeneficiaries.IndexOf(beneficiary);
            if (pIndex >= 0)
            {
                _pendingBeneficiaries[pIndex] = beneficiary;
                if (SelectedPendingBeneficiary == beneficiary) SelectedPendingBeneficiary = _pendingBeneficiaries[pIndex];
            }

            var aIndex = _approvedBeneficiaries.IndexOf(beneficiary);
            if (aIndex >= 0)
            {
                _approvedBeneficiaries[aIndex] = beneficiary;
                if (SelectedApprovedBeneficiary == beneficiary) SelectedApprovedBeneficiary = _approvedBeneficiaries[aIndex];
            }
        }

        private async Task<VerificationStatus> GetStagingStatusAsync(long residentsId)
        {
            await using var context = new AppDbContext();
            var staging = await context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ResidentsId == residentsId);
            
            return staging?.VerificationStatus ?? VerificationStatus.Pending;
        }

        private async Task CropDigitalIdPhotoAsync()
        {
            if (SelectedBeneficiary == null || _digitalIdPhotoImage == null) return;

            var cropDialog = new Views.PhotoCropDialog(_digitalIdPhotoImage)
            {
                Owner = Application.Current.MainWindow
            };

            if (cropDialog.ShowDialog() != true || cropDialog.CroppedImage == null) return;

            IsBusy = true;
            SetNeutralStatus("Saving cropped photo...");

            try
            {
                await using var context = new AppDbContext();
                var stagingId = await EnsureStagingRecordAsync(context);

                // Encode cropped BitmapSource to a temp PNG file
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Photos");
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                var fileName = $"beneficiary_{stagingId}_crop_{DateTime.Now:yyyyMMddHHmmss}.png";
                var destPath = Path.Combine(directory, fileName);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(cropDialog.CroppedImage));
                await using (var fs = new FileStream(destPath, FileMode.Create))
                    encoder.Save(fs);

                var digitalId = await context.BeneficiaryDigitalIds
                    .FirstOrDefaultAsync(d => d.BeneficiaryStagingId == stagingId && d.IsActive);

                var staging = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(s => s.StagingID == stagingId);

                if (digitalId != null)
                {
                    digitalId.PhotoPath = destPath;
                    await context.SaveChangesAsync();
                    SyncDigitalIdPreview(digitalId);
                    SetSuccessStatus("Cropped photo saved to digital ID.");
                }
                else if (staging != null)
                {
                    staging.PhotoPath = destPath;
                    await context.SaveChangesAsync();
                    SyncDigitalIdPreview(null, destPath);
                    SetSuccessStatus("Photo cropped. It will be applied when the digital ID is issued.");
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to save cropped photo: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RaiseActionCanExecuteChanged();
            }
        }

        private async Task UploadDigitalIdPhotoAsync()
        {
            if (SelectedBeneficiary == null || _currentUser == null) return;

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = $"Upload digital ID photo for {SelectedBeneficiary.DisplayName}"
            };

            if (openFileDialog.ShowDialog() != true) return;

            IsBusy = true;
            SetNeutralStatus("Uploading photo...");

            try
            {
                await using var context = new AppDbContext();
                var stagingId = await EnsureStagingRecordAsync(context);

                var photoPath = await SavePhotoAsync(openFileDialog.FileName, stagingId);
                
                var digitalId = await context.BeneficiaryDigitalIds
                    .FirstOrDefaultAsync(d => d.BeneficiaryStagingId == stagingId && d.IsActive);

                var staging = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(s => s.StagingID == stagingId);

                if (digitalId != null)
                {
                    digitalId.PhotoPath = photoPath;
                    await context.SaveChangesAsync();
                    
                    SetSuccessStatus("Photo uploaded and digital ID updated.");
                    SyncDigitalIdPreview(digitalId);
                }
                else if (staging != null)
                {
                    staging.PhotoPath = photoPath;
                    await context.SaveChangesAsync();
                    
                    SetSuccessStatus("Photo uploaded. It will be included when the digital ID is issued upon approval.");
                    SyncDigitalIdPreview(null, photoPath);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to upload photo: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string> SavePhotoAsync(string sourceFilePath, int stagingId)
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Photos");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var extension = Path.GetExtension(sourceFilePath);
            var fileName = $"beneficiary_{stagingId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var destinationPath = Path.Combine(directory, fileName);

            await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.CopyToAsync(destinationStream);

            return destinationPath;
        }

        private BitmapSource? BuildImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
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
                        // Update SearchText which will trigger filtering/selection
                        SearchText = scanPayload.Trim();
                        SetSuccessStatus($"Mobile Scan Received: {scanPayload}");

                        // If exactly one beneficiary is found, select it
                        var totalFound = PendingBeneficiaries.Count + ApprovedBeneficiaries.Count;
                        if (totalFound == 1)
                        {
                            SelectedBeneficiary = PendingBeneficiaries.FirstOrDefault() ?? ApprovedBeneficiaries.FirstOrDefault();
                        }
                    }
                }
                catch
                {
                    // Ignore transient errors during polling
                }

                await Task.Delay(2000);
            }
        }

        private async Task LoadSelectedBeneficiaryHistoryAsync()
        {
            if (SelectedBeneficiary == null) return;
            IsHistoryLoading = true;

            try
            {
                await using var context = new AppDbContext();
                var ledgerService = new BeneficiaryAssistanceLedgerService(context);
                var entries = await ledgerService.GetEntriesAsync(SelectedBeneficiary.CivilRegistryId, SelectedBeneficiary.BeneficiaryId);

                _selectedBeneficiaryHistory.Clear();
                foreach (var entry in entries)
                {
                    _selectedBeneficiaryHistory.Add(entry);
                }
            }
            finally
            {
                IsHistoryLoading = false;
                OnPropertyChanged(nameof(HistoryEmptyStateVisibility));
            }
        }

        private async Task ExecuteViewHistoryAsync()
        {
            await LoadSelectedBeneficiaryHistoryAsync();
        }

        private async Task ExecutePrintDigitalIdAsync()
        {
            if (SelectedBeneficiary == null || _currentUser == null) return;
            
            IsBusy = true;
            SetNeutralStatus($"Preparing digital ID for {SelectedBeneficiary.DisplayName}...");

            try
            {
                await using var context = new AppDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);
                
                var staging = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ResidentsId == SelectedBeneficiary.ResidentsId);

                if (staging == null)
                {
                    SetErrorStatus("Beneficiary not found in verification staging. Digital ID requires an approved staging record.");
                    return;
                }

                var digitalId = await digitalIdService.EnsureIssuedAsync(staging.StagingID, _currentUser.Id);
                var qrImage = QrCodeToolkitService.GenerateQrImage(digitalId.QrPayload, 6);

                BitmapSource? photoImage = null;
                if (!string.IsNullOrWhiteSpace(digitalId.PhotoPath))
                {
                    photoImage = Helpers.LocalImageLoader.Load(digitalId.PhotoPath) as BitmapSource;
                }

                var printService = new DigitalIdPrintService();
                printService.PrintCard(new DigitalIdPrintRequest(
                    SelectedBeneficiary.DisplayName,
                    digitalId.CardNumber,
                    SelectedBeneficiary.BeneficiaryId,
                    SelectedBeneficiary.CivilRegistryId,
                    photoImage,
                    qrImage));

                SetSuccessStatus("Digital ID prepared.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to prepare digital ID: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                var val = value ?? string.Empty;
                if (SetProperty(ref _searchText, val))
                {
                    QueueReloadFromFirstPage();
                }
            }
        }

        public Visibility NoResultsVisibility => (PendingBeneficiaries.Count == 0 && ApprovedBeneficiaries.Count == 0 && !IsBusy) ? Visibility.Visible : Visibility.Collapsed;

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
                if (FilteredBeneficiaryCount == 0)
                {
                    return "Showing 0 validated beneficiaries";
                }

                var start = ((CurrentPage - 1) * SelectedPageSize) + 1;
                var totalLoaded = PendingBeneficiaries.Count + ApprovedBeneficiaries.Count;
                var end = start + totalLoaded - 1;
                return $"Showing {start:N0}-{end:N0} of {FilteredBeneficiaryCount:N0} total records";
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

        public ICommand ViewHistoryCommand => _viewHistoryCommand;

        public ICommand PrintDigitalIdCommand => _printDigitalIdCommand;

        public ICommand CropDigitalIdPhotoCommand => _cropDigitalIdPhotoCommand;

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
                var selectedFilters = FilterOptions
                    .Where(o => o.IsSelected)
                    .Select(o => o.Label)
                    .ToList();

                var searchText = (SearchText ?? string.Empty).Trim();

                // Load Pending
                var pendingFilters = new List<string>(selectedFilters);
                if (!pendingFilters.Contains(MasterListQuickFilters.Pending))
                    pendingFilters.Add(MasterListQuickFilters.Pending);

                var pendingResult = await _queryService.LoadPageAsync(
                    new MasterListPageRequest
                    {
                        SearchText = searchText,
                        QuickFilters = pendingFilters,
                        PageNumber = Math.Max(1, targetPage),
                        PageSize = SelectedPageSize
                    },
                    loadCts.Token);

                // Load Approved
                var approvedFilters = new List<string>(selectedFilters);
                if (!approvedFilters.Contains(MasterListQuickFilters.Approved))
                    approvedFilters.Add(MasterListQuickFilters.Approved);

                var approvedResult = await _queryService.LoadPageAsync(
                    new MasterListPageRequest
                    {
                        SearchText = searchText,
                        QuickFilters = approvedFilters,
                        PageNumber = Math.Max(1, targetPage),
                        PageSize = SelectedPageSize
                    },
                    loadCts.Token);

                if (loadCts.IsCancellationRequested) return;

                _pendingBeneficiaries.Clear();
                if (pendingResult?.Beneficiaries != null)
                {
                    foreach (var b in pendingResult.Beneficiaries) _pendingBeneficiaries.Add(b);
                }

                _approvedBeneficiaries.Clear();
                if (approvedResult?.Beneficiaries != null)
                {
                    foreach (var b in approvedResult.Beneficiaries) _approvedBeneficiaries.Add(b);
                }
                
                OnPropertyChanged(nameof(NoResultsVisibility));

                if (pendingResult != null && approvedResult != null)
                {
                    TotalBeneficiaries = pendingResult.TotalBeneficiaries;
                    LinkedCivilRegistryCount = pendingResult.LinkedCivilRegistryCount;
                    SeniorCount = pendingResult.SeniorCount;
                    PwdCount = pendingResult.PwdCount;
                    FilteredBeneficiaryCount = pendingResult.FilteredBeneficiaryCount + approvedResult.FilteredBeneficiaryCount;
                    CurrentPage = Math.Max(1, targetPage);

                    SnapshotSourceSummary = $"Validated beneficiaries snapshot from {pendingResult.SourceDatabase} on {pendingResult.SourceServer}";
                    LastUpdatedSummary = pendingResult.LastUpdatedAt.HasValue
                        ? $"Last synced data: {pendingResult.LastUpdatedAt.Value:MMMM dd, yyyy hh:mm tt}"
                        : "Last synced data: unavailable";

                    SelectedBeneficiary = PendingBeneficiaries.FirstOrDefault() ?? ApprovedBeneficiaries.FirstOrDefault();
                }

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
                if (loadCts.IsCancellationRequested) return;
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
            if (!_autoRefresh) return;
            _ = LoadPageAsync(1, CancellationToken.None);
        }

        private void ClearResults()
        {
            _pendingBeneficiaries.Clear();
            _approvedBeneficiaries.Clear();
            SelectedBeneficiary = null;
            SelectedPendingBeneficiary = null;
            SelectedApprovedBeneficiary = null;
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

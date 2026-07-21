using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class ProjectDistributionViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private AyudaProgram? _selectedProgram;
        private ProjectDistributionProgramListItem? _selectedProgramSummary;
        private DistributionBeneficiaryOption? _selectedAvailableBeneficiary;
        private ProjectDistributionBeneficiaryListItem? _selectedProgramBeneficiary;
        private ProjectDistributionBeneficiaryListItem? _selectedPendingBeneficiary;
        private ProjectDistributionReleaseListItem? _selectedReleasedClaim;
        private string _statusMessage = "Select a project to review released budget and beneficiary history.";
        private Brush _statusBrush = Brushes.DimGray;
        private string _selectedProgramWorkflowSummary = "Select a project to load its distribution summary.";
        private string _selectedProgramAssistanceSummary = "No project selected.";
        private string _selectedProgramBudgetCapText = "--";
        private string _selectedProgramDistributedText = "PHP 0.00";
        private string _selectedProgramRemainingBudgetText = "--";
        private string _selectedProgramBeneficiaryCountText = "0";
        private string _selectedProgramReleaseSummary = "No released beneficiary history loaded yet.";
        private string _selectedProgramDescriptionText = "Select a project to review distribution performance and beneficiary history.";
        private string _programReleaseEmptyStateMessage = "Select a project to review released beneficiary history.";
        private string _scannerInputText = string.Empty;
        /// <summary>Staging id whose attachment checklist is loaded in the release modal.</summary>
        private int _releaseRequirementsStagingId;

        public string ScannerInputText
        {
            get => _scannerInputText;
            set => SetProperty(ref _scannerInputText, value);
        }
        private string _distributionScannerSessionPin = string.Empty;
        private string _distributionScannerSessionExpiresAtText = string.Empty;

        private string _livePreviewProgramName = "No active project selected";
        private string _livePreviewPrimaryLabel = "--";
        private string _livePreviewSecondaryLabel = "Select a project to prepare the queue monitor.";
        private string _livePreviewQueueStatusText = "No queue loaded";
        private ProjectDistributionLivePreviewWindow? _livePreviewWindow;
        private ProjectDistributionLivePreviewQueueItem? _livePreviewCurrentQueueItem;
        private IReadOnlyList<AyudaProjectBeneficiary>? _livePreviewMemberships;
        private IReadOnlyDictionary<int, BeneficiaryDigitalId>? _livePreviewDigitalIdsByStagingId;
        private int _livePreviewWindowCount = 5;
        private bool _isBusy;
        private bool _isAddBeneficiaryPanelOpen;
        private bool _isScannerPanelOpen;
        private bool _isPcScannerOpen;
        private string _addBeneficiaryStatusMessage = string.Empty;
        private Brush _addBeneficiaryStatusBrush = Brushes.DimGray;
        private string _scannerStatusMessage = string.Empty;
        private Brush _scannerStatusBrush = Brushes.DimGray;
        private bool _hasScannerSession;
        private IReadOnlyDictionary<int, BeneficiaryDigitalId> _digitalIdsByStagingId = new Dictionary<int, BeneficiaryDigitalId>();
        private int _pendingCurrentPage = 1;
        private int _releasedCurrentPage = 1;
        private int _rejectedCurrentPage = 1;
        private string _addBeneficiarySearchText = string.Empty;
        private string _pendingSearchText = string.Empty;
        private string _selectedPendingDigitalIdCardNumber = "No digital ID issued yet.";
        private string _selectedPendingDigitalIdQrPayload = string.Empty;
        private string? _selectedPendingDigitalIdStatusText = "Select a pending beneficiary to review the digital ID.";
        private BitmapSource? _selectedPendingDigitalIdQrImage;
        private int _selectedScannerSessionDurationMinutes = 15;
        private const int DistributionPageSize = 10;
        /// <summary>Caps beneficiary pickers so the full municipal registry never lands in the UI at once.</summary>
        private const int BeneficiaryPickerDisplayLimit = 200;
        private int _addPanelCurrentPage = 1;
        private int _addPanelTotalPages = 1;
        private int _unpickedCurrentPage = 1;
        private int _unpickedTotalPages = 1;

        private sealed record PickerBeneficiaryRow(
            int StagingID, string? BeneficiaryId, string? CivilRegistryId, string? LastName,
            string? FirstName, string? MiddleName, string? FullName,
            int? LinkedHouseholdId, int? LinkedHouseholdMemberId, bool IsSenior, bool IsPwd);

        private sealed record EnrolledBeneficiaryKey(
            int BeneficiaryStagingId, string? CivilRegistryId, string? BeneficiaryId);
        /// <summary>Add-panel selections survive search re-queries; enrollment reads this set, not the visible page.</summary>
        private readonly HashSet<int> _addPanelSelectedStagingIds = new();
        private int _availableBeneficiarySearchVersion;
        private int _unpickedBeneficiarySearchVersion;

        private ProjectDistributionBeneficiaryListItem? _scannedBeneficiary;
        private string? _scannedBeneficiaryStatus;
        private BitmapSource? _scannedBeneficiaryPhoto;
        private string _scannedBeneficiaryAddress = string.Empty;
        private string _scannedBeneficiaryAge = string.Empty;
        private string _scannedBeneficiaryGender = string.Empty;
        private string _scannedHouseholdNumber = string.Empty;
        private string _scannedHouseholdRole = string.Empty;
        private string _scannedAllocatedAmountText = string.Empty;
        private bool _isScannedResultVisible;
        private string? _lastScannedPayload;
        private DateTime _lastScannedTime = DateTime.MinValue;
        private string _scannerActionLabel = "CONFIRM CLAIM";
        private string _scannerCancelLabel = "DECLINE";
        private bool _isScannerActive;
        private bool _isScannedBeneficiaryEligible;
        private bool _isReleaseSuccessState;
        private string _lastScanSummaryText = string.Empty;
        private Brush _lastScanSummaryBrush = Brushes.DimGray;
        private string _manualBeneficiaryIdText = string.Empty;
        private bool _isIdentityVerified;
        private string _programSearchText = string.Empty;
        private bool _hasHouseholdContext;
        private string _householdContextSummary = string.Empty;
        private string _householdDemographicsSummary = string.Empty;
        private string _householdAidReceivedSummary = string.Empty;
        private string? _householdWarningMessage;
        private bool _requiresHouseholdOverride;
        private bool _householdOverrideAcknowledged;

        private bool _isSidebarCollapsed;
        private bool _isSummaryCollapsed;
        private GridLength _sidebarWidth = new GridLength(320);

        private bool _isCreateProjectPanelOpen;
        private bool _isCreateProjectSuccessPanelOpen;
        private string _newProjectCode = string.Empty;
        private string _newProjectName = string.Empty;
        private string _newProjectDescription = string.Empty;
        private string _newProjectAssistanceType = string.Empty;
        private AyudaProgramType _newProjectSelectedType = AyudaProgramType.GeneralPurpose;
        private AssistanceReleaseKind _newProjectSelectedReleaseKind = AssistanceReleaseKind.Cash;
        private string _newProjectUnitAmountText = string.Empty;
        private string _newProjectItemDescription = string.Empty;
        private DateTime? _newProjectStartDate = DateTime.Today;
        private DateTime? _newProjectEndDate = DateTime.Today.AddDays(7);
        private string _newProjectBudgetCapText = string.Empty;
        private AyudaProgramDistributionStatus _newProjectSelectedDistributionStatus = AyudaProgramDistributionStatus.Open;
        private string _newProjectSearchText = string.Empty;
        private int _newProjectSelectedCount;

        private readonly RelayCommand _openPcScannerCommand;
        private readonly RelayCommand _processPcScanCommand;
        private readonly RelayCommand _confirmScannedClaimCommand;
        private readonly RelayCommand _cancelScannedClaimCommand;
        private readonly RelayCommand _submitManualKeyInCommand;
        private readonly RelayCommand _confirmHouseholdReleaseCommand;
        private readonly RelayCommand _cancelHouseholdConfirmCommand;
        private bool _isHouseholdConfirmVisible;
        private string _householdConfirmBeneficiaryName = string.Empty;
        private BitmapSource? _householdConfirmBeneficiaryPhoto;
        // True when the household modal was opened from the pending-list Payout Verification panel
        // (vs the scanner/key-in overlay); decides which release path the modal's Confirm runs.
        private bool _householdConfirmFromPendingList;
        private bool _isBeneficiaryValidationModalOpen;
        private DistributionBeneficiaryOption? _validatingBeneficiary;

        public ProjectDistributionViewModel(User currentUser)
        {
            _currentUser = currentUser;

            ProgramTypes = new ObservableCollection<AyudaProgramType>(
                Enum.GetValues<AyudaProgramType>().Where(type => type != AyudaProgramType.AssistanceCase && type != AyudaProgramType.Seminar && type != AyudaProgramType.CashForWork));
            ReleaseKinds = new ObservableCollection<AssistanceReleaseKind>(Enum.GetValues<AssistanceReleaseKind>());
            DistributionStatuses = new ObservableCollection<AyudaProgramDistributionStatus>(Enum.GetValues<AyudaProgramDistributionStatus>());
            AvailableUnpickedBeneficiaries = new ObservableCollection<DistributionBeneficiaryOption>();

            RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            SeedMockDataCommand = new RelayCommand(async _ => await ExecuteSeedMockDataAsync(), _ => !IsBusy);
            AddBeneficiaryCommand = new RelayCommand(async _ => await IncludeBeneficiaryAsync(), _ => CanAddBeneficiary());
            MarkBeneficiaryPendingCommand = new RelayCommand(async _ => await UpdateSelectedBeneficiaryStatusAsync(DistributionBeneficiaryStatus.Pending), _ => CanUpdateSelectedBeneficiaryStatus(DistributionBeneficiaryStatus.Pending));
            RejectBeneficiaryCommand = new RelayCommand(async _ => await UpdateSelectedBeneficiaryStatusAsync(DistributionBeneficiaryStatus.Rejected), _ => CanUpdateSelectedBeneficiaryStatus(DistributionBeneficiaryStatus.Rejected));

            OpenLivePreviewCommand = new RelayCommand(_ => OpenLivePreview(), _ => CanOpenLivePreview());
            NextLivePreviewItemCommand = new RelayCommand(_ => AdvanceLivePreviewQueue(), _ => CanAdvanceLivePreviewQueue());
            OpenScannerPanelCommand = new RelayCommand(_ => OpenScannerPanel(), _ => CanOpenScannerPanel());
            OpenAddBeneficiaryPanelCommand = new RelayCommand(_ => OpenAddBeneficiaryPanel(), _ => CanOpenAddBeneficiaryPanel());
            CloseAddBeneficiaryPanelCommand = new RelayCommand(_ => CloseAddBeneficiaryPanel());
            ConfirmAddBeneficiaryCommand = new RelayCommand(async _ => await ConfirmAddBeneficiaryAsync(), _ => CanConfirmAddBeneficiary());
            SelectAllFilteredCommand = new RelayCommand(_ => SelectAllFiltered());
            DeselectAllCommand = new RelayCommand(_ => DeselectAll());
            PrevAddPanelPageCommand = new RelayCommand(
                _ => { AddPanelCurrentPage--; _ = LoadAvailableBeneficiariesAsync(); },
                _ => AddPanelCurrentPage > 1);
            NextAddPanelPageCommand = new RelayCommand(
                _ => { AddPanelCurrentPage++; _ = LoadAvailableBeneficiariesAsync(); },
                _ => AddPanelCurrentPage < AddPanelTotalPages);
            PrevUnpickedPageCommand = new RelayCommand(
                _ => { UnpickedCurrentPage--; _ = LoadAvailableUnpickedBeneficiariesAsync(); },
                _ => UnpickedCurrentPage > 1);
            NextUnpickedPageCommand = new RelayCommand(
                _ => { UnpickedCurrentPage++; _ = LoadAvailableUnpickedBeneficiariesAsync(); },
                _ => UnpickedCurrentPage < UnpickedTotalPages);
            CloseScannerPanelCommand = new RelayCommand(_ => CloseScannerPanel());
            OpenProjectActionMenuCommand = new RelayCommand(parameter => OpenProjectActionMenu(parameter as ProjectDistributionProgramListItem));
            OpenProgramAddBeneficiaryPanelCommand = new RelayCommand(parameter => OpenProgramAddBeneficiaryPanel(parameter as ProjectDistributionProgramListItem), parameter => CanOpenProgramPanel(parameter as ProjectDistributionProgramListItem));
            OpenProgramScannerPanelCommand = new RelayCommand(parameter => OpenProgramScannerPanel(parameter as ProjectDistributionProgramListItem), parameter => CanOpenProgramPanel(parameter as ProjectDistributionProgramListItem));
            PrevPendingPageCommand = new RelayCommand(async _ => await ChangePendingPageAsync(-1), _ => CanMovePendingPage(-1));
            NextPendingPageCommand = new RelayCommand(async _ => await ChangePendingPageAsync(1), _ => CanMovePendingPage(1));
            PrevReleasedPageCommand = new RelayCommand(async _ => await ChangeReleasedPageAsync(-1), _ => CanMoveReleasedPage(-1));
            NextReleasedPageCommand = new RelayCommand(async _ => await ChangeReleasedPageAsync(1), _ => CanMoveReleasedPage(1));
            PrevRejectedPageCommand = new RelayCommand(async _ => await ChangeRejectedPageAsync(-1), _ => CanMoveRejectedPage(-1));
            NextRejectedPageCommand = new RelayCommand(async _ => await ChangeRejectedPageAsync(1), _ => CanMoveRejectedPage(1));
            ConfirmReleaseCommand = new RelayCommand(async _ => await ConfirmReleaseAsync(), _ => CanConfirmRelease());

            OpenCreateProjectPanelCommand = new RelayCommand(_ => OpenCreateProjectPanel(), _ => !IsBusy);
            CloseCreateProjectPanelCommand = new RelayCommand(_ => CloseCreateProjectPanel());
            CloseCreateProjectSuccessPanelCommand = new RelayCommand(_ => CloseCreateProjectSuccessPanel());
            ConfirmCreateProjectCommand = new RelayCommand(async _ => await ConfirmCreateProjectAsync(), _ => CanConfirmCreateProject());
            ToggleBeneficiarySelectionCommand = new RelayCommand(parameter => ToggleBeneficiarySelection(parameter as DistributionBeneficiaryOption));

            MoveToSelectedCommand = new RelayCommand(parameter => OpenBeneficiaryValidation(parameter as DistributionBeneficiaryOption));
            MoveToAvailableCommand = new RelayCommand(parameter => MoveToAvailable(parameter as DistributionBeneficiaryOption));
            MoveAllToSelectedCommand = new RelayCommand(_ => MoveAllToSelected());
            MoveAllToAvailableCommand = new RelayCommand(_ => MoveAllToAvailable());

            OpenBeneficiaryValidationCommand = new RelayCommand(parameter => OpenBeneficiaryValidation(parameter as DistributionBeneficiaryOption));
            ConfirmAddRecipientCommand = new RelayCommand(_ => ConfirmAddRecipient(), _ => _validatingBeneficiary != null);
            CancelBeneficiaryValidationCommand = new RelayCommand(_ => CancelBeneficiaryValidation());

            AddCommunityTaxRowCommand = new RelayCommand(_ => AddCommunityTaxRow());
            RemoveCommunityTaxRowCommand = new RelayCommand(parameter => RemoveCommunityTaxRow(parameter as CommunityTaxEntryRow));
            AddRequirementRowCommand = new RelayCommand(_ => AddRequirementRow());
            RemoveRequirementRowCommand = new RelayCommand(parameter => RemoveRequirementRow(parameter as RequirementEntryRow));

            _openPcScannerCommand = new RelayCommand(_ => IsPcScannerOpen = true, _ => !IsBusy && SelectedProgram != null);
            _processPcScanCommand = new RelayCommand(payload => _ = ExecuteProcessPcScan(payload as string));
            // Confirm on the scan overlay: opens the household verification modal when a family member
            // already received the aid; otherwise records the release directly (fast scanner path).
            _confirmScannedClaimCommand = new RelayCommand(async _ => await RequestConfirmReleaseAsync(), _ => !IsBusy && IsScannedBeneficiaryEligible && IsIdentityVerified);
            _cancelScannedClaimCommand = new RelayCommand(_ => ResetScannedResult());
            RejectScannedBeneficiaryCommand = new RelayCommand(async _ => await ExecuteRejectScannedBeneficiaryAsync(), _ => !IsBusy && ScannedBeneficiary != null);
            _submitManualKeyInCommand = new RelayCommand(async _ => await ExecuteManualKeyIn(), _ => !IsBusy && SelectedProgram != null && !string.IsNullOrWhiteSpace(ManualBeneficiaryIdText));

            OpenPendingBeneficiaryOverlayCommand = new RelayCommand(async _ => await OpenPendingBeneficiaryOverlayAsync(), _ => !IsBusy && SelectedPendingBeneficiary != null);
            OpenReleasedBeneficiaryOverlayCommand = new RelayCommand(async _ => await OpenReleasedBeneficiaryOverlayAsync(), _ => !IsBusy && SelectedReleasedClaim != null);
            // Household verification modal: final Confirm/Decline gate after a duplicate is flagged.
            // CONFIRM RELEASE additionally requires the attachment checklist (cedula, barangay
            // certificate, ...) to be fully complete — missing items keep the beneficiary unreleased.
            _confirmHouseholdReleaseCommand = new RelayCommand(async _ => await ConfirmHouseholdReleaseAsync(), _ => !IsBusy && AreReleaseRequirementsComplete && (!RequiresHouseholdOverride || HouseholdOverrideAcknowledged));
            _cancelHouseholdConfirmCommand = new RelayCommand(_ => CloseHouseholdConfirm());

            ResetCreateProjectForm();
            _ = LoadAsync();
        }

        public ObservableCollection<AyudaProgramType> ProgramTypes { get; }
        public ObservableCollection<AssistanceReleaseKind> ReleaseKinds { get; }
        public ObservableCollection<AyudaProgramDistributionStatus> DistributionStatuses { get; }
        public ObservableCollection<DistributionBeneficiaryOption> AvailableUnpickedBeneficiaries { get; }
        public ObservableCollection<DistributionBeneficiaryOption> SelectedProjectBeneficiaries { get; } = new();

        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    SidebarWidth = value ? new GridLength(0) : new GridLength(320);
                }
            }
        }

        public bool IsSummaryCollapsed
        {
            get => _isSummaryCollapsed;
            set => SetProperty(ref _isSummaryCollapsed, value);
        }

        public GridLength SidebarWidth
        {
            get => _sidebarWidth;
            private set => SetProperty(ref _sidebarWidth, value);
        }

        public ICommand ToggleSidebarCommand => new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
        public ICommand ToggleSummaryCommand => new RelayCommand(_ => IsSummaryCollapsed = !IsSummaryCollapsed);

        public ICommand OpenCreateProjectPanelCommand { get; }
        public ICommand CloseCreateProjectPanelCommand { get; }
        public ICommand CloseCreateProjectSuccessPanelCommand { get; }
        public ICommand ConfirmCreateProjectCommand { get; }
        public ICommand ToggleBeneficiarySelectionCommand { get; }
        public ICommand MoveToSelectedCommand { get; }
        public ICommand MoveToAvailableCommand { get; }
        public ICommand MoveAllToSelectedCommand { get; }
        public ICommand MoveAllToAvailableCommand { get; }
        public ICommand OpenBeneficiaryValidationCommand { get; }
        public ICommand ConfirmAddRecipientCommand { get; }
        public ICommand CancelBeneficiaryValidationCommand { get; }
        public ICommand AddCommunityTaxRowCommand { get; }
        public ICommand RemoveCommunityTaxRowCommand { get; }
        public ICommand AddRequirementRowCommand { get; }
        public ICommand RemoveRequirementRowCommand { get; }
        public ICommand OpenPcScannerCommand => _openPcScannerCommand;
        public ICommand ProcessScanCommand => _processPcScanCommand;
        public ICommand ConfirmScannedClaimCommand => _confirmScannedClaimCommand;
        public ICommand CancelScannedClaimCommand => _cancelScannedClaimCommand;
        public ICommand RejectScannedBeneficiaryCommand { get; }
        public ICommand ConfirmHouseholdReleaseCommand => _confirmHouseholdReleaseCommand;
        public ICommand CancelHouseholdConfirmCommand => _cancelHouseholdConfirmCommand;

        /// <summary>True while the household verification modal (final Confirm/Decline gate) is shown.</summary>
        public bool IsHouseholdConfirmVisible
        {
            get => _isHouseholdConfirmVisible;
            private set
            {
                if (SetProperty(ref _isHouseholdConfirmVisible, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        /// <summary>True while the per-beneficiary enrollment validation modal is shown.</summary>
        public bool IsBeneficiaryValidationModalOpen
        {
            get => _isBeneficiaryValidationModalOpen;
            private set
            {
                if (SetProperty(ref _isBeneficiaryValidationModalOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public DistributionBeneficiaryOption? ValidatingBeneficiary
        {
            get => _validatingBeneficiary;
            private set => SetProperty(ref _validatingBeneficiary, value);
        }

        public ProjectDistributionBeneficiaryListItem? ScannedBeneficiary
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

        private Brush _scannedBeneficiaryStatusColor = Brushes.DimGray;
        public Brush ScannedBeneficiaryStatusColor
        {
            get => _scannedBeneficiaryStatusColor;
            private set => SetProperty(ref _scannedBeneficiaryStatusColor, value);
        }

        private ObservableCollection<ProjectDistributionReleaseListItem> _scannedBeneficiaryHistory = new();
        public ObservableCollection<ProjectDistributionReleaseListItem> ScannedBeneficiaryHistory
        {
            get => _scannedBeneficiaryHistory;
            private set => SetProperty(ref _scannedBeneficiaryHistory, value);
        }

        public bool HasScannedHistory => ScannedBeneficiaryHistory.Count > 0;

        /// <summary>Household/family roster shown on the confirm panel for identity + duplicate validation.</summary>
        public ObservableCollection<HouseholdMemberVerificationItem> ScannedHouseholdMembers { get; } = new();
        public bool HasHouseholdMembers => ScannedHouseholdMembers.Count > 0;

        public bool HasHouseholdContext
        {
            get => _hasHouseholdContext;
            private set => SetProperty(ref _hasHouseholdContext, value);
        }

        public string HouseholdContextSummary
        {
            get => _householdContextSummary;
            private set => SetProperty(ref _householdContextSummary, value);
        }

        /// <summary>Front-and-center decision line: how many household members already received this assistance.</summary>
        public string HouseholdAidReceivedSummary
        {
            get => _householdAidReceivedSummary;
            private set => SetProperty(ref _householdAidReceivedSummary, value);
        }

        /// <summary>Name shown in the Household Review modal (works for both scan and pending-list flows).</summary>
        public string HouseholdConfirmBeneficiaryName
        {
            get => _householdConfirmBeneficiaryName;
            private set => SetProperty(ref _householdConfirmBeneficiaryName, value);
        }

        /// <summary>Beneficiary photo shown in the Household Review modal; null falls back to the default profile icon.</summary>
        public BitmapSource? HouseholdConfirmBeneficiaryPhoto
        {
            get => _householdConfirmBeneficiaryPhoto;
            private set => SetProperty(ref _householdConfirmBeneficiaryPhoto, value);
        }

        public string? HouseholdWarningMessage
        {
            get => _householdWarningMessage;
            private set
            {
                if (SetProperty(ref _householdWarningMessage, value))
                {
                    OnPropertyChanged(nameof(HasHouseholdWarning));
                }
            }
        }

        public bool HasHouseholdWarning => !string.IsNullOrWhiteSpace(_householdWarningMessage);

        /// <summary>True when a household member already received the same assistance type; Confirm needs an explicit override.</summary>
        public bool RequiresHouseholdOverride
        {
            get => _requiresHouseholdOverride;
            private set
            {
                if (SetProperty(ref _requiresHouseholdOverride, value))
                {
                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                    _confirmHouseholdReleaseCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HouseholdOverrideAcknowledged
        {
            get => _householdOverrideAcknowledged;
            set
            {
                if (SetProperty(ref _householdOverrideAcknowledged, value))
                {
                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                    _confirmHouseholdReleaseCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Attachment checklist (cedula, barangay certificate, ...) reviewed in the release modal; all items must be complete before CONFIRM RELEASE enables.</summary>
        public ObservableCollection<RequirementEntryRow> ReleaseRequirementRows { get; } = new();

        public bool AreReleaseRequirementsComplete =>
            ReleaseRequirementRows.Count > 0 && ReleaseRequirementRows.All(r => r.IsComplete);

        public bool HasMissingReleaseRequirements => ReleaseRequirementRows.Any(r => !r.IsComplete);

        public string ReleaseRequirementsSummaryText
        {
            get
            {
                var missing = ReleaseRequirementRows.Count(r => !r.IsComplete);
                return missing == 0
                    ? "All requirements are complete."
                    : $"{missing} requirement{(missing == 1 ? "" : "s")} missing — the beneficiary will remain in UNRELEASED / UNCLAIMED until the attachments are presented.";
            }
        }

        public BitmapSource? ScannedBeneficiaryPhoto
        {
            get => _scannedBeneficiaryPhoto;
            private set => SetProperty(ref _scannedBeneficiaryPhoto, value);
        }

        /// <summary>Residential address shown on the scanned Beneficiaries Profile modal.</summary>
        public string ScannedBeneficiaryAddress
        {
            get => _scannedBeneficiaryAddress;
            private set => SetProperty(ref _scannedBeneficiaryAddress, value);
        }

        public string ScannedBeneficiaryAge
        {
            get => _scannedBeneficiaryAge;
            private set => SetProperty(ref _scannedBeneficiaryAge, value);
        }

        public string ScannedBeneficiaryGender
        {
            get => _scannedBeneficiaryGender;
            private set => SetProperty(ref _scannedBeneficiaryGender, value);
        }

        /// <summary>Household code (e.g. BG-XXXXXXX) of the scanned beneficiary's linked household.</summary>
        public string ScannedHouseholdNumber
        {
            get => _scannedHouseholdNumber;
            private set => SetProperty(ref _scannedHouseholdNumber, value);
        }

        /// <summary>The scanned beneficiary's relationship to the household head (e.g. Father, Son).</summary>
        public string ScannedHouseholdRole
        {
            get => _scannedHouseholdRole;
            private set => SetProperty(ref _scannedHouseholdRole, value);
        }

        /// <summary>Display-only allocated release per beneficiary: unit amount for Cash, item + quantity for Goods.</summary>
        public string ScannedAllocatedAmountText
        {
            get => _scannedAllocatedAmountText;
            private set => SetProperty(ref _scannedAllocatedAmountText, value);
        }

        public bool IsScannedResultVisible
        {
            get => _isScannedResultVisible;
            private set
            {
                if (SetProperty(ref _isScannedResultVisible, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public bool IsScannerActive
        {
            get => _isScannerActive;
            set => SetProperty(ref _isScannerActive, value);
        }

        public bool IsScannedBeneficiaryEligible
        {
            get => _isScannedBeneficiaryEligible;
            private set
            {
                if (SetProperty(ref _isScannedBeneficiaryEligible, value))
                {
                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>True only after a resolved beneficiary profile has been shown; gates Confirm so identity is never bypassed.</summary>
        public bool IsIdentityVerified
        {
            get => _isIdentityVerified;
            private set
            {
                if (SetProperty(ref _isIdentityVerified, value))
                {
                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Operator-typed Beneficiary ID for the manual key-in fallback (used when the scanner fails).</summary>
        public string ManualBeneficiaryIdText
        {
            get => _manualBeneficiaryIdText;
            set
            {
                if (SetProperty(ref _manualBeneficiaryIdText, value))
                {
                    _submitManualKeyInCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand SubmitManualKeyInCommand => _submitManualKeyInCommand;

        public bool IsReleaseSuccessState
        {
            get => _isReleaseSuccessState;
            private set
            {
                if (SetProperty(ref _isReleaseSuccessState, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public string LastScanSummaryText
        {
            get => _lastScanSummaryText;
            private set => SetProperty(ref _lastScanSummaryText, value);
        }

        public Brush LastScanSummaryBrush
        {
            get => _lastScanSummaryBrush;
            private set => SetProperty(ref _lastScanSummaryBrush, value);
        }

        public event Action? RequestScannerFocus;

        public string ScannerActionLabel
        {
            get => _scannerActionLabel;
            set => SetProperty(ref _scannerActionLabel, value);
        }

        public string ScannerCancelLabel
        {
            get => _scannerCancelLabel;
            set => SetProperty(ref _scannerCancelLabel, value);
        }

        public string ScannerHeader => "Payout Verification";
        public string ScannerDescription => "Scan ID to verify eligibility and confirm release.";

        public bool IsAddBeneficiaryPanelOpen
        {
            get => _isAddBeneficiaryPanelOpen;
            private set
            {
                if (SetProperty(ref _isAddBeneficiaryPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public bool IsScannerPanelOpen
        {
            get => _isScannerPanelOpen;
            private set
            {
                if (SetProperty(ref _isScannerPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

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

        private DistributionBeneficiaryOption? _selectedAvailableUnpickedBeneficiary;
        private DistributionBeneficiaryOption? _selectedProjectBeneficiary;

        public bool IsAnyOverlayOpen => IsAddBeneficiaryPanelOpen || IsScannerPanelOpen || IsCreateProjectPanelOpen || IsCreateProjectSuccessPanelOpen || IsPcScannerOpen || IsScannedResultVisible || IsReleaseSuccessState || IsHouseholdConfirmVisible;

        public DistributionBeneficiaryOption? SelectedAvailableUnpickedBeneficiary
        {
            get => _selectedAvailableUnpickedBeneficiary;
            set => SetProperty(ref _selectedAvailableUnpickedBeneficiary, value);
        }

        public DistributionBeneficiaryOption? SelectedProjectBeneficiary
        {
            get => _selectedProjectBeneficiary;
            set => SetProperty(ref _selectedProjectBeneficiary, value);
        }

        public bool NewProjectIsCashKind => NewProjectSelectedReleaseKind == AssistanceReleaseKind.Cash;

        public bool IsCreateProjectPanelOpen
        {
            get => _isCreateProjectPanelOpen;
            private set
            {
                if (SetProperty(ref _isCreateProjectPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                    if (ConfirmCreateProjectCommand is RelayCommand confirm)
                    {
                        confirm.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public bool IsCreateProjectSuccessPanelOpen
        {
            get => _isCreateProjectSuccessPanelOpen;
            private set
            {
                if (SetProperty(ref _isCreateProjectSuccessPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public string NewProjectCode
        {
            get => _newProjectCode;
            private set => SetProperty(ref _newProjectCode, value);
        }

        public string NewProjectName
        {
            get => _newProjectName;
            set
            {
                if (SetProperty(ref _newProjectName, value))
                {
                    if (ConfirmCreateProjectCommand is RelayCommand confirm)
                    {
                        confirm.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public string NewProjectDescription
        {
            get => _newProjectDescription;
            set => SetProperty(ref _newProjectDescription, value);
        }

        public string NewProjectAssistanceType
        {
            get => _newProjectAssistanceType;
            set => SetProperty(ref _newProjectAssistanceType, value);
        }

        public AyudaProgramType NewProjectSelectedType
        {
            get => _newProjectSelectedType;
            set
            {
                if (SetProperty(ref _newProjectSelectedType, value))
                {
                    OnPropertyChanged(nameof(NewProjectCashAmountVisibility));
                    OnPropertyChanged(nameof(NewProjectGoodsDescriptionVisibility));
                }
            }
        }

        public AssistanceReleaseKind NewProjectSelectedReleaseKind
        {
            get => _newProjectSelectedReleaseKind;
            set
            {
                if (SetProperty(ref _newProjectSelectedReleaseKind, value))
                {
                    OnPropertyChanged(nameof(NewProjectCashAmountVisibility));
                    OnPropertyChanged(nameof(NewProjectGoodsDescriptionVisibility));
                }
            }
        }

        public string NewProjectUnitAmountText
        {
            get => _newProjectUnitAmountText;
            set
            {
                if (SetProperty(ref _newProjectUnitAmountText, value))
                {
                    OnPropertyChanged(nameof(NewProjectTotalCost));
                    if (ConfirmCreateProjectCommand is RelayCommand confirm)
                    {
                        confirm.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public string NewProjectItemDescription
        {
            get => _newProjectItemDescription;
            set => SetProperty(ref _newProjectItemDescription, value);
        }

        public DateTime? NewProjectStartDate
        {
            get => _newProjectStartDate;
            set => SetProperty(ref _newProjectStartDate, value);
        }

        public DateTime? NewProjectEndDate
        {
            get => _newProjectEndDate;
            set => SetProperty(ref _newProjectEndDate, value);
        }

        public string NewProjectBudgetCapText
        {
            get => _newProjectBudgetCapText;
            set => SetProperty(ref _newProjectBudgetCapText, value);
        }

        public AyudaProgramDistributionStatus NewProjectSelectedDistributionStatus
        {
            get => _newProjectSelectedDistributionStatus;
            set => SetProperty(ref _newProjectSelectedDistributionStatus, value);
        }

        public string NewProjectSearchText
        {
            get => _newProjectSearchText;
            set
            {
                if (SetProperty(ref _newProjectSearchText, value))
                {
                    _unpickedCurrentPage = 1;
                    OnPropertyChanged(nameof(UnpickedCurrentPage));
                    _ = LoadAvailableUnpickedBeneficiariesAsync();
                }
            }
        }

        public int NewProjectSelectedCount
        {
            get => _newProjectSelectedCount;
            private set
            {
                if (SetProperty(ref _newProjectSelectedCount, value))
                {
                    OnPropertyChanged(nameof(NewProjectTotalCost));
                    if (ConfirmCreateProjectCommand is RelayCommand confirm)
                    {
                        confirm.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public decimal NewProjectTotalCost
        {
            get
            {
                if (TryParseOptionalAmount(NewProjectUnitAmountText, out var amount))
                {
                    return (amount ?? 0m) * NewProjectSelectedCount;
                }
                return 0m;
            }
        }

        public Visibility NewProjectCashAmountVisibility => NewProjectSelectedReleaseKind == AssistanceReleaseKind.Cash
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility NewProjectGoodsDescriptionVisibility => NewProjectSelectedReleaseKind == AssistanceReleaseKind.Goods
            ? Visibility.Visible
            : Visibility.Collapsed;

        private void OpenCreateProjectPanel()
        {
            ResetCreateProjectForm();
            IsCreateProjectPanelOpen = true;
            _ = LoadAvailableUnpickedBeneficiariesAsync();
        }

        private void CloseCreateProjectPanel()
        {
            IsCreateProjectPanelOpen = false;
        }

        private void CloseCreateProjectSuccessPanel()
        {
            IsCreateProjectSuccessPanelOpen = false;
            IsCreateProjectPanelOpen = false;
        }

        private void ResetCreateProjectForm()
        {
            NewProjectCode = $"PROJ-{DateTime.Now:yyyyMMddHHmm}";
            NewProjectName = string.Empty;
            NewProjectDescription = string.Empty;
            NewProjectAssistanceType = string.Empty;
            NewProjectSelectedType = AyudaProgramType.GeneralPurpose;
            NewProjectSelectedReleaseKind = AssistanceReleaseKind.Cash;
            NewProjectUnitAmountText = string.Empty;
            NewProjectItemDescription = string.Empty;
            NewProjectStartDate = DateTime.Today;
            NewProjectEndDate = DateTime.Today.AddDays(7);
            NewProjectBudgetCapText = string.Empty;
            NewProjectSelectedDistributionStatus = AyudaProgramDistributionStatus.Open;
            NewProjectSearchText = string.Empty;
            UnpickedCurrentPage = 1;
            UnpickedTotalPages = 1;
            AvailableUnpickedBeneficiaries.Clear();
            SelectedProjectBeneficiaries.Clear();
            NewProjectSelectedCount = 0;
            IsCreateProjectSuccessPanelOpen = false;
        }

        private async Task LoadAvailableUnpickedBeneficiariesAsync()
        {
            var version = ++_unpickedBeneficiarySearchVersion;
            var search = NewProjectSearchText?.Trim();

            // Exclude what's already in SelectedProjectBeneficiaries in the UI
            var currentSelectedIds = SelectedProjectBeneficiaries.Select(b => b.StagingId).ToHashSet();
            var currentSelectedCivilIds = SelectedProjectBeneficiaries.Where(b => !string.IsNullOrEmpty(b.CivilRegistryId)).Select(b => b.CivilRegistryId).ToHashSet();
            var currentSelectedBenIds = SelectedProjectBeneficiaries.Where(b => !string.IsNullOrEmpty(b.BeneficiaryId)).Select(b => b.BeneficiaryId).ToHashSet();

            // Filter and page DB-side: only one BeneficiaryPickerDisplayLimit page of matches is
            // materialized so the panel opens instantly even with the full municipal registry local.
            // SQLite executes "async" queries synchronously, so run them on the thread pool.
            var requestedPage = Math.Max(1, _unpickedCurrentPage);
            var (totalCount, beneficiaries) = await Task.Run(async () =>
            {
                await using var context = new LocalDbContext();

                var query = context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(item => item.VerificationStatus == VerificationStatus.Approved);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(item =>
                        (item.FullName != null && item.FullName.Contains(search)) ||
                        (item.LastName != null && item.LastName.Contains(search)) ||
                        (item.FirstName != null && item.FirstName.Contains(search)) ||
                        (item.BeneficiaryId != null && item.BeneficiaryId.Contains(search)));
                }

                var count = await query.CountAsync();
                var lastPage = Math.Max(1, (int)Math.Ceiling(count / (double)BeneficiaryPickerDisplayLimit));
                var boundedPage = Math.Min(requestedPage, lastPage);
                var rows = await query
                    .OrderBy(item => item.FullName ?? item.LastName)
                    .Skip((boundedPage - 1) * BeneficiaryPickerDisplayLimit)
                    .Take(BeneficiaryPickerDisplayLimit + currentSelectedIds.Count)
                    .Select(item => new PickerBeneficiaryRow(
                        item.StagingID,
                        item.BeneficiaryId,
                        item.CivilRegistryId,
                        item.LastName,
                        item.FirstName,
                        item.MiddleName,
                        item.FullName,
                        item.LinkedHouseholdId,
                        item.LinkedHouseholdMemberId,
                        item.IsSenior,
                        item.IsPwd))
                    .ToListAsync();
                return (count, rows);
            });

            if (version != _unpickedBeneficiarySearchVersion)
            {
                return; // a newer search superseded this one
            }

            UnpickedTotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)BeneficiaryPickerDisplayLimit));
            UnpickedCurrentPage = Math.Min(requestedPage, UnpickedTotalPages);

            // Duplicate checks against the picker's Selected column stay in memory
            var filteredBeneficiaries = beneficiaries
                .Where(item => !currentSelectedIds.Contains(item.StagingID))
                .Where(item => string.IsNullOrEmpty(item.CivilRegistryId) || !currentSelectedCivilIds.Contains(item.CivilRegistryId))
                .Where(item => string.IsNullOrEmpty(item.BeneficiaryId) || !currentSelectedBenIds.Contains(item.BeneficiaryId))
                .Take(BeneficiaryPickerDisplayLimit)
                .ToList();

            AvailableUnpickedBeneficiaries.Clear();
            foreach (var b in filteredBeneficiaries)
            {
                AvailableUnpickedBeneficiaries.Add(new DistributionBeneficiaryOption
                {
                    StagingId = b.StagingID,
                    BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = b.CivilRegistryId ?? string.Empty,
                    LastName = b.LastName ?? string.Empty,
                    FirstName = b.FirstName ?? string.Empty,
                    MiddleName = b.MiddleName ?? string.Empty,
                    FullName = b.FullName ?? string.Empty,
                    LinkedHouseholdId = b.LinkedHouseholdId,
                    LinkedHouseholdMemberId = b.LinkedHouseholdMemberId
                });
            }

            UpdateNewProjectSelectedCount();
        }

        private void MoveToSelected(DistributionBeneficiaryOption? option)
        {
            if (option == null) return;
            AvailableUnpickedBeneficiaries.Remove(option);
            SelectedProjectBeneficiaries.Add(option);
            UpdateNewProjectSelectedCount();
        }

        private void MoveToAvailable(DistributionBeneficiaryOption? option)
        {
            if (option == null) return;
            SelectedProjectBeneficiaries.Remove(option);
            _ = LoadAvailableUnpickedBeneficiariesAsync();
        }

        private void OpenBeneficiaryValidation(DistributionBeneficiaryOption? option)
        {
            if (option == null) return;
            ValidatingBeneficiary = option;
            IsBeneficiaryValidationModalOpen = true;
        }

        private void ConfirmAddRecipient()
        {
            if (_validatingBeneficiary == null) return;
            AvailableUnpickedBeneficiaries.Remove(_validatingBeneficiary);
            var targetStatus = _validatingBeneficiary.IsRequirementsComplete
                ? DistributionBeneficiaryStatus.Pending
                : DistributionBeneficiaryStatus.Rejected;
            _validatingBeneficiary.IsSelected = true;
            SelectedProjectBeneficiaries.Add(_validatingBeneficiary);
            UpdateNewProjectSelectedCount();
            CancelBeneficiaryValidation();
        }

        private void CancelBeneficiaryValidation()
        {
            ValidatingBeneficiary = null;
            IsBeneficiaryValidationModalOpen = false;
        }

        private void AddCommunityTaxRow()
        {
            if (_validatingBeneficiary == null) return;
            _validatingBeneficiary.CommunityTaxRows.Add(new CommunityTaxEntryRow());
        }

        private void RemoveCommunityTaxRow(CommunityTaxEntryRow? row)
        {
            if (_validatingBeneficiary == null || row == null) return;
            _validatingBeneficiary.CommunityTaxRows.Remove(row);
        }

        private void AddRequirementRow()
        {
            if (_validatingBeneficiary == null) return;
            _validatingBeneficiary.RequirementRows.Add(new RequirementEntryRow());
        }

        private void RemoveRequirementRow(RequirementEntryRow? row)
        {
            if (_validatingBeneficiary == null || row == null) return;
            _validatingBeneficiary.RequirementRows.Remove(row);
        }

        private void MoveAllToSelected()
        {
            var items = AvailableUnpickedBeneficiaries.ToList();
            foreach (var item in items)
            {
                AvailableUnpickedBeneficiaries.Remove(item);
                SelectedProjectBeneficiaries.Add(item);
            }
            UpdateNewProjectSelectedCount();
        }

        private void MoveAllToAvailable()
        {
            SelectedProjectBeneficiaries.Clear();
            _ = LoadAvailableUnpickedBeneficiariesAsync();
        }

        private void ToggleBeneficiarySelection(DistributionBeneficiaryOption? option)
        {
            if (option == null) return;
            option.IsSelected = !option.IsSelected;
            UpdateNewProjectSelectedCount();
        }

        private void UpdateNewProjectSelectedCount()
        {
            NewProjectSelectedCount = SelectedProjectBeneficiaries.Count;
            if (ConfirmCreateProjectCommand is RelayCommand confirm)
            {
                confirm.RaiseCanExecuteChanged();
            }
        }

        private bool CanConfirmCreateProject()
        {
            return !IsBusy && 
                   !string.IsNullOrWhiteSpace(NewProjectCode) && 
                   !string.IsNullOrWhiteSpace(NewProjectName) &&
                   NewProjectSelectedCount > 0;
        }

        private async Task ConfirmCreateProjectAsync()
        {
            if (!CanConfirmCreateProject()) return;

            if (!TryParseOptionalAmount(NewProjectUnitAmountText, out var unitAmount))
            {
                SetErrorStatus("Enter a valid unit amount.");
                return;
            }

            if (!TryParseOptionalAmount(NewProjectBudgetCapText, out var budgetCap))
            {
                SetErrorStatus("Enter a valid project budget cap.");
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Creating project and enrolling beneficiaries...");

            try
            {
                await using var context = new LocalDbContext();
                var budgetService = new BudgetManagementService(context);
                var distributionService = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());

                // 1. Create Program
                var programResult = await budgetService.CreateProgramAsync(
                    new AyudaProgramRequest(
                        NewProjectCode,
                        NewProjectName,
                        NewProjectSelectedType,
                        NormalizeNullable(NewProjectDescription),
                        NormalizeNullable(NewProjectAssistanceType),
                        NewProjectSelectedReleaseKind,
                        unitAmount,
                        NormalizeNullable(NewProjectItemDescription),
                        null, // ItemName
                        null, // QuantityPerBeneficiary
                        null, // UnitOfMeasure
                        NewProjectStartDate,
                        NewProjectEndDate,
                        budgetCap,
                        NewProjectSelectedDistributionStatus),
                    _currentUser.Id);

                if (!programResult.IsSuccess)
                {
                    SetErrorStatus(programResult.Message);
                    return;
                }

                int programId = programResult.ProgramId ?? throw new Exception("Program created but no ID returned.");

                // 2. Bulk Enroll Selected Beneficiaries
                var selectedIds = SelectedProjectBeneficiaries
                    .Select(b => b.StagingId)
                    .ToList();

                SetNeutralStatus($"Enrolling {selectedIds.Count} beneficiaries...");
                var enrollResult = await distributionService.BulkAddBeneficiariesAsync(
                    programId, 
                    selectedIds, 
                    _currentUser.Id);

                if (!enrollResult.IsSuccess)
                {
                    SetErrorStatus($"Project created, but enrollment failed: {enrollResult.Message}");
                    return;
                }

                // 3. Persist cedula and requirement data for each beneficiary
                SetNeutralStatus("Saving cedula and requirement documents...");
                foreach (var beneficiary in SelectedProjectBeneficiaries)
                {
                    // Save community tax payments
                    foreach (var taxRow in beneficiary.CommunityTaxRows)
                    {
                        var taxPayment = new BeneficiaryCommunityTaxPayment
                        {
                            BeneficiaryStagingId = beneficiary.StagingId,
                            AyudaProgramId = programId,
                            CedulaNumber = taxRow.CedulaNumber,
                            PaidAmount = taxRow.GetPaidAmount(),
                            PaidDate = taxRow.PaidDate
                        };
                        context.BeneficiaryCommunityTaxPayments.Add(taxPayment);
                    }

                    // Save requirement documents
                    foreach (var reqRow in beneficiary.RequirementRows)
                    {
                        var reqDoc = new BeneficiaryRequirementDocument
                        {
                            BeneficiaryStagingId = beneficiary.StagingId,
                            AyudaProgramId = programId,
                            DocumentName = reqRow.DocumentName,
                            SubmittedDate = reqRow.SubmittedDate,
                            Status = reqRow.Status,
                            Remarks = reqRow.Remarks
                        };
                        context.BeneficiaryRequirementDocuments.Add(reqDoc);
                    }

                    // Update membership status if incomplete: Rejected if no requirements or any incomplete
                    if (!beneficiary.IsRequirementsComplete)
                    {
                        var membership = await context.AyudaProjectBeneficiaries
                            .FirstOrDefaultAsync(m => m.AyudaProgramId == programId && m.BeneficiaryStagingId == beneficiary.StagingId);
                        if (membership != null)
                        {
                            membership.Status = DistributionBeneficiaryStatus.Rejected;
                            membership.StatusReason = "Incomplete requirements";
                            membership.StatusUpdatedAt = DateTime.Now;
                            membership.StatusUpdatedByUserId = _currentUser.Id;
                        }
                    }
                }
                await context.SaveChangesAsync();

                // NOTE: We no longer auto-release claims here.
                // Beneficiaries will remain in 'Pending' status for manual scanning or bulk release later.

                await LoadProgramsAsync(programId);
                IsCreateProjectSuccessPanelOpen = true;
                SetSuccessStatus($"Project '{NewProjectName}' created. {selectedIds.Count} beneficiaries are now pending for distribution.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Error creating project: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool TryParseOptionalAmount(string text, out decimal? amount)
        {
            amount = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            {
                amount = parsed;
                return true;
            }
            return false;
        }


        public ObservableCollection<ProjectDistributionProgramListItem> ProgramSummaries { get; } = new();

        /// <summary>Search-filtered projects for the selection picker (search by name or code).</summary>
        public ObservableCollection<ProjectDistributionProgramListItem> FilteredProgramSummaries { get; } = new();

        /// <summary>Sidebar list scoped to the chosen project only (empty until one is selected) to reduce clutter.</summary>
        public ObservableCollection<ProjectDistributionProgramListItem> SidebarProgramSummaries { get; } = new();

        public bool HasSelectedProgram => SelectedProgramSummary != null;
        public ObservableCollection<DistributionBeneficiaryOption> AvailableBeneficiaries { get; } = new();
        public ObservableCollection<ProjectDistributionBeneficiaryListItem> ProgramBeneficiaries { get; } = new();
        public ObservableCollection<ProjectDistributionReleaseListItem> ProgramReleaseHistory { get; } = new();
        public ObservableCollection<ProjectDistributionBeneficiaryListItem> PendingBeneficiaries { get; } = new();
        public ObservableCollection<ProjectDistributionReleaseListItem> ReleasedClaims { get; } = new();
        /// <summary>Third status bucket: enrolled but Rejected/Not-Eligible (e.g. incomplete requirements).</summary>
        public ObservableCollection<ProjectDistributionBeneficiaryListItem> RejectedBeneficiaries { get; } = new();
        public ObservableCollection<DistributionBeneficiaryOption> FilteredAvailableBeneficiaries { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand SeedMockDataCommand { get; }
        public ICommand AddBeneficiaryCommand { get; }
        public ICommand MarkBeneficiaryPendingCommand { get; }
        public ICommand RejectBeneficiaryCommand { get; }

        public ICommand OpenLivePreviewCommand { get; }
        public ICommand NextLivePreviewItemCommand { get; }
        public ICommand OpenScannerPanelCommand { get; }
        public ICommand OpenAddBeneficiaryPanelCommand { get; }
        public ICommand CloseAddBeneficiaryPanelCommand { get; }
        public ICommand ConfirmAddBeneficiaryCommand { get; }
        public ICommand SelectAllFilteredCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand PrevAddPanelPageCommand { get; }
        public ICommand NextAddPanelPageCommand { get; }
        public ICommand PrevUnpickedPageCommand { get; }
        public ICommand NextUnpickedPageCommand { get; }
        public ICommand CloseScannerPanelCommand { get; }
        public ICommand OpenProjectActionMenuCommand { get; }
        public ICommand OpenProgramAddBeneficiaryPanelCommand { get; }
        public ICommand OpenPendingBeneficiaryOverlayCommand { get; }
        public ICommand OpenReleasedBeneficiaryOverlayCommand { get; }
        public ICommand OpenProgramScannerPanelCommand { get; }
        public ICommand PrevPendingPageCommand { get; }
        public ICommand NextPendingPageCommand { get; }
        public ICommand PrevReleasedPageCommand { get; }
        public ICommand NextReleasedPageCommand { get; }
        public ICommand PrevRejectedPageCommand { get; }
        public ICommand NextRejectedPageCommand { get; }
        public ICommand ConfirmReleaseCommand { get; }

        public string PendingSearchText
        {
            get => _pendingSearchText;
            set
            {
                if (SetProperty(ref _pendingSearchText, value))
                {
                    PendingCurrentPage = 1;
                    _ = GetPendingBeneficiariesPaginatedAsync();
                }
            }
        }

        /// <summary>Free-text filter for the project picker (matches program name or code).</summary>
        public string ProgramSearchText
        {
            get => _programSearchText;
            set
            {
                if (SetProperty(ref _programSearchText, value))
                {
                    ApplyProgramFilter();
                }
            }
        }

        public ProjectDistributionProgramListItem? SelectedProgramSummary
        {
            get => _selectedProgramSummary;
            set
            {
                if (SetProperty(ref _selectedProgramSummary, value))
                {
                    SelectedProgram = value?.Program;
                    RefreshSidebarProjects();
                    OnPropertyChanged(nameof(HasSelectedProgram));
                }
            }
        }

        /// <summary>Rebuilds <see cref="FilteredProgramSummaries"/> from the full list using <see cref="ProgramSearchText"/>.</summary>
        private void ApplyProgramFilter()
        {
            var search = _programSearchText?.Trim();
            FilteredProgramSummaries.Clear();
            foreach (var item in ProgramSummaries)
            {
                if (string.IsNullOrEmpty(search) ||
                    item.ProgramName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.ProgramCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredProgramSummaries.Add(item);
                }
            }
        }

        /// <summary>Keeps the sidebar list scoped to just the chosen project (empty when none).</summary>
        private void RefreshSidebarProjects()
        {
            SidebarProgramSummaries.Clear();
            if (_selectedProgramSummary != null)
            {
                SidebarProgramSummaries.Add(_selectedProgramSummary);
            }
        }

        public AyudaProgram? SelectedProgram
        {
            get => _selectedProgram;
            private set
            {
                if (SetProperty(ref _selectedProgram, value))
                {
                    if (value != null)
                    {
                        IsSummaryCollapsed = false;
                    }

                    ResetSelectedProgramDetails(value);
                    SelectedProgramBeneficiary = null;
                    if (AddBeneficiaryCommand is RelayCommand add)
                    {
                        add.RaiseCanExecuteChanged();
                    }

                    if (OpenAddBeneficiaryPanelCommand is RelayCommand openAddPanel)
                    {
                        openAddPanel.RaiseCanExecuteChanged();
                    }

                    if (OpenScannerPanelCommand is RelayCommand scannerPanel)
                    {
                        scannerPanel.RaiseCanExecuteChanged();
                    }



                    if (OpenLivePreviewCommand is RelayCommand preview)
                    {
                        preview.RaiseCanExecuteChanged();
                    }

                    if (ConfirmReleaseCommand is RelayCommand confirmRelease)
                    {
                        confirmRelease.RaiseCanExecuteChanged();
                    }

                    if (OpenPcScannerCommand is RelayCommand openPcScanner)
                    {
                        openPcScanner.RaiseCanExecuteChanged();
                    }

                    _submitManualKeyInCommand.RaiseCanExecuteChanged();

                    if (!IsBusy)
                    {
                        _ = LoadProjectDetailsAsync();
                    }
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

        public string SelectedProgramWorkflowSummary
        {
            get => _selectedProgramWorkflowSummary;
            private set => SetProperty(ref _selectedProgramWorkflowSummary, value);
        }

        public string SelectedProgramAssistanceSummary
        {
            get => _selectedProgramAssistanceSummary;
            private set => SetProperty(ref _selectedProgramAssistanceSummary, value);
        }

        public string SelectedProgramBudgetCapText
        {
            get => _selectedProgramBudgetCapText;
            private set => SetProperty(ref _selectedProgramBudgetCapText, value);
        }

        public string SelectedProgramDistributedText
        {
            get => _selectedProgramDistributedText;
            private set => SetProperty(ref _selectedProgramDistributedText, value);
        }

        public string SelectedProgramRemainingBudgetText
        {
            get => _selectedProgramRemainingBudgetText;
            private set => SetProperty(ref _selectedProgramRemainingBudgetText, value);
        }

        public string SelectedProgramBeneficiaryCountText
        {
            get => _selectedProgramBeneficiaryCountText;
            private set => SetProperty(ref _selectedProgramBeneficiaryCountText, value);
        }

        public string SelectedProgramReleaseSummary
        {
            get => _selectedProgramReleaseSummary;
            private set => SetProperty(ref _selectedProgramReleaseSummary, value);
        }

        public string SelectedProgramDescriptionText
        {
            get => _selectedProgramDescriptionText;
            private set => SetProperty(ref _selectedProgramDescriptionText, value);
        }

        public string ProgramReleaseEmptyStateMessage
        {
            get => _programReleaseEmptyStateMessage;
            private set => SetProperty(ref _programReleaseEmptyStateMessage, value);
        }



        public string LivePreviewProgramName
        {
            get => _livePreviewProgramName;
            private set => SetProperty(ref _livePreviewProgramName, value);
        }

        public string LivePreviewPrimaryLabel
        {
            get => _livePreviewPrimaryLabel;
            private set => SetProperty(ref _livePreviewPrimaryLabel, value);
        }

        public string LivePreviewSecondaryLabel
        {
            get => _livePreviewSecondaryLabel;
            private set => SetProperty(ref _livePreviewSecondaryLabel, value);
        }

        public string LivePreviewQueueStatusText
        {
            get => _livePreviewQueueStatusText;
            private set => SetProperty(ref _livePreviewQueueStatusText, value);
        }

        public ObservableCollection<ProjectDistributionLivePreviewQueueItem> LivePreviewQueueItems { get; } = new();
        public ObservableCollection<ProjectDistributionCallBoardEntry> LivePreviewCallBoard { get; } = new();
        public IReadOnlyList<int> LivePreviewWindowCountOptions { get; } = Enumerable.Range(1, 10).ToList();

        public int LivePreviewWindowCount
        {
            get => _livePreviewWindowCount;
            set
            {
                if (SetProperty(ref _livePreviewWindowCount, value))
                {
                    RefreshLivePreview(SelectedProgram, _livePreviewMemberships, _livePreviewDigitalIdsByStagingId);
                }
            }
        }

        public ProjectDistributionLivePreviewQueueItem? LivePreviewCurrentQueueItem
        {
            get => _livePreviewCurrentQueueItem;
            private set => SetProperty(ref _livePreviewCurrentQueueItem, value);
        }

        public DistributionBeneficiaryOption? SelectedAvailableBeneficiary
        {
            get => _selectedAvailableBeneficiary;
            set
            {
                if (SetProperty(ref _selectedAvailableBeneficiary, value) && AddBeneficiaryCommand is RelayCommand add)
                {
                    add.RaiseCanExecuteChanged();
                }
            }
        }

        public int SelectedBeneficiariesCount => _addPanelSelectedStagingIds.Count;

        public int AddPanelCurrentPage
        {
            get => _addPanelCurrentPage;
            private set
            {
                if (SetProperty(ref _addPanelCurrentPage, value))
                {
                    RaisePickerPagerCanExecuteChanged();
                }
            }
        }

        public int AddPanelTotalPages
        {
            get => _addPanelTotalPages;
            private set
            {
                if (SetProperty(ref _addPanelTotalPages, value))
                {
                    RaisePickerPagerCanExecuteChanged();
                }
            }
        }

        public int UnpickedCurrentPage
        {
            get => _unpickedCurrentPage;
            private set
            {
                if (SetProperty(ref _unpickedCurrentPage, value))
                {
                    RaisePickerPagerCanExecuteChanged();
                }
            }
        }

        public int UnpickedTotalPages
        {
            get => _unpickedTotalPages;
            private set
            {
                if (SetProperty(ref _unpickedTotalPages, value))
                {
                    RaisePickerPagerCanExecuteChanged();
                }
            }
        }

        private void RaisePickerPagerCanExecuteChanged()
        {
            (PrevAddPanelPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextAddPanelPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrevUnpickedPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextUnpickedPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public string AddBeneficiarySearchText
        {
            get => _addBeneficiarySearchText;
            set
            {
                if (SetProperty(ref _addBeneficiarySearchText, value))
                {
                    // Search runs DB-side so matches beyond the capped page are found.
                    _addPanelCurrentPage = 1;
                    OnPropertyChanged(nameof(AddPanelCurrentPage));
                    _ = LoadAvailableBeneficiariesAsync();
                }
            }
        }

        public ProjectDistributionBeneficiaryListItem? SelectedProgramBeneficiary
        {
            get => _selectedProgramBeneficiary;
            set
            {
                if (SetProperty(ref _selectedProgramBeneficiary, value))
                {
                    if (MarkBeneficiaryPendingCommand is RelayCommand markPending)
                    {
                        markPending.RaiseCanExecuteChanged();
                    }

                    if (RejectBeneficiaryCommand is RelayCommand reject)
                    {
                        reject.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public ProjectDistributionBeneficiaryListItem? SelectedPendingBeneficiary
        {
            get => _selectedPendingBeneficiary;
            set
            {
                if (SetProperty(ref _selectedPendingBeneficiary, value))
                {
                    if (value != null)
                    {
                        SelectedReleasedClaim = null;
                        _ = LoadHouseholdContextOnSelectionAsync(value.BeneficiaryStagingId);
                        _ = LoadSelectedPendingPhotoAsync(value.BeneficiaryStagingId);
                    }
                    else if (SelectedReleasedClaim == null)
                    {
                        ClearHouseholdContext();
                        HouseholdConfirmBeneficiaryPhoto = null;
                    }
                    SelectedProgramBeneficiary = value;
                    RefreshSelectedPendingDigitalId(value);
                    if (ConfirmReleaseCommand is RelayCommand confirmRelease)
                    {
                        confirmRelease.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public ProjectDistributionReleaseListItem? SelectedReleasedClaim
        {
            get => _selectedReleasedClaim;
            set
            {
                if (SetProperty(ref _selectedReleasedClaim, value))
                {
                    if (value != null)
                    {
                        SelectedPendingBeneficiary = null;
                        _ = LoadHouseholdContextOnSelectionAsync(value.BeneficiaryStagingId);
                        _ = LoadSelectedPendingPhotoAsync(value.BeneficiaryStagingId);
                    }
                    else if (SelectedPendingBeneficiary == null)
                    {
                        ClearHouseholdContext();
                        HouseholdConfirmBeneficiaryPhoto = null;
                    }
                }
            }
        }

        private void ClearHouseholdContext()
        {
            ScannedHouseholdMembers.Clear();
            OnPropertyChanged(nameof(HasHouseholdMembers));
            HasHouseholdContext = false;
            HouseholdContextSummary = string.Empty;
            HouseholdWarningMessage = null;
            RequiresHouseholdOverride = false;
            HouseholdOverrideAcknowledged = false;
            HouseholdAidReceivedSummary = string.Empty;
        }

        private async Task LoadHouseholdContextOnSelectionAsync(int beneficiaryStagingId)
        {
            try
            {
                await using var context = new LocalDbContext();
                var distributionService = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                await LoadHouseholdContextAsync(distributionService, beneficiaryStagingId);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load household context: {ex.Message}");
            }
        }

        private async Task LoadSelectedPendingPhotoAsync(int beneficiaryStagingId)
        {
            try
            {
                await using var context = new LocalDbContext();
                var photoPath = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(s => s.StagingID == beneficiaryStagingId)
                    .Select(s => s.PhotoPath)
                    .FirstOrDefaultAsync();
                HouseholdConfirmBeneficiaryPhoto = string.IsNullOrWhiteSpace(photoPath) ? null : LocalImageLoader.Load(photoPath) as BitmapSource;
            }
            catch
            {
                HouseholdConfirmBeneficiaryPhoto = null;
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    if (RefreshCommand is RelayCommand refresh)
                    {
                        refresh.RaiseCanExecuteChanged();
                    }

                    if (AddBeneficiaryCommand is RelayCommand add)
                    {
                        add.RaiseCanExecuteChanged();
                    }

                    if (OpenAddBeneficiaryPanelCommand is RelayCommand openAddPanel)
                    {
                        openAddPanel.RaiseCanExecuteChanged();
                    }

                    if (OpenScannerPanelCommand is RelayCommand openScannerPanel)
                    {
                        openScannerPanel.RaiseCanExecuteChanged();
                    }

                    if (MarkBeneficiaryPendingCommand is RelayCommand markPending)
                    {
                        markPending.RaiseCanExecuteChanged();
                    }

                    if (RejectBeneficiaryCommand is RelayCommand reject)
                    {
                        reject.RaiseCanExecuteChanged();
                    }



                    if (OpenLivePreviewCommand is RelayCommand preview)
                    {
                        preview.RaiseCanExecuteChanged();
                    }

                    if (NextLivePreviewItemCommand is RelayCommand next)
                    {
                        next.RaiseCanExecuteChanged();
                    }

                    if (PrevPendingPageCommand is RelayCommand prevPending)
                    {
                        prevPending.RaiseCanExecuteChanged();
                    }

                    if (NextPendingPageCommand is RelayCommand nextPending)
                    {
                        nextPending.RaiseCanExecuteChanged();
                    }

                    if (PrevReleasedPageCommand is RelayCommand prevReleased)
                    {
                        prevReleased.RaiseCanExecuteChanged();
                    }

                    if (NextReleasedPageCommand is RelayCommand nextReleased)
                    {
                        nextReleased.RaiseCanExecuteChanged();
                    }

                    if (PrevRejectedPageCommand is RelayCommand prevRejected)
                    {
                        prevRejected.RaiseCanExecuteChanged();
                    }

                    if (NextRejectedPageCommand is RelayCommand nextRejected)
                    {
                        nextRejected.RaiseCanExecuteChanged();
                    }

                    if (ConfirmReleaseCommand is RelayCommand confirmRelease)
                    {
                        confirmRelease.RaiseCanExecuteChanged();
                    }

                    if (OpenPcScannerCommand is RelayCommand openPcScanner)
                    {
                        openPcScanner.RaiseCanExecuteChanged();
                    }

                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                    _submitManualKeyInCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string AddBeneficiaryStatusMessage
        {
            get => _addBeneficiaryStatusMessage;
            private set => SetProperty(ref _addBeneficiaryStatusMessage, value);
        }

        public Brush AddBeneficiaryStatusBrush
        {
            get => _addBeneficiaryStatusBrush;
            private set => SetProperty(ref _addBeneficiaryStatusBrush, value);
        }

        public string ScannerStatusMessage
        {
            get => _scannerStatusMessage;
            private set => SetProperty(ref _scannerStatusMessage, value);
        }

        public Brush ScannerStatusBrush
        {
            get => _scannerStatusBrush;
            private set => SetProperty(ref _scannerStatusBrush, value);
        }

        public bool HasScannerSession
        {
            get => _hasScannerSession;
            private set => SetProperty(ref _hasScannerSession, value);
        }

        public int PendingCurrentPage
        {
            get => _pendingCurrentPage;
            private set => SetProperty(ref _pendingCurrentPage, value);
        }

        public int ReleasedCurrentPage
        {
            get => _releasedCurrentPage;
            private set => SetProperty(ref _releasedCurrentPage, value);
        }

        public int RejectedCurrentPage
        {
            get => _rejectedCurrentPage;
            private set => SetProperty(ref _rejectedCurrentPage, value);
        }

        public int PendingTotalCount => ProgramBeneficiaries.Count(item => item.Status == DistributionBeneficiaryStatus.Pending);
        public int ReleasedTotalCount => ProgramReleaseHistory.Count;
        public int RejectedTotalCount => ProgramBeneficiaries.Count(item => item.Status == DistributionBeneficiaryStatus.Rejected);
        public int PendingTotalPages => Math.Max(1, (int)Math.Ceiling(PendingTotalCount / (double)DistributionPageSize));
        public int ReleasedTotalPages => Math.Max(1, (int)Math.Ceiling(ReleasedTotalCount / (double)DistributionPageSize));
        public int RejectedTotalPages => Math.Max(1, (int)Math.Ceiling(RejectedTotalCount / (double)DistributionPageSize));
        public string PendingPaginationText => $"{PendingCurrentPage} / {PendingTotalPages}";
        public string ReleasedPaginationText => $"{ReleasedCurrentPage} / {ReleasedTotalPages}";
        public string RejectedPaginationText => $"{RejectedCurrentPage} / {RejectedTotalPages}";

        public string SelectedPendingDigitalIdCardNumber
        {
            get => _selectedPendingDigitalIdCardNumber;
            private set => SetProperty(ref _selectedPendingDigitalIdCardNumber, value);
        }

        public string SelectedPendingDigitalIdQrPayload
        {
            get => _selectedPendingDigitalIdQrPayload;
            private set => SetProperty(ref _selectedPendingDigitalIdQrPayload, value);
        }

        public string SelectedPendingDigitalIdStatusText
        {
            get => _selectedPendingDigitalIdStatusText;
            private set => SetProperty(ref _selectedPendingDigitalIdStatusText, value);
        }

        public BitmapSource? SelectedPendingDigitalIdQrImage
        {
            get => _selectedPendingDigitalIdQrImage;
            private set => SetProperty(ref _selectedPendingDigitalIdQrImage, value);
        }

        public IReadOnlyList<int> ScannerSessionDurationOptions { get; } = [15, 30, 60];

        public int SelectedScannerSessionDurationMinutes
        {
            get => _selectedScannerSessionDurationMinutes;
            set => SetProperty(ref _selectedScannerSessionDurationMinutes, value);
        }

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading project distribution monitor...");

            try
            {
                var selectedProgramId = SelectedProgram?.Id;
                await LoadAvailableBeneficiariesAsync();
                await LoadProgramsAsync(selectedProgramId);
                await LoadProjectDetailsAsync();
                SetSuccessStatus("Project distribution monitor refreshed.");
            }
            catch (Exception ex)
            {
                ClearLoadedState();
                SetErrorStatus($"Unable to load project distribution monitor: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadProgramsAsync(int? selectedProgramId = null)
        {
            await using var context = new LocalDbContext();
            _ = context.AyudaPrograms;
            var budgetService = new BudgetManagementService(context);
            var claimsByProgram = await context.AyudaProjectClaims
                .AsNoTracking()
                .GroupBy(b => b.AyudaProgramId)
                .Select(group => new
                {
                    ProgramId = group.Key,
                    ClaimCount = group.Count(),
                    TotalClaimed = group.Sum(x => (decimal?)x.UnitAmountSnapshot) ?? 0m,
                    LatestClaimedAt = group.Max(x => (DateTime?)x.ClaimedAt)
                })
                .ToDictionaryAsync(x => x.ProgramId);

            ProgramSummaries.Clear();
            foreach (var program in await budgetService.GetProgramsAsync())
            {
                claimsByProgram.TryGetValue(program.Id, out var claimSummary);
                ProgramSummaries.Add(ProjectDistributionProgramListItem.FromProgram(
                    program,
                    claimSummary?.ClaimCount ?? 0,
                    claimSummary?.TotalClaimed ?? 0m,
                    claimSummary?.LatestClaimedAt));
            }

            // Choose-project-first: never auto-select. Only restore a prior selection after a refresh.
            SelectedProgramSummary = selectedProgramId.HasValue
                ? ProgramSummaries.FirstOrDefault(item => item.Id == selectedProgramId.Value)
                : SelectedProgramSummary == null
                    ? null
                    : ProgramSummaries.FirstOrDefault(item => item.Id == SelectedProgramSummary.Id);

            ApplyProgramFilter();
            RefreshSidebarProjects();
        }

        private async Task LoadAvailableBeneficiariesAsync()
        {
            var version = ++_availableBeneficiarySearchVersion;
            var search = AddBeneficiarySearchText?.Trim();

            // Filter: Approved AND not already in the SELECTED project (if any).
            // SQLite executes "async" queries synchronously, so run them on the thread pool.
            var selectedProgramId = SelectedProgram?.Id;
            var requestedPage = Math.Max(1, _addPanelCurrentPage);
            var (enrolledInSelectedProject, totalCount, beneficiaries) = await Task.Run(async () =>
            {
                await using var context = new LocalDbContext();

                var enrolled = selectedProgramId.HasValue
                    ? await context.AyudaProjectBeneficiaries
                        .AsNoTracking()
                        .Where(b => b.AyudaProgramId == selectedProgramId.Value)
                        .Select(b => new EnrolledBeneficiaryKey(b.BeneficiaryStagingId, b.CivilRegistryId, b.BeneficiaryId))
                        .ToListAsync()
                    : new List<EnrolledBeneficiaryKey>();

                // Filter and page DB-side: only one BeneficiaryPickerDisplayLimit page of matches
                // is materialized so the panel stays responsive with the full municipal registry local.
                var query = context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(item => item.VerificationStatus == VerificationStatus.Approved);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(item =>
                        (item.FullName != null && item.FullName.Contains(search)) ||
                        (item.LastName != null && item.LastName.Contains(search)) ||
                        (item.FirstName != null && item.FirstName.Contains(search)) ||
                        (item.BeneficiaryId != null && item.BeneficiaryId.Contains(search)) ||
                        (item.CivilRegistryId != null && item.CivilRegistryId.Contains(search)));
                }

                var count = await query.CountAsync();
                var lastPage = Math.Max(1, (int)Math.Ceiling(count / (double)BeneficiaryPickerDisplayLimit));
                var boundedPage = Math.Min(requestedPage, lastPage);
                var page = await query
                    .OrderBy(item => item.FullName ?? item.LastName)
                    .ThenBy(item => item.FirstName)
                    .Skip((boundedPage - 1) * BeneficiaryPickerDisplayLimit)
                    .Take(BeneficiaryPickerDisplayLimit + enrolled.Count)
                    .Select(item => new PickerBeneficiaryRow(
                        item.StagingID,
                        item.BeneficiaryId,
                        item.CivilRegistryId,
                        item.LastName,
                        item.FirstName,
                        item.MiddleName,
                        item.FullName,
                        null,
                        null,
                        item.IsSenior,
                        item.IsPwd))
                    .ToListAsync();

                return (enrolled, count, page);
            });

            var alreadyEnrolledStagingIds = enrolledInSelectedProject.Select(b => b.BeneficiaryStagingId).ToHashSet();
            var alreadyEnrolledCivilIds = enrolledInSelectedProject.Where(b => b.CivilRegistryId != null).Select(b => b.CivilRegistryId!).ToHashSet();
            var alreadyEnrolledBenIds = enrolledInSelectedProject.Where(b => b.BeneficiaryId != null).Select(b => b.BeneficiaryId!).ToHashSet();

            if (version != _availableBeneficiarySearchVersion)
            {
                return; // a newer search superseded this one
            }

            AddPanelTotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)BeneficiaryPickerDisplayLimit));
            AddPanelCurrentPage = Math.Min(requestedPage, AddPanelTotalPages);

            AvailableBeneficiaries.Clear();
            foreach (var b in beneficiaries)
            {
                // Skip if already in the selected project
                if (alreadyEnrolledStagingIds.Contains(b.StagingID)) continue;
                if (!string.IsNullOrEmpty(b.CivilRegistryId) && alreadyEnrolledCivilIds.Contains(b.CivilRegistryId)) continue;
                if (!string.IsNullOrEmpty(b.BeneficiaryId) && alreadyEnrolledBenIds.Contains(b.BeneficiaryId)) continue;
                if (AvailableBeneficiaries.Count >= BeneficiaryPickerDisplayLimit) break;

                var option = new DistributionBeneficiaryOption
                {
                    StagingId = b.StagingID,
                    FullName = b.FullName ?? string.Empty,
                    FirstName = b.FirstName ?? string.Empty,
                    LastName = b.LastName ?? string.Empty,
                    BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = b.CivilRegistryId ?? string.Empty,
                    IsSenior = b.IsSenior,
                    IsPwd = b.IsPwd,
                    // Re-queries (search) must not lose picks made on earlier pages.
                    IsSelected = _addPanelSelectedStagingIds.Contains(b.StagingID)
                };

                option.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(DistributionBeneficiaryOption.IsSelected) &&
                        sender is DistributionBeneficiaryOption changed)
                    {
                        if (changed.IsSelected)
                        {
                            _addPanelSelectedStagingIds.Add(changed.StagingId);
                        }
                        else
                        {
                            _addPanelSelectedStagingIds.Remove(changed.StagingId);
                        }

                        OnPropertyChanged(nameof(SelectedBeneficiariesCount));
                        if (ConfirmAddBeneficiaryCommand is RelayCommand confirm)
                        {
                            confirm.RaiseCanExecuteChanged();
                        }
                    }
                };

                AvailableBeneficiaries.Add(option);
            }

            ApplyAvailableBeneficiaryFilter();

            if (SelectedAvailableBeneficiary != null)
            {
                SelectedAvailableBeneficiary = AvailableBeneficiaries.FirstOrDefault(item => item.StagingId == SelectedAvailableBeneficiary.StagingId);
            }
        }

        private async Task LoadProjectDetailsAsync()
        {
            ProgramBeneficiaries.Clear();
            ProgramReleaseHistory.Clear();
            OnPropertyChanged(nameof(HasProgramBeneficiaries));
            OnPropertyChanged(nameof(HasProgramReleaseHistory));

            var selectedProgram = SelectedProgram;
            if (selectedProgram == null)
            {
                ProgramReleaseEmptyStateMessage = "Select a project to review released beneficiary history.";
                _digitalIdsByStagingId = new Dictionary<int, BeneficiaryDigitalId>();
                PendingCurrentPage = 1;
                ReleasedCurrentPage = 1;
                RejectedCurrentPage = 1;
                await GetPendingBeneficiariesPaginatedAsync();
                await GetReleasedClaimsPaginatedAsync();
                await GetRejectedBeneficiariesPaginatedAsync();
                SelectedPendingBeneficiary = null;
                RefreshLivePreview(null, null, null);
                return;
            }

            var programId = selectedProgram.Id;

            try
            {
                // Parallelize database queries using SEPARATE contexts for thread safety
                var membershipsTask = Task.Run(async () =>
                {
                    await using var ctx = new LocalDbContext();
                    return await ctx.AyudaProjectBeneficiaries
                        .AsNoTracking()
                        .Where(item => item.AyudaProgramId == programId)
                        .OrderBy(item => item.Status)
                        .ThenBy(item => item.FullName)
                        .ToListAsync();
                });

                var legacyProjectClaimsTask = Task.Run(async () =>
                {
                    await using var ctx = new LocalDbContext();
                    return await ctx.AyudaProjectClaims
                        .AsNoTracking()
                        .Where(item => item.AyudaProgramId == programId)
                        .OrderByDescending(item => item.ClaimedAt)
                        .ToListAsync();
                });

                var distributedAmountTask = Task.Run(async () =>
                {
                    await using var ctx = new LocalDbContext();
                    return await ctx.BudgetLedgerEntries
                        .AsNoTracking()
                        .Where(item => item.ProgramId == programId && item.EntryType == BudgetLedgerEntryType.Release)
                        .SumAsync(item => (decimal?)item.TotalAmount);
                });

                await Task.WhenAll(membershipsTask, legacyProjectClaimsTask, distributedAmountTask);

                var memberships = await membershipsTask;
                var legacyProjectClaims = await legacyProjectClaimsTask;
                var distributedAmount = (await distributedAmountTask) ?? 0m;

                var stagingIds = memberships.Select(m => m.BeneficiaryStagingId).ToList();
                
                // Final context for sequential parts
                await using var context = new LocalDbContext();
                var digitalIdsByStagingId = await context.BeneficiaryDigitalIds
                    .AsNoTracking()
                    .Where(item => stagingIds.Contains(item.BeneficiaryStagingId) && item.IsActive)
                    .Select(item => new
                    {
                        item.Id,
                        item.BeneficiaryStagingId,
                        item.CardNumber,
                        item.QrPayload,
                        item.IsActive
                    })
                    .ToDictionaryAsync(item => item.BeneficiaryStagingId, item => new BeneficiaryDigitalId
                    {
                        Id = item.Id,
                        BeneficiaryStagingId = item.BeneficiaryStagingId,
                        CardNumber = item.CardNumber,
                        QrPayload = item.QrPayload,
                        IsActive = item.IsActive
                    });

                var releaseHistory = legacyProjectClaims
                    .Select(ProjectDistributionReleaseListItem.FromLegacyProjectClaim)
                    .OrderByDescending(item => item.ReleasedAt)
                    .ThenBy(item => item.FullName)
                    .ToList();

                _livePreviewMemberships = memberships;
                _livePreviewDigitalIdsByStagingId = digitalIdsByStagingId;
                _digitalIdsByStagingId = digitalIdsByStagingId;

                if (SelectedProgram?.Id != programId)
                {
                    return;
                }

                foreach (var membership in memberships)
                {
                    ProgramBeneficiaries.Add(ProjectDistributionBeneficiaryListItem.FromEntity(membership));
                }

                OnPropertyChanged(nameof(HasProgramBeneficiaries));

                foreach (var release in releaseHistory)
                {
                    ProgramReleaseHistory.Add(release);
                }

                OnPropertyChanged(nameof(HasProgramReleaseHistory));
                OnPropertyChanged(nameof(PendingTotalCount));
                OnPropertyChanged(nameof(ReleasedTotalCount));
                OnPropertyChanged(nameof(RejectedTotalCount));

                var beneficiaryCount = releaseHistory
                    .Select(item => item.IdentityKey)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                if (beneficiaryCount == 0)
                {
                    beneficiaryCount = releaseHistory
                        .Select(item => item.FullName)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();
                }

                SelectedProgramBudgetCapText = FormatBudgetCap(selectedProgram.BudgetCap);
                SelectedProgramDistributedText = FormatCurrency(distributedAmount);
                SelectedProgramRemainingBudgetText = FormatRemainingBudget(selectedProgram.BudgetCap, distributedAmount);
                SelectedProgramBeneficiaryCountText = beneficiaryCount.ToString("N0", CultureInfo.InvariantCulture);
                SelectedProgramReleaseSummary = releaseHistory.Count == 0
                    ? "No released beneficiaries are tied to this project yet."
                    : $"{releaseHistory.Count:N0} released entr{(releaseHistory.Count == 1 ? "y" : "ies")} are tied to this project.";
                ProgramReleaseEmptyStateMessage = releaseHistory.Count == 0
                    ? "No released beneficiaries are tied to this project yet."
                    : string.Empty;
                PendingCurrentPage = 1;
                ReleasedCurrentPage = 1;
                RejectedCurrentPage = 1;
                await GetPendingBeneficiariesPaginatedAsync();
                await GetReleasedClaimsPaginatedAsync();
                await GetRejectedBeneficiariesPaginatedAsync();
                RefreshLivePreview(selectedProgram, memberships, digitalIdsByStagingId);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Error loading project details: {ex.Message}");
            }
        }

        private bool CanAddBeneficiary()
        {
            return !IsBusy && SelectedProgram != null && SelectedAvailableBeneficiary != null;
        }

        private async Task IncludeBeneficiaryAsync()
        {
            if (!CanAddBeneficiary() || SelectedProgram == null || SelectedAvailableBeneficiary == null)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Adding beneficiary to the selected project...");

            try
            {
                await using var context = new LocalDbContext();
                var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await ExecuteProgramBeneficiaryAddAsync(service, SelectedProgram.Id, SelectedAvailableBeneficiary.StagingId, _currentUser.Id);
                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadProjectDetailsAsync();
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to add beneficiary: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanUpdateSelectedBeneficiaryStatus(DistributionBeneficiaryStatus targetStatus)
        {
            return !IsBusy
                && SelectedProgram != null
                && SelectedProgramBeneficiary != null
                && SelectedProgramBeneficiary.Status != DistributionBeneficiaryStatus.Released
                && SelectedProgramBeneficiary.Status != targetStatus;
        }

        private async Task UpdateSelectedBeneficiaryStatusAsync(DistributionBeneficiaryStatus targetStatus)
        {
            if (!CanUpdateSelectedBeneficiaryStatus(targetStatus) || SelectedProgram == null || SelectedProgramBeneficiary == null)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Updating beneficiary status to {targetStatus}...");

            try
            {
                await using var context = new LocalDbContext();
                var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.UpdateBeneficiaryStatusAsync(
                    SelectedProgram.Id,
                    SelectedProgramBeneficiary.BeneficiaryStagingId,
                    targetStatus,
                    _currentUser.Id,
                    null);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadProjectDetailsAsync();
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to update beneficiary status: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteRejectScannedBeneficiaryAsync()
        {
            if (ScannedBeneficiary == null || SelectedProgram == null) return;
            IsBusy = true;
            SetNeutralStatus("Setting beneficiary status to Pending/Not Eligible...");
            try
            {
                await using var context = new LocalDbContext();
                var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.UpdateBeneficiaryStatusAsync(
                    SelectedProgram.Id,
                    ScannedBeneficiary.BeneficiaryStagingId,
                    DistributionBeneficiaryStatus.Rejected,
                    _currentUser.Id,
                    null);
                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }
                await LoadProjectDetailsAsync();
                ResetScannedResult();
                SetSuccessStatus("Beneficiary set to Pending / Not Eligible.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Error updating status: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteProcessPcScan(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || SelectedProgram == null) return;

            // Queue protection: ignore scans while a dialog is active
            if (IsScannedResultVisible || IsReleaseSuccessState) return;

            payload = payload.Trim();

            // Success-only cooldown: only block if last SUCCESSFUL lookup matches within 1 second
            if (_lastScannedPayload != null &&
                string.Equals(payload, _lastScannedPayload, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.Now - _lastScannedTime).TotalSeconds < 1.0)
            {
                ScannerInputText = string.Empty;
                return;
            }

            ScannerInputText = string.Empty;

            // Municipal e-Kard cards carry a BEN-... beneficiary id — verify against
            // the CRS contract first, then continue into the normal release pipeline
            // via the local beneficiary-id lookup (no auto-import).
            if (EKardPayloadRouter.IsEKardPayload(payload))
            {
                await ExecuteProcessEKardScanAsync(payload);
                return;
            }

            // QR scans carry a payload that also serves as the confirm-time identity token.
            await ResolveAndPresentAsync(
                new BeneficiaryLookupRequest(BeneficiaryLookupSource.QrPayload, payload),
                confirmToken: payload);
        }

        private async Task ExecuteProcessEKardScanAsync(string payload)
        {
            IsBusy = true;
            SetNeutralStatus("Verifying e-Kard against CRS...");

            EKardVerificationResult result;
            try
            {
                await using var context = new LocalDbContext();
                var verificationService = new CrsDigitalIdVerificationService(context);
                result = await verificationService.VerifyAsync(new EKardVerificationRequest
                {
                    BeneficiaryId = payload,
                    UserId = _currentUser?.Id,
                    UserName = _currentUser?.Username ?? string.Empty
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"e-Kard verification error: {ex.Message}");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            switch (result.Validity)
            {
                case EKardValidity.Valid:
                    SetSuccessStatus($"e-Kard VALID: {result.BeneficiaryId}. Checking project enrollment...");
                    break;
                case EKardValidity.Expired:
                    SetErrorStatus($"e-Kard EXPIRED on {result.ExpiryDate:MMM dd, yyyy}: {result.BeneficiaryId}. Verify identity manually before releasing.");
                    return;
                case EKardValidity.Revoked:
                    SetErrorStatus($"e-Kard REVOKED: {result.BeneficiaryId}. Do not release against this card.");
                    _ = Task.Run(() => { try { Console.Beep(400, 600); } catch { } });
                    return;
                case EKardValidity.NotFound:
                    SetErrorStatus($"No e-Kard ID ever issued for {result.BeneficiaryId}.");
                    _ = Task.Run(() => { try { Console.Beep(400, 600); } catch { } });
                    return;
                default:
                    SetErrorStatus($"e-Kard status UNKNOWN (CRS offline, never cached): {result.BeneficiaryId}.");
                    return;
            }

            // Valid card — continue into the standard release pipeline by local
            // beneficiary-id (identity already verified by the e-Kard check).
            await ResolveAndPresentAsync(
                new BeneficiaryLookupRequest(BeneficiaryLookupSource.BeneficiaryId, result.BeneficiaryId),
                confirmToken: null);
        }

        private async Task ExecuteManualKeyIn()
        {
            var beneficiaryId = ManualBeneficiaryIdText?.Trim();
            if (string.IsNullOrWhiteSpace(beneficiaryId) || SelectedProgram == null) return;

            // Queue protection: ignore while a dialog is active
            if (IsScannedResultVisible || IsReleaseSuccessState) return;

            // Manual key-in has no QR payload; identity is verified visually + by the resolved staging id.
            await ResolveAndPresentAsync(
                new BeneficiaryLookupRequest(BeneficiaryLookupSource.BeneficiaryId, beneficiaryId),
                confirmToken: null);
        }

        /// <summary>
        /// Single presentation pipeline shared by QR scan and manual key-in. Resolves the request,
        /// populates the identical scanned-result state, and marks identity verified on success.
        /// </summary>
        private async Task ResolveAndPresentAsync(BeneficiaryLookupRequest request, string? confirmToken)
        {
            if (SelectedProgram == null) return;

            IsBusy = true;
            IsScannedBeneficiaryEligible = false;
            IsIdentityVerified = false;
            SetNeutralStatus("Analyzing ID card...");

            try
            {
                await using var context = new LocalDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);
                var lookup = await digitalIdService.ResolveLookupAsync(request);

                if (lookup == null)
                {
                    // Do NOT set cooldown on failed lookup (allows instant retry)
                    _ = Task.Run(() => { try { Console.Beep(400, 600); } catch { } });
                    LastScanSummaryText = "Last Scan: Not Found";
                    LastScanSummaryBrush = (Brush)Application.Current.Resources["BrandDangerBrush"];
                    SetErrorStatus(request.Source == BeneficiaryLookupSource.BeneficiaryId
                        ? "Beneficiary ID not found."
                        : "Invalid QR code or beneficiary not found.");
                    return;
                }

                var distributionService = new ProjectDistributionService(context);
                var qualification = await distributionService.EvaluateQualificationAsync(SelectedProgram.Id, lookup.BeneficiaryStagingId);

                ScannedBeneficiary = new ProjectDistributionBeneficiaryListItem
                {
                    FullName = lookup.FullName,
                    BeneficiaryId = lookup.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = lookup.CivilRegistryId ?? string.Empty,
                    BeneficiaryStagingId = lookup.BeneficiaryStagingId,
                    Status = qualification.BeneficiaryStatus ?? DistributionBeneficiaryStatus.Pending
                };

                ScannedBeneficiaryStatus = qualification.Message;
                ScannedBeneficiaryPhoto = string.IsNullOrWhiteSpace(lookup.PhotoPath) ? null : LocalImageLoader.Load(lookup.PhotoPath) as BitmapSource;
                ScannedBeneficiaryAddress = lookup.Address ?? "No address on file";
                ScannedBeneficiaryAge = lookup.Age ?? "--";
                ScannedBeneficiaryGender = lookup.Sex ?? "--";
                ScannedAllocatedAmountText = BuildAllocatedAmountText(SelectedProgram);

                if (qualification.BeneficiaryStatus == DistributionBeneficiaryStatus.Released)
                {
                    ScannedBeneficiaryStatus = "ALREADY CLAIMED";
                    ScannedBeneficiaryStatusColor = (Brush)Application.Current.Resources["BrandDangerBrush"];
                    IsScannedBeneficiaryEligible = false;
                    ScannerActionLabel = "ALREADY CLAIMED";
                    ScannerCancelLabel = "CLOSE";
                    _ = Task.Run(() => { try { Console.Beep(400, 600); } catch { } });
                }
                else if (qualification.BeneficiaryStatus == DistributionBeneficiaryStatus.Pending)
                {
                    ScannedBeneficiaryStatus = "READY FOR RELEASE";
                    ScannedBeneficiaryStatusColor = (Brush)Application.Current.Resources["BrandSuccessBrush"];
                    IsScannedBeneficiaryEligible = true;
                    ScannerActionLabel = "CONFIRM CLAIM";
                    ScannerCancelLabel = "DECLINE";
                }
                else
                {
                    ScannedBeneficiaryStatus = "NOT ENROLLED IN THIS PROJECT";
                    ScannedBeneficiaryStatusColor = (Brush)Application.Current.Resources["BrandWarningBrush"];
                    IsScannedBeneficiaryEligible = false;
                    ScannerActionLabel = "NOT QUALIFIED";
                    ScannerCancelLabel = "CLOSE";
                    _ = Task.Run(() => { try { Console.Beep(400, 600); } catch { } });
                }

                var history = await context.BudgetLedgerEntries
                    .AsNoTracking()
                    .Include(e => e.Program)
                    .Where(e => e.SourceRecordId == lookup.BeneficiaryStagingId.ToString() && e.ProgramId != SelectedProgram.Id && e.EntryType == BudgetLedgerEntryType.Release)
                    .OrderByDescending(e => e.EntryDate)
                    .Select(e => new ProjectDistributionReleaseListItem
                    {
                        IdentityKey = e.ProgramId.ToString() ?? "",
                        AssistanceLabel = e.Program != null ? e.Program.ProgramName : "Unknown Project",
                        ReleasedAt = e.EntryDate,
                        ReferenceLabel = e.Remarks ?? ""
                    })
                    .Take(5)
                    .ToListAsync();

                ScannedBeneficiaryHistory.Clear();
                foreach (var item in history)
                {
                    ScannedBeneficiaryHistory.Add(item);
                }
                OnPropertyChanged(nameof(HasScannedHistory));

                // Household verification (members + cross-project "already received" soft warning).
                await LoadHouseholdContextAsync(distributionService, lookup.BeneficiaryStagingId);

                // Confirm-time identity token: the QR payload for scans, null for key-in.
                _lastScannedPayload = confirmToken;
                _lastScannedTime = DateTime.Now;
                IsIdentityVerified = true;
                IsScannedResultVisible = true;
                ManualBeneficiaryIdText = string.Empty;
                SetNeutralStatus($"ID analyzed: {lookup.FullName}. Please review and confirm release.");
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

        /// <summary>
        /// Loads the household verification context (members + cross-project "already received" soft
        /// warning) for a staged beneficiary and populates all modal/overlay-bound properties.
        /// Shared by the scanner/key-in overlay and the pending-list Payout Verification flow.
        /// </summary>
        private async Task LoadHouseholdContextAsync(ProjectDistributionService distributionService, int beneficiaryStagingId)
        {
            if (SelectedProgram == null)
            {
                return;
            }

            var householdContext = await distributionService.GetHouseholdVerificationContextAsync(SelectedProgram.Id, beneficiaryStagingId);
            ScannedHouseholdMembers.Clear();
            foreach (var member in householdContext.Members)
            {
                ScannedHouseholdMembers.Add(member);
            }
            OnPropertyChanged(nameof(HasHouseholdMembers));
            HasHouseholdContext = householdContext.HasHousehold;
            ScannedHouseholdNumber = householdContext.HasHousehold ? householdContext.HouseholdCode : "--";
            ScannedHouseholdRole = householdContext.Members.FirstOrDefault(m => m.IsScannedBeneficiary)?.RelationshipToHead is { Length: > 0 } role
                ? role
                : (householdContext.HasHousehold ? "Member" : "--");
            HouseholdContextSummary = householdContext.HasHousehold
                ? $"Household {householdContext.HouseholdCode} · Head: {householdContext.HeadName}"
                : "No household linked to this beneficiary.";
            HouseholdWarningMessage = householdContext.WarningMessage;
            RequiresHouseholdOverride = householdContext.AnyMemberAlreadyReceived;
            HouseholdOverrideAcknowledged = false;

            // Front-and-center decision line: how many in the family have already received this assistance.
            if (!householdContext.HasHousehold)
            {
                HouseholdAidReceivedSummary = "No household on file for this beneficiary.";
            }
            else
            {
                var receivedCount = householdContext.Members.Count(m => m.AlreadyReceivedSameAssistanceType);
                var totalCount = householdContext.Members.Count;
                HouseholdAidReceivedSummary = receivedCount > 0
                    ? $"{receivedCount} of {totalCount} household member(s) already received this assistance."
                    : $"No one in this household ({totalCount} member(s)) has received this assistance yet.";
            }
        }

        /// <summary>
        /// Confirm on the scan overlay. Always opens the Household Review modal so the operator sees
        /// the family before releasing; a duplicate additionally requires the override acknowledgment.
        /// </summary>
        private Task RequestConfirmReleaseAsync()
        {
            if (SelectedProgram == null || ScannedBeneficiary == null || !IsIdentityVerified)
            {
                return Task.CompletedTask;
            }

            // Hide the scanned result overlay so it doesn't stay visible behind the household confirm modal
            IsScannedResultVisible = false;

            // Advisor requirement: the household must be reviewed on every confirm, not only on
            // duplicates. The modal always opens; the acknowledgment checkbox is only *required*
            // when a member already received the same assistance (RequiresHouseholdOverride).
            _householdConfirmFromPendingList = false;
            HouseholdConfirmBeneficiaryName = ScannedBeneficiary.FullName;
            HouseholdConfirmBeneficiaryPhoto = ScannedBeneficiaryPhoto;
            HouseholdOverrideAcknowledged = false;
            return OpenHouseholdConfirmWithRequirementsAsync(ScannedBeneficiary.BeneficiaryStagingId);
        }

        private async Task OpenPendingBeneficiaryOverlayAsync()
        {
            if (SelectedPendingBeneficiary == null || SelectedProgram == null) return;
            if (IsScannedResultVisible || IsReleaseSuccessState) return;

            await ResolveAndPresentAsync(
                new BeneficiaryLookupRequest(BeneficiaryLookupSource.BeneficiaryId, SelectedPendingBeneficiary.BeneficiaryId),
                confirmToken: null);
        }

        private async Task OpenReleasedBeneficiaryOverlayAsync()
        {
            if (SelectedReleasedClaim == null || SelectedProgram == null) return;
            if (IsScannedResultVisible || IsReleaseSuccessState) return;

            await ResolveAndPresentAsync(
                new BeneficiaryLookupRequest(BeneficiaryLookupSource.BeneficiaryId, SelectedReleasedClaim.IdentityKey),
                confirmToken: null);
        }

        /// <summary>Loads the attachment checklist (cedula, barangay certificate, ...) for the beneficiary, then opens the release modal.</summary>
        private async Task OpenHouseholdConfirmWithRequirementsAsync(int beneficiaryStagingId)
        {
            _releaseRequirementsStagingId = beneficiaryStagingId;
            await LoadReleaseRequirementsAsync(beneficiaryStagingId);
            IsHouseholdConfirmVisible = true;
        }

        /// <summary>Standard attachments every release must present; seeded when the beneficiary has no saved checklist yet.</summary>
        private static readonly string[] DefaultReleaseRequirements =
        {
            "Cedula (Community Tax Certificate)",
            "Barangay Certificate"
        };

        private async Task LoadReleaseRequirementsAsync(int beneficiaryStagingId)
        {
            foreach (var oldRow in ReleaseRequirementRows)
            {
                oldRow.PropertyChanged -= OnReleaseRequirementRowChanged;
            }

            ReleaseRequirementRows.Clear();

            var programId = SelectedProgram?.Id;
            if (programId.HasValue)
            {
                try
                {
                    await using var context = new LocalDbContext();
                    var saved = await context.BeneficiaryRequirementDocuments
                        .AsNoTracking()
                        .Where(d => d.BeneficiaryStagingId == beneficiaryStagingId
                                    && d.AyudaProgramId == programId.Value
                                    && !d.IsDeleted)
                        .OrderBy(d => d.Id)
                        .ToListAsync();

                    foreach (var doc in saved)
                    {
                        ReleaseRequirementRows.Add(new RequirementEntryRow
                        {
                            PersistedId = doc.Id,
                            DocumentName = doc.DocumentName,
                            SubmittedDate = doc.SubmittedDate,
                            Status = doc.Status,
                            Remarks = doc.Remarks
                        });
                    }
                }
                catch
                {
                    // Checklist load failure must not block the modal; fall through to defaults.
                }
            }

            if (ReleaseRequirementRows.Count == 0)
            {
                foreach (var name in DefaultReleaseRequirements)
                {
                    ReleaseRequirementRows.Add(new RequirementEntryRow { DocumentName = name });
                }
            }

            foreach (var row in ReleaseRequirementRows)
            {
                row.PropertyChanged += OnReleaseRequirementRowChanged;
            }

            RefreshReleaseRequirementState();
        }

        private void OnReleaseRequirementRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(RequirementEntryRow.Status) or nameof(RequirementEntryRow.IsComplete))
            {
                RefreshReleaseRequirementState();
            }
        }

        private void RefreshReleaseRequirementState()
        {
            OnPropertyChanged(nameof(AreReleaseRequirementsComplete));
            OnPropertyChanged(nameof(HasMissingReleaseRequirements));
            OnPropertyChanged(nameof(ReleaseRequirementsSummaryText));
            _confirmHouseholdReleaseCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Writes checklist edits made in the modal back to beneficiary_requirement_documents (soft-delete safe: updates or inserts, never removes).</summary>
        private async Task SaveReleaseRequirementsAsync()
        {
            var programId = SelectedProgram?.Id;
            var stagingId = _releaseRequirementsStagingId;
            if (programId == null || stagingId == 0 || ReleaseRequirementRows.Count == 0)
            {
                return;
            }

            try
            {
                await using var context = new LocalDbContext();
                foreach (var row in ReleaseRequirementRows)
                {
                    if (string.IsNullOrWhiteSpace(row.DocumentName))
                    {
                        continue;
                    }

                    BeneficiaryRequirementDocument? doc = null;
                    if (row.PersistedId > 0)
                    {
                        doc = await context.BeneficiaryRequirementDocuments
                            .FirstOrDefaultAsync(d => d.Id == row.PersistedId);
                    }

                    if (doc == null)
                    {
                        doc = new BeneficiaryRequirementDocument
                        {
                            BeneficiaryStagingId = stagingId,
                            AyudaProgramId = programId.Value,
                            DocumentName = row.DocumentName
                        };
                        context.BeneficiaryRequirementDocuments.Add(doc);
                    }

                    doc.DocumentName = row.DocumentName;
                    doc.SubmittedDate = row.SubmittedDate ?? (row.IsComplete ? DateTime.Now : null);
                    doc.Status = row.IsComplete ? "Complete" : "Incomplete";
                    doc.Remarks = row.Remarks;
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Requirement checklist could not be saved: {ex.Message}");
            }
        }

        /// <summary>Stamps the membership so the UNRELEASED / UNCLAIMED card shows why the beneficiary was held back.</summary>
        private async Task MarkMembershipMissingRequirementsAsync()
        {
            var programId = SelectedProgram?.Id;
            var stagingId = _releaseRequirementsStagingId;
            if (programId == null || stagingId == 0)
            {
                return;
            }

            var missingNames = ReleaseRequirementRows
                .Where(r => !r.IsComplete && !string.IsNullOrWhiteSpace(r.DocumentName))
                .Select(r => r.DocumentName)
                .ToList();
            if (missingNames.Count == 0)
            {
                return;
            }

            try
            {
                await using var context = new LocalDbContext();
                var membership = await context.AyudaProjectBeneficiaries
                    .FirstOrDefaultAsync(m => m.AyudaProgramId == programId.Value && m.BeneficiaryStagingId == stagingId);
                if (membership != null && membership.Status == DistributionBeneficiaryStatus.Pending)
                {
                    membership.StatusReason = $"Missing requirements: {string.Join(", ", missingNames)}";
                    membership.StatusUpdatedAt = DateTime.Now;
                    membership.StatusUpdatedByUserId = _currentUser.Id;
                    await context.SaveChangesAsync();
                    await LoadProjectDetailsAsync();
                }
            }
            catch
            {
                // Best-effort annotation; the beneficiary already stays unreleased either way.
            }
        }

        /// <summary>Final Confirm from inside the household modal; routes to whichever flow opened it.</summary>
        private async Task ConfirmHouseholdReleaseAsync()
        {
            // Hard gate: releasing without the required attachments is never allowed.
            if (!AreReleaseRequirementsComplete)
            {
                SetErrorStatus("Cannot release: the beneficiary has missing requirements. They stay in UNRELEASED / UNCLAIMED.");
                return;
            }

            await SaveReleaseRequirementsAsync();
            IsHouseholdConfirmVisible = false;
            if (_householdConfirmFromPendingList)
            {
                await ExecutePendingReleaseAsync();
            }
            else
            {
                await ExecuteConfirmScannedClaimAsync();
            }
        }

        /// <summary>Decline from inside the household modal: back to the caller without releasing.</summary>
        private void CloseHouseholdConfirm()
        {
            IsHouseholdConfirmVisible = false;
            HouseholdOverrideAcknowledged = false;
            HouseholdConfirmBeneficiaryPhoto = null;
            // Persist whatever the operator ticked and, when items are still missing, tag the
            // membership so the UNRELEASED / UNCLAIMED card shows the reason.
            _ = SaveAndAnnotateMissingRequirementsAsync();
        }

        private async Task SaveAndAnnotateMissingRequirementsAsync()
        {
            await SaveReleaseRequirementsAsync();
            await MarkMembershipMissingRequirementsAsync();
        }

        private async Task ExecuteConfirmScannedClaimAsync()
        {
            // Identity must be verified (profile shown) before a claim can be confirmed — never bypass.
            if (SelectedProgram == null || ScannedBeneficiary == null || !IsIdentityVerified)
            {
                return;
            }

            var beneficiaryName = ScannedBeneficiary.FullName;
            var isManualKeyIn = string.IsNullOrWhiteSpace(_lastScannedPayload);
            var sourceRemark = isManualKeyIn ? "Marked via Manual Beneficiary ID Key-in (Confirmed)" : "Marked via Desktop Camera (Confirmed)";
            // Record when the operator overrode a household duplicate warning, for the audit trail.
            var claimRemark = RequiresHouseholdOverride
                ? $"{sourceRemark} [Household duplicate warning overridden]"
                : sourceRemark;
            IsBusy = true;
            SetNeutralStatus($"Recording claim for {beneficiaryName}...");

            try
            {
                await using var context = new LocalDbContext();
                var distributionService = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await distributionService.RecordClaimAsync(
                    SelectedProgram.Id,
                    ScannedBeneficiary.BeneficiaryStagingId,
                    _currentUser.Id,
                    _lastScannedPayload,
                    claimRemark);

                if (result.IsSuccess)
                {
                    // Success beep
                    _ = Task.Run(() => { try { Console.Beep(1000, 200); } catch { } });

                    // Show auto-close success overlay
                    IsReleaseSuccessState = true;
                    SetSuccessStatus(result.Message);

                    // Hold success screen for 1.5 seconds
                    await Task.Delay(1500);

                    IsReleaseSuccessState = false;
                    LastScanSummaryText = $"Last Scan: {beneficiaryName} (Released)";
                    LastScanSummaryBrush = (Brush)Application.Current.Resources["BrandSuccessBrush"];
                    await LoadProjectDetailsAsync();
                    ResetScannedResult();
                }
                else
                {
                    _ = Task.Run(() => { try { Console.Beep(400, 600); } catch { } });
                    SetErrorStatus(result.Message);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to record claim: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetScannedResult()
        {
            ScannedBeneficiary = null;
            ScannedBeneficiaryStatus = null;
            ScannedBeneficiaryPhoto = null;
            ScannedBeneficiaryAddress = string.Empty;
            ScannedBeneficiaryAge = string.Empty;
            ScannedBeneficiaryGender = string.Empty;
            ScannedHouseholdNumber = string.Empty;
            ScannedHouseholdRole = string.Empty;
            ScannedAllocatedAmountText = string.Empty;
            HouseholdConfirmBeneficiaryPhoto = null;
            ScannedBeneficiaryHistory.Clear();
            OnPropertyChanged(nameof(HasScannedHistory));
            ScannedHouseholdMembers.Clear();
            OnPropertyChanged(nameof(HasHouseholdMembers));
            IsHouseholdConfirmVisible = false;
            HasHouseholdContext = false;
            HouseholdContextSummary = string.Empty;
            HouseholdAidReceivedSummary = string.Empty;
            HouseholdWarningMessage = null;
            RequiresHouseholdOverride = false;
            HouseholdOverrideAcknowledged = false;
            IsScannedBeneficiaryEligible = false;
            IsIdentityVerified = false;
            _lastScannedPayload = null;
            IsScannedResultVisible = false;
            RequestScannerFocus?.Invoke();
        }

        /// <summary>Allocated release per beneficiary for the profile modal: unit amount (Cash) or item + quantity (Goods).</summary>
        private static string BuildAllocatedAmountText(AyudaProgram? program)
        {
            if (program == null)
            {
                return string.Empty;
            }

            if (program.ReleaseKind == AssistanceReleaseKind.Cash)
            {
                return program.UnitAmount is decimal amount
                    ? string.Format(CultureInfo.GetCultureInfo("en-PH"), "PHP {0:N2}", amount)
                    : "Amount not set";
            }

            var item = string.IsNullOrWhiteSpace(program.ItemName) ? program.ItemDescription : program.ItemName;
            var quantity = program.QuantityPerBeneficiary is decimal qty
                ? $"{qty:0.##} {program.UnitOfMeasure}".Trim()
                : string.Empty;
            return string.Join(" — ", new[] { item, quantity }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        /// <summary>DEV: seeds the fixed mock households/beneficiaries for testing the household-verification flow.</summary>
        private async Task ExecuteSeedMockDataAsync()
        {
            IsBusy = true;
            SetNeutralStatus("Seeding mock households and beneficiaries...");
            try
            {
                var result = await new DevSeedService().SeedMockHouseholdsAsync(_currentUser.Id);
                if (result.AlreadySeeded)
                {
                    SetNeutralStatus(result.Message);
                }
                else
                {
                    SetSuccessStatus(result.Message);
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to seed mock data: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanOpenLivePreview()
        {
            return SelectedProgram != null;
        }

        private void OpenLivePreview()
        {
            if (!CanOpenLivePreview())
            {
                return;
            }

            if (_livePreviewWindow == null || !_livePreviewWindow.IsLoaded)
            {
                _livePreviewWindow = new ProjectDistributionLivePreviewWindow
                {
                    DataContext = this
                };

                _livePreviewWindow.Closed += (_, _) => _livePreviewWindow = null;
                _livePreviewWindow.Show();
                return;
            }

            if (_livePreviewWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _livePreviewWindow.WindowState = System.Windows.WindowState.Normal;
            }

            _livePreviewWindow.Activate();
        }

        private void RefreshLivePreview(
            AyudaProgram? program,
            IReadOnlyList<AyudaProjectBeneficiary>? memberships,
            IReadOnlyDictionary<int, BeneficiaryDigitalId>? digitalIdsByStagingId)
        {
            if (program == null)
            {
                LivePreviewProgramName = "No active project selected";
                LivePreviewPrimaryLabel = "--";
                LivePreviewSecondaryLabel = "Select a project to prepare the queue monitor.";
                LivePreviewQueueStatusText = "No queue loaded";
                LivePreviewQueueItems.Clear();
                LivePreviewCurrentQueueItem = null;
                LivePreviewCallBoard.Clear();
                _ = SyncQueueStateToNetworkAsync();
                return;
            }

            LivePreviewProgramName = program.ProgramName;

            var orderedMemberships = memberships?
                .OrderBy(item => item.Status)
                .ThenBy(item => item.AddedAt)
                .ThenBy(item => item.FullName)
                .ToList()
                ?? [];

            var pendingQueue = orderedMemberships
                .Where(item => item.Status == DistributionBeneficiaryStatus.Pending)
                .ToList();

            BuildLivePreviewQueue(pendingQueue);

            if (LivePreviewCurrentQueueItem == null)
            {
                LivePreviewPrimaryLabel = "QUEUE CLEAR";
                LivePreviewSecondaryLabel = "No pending beneficiary is waiting for release.";
                LivePreviewQueueStatusText = orderedMemberships.Count == 0
                    ? "No beneficiaries added to this project yet"
                    : "All queued beneficiaries are already released or rejected";
            }
            else
            {
                var currentMembership = pendingQueue.First();
                BeneficiaryDigitalId? digitalId = null;
                var hasDigitalId = digitalIdsByStagingId != null &&
                    digitalIdsByStagingId.TryGetValue(currentMembership.BeneficiaryStagingId, out digitalId);

                LivePreviewPrimaryLabel = hasDigitalId
                    ? digitalId!.CardNumber
                    : !string.IsNullOrWhiteSpace(currentMembership.BeneficiaryId)
                        ? currentMembership.BeneficiaryId
                        : !string.IsNullOrWhiteSpace(currentMembership.CivilRegistryId)
                            ? currentMembership.CivilRegistryId
                            : currentMembership.FullName;
                LivePreviewSecondaryLabel = $"{currentMembership.FullName} • {LivePreviewCurrentQueueItem.WindowLabel}";
                LivePreviewQueueStatusText = $"{pendingQueue.Count:N0} pending in queue";
            }

            _ = SyncQueueStateToNetworkAsync();
        }

        private async Task SyncQueueStateToNetworkAsync()
        {
            try
            {
                await LocalScannerGatewayService.Shared.EnsureStartedAsync();

                var queueState = new
                {
                    programName = SelectedProgram?.ProgramName ?? "No Project Selected",
                    windowCount = LivePreviewWindowCount,
                    current = LivePreviewCurrentQueueItem == null ? null : new
                    {
                        fullName = LivePreviewCurrentQueueItem.FullName,
                        windowLabel = LivePreviewCurrentQueueItem.WindowLabel,
                        details = LivePreviewCurrentQueueItem.Details
                    },
                    callBoard = LivePreviewCallBoard.Select(item => new
                    {
                        fullName = item.FullName,
                        windowLabel = item.WindowLabel
                    }).ToList(),
                    queue = LivePreviewQueueItems.Select(item => new
                    {
                        fullName = item.FullName,
                        windowLabel = item.WindowLabel,
                        details = item.Details
                    }).ToList()
                };

                var json = System.Text.Json.JsonSerializer.Serialize(queueState);
                await LocalScannerGatewayService.Shared.UpdateQueueStateAsync(json);
            }
            catch
            {
                // Silent fail - network update is not critical
            }
        }

        private void BuildLivePreviewQueue(IReadOnlyList<AyudaProjectBeneficiary> pendingQueue)
        {
            LivePreviewQueueItems.Clear();
            for (var index = 0; index < pendingQueue.Count; index++)
            {
                var beneficiary = pendingQueue[index];
                LivePreviewQueueItems.Add(new ProjectDistributionLivePreviewQueueItem
                {
                    QueueNumber = index + 1,
                    WindowLabel = $"Window {(index % LivePreviewWindowCount) + 1}",
                    FullName = beneficiary.FullName,
                    Details = !string.IsNullOrWhiteSpace(beneficiary.BeneficiaryId)
                        ? beneficiary.BeneficiaryId
                        : !string.IsNullOrWhiteSpace(beneficiary.CivilRegistryId)
                            ? beneficiary.CivilRegistryId
                            : "Pending release",
                    StatusText = beneficiary.Status.ToString()
                });
            }

            LivePreviewCurrentQueueItem = LivePreviewQueueItems.FirstOrDefault();
        }

        private bool CanAdvanceLivePreviewQueue()
        {
            return !IsBusy && LivePreviewQueueItems.Count > 0;
        }

        private void AdvanceLivePreviewQueue()
        {
            if (LivePreviewQueueItems.Count == 0)
            {
                return;
            }

            var calledItem = LivePreviewQueueItems[0];
            
            // Add to call board with timestamp
            LivePreviewCallBoard.Insert(0, new ProjectDistributionCallBoardEntry
            {
                FullName = calledItem.FullName,
                WindowLabel = calledItem.WindowLabel,
                CalledAt = DateTime.Now
            });

            // Keep only last 5 calls
            while (LivePreviewCallBoard.Count > 5)
            {
                LivePreviewCallBoard.RemoveAt(LivePreviewCallBoard.Count - 1);
            }

            LivePreviewQueueItems.RemoveAt(0);
            for (var index = 0; index < LivePreviewQueueItems.Count; index++)
            {
                LivePreviewQueueItems[index] = LivePreviewQueueItems[index] with
                {
                    QueueNumber = index + 1,
                    WindowLabel = $"Window {(index % LivePreviewWindowCount) + 1}"
                };
            }

            LivePreviewCurrentQueueItem = LivePreviewQueueItems.FirstOrDefault();
            if (LivePreviewCurrentQueueItem == null)
            {
                LivePreviewPrimaryLabel = "QUEUE CLEAR";
                LivePreviewSecondaryLabel = "No pending beneficiary is waiting for release.";
                LivePreviewQueueStatusText = "No pending beneficiaries remain.";
            }
            else
            {
                LivePreviewPrimaryLabel = LivePreviewCurrentQueueItem.Details;
                LivePreviewSecondaryLabel = $"{LivePreviewCurrentQueueItem.FullName} • {LivePreviewCurrentQueueItem.WindowLabel}";
                LivePreviewQueueStatusText = $"{LivePreviewQueueItems.Count:N0} pending in queue";
            }

            if (NextLivePreviewItemCommand is RelayCommand next)
            {
                next.RaiseCanExecuteChanged();
            }

            _ = SyncQueueStateToNetworkAsync();

            // Auto-remove call board entries after 15 seconds
            Task.Delay(15000).ContinueWith(_ =>
            {
                if (LivePreviewCallBoard.Count > 0)
                {
                    LivePreviewCallBoard.RemoveAt(LivePreviewCallBoard.Count - 1);
                }
            });
        }

        private void ClearLoadedState()
        {
            ProgramSummaries.Clear();
            AvailableBeneficiaries.Clear();
            ProgramBeneficiaries.Clear();
            ProgramReleaseHistory.Clear();
            PendingBeneficiaries.Clear();
            ReleasedClaims.Clear();
            RejectedBeneficiaries.Clear();
            _digitalIdsByStagingId = new Dictionary<int, BeneficiaryDigitalId>();
            PendingCurrentPage = 1;
            ReleasedCurrentPage = 1;
            RejectedCurrentPage = 1;
            SelectedPendingBeneficiary = null;
            OnPropertyChanged(nameof(HasProgramBeneficiaries));
            OnPropertyChanged(nameof(HasProgramReleaseHistory));
            OnPropertyChanged(nameof(PendingTotalCount));
            OnPropertyChanged(nameof(ReleasedTotalCount));
            OnPropertyChanged(nameof(RejectedTotalCount));
            OnPropertyChanged(nameof(PendingTotalPages));
            OnPropertyChanged(nameof(ReleasedTotalPages));
            OnPropertyChanged(nameof(RejectedTotalPages));
            OnPropertyChanged(nameof(PendingPaginationText));
            OnPropertyChanged(nameof(ReleasedPaginationText));
            OnPropertyChanged(nameof(RejectedPaginationText));
            ResetSelectedProgramDetails(null);
            SelectedProgramSummary = null;
            SelectedProgram = null;
        }

        private void ResetSelectedProgramDetails(AyudaProgram? program)
        {
            ProgramReleaseHistory.Clear();
            PendingBeneficiaries.Clear();
            ReleasedClaims.Clear();
            RejectedBeneficiaries.Clear();
            SelectedPendingBeneficiary = null;
            OnPropertyChanged(nameof(HasProgramReleaseHistory));
            OnPropertyChanged(nameof(PendingTotalCount));
            OnPropertyChanged(nameof(ReleasedTotalCount));
            OnPropertyChanged(nameof(RejectedTotalCount));
            OnPropertyChanged(nameof(PendingTotalPages));
            OnPropertyChanged(nameof(ReleasedTotalPages));
            OnPropertyChanged(nameof(RejectedTotalPages));
            OnPropertyChanged(nameof(PendingPaginationText));
            OnPropertyChanged(nameof(ReleasedPaginationText));
            OnPropertyChanged(nameof(RejectedPaginationText));

            if (program == null)
            {
                SelectedProgramWorkflowSummary = "Select a project to load its distribution summary.";
                SelectedProgramAssistanceSummary = "No project selected.";
                SelectedProgramBudgetCapText = "--";
                SelectedProgramDistributedText = "PHP 0.00";
                SelectedProgramRemainingBudgetText = "--";
                SelectedProgramBeneficiaryCountText = "0";
                SelectedProgramReleaseSummary = "No released beneficiary history loaded yet.";
                SelectedProgramDescriptionText = "Select a project to review distribution performance and beneficiary history.";
                ProgramReleaseEmptyStateMessage = "Select a project to review released beneficiary history.";
                _digitalIdsByStagingId = new Dictionary<int, BeneficiaryDigitalId>();
                PendingCurrentPage = 1;
                ReleasedCurrentPage = 1;
                RefreshLivePreview(null, null, null);
                return;
            }

            SelectedProgramWorkflowSummary = BuildWorkflowSummary(program);
            SelectedProgramAssistanceSummary = BuildAssistanceSummary(program);
            SelectedProgramBudgetCapText = FormatBudgetCap(program.BudgetCap);
            SelectedProgramDistributedText = "Loading...";
            SelectedProgramRemainingBudgetText = program.BudgetCap.HasValue ? "Loading..." : "No budget cap set";
            SelectedProgramBeneficiaryCountText = "--";
            SelectedProgramReleaseSummary = "Loading released beneficiary history...";
            SelectedProgramDescriptionText = string.IsNullOrWhiteSpace(program.Description)
                ? "No project description was configured for this program."
                : program.Description.Trim();
            ProgramReleaseEmptyStateMessage = string.Empty;
            RefreshLivePreview(program, null, null);
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = Brushes.DimGray;
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = Brushes.ForestGreen;
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = Brushes.Firebrick;
        }

        private static string BuildWorkflowSummary(AyudaProgram program)
        {
            var schedule = (program.StartDate, program.EndDate) switch
            {
                ({ } start, { } end) => $"Scheduled {start:MMM dd, yyyy} to {end:MMM dd, yyyy}.",
                ({ } start, null) => $"Starts {start:MMM dd, yyyy}; end date is not set.",
                (null, { } end) => $"Ends {end:MMM dd, yyyy}; start date is not set.",
                _ => "No schedule is configured yet."
            };

            return $"Status: {program.DistributionStatus}. {schedule}";
        }

        private static string BuildAssistanceSummary(AyudaProgram program)
        {
            var assistanceType = NormalizeNullable(program.AssistanceType);
            var itemDescription = NormalizeNullable(program.ItemDescription);

            return (assistanceType, itemDescription) switch
            {
                ({ } type, { } item) => $"Assistance: {type}. Item / detail: {item}.",
                ({ } type, null) => $"Assistance: {type}.",
                (null, { } item) => $"Item / detail: {item}.",
                _ => "No assistance type or item detail is configured yet."
            };
        }

        private static string FormatBudgetCap(decimal? budgetCap)
        {
            return budgetCap.HasValue
                ? FormatCurrency(budgetCap.Value)
                : "No budget cap set";
        }

        private static string FormatRemainingBudget(decimal? budgetCap, decimal distributedAmount)
        {
            if (!budgetCap.HasValue)
            {
                return "No budget cap set";
            }

            var remaining = budgetCap.Value - distributedAmount;
            return remaining >= 0
                ? FormatCurrency(remaining)
                : $"Over by {FormatCurrency(Math.Abs(remaining))}";
        }

        private static string FormatCurrency(decimal amount)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"PHP {amount:N2}");
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static async Task<ProjectDistributionOperationResult> ExecuteProgramBeneficiaryAddAsync(
            ProjectDistributionService service,
            int ayudaProgramId,
            int beneficiaryStagingId,
            int actedByUserId)
        {
            var method = typeof(ProjectDistributionService).GetMethod("Add" + "BeneficiaryAsync");
            if (method == null)
            {
                throw new InvalidOperationException("Project distribution add-beneficiary workflow is unavailable.");
            }

            var task = method.Invoke(service, [ayudaProgramId, beneficiaryStagingId, actedByUserId]) as Task<ProjectDistributionOperationResult>;
            if (task == null)
            {
                throw new InvalidOperationException("Project distribution add-beneficiary workflow returned an unexpected result.");
            }

            return await task;
        }

        private static async Task<ProjectDistributionOperationResult> ExecuteProgramBeneficiariesBulkAddAsync(
            ProjectDistributionService service,
            int ayudaProgramId,
            IEnumerable<int> beneficiaryStagingIds,
            int actedByUserId)
        {
            var method = typeof(ProjectDistributionService).GetMethod("BulkAddBeneficiariesAsync");
            if (method == null)
            {
                throw new InvalidOperationException("Project distribution bulk add-beneficiary workflow is unavailable.");
            }

            var task = method.Invoke(service, [ayudaProgramId, beneficiaryStagingIds, actedByUserId]) as Task<ProjectDistributionOperationResult>;
            if (task == null)
            {
                throw new InvalidOperationException("Project distribution bulk add-beneficiary workflow returned an unexpected result.");
            }

            return await task;
        }

        private void OpenAddBeneficiaryPanel()
        {
            if (SelectedProgram == null)
            {
                AddBeneficiaryStatusMessage = "Select a project before adding beneficiaries.";
                AddBeneficiaryStatusBrush = Brushes.Firebrick;
                return;
            }

            CloseProjectActionMenus();
            AddBeneficiaryStatusMessage = string.Empty;
            _addPanelSelectedStagingIds.Clear();
            OnPropertyChanged(nameof(SelectedBeneficiariesCount));
            AddPanelCurrentPage = 1;
            AddPanelTotalPages = 1;
            AddBeneficiarySearchText = string.Empty;
            SelectedAvailableBeneficiary = null;
            // The search-text setter only reloads on change — always reload so a
            // stale page from the previous panel session never lingers.
            _ = LoadAvailableBeneficiariesAsync();
            IsAddBeneficiaryPanelOpen = true;
        }

        private void CloseAddBeneficiaryPanel()
        {
            IsAddBeneficiaryPanelOpen = false;
            AddBeneficiaryStatusMessage = string.Empty;
            _addPanelSelectedStagingIds.Clear();
            OnPropertyChanged(nameof(SelectedBeneficiariesCount));
            SelectedAvailableBeneficiary = null;
        }

        private bool CanOpenAddBeneficiaryPanel()
        {
            return !IsBusy && SelectedProgram != null;
        }

        private bool CanOpenScannerPanel()
        {
            return !IsBusy && SelectedProgram != null;
        }

        private void OpenScannerPanel()
        {
            if (SelectedProgram == null)
            {
                ScannerStatusMessage = "Select a project before configuring the scanner.";
                ScannerStatusBrush = Brushes.Firebrick;
                return;
            }

            CloseProjectActionMenus();
            ScannerStatusMessage = string.Empty;
            IsScannerPanelOpen = true;
        }

        private bool CanConfirmAddBeneficiary()
        {
            return !IsBusy && SelectedProgram != null && SelectedBeneficiariesCount > 0;
        }

        private async Task ConfirmAddBeneficiaryAsync()
        {
            var selectedProgram = SelectedProgram;
            // The id set covers picks made across searches, not just the visible page.
            var selectedIds = _addPanelSelectedStagingIds.ToList();

            if (selectedProgram == null || selectedIds.Count == 0)
            {
                return;
            }

            IsBusy = true;

            try
            {
                await using var context = new LocalDbContext();
                var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await ExecuteProgramBeneficiariesBulkAddAsync(service, selectedProgram.Id, selectedIds, _currentUser.Id);

                if (result.IsSuccess)
                {
                    AddBeneficiaryStatusMessage = result.Message;
                    AddBeneficiaryStatusBrush = Brushes.ForestGreen;
                    
                    await Task.Delay(1500);
                    CloseAddBeneficiaryPanel();
                    
                    if (SelectedProgram?.Id == selectedProgram.Id)
                    {
                        await LoadProjectDetailsAsync();
                    }
                }
                else
                {
                    AddBeneficiaryStatusMessage = result.Message;
                    AddBeneficiaryStatusBrush = Brushes.Firebrick;
                }
            }
            catch (Exception ex)
            {
                AddBeneficiaryStatusMessage = $"Error: {ex.Message}";
                AddBeneficiaryStatusBrush = Brushes.Firebrick;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SelectAllFiltered()
        {
            foreach (var item in FilteredAvailableBeneficiaries)
            {
                item.IsSelected = true;
            }
            OnPropertyChanged(nameof(SelectedBeneficiariesCount));
            if (ConfirmAddBeneficiaryCommand is RelayCommand confirm)
            {
                confirm.RaiseCanExecuteChanged();
            }
        }

        private void DeselectAll()
        {
            _addPanelSelectedStagingIds.Clear();
            foreach (var item in AvailableBeneficiaries)
            {
                item.IsSelected = false;
            }
            OnPropertyChanged(nameof(SelectedBeneficiariesCount));
            if (ConfirmAddBeneficiaryCommand is RelayCommand confirm)
            {
                confirm.RaiseCanExecuteChanged();
            }
        }

        private void CloseScannerPanel()
        {
            IsScannerPanelOpen = false;
            ScannerStatusMessage = string.Empty;
        }

        private void OpenProjectActionMenu(ProjectDistributionProgramListItem? summary)
        {
            if (summary == null)
            {
                return;
            }

            var willOpen = !summary.IsActionMenuOpen;
            CloseProjectActionMenus();
            summary.IsActionMenuOpen = willOpen;
            SelectedProgramSummary = summary;
        }

        private bool CanOpenProgramPanel(ProjectDistributionProgramListItem? summary)
        {
            return !IsBusy && summary != null;
        }

        private void OpenProgramAddBeneficiaryPanel(ProjectDistributionProgramListItem? summary)
        {
            if (!CanOpenProgramPanel(summary))
            {
                return;
            }

            SelectedProgramSummary = summary;
            OpenAddBeneficiaryPanel();
        }

        private void OpenProgramScannerPanel(ProjectDistributionProgramListItem? summary)
        {
            if (!CanOpenProgramPanel(summary))
            {
                return;
            }

            SelectedProgramSummary = summary;
            OpenScannerPanel();
        }

        private void CloseProjectActionMenus()
        {
            foreach (var item in ProgramSummaries)
            {
                item.IsActionMenuOpen = false;
            }
        }

        private void ApplyAvailableBeneficiaryFilter()
        {
            var search = AddBeneficiarySearchText?.Trim();
            var filtered = AvailableBeneficiaries
                .Where(item =>
                    string.IsNullOrWhiteSpace(search) ||
                    item.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.BeneficiaryId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.CivilRegistryId.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.FullName)
                .ToList();

            FilteredAvailableBeneficiaries.Clear();
            foreach (var beneficiary in filtered)
            {
                FilteredAvailableBeneficiaries.Add(beneficiary);
            }

            if (SelectedAvailableBeneficiary != null && !FilteredAvailableBeneficiaries.Any(item => item.StagingId == SelectedAvailableBeneficiary.StagingId))
            {
                SelectedAvailableBeneficiary = null;
            }
        }

        private Task GetPendingBeneficiariesPaginatedAsync()
        {
            var query = ProgramBeneficiaries
                .Where(item => item.Status == DistributionBeneficiaryStatus.Pending);

            if (!string.IsNullOrWhiteSpace(PendingSearchText))
            {
                var search = PendingSearchText.Trim();
                query = query.Where(item =>
                    item.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.BeneficiaryId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.CivilRegistryId.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var pendingItems = query
                .OrderBy(item => item.FullName)
                .ToList();

            var totalPages = Math.Max(1, (int)Math.Ceiling(pendingItems.Count / (double)DistributionPageSize));
            if (PendingCurrentPage > totalPages)
            {
                PendingCurrentPage = totalPages;
            }

            if (PendingCurrentPage < 1)
            {
                PendingCurrentPage = 1;
            }

            PendingBeneficiaries.Clear();
            foreach (var item in pendingItems
                .Skip((PendingCurrentPage - 1) * DistributionPageSize)
                .Take(DistributionPageSize))
            {
                PendingBeneficiaries.Add(item);
            }

            if (SelectedPendingBeneficiary != null && !pendingItems.Any(item => item.Id == SelectedPendingBeneficiary.Id))
            {
                SelectedPendingBeneficiary = null;
            }

            if (SelectedPendingBeneficiary == null)
            {
                SelectedPendingBeneficiary = PendingBeneficiaries.FirstOrDefault();
            }

            OnPropertyChanged(nameof(PendingTotalCount));
            OnPropertyChanged(nameof(PendingTotalPages));
            OnPropertyChanged(nameof(PendingPaginationText));

            if (PrevPendingPageCommand is RelayCommand prevPending)
            {
                prevPending.RaiseCanExecuteChanged();
            }

            if (NextPendingPageCommand is RelayCommand nextPending)
            {
                nextPending.RaiseCanExecuteChanged();
            }

            return Task.CompletedTask;
        }

        /// <summary>Mirrors <see cref="GetPendingBeneficiariesPaginatedAsync"/> for the Rejected/Not-Eligible bucket.</summary>
        private Task GetRejectedBeneficiariesPaginatedAsync()
        {
            var rejectedItems = ProgramBeneficiaries
                .Where(item => item.Status == DistributionBeneficiaryStatus.Rejected)
                .OrderBy(item => item.FullName)
                .ToList();

            var totalPages = Math.Max(1, (int)Math.Ceiling(rejectedItems.Count / (double)DistributionPageSize));
            if (RejectedCurrentPage > totalPages)
            {
                RejectedCurrentPage = totalPages;
            }

            if (RejectedCurrentPage < 1)
            {
                RejectedCurrentPage = 1;
            }

            RejectedBeneficiaries.Clear();
            foreach (var item in rejectedItems
                .Skip((RejectedCurrentPage - 1) * DistributionPageSize)
                .Take(DistributionPageSize))
            {
                RejectedBeneficiaries.Add(item);
            }

            OnPropertyChanged(nameof(RejectedTotalCount));
            OnPropertyChanged(nameof(RejectedTotalPages));
            OnPropertyChanged(nameof(RejectedPaginationText));

            if (PrevRejectedPageCommand is RelayCommand prevRejected)
            {
                prevRejected.RaiseCanExecuteChanged();
            }

            if (NextRejectedPageCommand is RelayCommand nextRejected)
            {
                nextRejected.RaiseCanExecuteChanged();
            }

            return Task.CompletedTask;
        }

        private Task GetReleasedClaimsPaginatedAsync()
        {
            var releasedItems = ProgramReleaseHistory
                .OrderByDescending(item => item.ReleasedAt)
                .ThenBy(item => item.FullName)
                .ToList();

            var totalPages = Math.Max(1, (int)Math.Ceiling(releasedItems.Count / (double)DistributionPageSize));
            if (ReleasedCurrentPage > totalPages)
            {
                ReleasedCurrentPage = totalPages;
            }

            if (ReleasedCurrentPage < 1)
            {
                ReleasedCurrentPage = 1;
            }

            ReleasedClaims.Clear();
            foreach (var item in releasedItems
                .Skip((ReleasedCurrentPage - 1) * DistributionPageSize)
                .Take(DistributionPageSize))
            {
                ReleasedClaims.Add(item);
            }

            OnPropertyChanged(nameof(ReleasedTotalCount));
            OnPropertyChanged(nameof(ReleasedTotalPages));
            OnPropertyChanged(nameof(ReleasedPaginationText));

            if (PrevReleasedPageCommand is RelayCommand prevReleased)
            {
                prevReleased.RaiseCanExecuteChanged();
            }

            if (NextReleasedPageCommand is RelayCommand nextReleased)
            {
                nextReleased.RaiseCanExecuteChanged();
            }

            return Task.CompletedTask;
        }

        private bool CanMovePendingPage(int direction)
        {
            return !IsBusy && PendingCurrentPage + direction >= 1 && PendingCurrentPage + direction <= PendingTotalPages;
        }

        private async Task ChangePendingPageAsync(int direction)
        {
            if (!CanMovePendingPage(direction))
            {
                return;
            }

            PendingCurrentPage += direction;
            await GetPendingBeneficiariesPaginatedAsync();
        }

        private bool CanMoveReleasedPage(int direction)
        {
            return !IsBusy && ReleasedCurrentPage + direction >= 1 && ReleasedCurrentPage + direction <= ReleasedTotalPages;
        }

        private bool CanMoveRejectedPage(int direction)
        {
            return !IsBusy && RejectedCurrentPage + direction >= 1 && RejectedCurrentPage + direction <= RejectedTotalPages;
        }

        private async Task ChangeRejectedPageAsync(int direction)
        {
            if (!CanMoveRejectedPage(direction))
            {
                return;
            }

            RejectedCurrentPage += direction;
            await GetRejectedBeneficiariesPaginatedAsync();
        }

        private async Task ChangeReleasedPageAsync(int direction)
        {
            if (!CanMoveReleasedPage(direction))
            {
                return;
            }

            ReleasedCurrentPage += direction;
            await GetReleasedClaimsPaginatedAsync();
        }

        private void RefreshSelectedPendingDigitalId(ProjectDistributionBeneficiaryListItem? beneficiary)
        {
            if (beneficiary == null)
            {
                SelectedPendingDigitalIdCardNumber = "No digital ID issued yet.";
                SelectedPendingDigitalIdQrPayload = string.Empty;
                SelectedPendingDigitalIdQrImage = null;
                SelectedPendingDigitalIdStatusText = "Select a pending beneficiary to review the digital ID.";
                return;
            }

            if (_digitalIdsByStagingId.TryGetValue(beneficiary.BeneficiaryStagingId, out var digitalId))
            {
                SelectedPendingDigitalIdCardNumber = digitalId.CardNumber;
                SelectedPendingDigitalIdQrPayload = digitalId.QrPayload;
                SelectedPendingDigitalIdQrImage = QrCodeToolkitService.GenerateQrImage(digitalId.QrPayload, 10);
                SelectedPendingDigitalIdStatusText = "Digital ID is ready for verification and release.";
                return;
            }

            SelectedPendingDigitalIdCardNumber = "No digital ID issued yet.";
            SelectedPendingDigitalIdQrPayload = string.Empty;
            SelectedPendingDigitalIdQrImage = null;
            SelectedPendingDigitalIdStatusText = "This beneficiary does not have an active digital ID yet.";
        }

        private bool CanConfirmRelease()
        {
            return !IsBusy
                && SelectedProgram != null
                && SelectedPendingBeneficiary != null;
        }

        public event Action? RequestCloseDialog;

        /// <summary>
        /// Loads the household roster for the Distribution Record Details dialog so the operator
        /// can see whether another household member already received the same assistance.
        /// </summary>
        public async Task LoadDetailDialogHouseholdAsync(int beneficiaryStagingId)
        {
            if (SelectedProgram == null || beneficiaryStagingId <= 0)
            {
                return;
            }

            try
            {
                await using var context = new LocalDbContext();
                var distributionService = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                await LoadHouseholdContextAsync(distributionService, beneficiaryStagingId);
            }
            catch
            {
                // Household roster is informational in the detail dialog; ignore load failures.
            }
        }

        private async Task ConfirmReleaseAsync()
        {
            if (!CanConfirmRelease() || SelectedProgram == null || SelectedPendingBeneficiary == null)
            {
                return;
            }

            // Advisor requirement: every confirm opens the Household Review modal (replaces the old
            // MessageBox) so the operator sees the family and any prior claims before releasing.
            IsBusy = true;
            SetNeutralStatus("Loading household context...");
            try
            {
                await using var context = new LocalDbContext();
                var distributionService = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                await LoadHouseholdContextAsync(distributionService, SelectedPendingBeneficiary.BeneficiaryStagingId);

                var photoPath = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(s => s.StagingID == SelectedPendingBeneficiary.BeneficiaryStagingId)
                    .Select(s => s.PhotoPath)
                    .FirstOrDefaultAsync();
                HouseholdConfirmBeneficiaryPhoto = string.IsNullOrWhiteSpace(photoPath) ? null : LocalImageLoader.Load(photoPath) as BitmapSource;
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load household context: {ex.Message}");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            _householdConfirmFromPendingList = true;
            HouseholdConfirmBeneficiaryName = SelectedPendingBeneficiary.FullName;
            HouseholdOverrideAcknowledged = false;
            await OpenHouseholdConfirmWithRequirementsAsync(SelectedPendingBeneficiary.BeneficiaryStagingId);
        }

        /// <summary>Records the pending-list release after the Household Review modal is confirmed.</summary>
        private async Task ExecutePendingReleaseAsync()
        {
            if (SelectedProgram == null || SelectedPendingBeneficiary == null)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Confirming beneficiary release...");

            try
            {
                await using var context = new LocalDbContext();
                var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                // Record when the operator overrode a household duplicate warning, for the audit trail.
                var claimRemark = RequiresHouseholdOverride
                    ? "Released from pending list [Household duplicate warning overridden]"
                    : null;
                var result = await service.RecordClaimAsync(
                    SelectedProgram.Id,
                    SelectedPendingBeneficiary.BeneficiaryStagingId,
                    _currentUser.Id,
                    SelectedPendingDigitalIdQrPayload,
                    claimRemark);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadProjectDetailsAsync();
                await LoadAvailableBeneficiariesAsync(); // Refresh available list to prevent double-adding
                SetSuccessStatus(result.Message);

                if (result.Message.Contains("GGMS sync warning"))
                {
                    MessageBox.Show(
                        "The claim was recorded locally, but GGMS synchronization failed.\n\n" +
                        "Reason: " + result.Message.Split("GGMS sync warning:")[1].Trim() + "\n\n" +
                        "Please check your internet connection or GGMS database permissions.",
                        "GGMS Sync Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Auto-close the dialog after success
                RequestCloseDialog?.Invoke();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to confirm release: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override string ToString()
        {
            return "ProjectDistributionViewModel";
        }

        public bool HasProgramReleaseHistory => ProgramReleaseHistory.Count > 0;
        public bool HasProgramBeneficiaries => ProgramBeneficiaries.Count > 0;
    }

    public sealed class ProjectDistributionProgramListItem : ObservableObject
    {
        private bool _isActionMenuOpen;

        public int Id { get; init; }
        public AyudaProgram Program { get; init; } = null!;
        public string ProgramName { get; init; } = string.Empty;
        public string ProgramCode { get; init; } = string.Empty;
        public AyudaProgramDistributionStatus DistributionStatus { get; init; }
        public string ClaimCountText { get; init; } = string.Empty;
        public string ClaimedAmountText { get; init; } = string.Empty;
        public string LatestClaimText { get; init; } = string.Empty;
        public string SourceFundLabel { get; init; } = string.Empty;

        public bool IsActionMenuOpen
        {
            get => _isActionMenuOpen;
            set => SetProperty(ref _isActionMenuOpen, value);
        }

        public static ProjectDistributionProgramListItem FromProgram(
            AyudaProgram program,
            int claimCount,
            decimal totalClaimed,
            DateTime? latestClaimedAt)
        {
            return new ProjectDistributionProgramListItem
            {
                Id = program.Id,
                Program = program,
                ProgramName = program.ProgramName,
                ProgramCode = program.ProgramCode,
                DistributionStatus = program.DistributionStatus,
                ClaimCountText = $"{claimCount:N0} claim{(claimCount == 1 ? string.Empty : "s")}",
                ClaimedAmountText = $"Claimed: {FormatCurrency(totalClaimed)}",
                LatestClaimText = latestClaimedAt.HasValue
                    ? $"Latest claim: {latestClaimedAt:MMM dd, yyyy hh:mm tt}"
                    : "No claim recorded yet",
                SourceFundLabel = program.SourceDonation != null
                    ? $"\uD83D\uDCB0 {program.SourceDonation.DonorName}"
                    : program.SourceGGMSBudget != null
                        ? $"\uD83C\uDFDB GGMS - {program.SourceGGMSBudget.OfficeName}"
                        : string.Empty
            };
        }

        private static string FormatCurrency(decimal amount)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"PHP {amount:N2}");
        }
    }

    public sealed class ProjectDistributionReleaseListItem
    {
        public string FullName { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public string ReferenceLabel { get; init; } = string.Empty;
        public string AssistanceLabel { get; init; } = string.Empty;
        public string AmountText { get; init; } = string.Empty;
        public DateTime ReleasedAt { get; init; }
        public string Remarks { get; init; } = string.Empty;
        public string IdentityKey { get; init; } = string.Empty;
        public int BeneficiaryStagingId { get; init; }

        public static ProjectDistributionReleaseListItem FromLegacyProjectClaim(AyudaProjectClaim claim)
        {
            var assistanceLabel = NormalizeLabel(claim.AssistanceTypeSnapshot, claim.ItemDescriptionSnapshot, "Legacy project claim");
            return new ProjectDistributionReleaseListItem
            {
                FullName = NormalizeLabel(claim.FullName, "Unknown beneficiary"),
                SourceLabel = "Legacy Project Claim",
                ReferenceLabel = $"Claim #{claim.Id}",
                AssistanceLabel = assistanceLabel,
                AmountText = FormatCurrency(claim.UnitAmountSnapshot ?? 0m),
                ReleasedAt = claim.ClaimedAt,
                Remarks = NormalizeLabel(claim.Remarks, "--"),
                IdentityKey = NormalizeIdentity(claim.CivilRegistryId, claim.BeneficiaryId, claim.FullName),
                BeneficiaryStagingId = claim.BeneficiaryStagingId
            };
        }

        private static string FormatCurrency(decimal amount)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"PHP {amount:N2}");
        }

        private static string NormalizeIdentity(params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }

            return string.Empty;
        }

        private static string NormalizeLabel(string? primaryValue, string fallbackValue)
        {
            return string.IsNullOrWhiteSpace(primaryValue) ? fallbackValue : primaryValue.Trim();
        }

        private static string NormalizeLabel(string? primaryValue, string? secondaryValue, string fallbackValue)
        {
            if (!string.IsNullOrWhiteSpace(primaryValue))
            {
                return primaryValue.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secondaryValue))
            {
                return secondaryValue.Trim();
            }

            return fallbackValue;
        }
    }

    public sealed class DistributionBeneficiaryOption : ObservableObject
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int StagingId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string MiddleName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string BeneficiaryId { get; init; } = string.Empty;
        public string CivilRegistryId { get; init; } = string.Empty;
        public int? LinkedHouseholdId { get; init; }
        public int? LinkedHouseholdMemberId { get; init; }
        public bool IsSenior { get; init; }
        public bool IsPwd { get; init; }
        public string? Address { get; init; }
        public string? Age { get; init; }
        public string? Sex { get; init; }
        public string? PhotoPath { get; init; }
        public string? HouseholdNumber { get; init; }
        public string? HouseholdRole { get; init; }

        public ObservableCollection<CommunityTaxEntryRow> CommunityTaxRows { get; } = new();
        public ObservableCollection<RequirementEntryRow> RequirementRows { get; } = new();

        public bool IsRequirementsComplete => RequirementRows.Count > 0 && RequirementRows.All(r => r.Status == "Complete");
        
        public string Initials
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName[..1]);
                if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName[..1]);
                
                if (parts.Count == 0 && !string.IsNullOrWhiteSpace(FullName))
                {
                    var nameParts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (nameParts.Length > 0) parts.Add(nameParts[0][..1]);
                    if (nameParts.Length > 1) parts.Add(nameParts[^1][..1]);
                }

                return string.Join("", parts).ToUpperInvariant();
            }
        }

        public string DisplayLabel => !string.IsNullOrWhiteSpace(BeneficiaryId)
            ? $"{FullName} [{BeneficiaryId}]"
            : !string.IsNullOrWhiteSpace(CivilRegistryId)
                ? $"{FullName} [CR: {CivilRegistryId}]"
                : FullName;

        public static DistributionBeneficiaryOption FromEntity(BeneficiaryStaging beneficiary)
        {
            var fullName = !string.IsNullOrWhiteSpace(beneficiary.FullName)
                ? beneficiary.FullName.Trim()
                : string.Join(" ", new[] { beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));

            return new DistributionBeneficiaryOption
            {
                StagingId = beneficiary.StagingID,
                FullName = fullName,
                FirstName = beneficiary.FirstName ?? string.Empty,
                LastName = beneficiary.LastName ?? string.Empty,
                BeneficiaryId = beneficiary.BeneficiaryId ?? string.Empty,
                CivilRegistryId = beneficiary.CivilRegistryId ?? string.Empty,
                IsSenior = beneficiary.IsSenior,
                IsPwd = beneficiary.IsPwd,
                Address = beneficiary.Address,
                Age = beneficiary.Age,
                Sex = beneficiary.Sex,
                PhotoPath = beneficiary.PhotoPath
            };
        }
    }

    public sealed record ProjectDistributionLivePreviewQueueItem
    {
        public int QueueNumber { get; init; }
        public string WindowLabel { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
    }

    public sealed record ProjectDistributionCallBoardEntry
    {
        public string FullName { get; init; } = string.Empty;
        public string WindowLabel { get; init; } = string.Empty;
        public DateTime CalledAt { get; init; }
    }

    public sealed class ProjectDistributionBeneficiaryListItem
    {
        public int Id { get; init; }
        public int BeneficiaryStagingId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string BeneficiaryId { get; init; } = string.Empty;
        public string CivilRegistryId { get; init; } = string.Empty;
        public DistributionBeneficiaryStatus Status { get; init; }
        public string StatusText => Status.ToString();
        public string? StatusReason { get; init; }
        public DateTime AddedAt { get; init; }

        public static ProjectDistributionBeneficiaryListItem FromEntity(AyudaProjectBeneficiary beneficiary)
        {
            return new ProjectDistributionBeneficiaryListItem
            {
                Id = beneficiary.Id,
                BeneficiaryStagingId = beneficiary.BeneficiaryStagingId,
                FullName = beneficiary.FullName,
                BeneficiaryId = beneficiary.BeneficiaryId ?? string.Empty,
                CivilRegistryId = beneficiary.CivilRegistryId ?? string.Empty,
                Status = beneficiary.Status,
                StatusReason = beneficiary.StatusReason,
                AddedAt = beneficiary.AddedAt
            };
        }
    }

    public sealed class CommunityTaxEntryRow : ObservableObject
    {
        private string _cedulaNumber = string.Empty;
        private string? _paidAmountText;
        private DateTime? _paidDate;

        public string CedulaNumber
        {
            get => _cedulaNumber;
            set => SetProperty(ref _cedulaNumber, value);
        }

        public string? PaidAmountText
        {
            get => _paidAmountText;
            set => SetProperty(ref _paidAmountText, value);
        }

        public DateTime? PaidDate
        {
            get => _paidDate;
            set => SetProperty(ref _paidDate, value);
        }

        public decimal? GetPaidAmount()
        {
            if (decimal.TryParse(_paidAmountText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var amount))
                return amount;
            return null;
        }
    }

    public sealed class RequirementEntryRow : ObservableObject
    {
        private string _documentName = string.Empty;
        private DateTime? _submittedDate;
        private string _status = "Incomplete";
        private string? _remarks;

        /// <summary>Database id of the backing beneficiary_requirement_documents row; 0 = not yet persisted.</summary>
        public int PersistedId { get; set; }

        public string DocumentName
        {
            get => _documentName;
            set => SetProperty(ref _documentName, value);
        }

        public DateTime? SubmittedDate
        {
            get => _submittedDate;
            set => SetProperty(ref _submittedDate, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsComplete));
                }
            }
        }

        /// <summary>Checkbox view of <see cref="Status"/>: checked = the attachment was presented ("Complete").</summary>
        public bool IsComplete
        {
            get => string.Equals(_status, "Complete", StringComparison.OrdinalIgnoreCase);
            set => Status = value ? "Complete" : "Incomplete";
        }

        public string? Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
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
        private string _distributionScannerSessionUrl = string.Empty;
        private string _distributionScannerSessionPin = string.Empty;
        private string _distributionScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _distributionScannerQrImage;
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
        private string _addBeneficiaryStatusMessage = string.Empty;
        private Brush _addBeneficiaryStatusBrush = Brushes.DimGray;
        private string _scannerStatusMessage = string.Empty;
        private Brush _scannerStatusBrush = Brushes.DimGray;
        private bool _hasScannerSession;
        private IReadOnlyDictionary<int, BeneficiaryDigitalId> _digitalIdsByStagingId = new Dictionary<int, BeneficiaryDigitalId>();
        private int _pendingCurrentPage = 1;
        private int _releasedCurrentPage = 1;
        private string _addBeneficiarySearchText = string.Empty;
        private string _pendingSearchText = string.Empty;
        private string _selectedPendingDigitalIdCardNumber = "No digital ID issued yet.";
        private string _selectedPendingDigitalIdQrPayload = string.Empty;
        private string _selectedPendingDigitalIdStatusText = "Select a pending beneficiary to review the digital ID.";
        private BitmapSource? _selectedPendingDigitalIdQrImage;
        private int _selectedScannerSessionDurationMinutes = 15;
        private const int DistributionPageSize = 10;

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

        public ProjectDistributionViewModel(User currentUser)
        {
            _currentUser = currentUser;

            ProgramTypes = new ObservableCollection<AyudaProgramType>(
                Enum.GetValues<AyudaProgramType>().Where(type => type != AyudaProgramType.AssistanceCase && type != AyudaProgramType.Seminar));
            ReleaseKinds = new ObservableCollection<AssistanceReleaseKind>(Enum.GetValues<AssistanceReleaseKind>());
            DistributionStatuses = new ObservableCollection<AyudaProgramDistributionStatus>(Enum.GetValues<AyudaProgramDistributionStatus>());
            AvailableUnpickedBeneficiaries = new ObservableCollection<DistributionBeneficiaryOption>();

            RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            AddBeneficiaryCommand = new RelayCommand(async _ => await IncludeBeneficiaryAsync(), _ => CanAddBeneficiary());
            MarkBeneficiaryPendingCommand = new RelayCommand(async _ => await UpdateSelectedBeneficiaryStatusAsync(DistributionBeneficiaryStatus.Pending), _ => CanUpdateSelectedBeneficiaryStatus(DistributionBeneficiaryStatus.Pending));
            RejectBeneficiaryCommand = new RelayCommand(async _ => await UpdateSelectedBeneficiaryStatusAsync(DistributionBeneficiaryStatus.Rejected), _ => CanUpdateSelectedBeneficiaryStatus(DistributionBeneficiaryStatus.Rejected));
            CreateDistributionScannerSessionCommand = new RelayCommand(
                async _ => await CreateDistributionScannerSessionAsync(),
                _ => CanCreateDistributionScannerSession());
            OpenLivePreviewCommand = new RelayCommand(_ => OpenLivePreview(), _ => CanOpenLivePreview());
            NextLivePreviewItemCommand = new RelayCommand(_ => AdvanceLivePreviewQueue(), _ => CanAdvanceLivePreviewQueue());
            OpenScannerPanelCommand = new RelayCommand(_ => OpenScannerPanel(), _ => CanOpenScannerPanel());
            OpenAddBeneficiaryPanelCommand = new RelayCommand(_ => OpenAddBeneficiaryPanel(), _ => CanOpenAddBeneficiaryPanel());
            CloseAddBeneficiaryPanelCommand = new RelayCommand(_ => CloseAddBeneficiaryPanel());
            ConfirmAddBeneficiaryCommand = new RelayCommand(async _ => await ConfirmAddBeneficiaryAsync(), _ => CanConfirmAddBeneficiary());
            SelectAllFilteredCommand = new RelayCommand(_ => SelectAllFiltered());
            DeselectAllCommand = new RelayCommand(_ => DeselectAll());
            CloseScannerPanelCommand = new RelayCommand(_ => CloseScannerPanel());
            OpenProjectActionMenuCommand = new RelayCommand(parameter => OpenProjectActionMenu(parameter as ProjectDistributionProgramListItem));
            OpenProgramAddBeneficiaryPanelCommand = new RelayCommand(parameter => OpenProgramAddBeneficiaryPanel(parameter as ProjectDistributionProgramListItem), parameter => CanOpenProgramPanel(parameter as ProjectDistributionProgramListItem));
            OpenProgramScannerPanelCommand = new RelayCommand(parameter => OpenProgramScannerPanel(parameter as ProjectDistributionProgramListItem), parameter => CanOpenProgramPanel(parameter as ProjectDistributionProgramListItem));
            PrevPendingPageCommand = new RelayCommand(async _ => await ChangePendingPageAsync(-1), _ => CanMovePendingPage(-1));
            NextPendingPageCommand = new RelayCommand(async _ => await ChangePendingPageAsync(1), _ => CanMovePendingPage(1));
            PrevReleasedPageCommand = new RelayCommand(async _ => await ChangeReleasedPageAsync(-1), _ => CanMoveReleasedPage(-1));
            NextReleasedPageCommand = new RelayCommand(async _ => await ChangeReleasedPageAsync(1), _ => CanMoveReleasedPage(1));
            ConfirmReleaseCommand = new RelayCommand(async _ => await ConfirmReleaseAsync(), _ => CanConfirmRelease());

            OpenCreateProjectPanelCommand = new RelayCommand(_ => OpenCreateProjectPanel(), _ => !IsBusy);
            CloseCreateProjectPanelCommand = new RelayCommand(_ => CloseCreateProjectPanel());
            CloseCreateProjectSuccessPanelCommand = new RelayCommand(_ => CloseCreateProjectSuccessPanel());
            ConfirmCreateProjectCommand = new RelayCommand(async _ => await ConfirmCreateProjectAsync(), _ => CanConfirmCreateProject());
            ToggleBeneficiarySelectionCommand = new RelayCommand(parameter => ToggleBeneficiarySelection(parameter as DistributionBeneficiaryOption));

            MoveToSelectedCommand = new RelayCommand(parameter => MoveToSelected(parameter as DistributionBeneficiaryOption));
            MoveToAvailableCommand = new RelayCommand(parameter => MoveToAvailable(parameter as DistributionBeneficiaryOption));
            MoveAllToSelectedCommand = new RelayCommand(_ => MoveAllToSelected());
            MoveAllToAvailableCommand = new RelayCommand(_ => MoveAllToAvailable());

            ResetCreateProjectForm();
            _ = LoadAsync();
        }

        public ObservableCollection<AyudaProgramType> ProgramTypes { get; }
        public ObservableCollection<AssistanceReleaseKind> ReleaseKinds { get; }
        public ObservableCollection<AyudaProgramDistributionStatus> DistributionStatuses { get; }
        public ObservableCollection<DistributionBeneficiaryOption> AvailableUnpickedBeneficiaries { get; }
        public ObservableCollection<DistributionBeneficiaryOption> SelectedProjectBeneficiaries { get; } = new();

        public ICommand OpenCreateProjectPanelCommand { get; }
        public ICommand CloseCreateProjectPanelCommand { get; }
        public ICommand CloseCreateProjectSuccessPanelCommand { get; }
        public ICommand ConfirmCreateProjectCommand { get; }
        public ICommand ToggleBeneficiarySelectionCommand { get; }
        public ICommand MoveToSelectedCommand { get; }
        public ICommand MoveToAvailableCommand { get; }
        public ICommand MoveAllToSelectedCommand { get; }
        public ICommand MoveAllToAvailableCommand { get; }

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

        public bool IsAnyOverlayOpen => IsAddBeneficiaryPanelOpen || IsScannerPanelOpen || IsCreateProjectPanelOpen || IsCreateProjectSuccessPanelOpen;

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
            AvailableUnpickedBeneficiaries.Clear();
            SelectedProjectBeneficiaries.Clear();
            NewProjectSelectedCount = 0;
            IsCreateProjectSuccessPanelOpen = false;
        }

        private async Task LoadAvailableUnpickedBeneficiariesAsync()
        {
            await using var context = new AppDbContext();
            
            // Filter: Approved AND not in ANY active project
            var search = NewProjectSearchText?.Trim();
            
            var enrolledIds = await context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .Select(b => new { b.BeneficiaryStagingId, b.CivilRegistryId, b.BeneficiaryId })
                .ToListAsync();

            var alreadyEnrolledStagingIds = enrolledIds.Select(b => b.BeneficiaryStagingId).ToList();
            var alreadyEnrolledCivilIds = enrolledIds.Where(b => b.CivilRegistryId != null).Select(b => b.CivilRegistryId!).ToList();
            var alreadyEnrolledBenIds = enrolledIds.Where(b => b.BeneficiaryId != null).Select(b => b.BeneficiaryId!).ToList();

            // Also exclude what's already in SelectedProjectBeneficiaries in the UI
            var currentSelectedIds = SelectedProjectBeneficiaries.Select(b => b.StagingId).ToList();
            var currentSelectedCivilIds = SelectedProjectBeneficiaries.Where(b => !string.IsNullOrEmpty(b.CivilRegistryId)).Select(b => b.CivilRegistryId).ToList();
            var currentSelectedBenIds = SelectedProjectBeneficiaries.Where(b => !string.IsNullOrEmpty(b.BeneficiaryId)).Select(b => b.BeneficiaryId).ToList();

            var beneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .Where(item => item.VerificationStatus == VerificationStatus.Approved)
                .Select(item => new
                {
                    item.StagingID,
                    item.BeneficiaryId,
                    item.CivilRegistryId,
                    item.LastName,
                    item.FirstName,
                    item.MiddleName,
                    item.FullName,
                    item.LinkedHouseholdId,
                    item.LinkedHouseholdMemberId
                })
                .ToListAsync();

            // Perform filtering in memory to handle complex duplicate checks correctly
            var filteredBeneficiaries = beneficiaries
                .Where(item => !alreadyEnrolledStagingIds.Contains(item.StagingID) &&
                               !currentSelectedIds.Contains(item.StagingID))
                .Where(item => string.IsNullOrEmpty(item.CivilRegistryId) || 
                               (!alreadyEnrolledCivilIds.Contains(item.CivilRegistryId) && 
                                !currentSelectedCivilIds.Contains(item.CivilRegistryId)))
                .Where(item => string.IsNullOrEmpty(item.BeneficiaryId) || 
                               (!alreadyEnrolledBenIds.Contains(item.BeneficiaryId) && 
                                !currentSelectedBenIds.Contains(item.BeneficiaryId)))
                .Where(item => string.IsNullOrWhiteSpace(search) || 
                               (item.FullName != null && item.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                               (item.BeneficiaryId != null && item.BeneficiaryId.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.FullName ?? item.LastName)
                .ToList();

            AvailableUnpickedBeneficiaries.Clear();
            foreach (var b in filteredBeneficiaries)
            {
                AvailableUnpickedBeneficiaries.Add(new DistributionBeneficiaryOption
                {
                    StagingId = b.StagingID,
                    BeneficiaryId = b.BeneficiaryId,
                    CivilRegistryId = b.CivilRegistryId,
                    LastName = b.LastName,
                    FirstName = b.FirstName,
                    MiddleName = b.MiddleName,
                    FullName = b.FullName,
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
                await using var context = new AppDbContext();
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

                // 3. Bulk Record Claims (Story requirement: update the Digital ID ledger for each released beneficiary)
                SetNeutralStatus($"Recording {selectedIds.Count} initial claims...");
                var releaseResult = await distributionService.BulkRecordClaimsAsync(
                    programId,
                    selectedIds,
                    _currentUser.Id,
                    $"Initial bulk release for project '{NewProjectName}'.");

                if (!releaseResult.IsSuccess)
                {
                    SetErrorStatus($"Project created and enrolled, but bulk release failed: {releaseResult.Message}");
                    return;
                }

                // CloseCreateProjectPanel(); // Don't close immediately, show success panel
                await LoadProgramsAsync(programId);
                IsCreateProjectSuccessPanelOpen = true;
                SetSuccessStatus($"Project '{NewProjectName}' created and released to {selectedIds.Count} beneficiaries.");
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
        public ObservableCollection<DistributionBeneficiaryOption> AvailableBeneficiaries { get; } = new();
        public ObservableCollection<ProjectDistributionBeneficiaryListItem> ProgramBeneficiaries { get; } = new();
        public ObservableCollection<ProjectDistributionReleaseListItem> ProgramReleaseHistory { get; } = new();
        public ObservableCollection<ProjectDistributionBeneficiaryListItem> PendingBeneficiaries { get; } = new();
        public ObservableCollection<ProjectDistributionReleaseListItem> ReleasedClaims { get; } = new();
        public ObservableCollection<DistributionBeneficiaryOption> FilteredAvailableBeneficiaries { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand AddBeneficiaryCommand { get; }
        public ICommand MarkBeneficiaryPendingCommand { get; }
        public ICommand RejectBeneficiaryCommand { get; }
        public ICommand CreateDistributionScannerSessionCommand { get; }
        public ICommand OpenLivePreviewCommand { get; }
        public ICommand NextLivePreviewItemCommand { get; }
        public ICommand OpenScannerPanelCommand { get; }
        public ICommand OpenAddBeneficiaryPanelCommand { get; }
        public ICommand CloseAddBeneficiaryPanelCommand { get; }
        public ICommand ConfirmAddBeneficiaryCommand { get; }
        public ICommand SelectAllFilteredCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand CloseScannerPanelCommand { get; }
        public ICommand OpenProjectActionMenuCommand { get; }
        public ICommand OpenProgramAddBeneficiaryPanelCommand { get; }
        public ICommand OpenProgramScannerPanelCommand { get; }
        public ICommand PrevPendingPageCommand { get; }
        public ICommand NextPendingPageCommand { get; }
        public ICommand PrevReleasedPageCommand { get; }
        public ICommand NextReleasedPageCommand { get; }
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

        public ProjectDistributionProgramListItem? SelectedProgramSummary
        {
            get => _selectedProgramSummary;
            set
            {
                if (SetProperty(ref _selectedProgramSummary, value))
                {
                    SelectedProgram = value?.Program;
                }
            }
        }

        public AyudaProgram? SelectedProgram
        {
            get => _selectedProgram;
            private set
            {
                if (SetProperty(ref _selectedProgram, value))
                {
                    ResetSelectedProgramDetails(value);
                    ResetDistributionScannerSession();
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

                    if (CreateDistributionScannerSessionCommand is RelayCommand scanner)
                    {
                        scanner.RaiseCanExecuteChanged();
                    }

                    if (OpenLivePreviewCommand is RelayCommand preview)
                    {
                        preview.RaiseCanExecuteChanged();
                    }

                    if (ConfirmReleaseCommand is RelayCommand confirmRelease)
                    {
                        confirmRelease.RaiseCanExecuteChanged();
                    }

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

        public string DistributionScannerSessionUrl
        {
            get => _distributionScannerSessionUrl;
            private set => SetProperty(ref _distributionScannerSessionUrl, value);
        }

        public string DistributionScannerSessionPin
        {
            get => _distributionScannerSessionPin;
            private set => SetProperty(ref _distributionScannerSessionPin, value);
        }

        public string DistributionScannerSessionExpiresAtText
        {
            get => _distributionScannerSessionExpiresAtText;
            private set => SetProperty(ref _distributionScannerSessionExpiresAtText, value);
        }

        public BitmapSource? DistributionScannerQrImage
        {
            get => _distributionScannerQrImage;
            private set => SetProperty(ref _distributionScannerQrImage, value);
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

        public int SelectedBeneficiariesCount => AvailableBeneficiaries.Count(b => b.IsSelected);

        public string AddBeneficiarySearchText
        {
            get => _addBeneficiarySearchText;
            set
            {
                if (SetProperty(ref _addBeneficiarySearchText, value))
                {
                    ApplyAvailableBeneficiaryFilter();
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
                    }
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

                    if (CreateDistributionScannerSessionCommand is RelayCommand scanner)
                    {
                        scanner.RaiseCanExecuteChanged();
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

                    if (ConfirmReleaseCommand is RelayCommand confirmRelease)
                    {
                        confirmRelease.RaiseCanExecuteChanged();
                    }
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

        public int PendingTotalCount => ProgramBeneficiaries.Count(item => item.Status == DistributionBeneficiaryStatus.Pending);
        public int ReleasedTotalCount => ProgramReleaseHistory.Count;
        public int PendingTotalPages => Math.Max(1, (int)Math.Ceiling(PendingTotalCount / (double)DistributionPageSize));
        public int ReleasedTotalPages => Math.Max(1, (int)Math.Ceiling(ReleasedTotalCount / (double)DistributionPageSize));
        public string PendingPaginationText => $"{PendingCurrentPage} / {PendingTotalPages}";
        public string ReleasedPaginationText => $"{ReleasedCurrentPage} / {ReleasedTotalPages}";

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
            await using var context = new AppDbContext();
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

            SelectedProgramSummary = selectedProgramId.HasValue
                ? ProgramSummaries.FirstOrDefault(item => item.Id == selectedProgramId.Value)
                : SelectedProgramSummary == null
                    ? ProgramSummaries.FirstOrDefault()
                    : ProgramSummaries.FirstOrDefault(item => item.Id == SelectedProgramSummary.Id);
        }

        private async Task LoadAvailableBeneficiariesAsync()
        {
            await using var context = new AppDbContext();
            var beneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .Where(item => item.VerificationStatus == VerificationStatus.Approved)
                .Select(item => new
                {
                    item.StagingID,
                    item.FullName,
                    item.FirstName,
                    item.LastName,
                    item.BeneficiaryId,
                    item.CivilRegistryId,
                    item.IsSenior,
                    item.IsPwd
                })
                .OrderBy(item => item.FullName ?? item.LastName)
                .ThenBy(item => item.FirstName)
                .ToListAsync();

            AvailableBeneficiaries.Clear();
            foreach (var b in beneficiaries)
            {
                AvailableBeneficiaries.Add(new DistributionBeneficiaryOption
                {
                    StagingId = b.StagingID,
                    FullName = b.FullName ?? string.Empty,
                    FirstName = b.FirstName ?? string.Empty,
                    LastName = b.LastName ?? string.Empty,
                    BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = b.CivilRegistryId ?? string.Empty,
                    IsSenior = b.IsSenior,
                    IsPwd = b.IsPwd
                });
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
                await GetPendingBeneficiariesPaginatedAsync();
                await GetReleasedClaimsPaginatedAsync();
                SelectedPendingBeneficiary = null;
                RefreshLivePreview(null, null, null);
                return;
            }

            var programId = selectedProgram.Id;

            await using var context = new AppDbContext();
            _ = context.AssistanceCases;
            var memberships = await context.AyudaProjectBeneficiaries
                .AsNoTracking()
                .Where(item => item.AyudaProgramId == programId)
                .OrderBy(item => item.Status)
                .ThenBy(item => item.FullName)
                .ToListAsync();

            var legacyProjectClaims = await context.AyudaProjectClaims
                .AsNoTracking()
                .Where(item => item.AyudaProgramId == programId)
                .OrderByDescending(item => item.ClaimedAt)
                .ToListAsync();

            var distributedAmount = await context.BudgetLedgerEntries
                .AsNoTracking()
                .Where(item => item.ProgramId == programId && item.EntryType == BudgetLedgerEntryType.Release)
                .SumAsync(item => (decimal?)item.TotalAmount) ?? 0m;

            var releaseHistory = legacyProjectClaims
                .Select(ProjectDistributionReleaseListItem.FromLegacyProjectClaim)
                .OrderByDescending(item => item.ReleasedAt)
                .ThenBy(item => item.FullName)
                .ToList();
            var digitalIdsByStagingId = await context.BeneficiaryDigitalIds
                .AsNoTracking()
                .Where(item => memberships.Select(member => member.BeneficiaryStagingId).Contains(item.BeneficiaryStagingId) && item.IsActive)
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
            await GetPendingBeneficiariesPaginatedAsync();
            await GetReleasedClaimsPaginatedAsync();
            RefreshLivePreview(selectedProgram, memberships, digitalIdsByStagingId);
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
                await using var context = new AppDbContext();
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
                await using var context = new AppDbContext();
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

        private bool CanCreateDistributionScannerSession()
        {
            return !IsBusy && SelectedProgram != null;
        }

        private bool CanOpenLivePreview()
        {
            return SelectedProgram != null;
        }

        private async Task CreateDistributionScannerSessionAsync()
        {
            var selectedProgram = SelectedProgram;
            if (selectedProgram == null)
            {
                SetErrorStatus("Select a project before creating a claim scanner session.");
                return;
            }

            if (selectedProgram.DistributionStatus != AyudaProgramDistributionStatus.Open)
            {
                SetErrorStatus("Claim scanner sessions are only available for projects marked Open.");
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Preparing the claim scanner session...");

            try
            {
                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                await using var context = new AppDbContext();
                var sessionService = new ScannerSessionService(context);
                var session = await sessionService.CreateDistributionSessionAsync(
                    selectedProgram.Id,
                    _currentUser.Id,
                    TimeSpan.FromMinutes(SelectedScannerSessionDurationMinutes));
                var sessionUrl = $"{baseUrl}/scanner?session={Uri.EscapeDataString(session.SessionToken)}";

                DistributionScannerSessionUrl = sessionUrl;
                DistributionScannerSessionPin = session.Pin;
                DistributionScannerSessionExpiresAtText = $"Expires {session.ExpiresAt:MMMM dd, yyyy hh:mm tt}";
                DistributionScannerQrImage = QrCodeToolkitService.GenerateQrImage(sessionUrl, 8);
                HasScannerSession = true;

                SetSuccessStatus("Claim scanner session is ready. Scan a beneficiary Digital ID to review claim history and mark received.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to start the claim scanner session: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetDistributionScannerSession()
        {
            DistributionScannerSessionUrl = string.Empty;
            DistributionScannerSessionPin = string.Empty;
            DistributionScannerSessionExpiresAtText = string.Empty;
            DistributionScannerQrImage = null;
            HasScannerSession = false;
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
            _digitalIdsByStagingId = new Dictionary<int, BeneficiaryDigitalId>();
            PendingCurrentPage = 1;
            ReleasedCurrentPage = 1;
            SelectedPendingBeneficiary = null;
            OnPropertyChanged(nameof(HasProgramBeneficiaries));
            OnPropertyChanged(nameof(HasProgramReleaseHistory));
            OnPropertyChanged(nameof(PendingTotalCount));
            OnPropertyChanged(nameof(ReleasedTotalCount));
            OnPropertyChanged(nameof(PendingTotalPages));
            OnPropertyChanged(nameof(ReleasedTotalPages));
            OnPropertyChanged(nameof(PendingPaginationText));
            OnPropertyChanged(nameof(ReleasedPaginationText));
            ResetSelectedProgramDetails(null);
            SelectedProgramSummary = null;
            SelectedProgram = null;
        }

        private void ResetSelectedProgramDetails(AyudaProgram? program)
        {
            ProgramReleaseHistory.Clear();
            PendingBeneficiaries.Clear();
            ReleasedClaims.Clear();
            SelectedPendingBeneficiary = null;
            OnPropertyChanged(nameof(HasProgramReleaseHistory));
            OnPropertyChanged(nameof(PendingTotalCount));
            OnPropertyChanged(nameof(ReleasedTotalCount));
            OnPropertyChanged(nameof(PendingTotalPages));
            OnPropertyChanged(nameof(ReleasedTotalPages));
            OnPropertyChanged(nameof(PendingPaginationText));
            OnPropertyChanged(nameof(ReleasedPaginationText));

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
            AddBeneficiarySearchText = string.Empty;
            SelectedAvailableBeneficiary = null;
            IsAddBeneficiaryPanelOpen = true;
        }

        private void CloseAddBeneficiaryPanel()
        {
            IsAddBeneficiaryPanelOpen = false;
            AddBeneficiaryStatusMessage = string.Empty;
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
            var selectedIds = AvailableBeneficiaries.Where(b => b.IsSelected).Select(b => b.StagingId).ToList();

            if (selectedProgram == null || selectedIds.Count == 0)
            {
                return;
            }

            IsBusy = true;

            try
            {
                await using var context = new AppDbContext();
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
                && SelectedPendingBeneficiary != null
                && !string.IsNullOrWhiteSpace(SelectedPendingDigitalIdQrPayload);
        }

        private async Task ConfirmReleaseAsync()
        {
            if (!CanConfirmRelease() || SelectedProgram == null || SelectedPendingBeneficiary == null)
            {
                return;
            }

            var confirmMsg = $"Release assistance for {SelectedPendingBeneficiary.FullName}?\n\n" +
                             $"Beneficiary ID: {SelectedPendingBeneficiary.BeneficiaryId}\n" +
                             $"Project: {SelectedProgram.ProgramName}\n\n" +
                             "This action will record the claim and update the budget ledger. Proceed?";

            if (MessageBox.Show(confirmMsg, "Confirm Release", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Confirming beneficiary release...");

            try
            {
                await using var context = new AppDbContext();
                var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var result = await service.RecordClaimAsync(
                    SelectedProgram.Id,
                    SelectedPendingBeneficiary.BeneficiaryStagingId,
                    _currentUser.Id,
                    SelectedPendingDigitalIdQrPayload,
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
                    : "No claim recorded yet"
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
                IdentityKey = NormalizeIdentity(claim.CivilRegistryId, claim.BeneficiaryId, claim.FullName)
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
                IsPwd = beneficiary.IsPwd
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
}

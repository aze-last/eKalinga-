using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.ViewModels
{
    internal enum BudgetWorkspacePanel
    {
        Dashboard,
        GovernmentSync,
        Ledger,
        ProjectCreation
    }

    public sealed class BudgetRecordListItem
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public decimal? BudgetCap { get; init; }
        public string Status { get; init; } = string.Empty;
        public object OriginalItem { get; init; } = default!;
        public bool HasLinkedProject { get; init; }
        public string LinkedProjectName { get; init; } = string.Empty;
        public bool IsFundSource => Category == "Private Donation" || Category == "Government Fund" || Category == "GGMS Project";
    }

    public class EnrollmentBeneficiaryOption : ObservableObject
    {
        private bool _isSelected;
        public int StagingId { get; init; }
        public string BeneficiaryId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public sealed class BudgetViewModel : ObservableObject
    {
        private const string AllLedgerSourceFilter = "All Sources";
        private const string AllTypeFilter = "All Categories";
        private readonly User _currentUser;

        /// <summary>Fires after a project is created. Code-behind shows the "Go to Distribution?" prompt.</summary>
        internal event Action<string>? ProjectCreatedGoToDistribution;
        private readonly RelayCommand _openDashboardPanelCommand;
        private readonly RelayCommand _openGovernmentSyncPanelCommand;
        private readonly RelayCommand _openLedgerPanelCommand;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _syncGovernmentBudgetCommand;
        private readonly RelayCommand _openProjectCreationPanelCommand;
        private readonly RelayCommand _closeProjectCreationPanelCommand;
        private readonly RelayCommand _confirmCreateProjectCommand;
        private readonly RelayCommand _closePanelCommand;
        private readonly RelayCommand _closeLedgerHistoryCardCommand;
        private readonly RelayCommand _clearSelectedBudgetCommand;
        private readonly RelayCommand _browseProofCommand;
        private readonly RelayCommand _exportLedgerCommand;
        private readonly RelayCommand _navigatePreviousCommand;
        private readonly RelayCommand _navigateNextCommand;
        private readonly RelayCommand _selectAllFilteredEnrollmentCommand;
        private readonly RelayCommand _deselectAllEnrollmentCommand;
        private readonly RelayCommand _openNewDonationProjectCommand;
        private BudgetWorkspacePanel _activePanel = BudgetWorkspacePanel.Dashboard;
        private string _currentPanelTitle = "Budget Management";
        private string _currentPanelSubtitle = "Select a budget project or global cap to view and manage financial details.";
        private string _statusMessage = "Loading budget controls...";
        private Brush _statusBrush = Brushes.DimGray;
        private decimal _combinedAvailable;
        private decimal _governmentAvailable;
        private decimal _privateAvailable;
        private decimal _unrestrictedAvailable;
        private decimal _lockedAvailable;
        private decimal _releasedTotal;
        private decimal _weeklySpent;
        private decimal _monthlySpent;
        private decimal _governmentAllocated;
        private decimal _governmentSpentReference;
        private decimal _assistanceCaseBudgetCapTotal;
        private decimal _cashForWorkBudgetCapTotal;
        private string _governmentOfficeCode = "Not configured";
        private string _governmentOfficeName = "Not configured";
        private string _latestGovernmentSyncLabel = "No government sync yet.";
        private PrivateDonationDonorType _selectedDonorType = PrivateDonationDonorType.Person;
        private DonationProofType _selectedProofType = DonationProofType.Cash;
        private string _donorName = string.Empty;
        private bool _isCashDonation = true;
        private string _donationItemName = string.Empty;
        private string _donationQuantityText = string.Empty;
        private string _donationUnitOfMeasure = string.Empty;
        private string _donationAmountText = string.Empty;
        private DateTime _donationDateReceived = DateTime.Today;
        private string _donationReferenceNumber = string.Empty;
        private string _donationRemarks = string.Empty;
        private string _proofReferenceNumber = string.Empty;
        private string _proofFilePath = string.Empty;
        private string _newProjectName = string.Empty;
        private string _newProjectCode = string.Empty;
        private string _newProjectDescription = string.Empty;
        private string _newProjectUnitAmountText = string.Empty;
        private string _newProjectItemDescription = string.Empty;
        private string _newProjectItemName = string.Empty;
        private string _newProjectQuantityText = string.Empty;
        private string _newProjectUnitOfMeasure = string.Empty;
        private string _newProjectBudgetCapText = string.Empty;
        private DateTime? _newProjectStartDate = DateTime.Today;
        private DateTime? _newProjectEndDate = DateTime.Today.AddMonths(1);
        private AyudaProgramType _selectedProgramType = AyudaProgramType.GeneralPurpose;
        private AssistanceReleaseKind _selectedReleaseKind = AssistanceReleaseKind.Cash;
        private bool _isProjectCreationPanelOpen;
        private bool _isBusy;
        private bool _hasAutoSyncedGgmsProjects;
        private ICollectionView _ledgerEntriesView;
        private BudgetLedgerEntryListItem? _selectedLedgerEntry;
        private string _ledgerSearchText = string.Empty;
        private string _selectedLedgerSourceFilter = AllLedgerSourceFilter;

        private BudgetRecordListItem? _selectedBudget;
        private ICollectionView _budgetsView;
        private string _searchText = string.Empty;
        private string _selectedTypeFilter = AllTypeFilter;
        private int _currentIndex = -1;
        private decimal _selectedBudgetRemaining;
        private bool _isGlobalCapSelected;
        
        private readonly RelayCommand _unlockFundsCommand;
        private string _unlockRemarks = string.Empty;

        private bool _isNewDonationMode;
        private string _enrollmentSearchText = string.Empty;
        private int _selectedEnrollmentCount;
        private string _enrollmentResultSummary = string.Empty;
        /// <summary>Selection survives searches; the display list is capped so the 40k-row registry never lands in the UI.</summary>
        private readonly HashSet<int> _selectedEnrollmentStagingIds = new();
        private int _enrollmentSearchVersion;
        private const int EnrollmentDisplayLimit = 200;
        private int _currentEnrollmentPage = 1;
        private int _totalEnrollmentPages = 1;
        private readonly RelayCommand _previousEnrollmentPageCommand;
        private readonly RelayCommand _nextEnrollmentPageCommand;

        private int _currentLedgerPage = 1;
        private int _totalLedgerPages = 1;
        private int _totalLedgerEntries = 0;
        private const int LedgerPageSize = 25;

        public int CurrentLedgerPage
        {
            get => _currentLedgerPage;
            private set => SetProperty(ref _currentLedgerPage, value);
        }

        public int TotalLedgerPages
        {
            get => _totalLedgerPages;
            private set => SetProperty(ref _totalLedgerPages, value);
        }

        public int TotalLedgerEntries
        {
            get => _totalLedgerEntries;
            private set => SetProperty(ref _totalLedgerEntries, value);
        }

        private readonly RelayCommand _nextLedgerPageCommand;
        private readonly RelayCommand _previousLedgerPageCommand;

        public ICommand NextLedgerPageCommand => _nextLedgerPageCommand;
        public ICommand PreviousLedgerPageCommand => _previousLedgerPageCommand;

        public ICommand OpenProjectCreationPanelCommand => _openProjectCreationPanelCommand;
        public ICommand CloseProjectCreationPanelCommand => _closeProjectCreationPanelCommand;
        public ICommand ConfirmCreateProjectCommand => _confirmCreateProjectCommand;

        public ObservableCollection<AyudaProgramType> ProgramTypes { get; } = new(Enum.GetValues<AyudaProgramType>());
        public ObservableCollection<AssistanceReleaseKind> ReleaseKinds { get; } = new(Enum.GetValues<AssistanceReleaseKind>());

        public int? NewProjectSourceDonationId { get; set; }
        public int? NewProjectSourceGGMSBudgetId { get; set; }
        /// <summary>GGMS project_details_id (e.g. OPP-2026-0006) when creating from a mirrored GGMS project.</summary>
        public string? NewProjectSourceProjectDetailsId { get; set; }
        /// <summary>Spending envelope of the selected GGMS project; caps the new project's budget.</summary>
        public decimal? NewProjectSourceProjectBudget { get; set; }

        private string _newProjectSourceDescription = string.Empty;
        public string NewProjectSourceDescription
        {
            get => _newProjectSourceDescription;
            set => SetProperty(ref _newProjectSourceDescription, value);
        }

        public string NewProjectName
        {
            get => _newProjectName;
            set { if (SetProperty(ref _newProjectName, value)) _confirmCreateProjectCommand.RaiseCanExecuteChanged(); }
        }

        public string NewProjectCode
        {
            get => _newProjectCode;
            set { if (SetProperty(ref _newProjectCode, value)) _confirmCreateProjectCommand.RaiseCanExecuteChanged(); }
        }

        public string NewProjectDescription
        {
            get => _newProjectDescription;
            set => SetProperty(ref _newProjectDescription, value);
        }

        public string NewProjectUnitAmountText
        {
            get => _newProjectUnitAmountText;
            set { if (SetProperty(ref _newProjectUnitAmountText, value)) _confirmCreateProjectCommand.RaiseCanExecuteChanged(); }
        }

        public string NewProjectItemDescription
        {
            get => _newProjectItemDescription;
            set => SetProperty(ref _newProjectItemDescription, value);
        }

        public string NewProjectItemName
        {
            get => _newProjectItemName;
            set
            {
                SetProperty(ref _newProjectItemName, value);
                ((RelayCommand)_confirmCreateProjectCommand).RaiseCanExecuteChanged();
            }
        }

        public string NewProjectQuantityText
        {
            get => _newProjectQuantityText;
            set
            {
                SetProperty(ref _newProjectQuantityText, value);
                ((RelayCommand)_confirmCreateProjectCommand).RaiseCanExecuteChanged();
            }
        }

        public string NewProjectUnitOfMeasure
        {
            get => _newProjectUnitOfMeasure;
            set
            {
                SetProperty(ref _newProjectUnitOfMeasure, value);
                ((RelayCommand)_confirmCreateProjectCommand).RaiseCanExecuteChanged();
            }
        }

        public string NewProjectBudgetCapText
        {
            get => _newProjectBudgetCapText;
            set => SetProperty(ref _newProjectBudgetCapText, value);
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

        public AyudaProgramType SelectedProgramType
        {
            get => _selectedProgramType;
            set => SetProperty(ref _selectedProgramType, value);
        }

        public AssistanceReleaseKind SelectedReleaseKind
        {
            get => _selectedReleaseKind;
            set
            {
                if (SetProperty(ref _selectedReleaseKind, value))
                {
                    OnPropertyChanged(nameof(IsGoodsReleaseKind));
                    OnPropertyChanged(nameof(IsCashReleaseKind));
                    ((RelayCommand)_confirmCreateProjectCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsGoodsReleaseKind => _selectedReleaseKind == AssistanceReleaseKind.Goods;
        public bool IsCashReleaseKind => _selectedReleaseKind == AssistanceReleaseKind.Cash;

        public bool IsProjectCreationPanelOpen
        {
            get => _isProjectCreationPanelOpen;
            private set
            {
                if (SetProperty(ref _isProjectCreationPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                    OnPropertyChanged(nameof(ProjectCreationPanelVisibility));
                }
            }
        }

        public Visibility ProjectCreationPanelVisibility => IsProjectCreationPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        private void OpenProjectCreationPanel(object sourceObj)
        {
            if (IsBusy) return;

            if (sourceObj == null)
            {
                SetErrorStatus("Select a Private Donation or GGMS Fund row from the list below, then click CREATE PROJECT.");
                return;
            }

            ResetProjectCreationForm();
            IsNewDonationMode = false;

            if (sourceObj is BudgetRecordListItem item)
            {
                if (item.OriginalItem is PrivateDonation donation)
                {
                    NewProjectSourceDonationId = donation.Id;
                    NewProjectSourceGGMSBudgetId = null;
                    NewProjectSourceProjectDetailsId = null;
                    NewProjectSourceProjectBudget = null;
                    NewProjectSourceDescription = $"Source: Private Donation - {donation.DonorName} (PHP {donation.Amount:N2})";
                }
                else if (item.OriginalItem is GovernmentBudgetSnapshot ggms)
                {
                    NewProjectSourceGGMSBudgetId = ggms.Id;
                    NewProjectSourceDonationId = null;
                    NewProjectSourceProjectDetailsId = null;
                    NewProjectSourceProjectBudget = null;
                    NewProjectSourceDescription = $"Source: GGMS Allocation - {ggms.OfficeName} (PHP {ggms.AllocatedAmount:N2})";
                }
                else if (item.OriginalItem is GgmsProjectCache ggmsProject)
                {
                    if (ggmsProject.IsLinked)
                    {
                        SetErrorStatus($"GGMS project '{ggmsProject.ProjectDetailsId}' is already linked to a local project.");
                        return;
                    }

                    if (string.Equals(ggmsProject.Status, "archived", StringComparison.OrdinalIgnoreCase))
                    {
                        SetErrorStatus($"GGMS project '{ggmsProject.ProjectDetailsId}' is archived on GGMS and can no longer be linked.");
                        return;
                    }

                    NewProjectSourceProjectDetailsId = ggmsProject.ProjectDetailsId;
                    NewProjectSourceProjectBudget = ggmsProject.TotalBudget;
                    NewProjectSourceDonationId = null;
                    NewProjectSourceGGMSBudgetId = null;
                    NewProjectName = ggmsProject.ProjectName;
                    NewProjectDescription = ggmsProject.Description ?? string.Empty;
                    NewProjectSourceDescription = $"Source: GGMS Project {ggmsProject.ProjectDetailsId} - {ggmsProject.ProjectName} (PHP {ggmsProject.TotalBudget:N2})";
                }
                else
                {
                    SetErrorStatus("Select a Private Donation, GGMS Fund, or GGMS Project row to create a project.");
                    return;
                }
            }
            else
            {
                SetErrorStatus("Select a Private Donation, GGMS Fund, or GGMS Project row to create a project.");
                return;
            }

            SetActivePanel(BudgetWorkspacePanel.ProjectCreation);
            IsProjectCreationPanelOpen = true;
            _ = LoadEnrollmentBeneficiariesAsync();
        }

        private void OpenNewDonationProjectPanel()
        {
            if (IsBusy) return;

            ResetProjectCreationForm();
            ResetDonationForm();
            IsNewDonationMode = true;
            NewProjectSourceDonationId = null;
            NewProjectSourceGGMSBudgetId = null;
            NewProjectSourceProjectDetailsId = null;
            NewProjectSourceProjectBudget = null;
            NewProjectSourceDescription = "Source: New Private Donation (recorded on confirm)";

            SetActivePanel(BudgetWorkspacePanel.ProjectCreation);
            IsProjectCreationPanelOpen = true;
            _ = LoadEnrollmentBeneficiariesAsync();
        }

        private void CloseProjectCreationPanel()
        {
            IsProjectCreationPanelOpen = false;
            IsNewDonationMode = false;
            ClearEnrollmentSelection();
            SetActivePanel(BudgetWorkspacePanel.Dashboard);
        }


        private bool CanConfirmCreateProject()
        {
            if (string.IsNullOrWhiteSpace(NewProjectName) || string.IsNullOrWhiteSpace(NewProjectCode))
                return false;

            if (IsNewDonationMode)
            {
                if (string.IsNullOrWhiteSpace(DonorName))
                    return false;

                if (IsCashDonation)
                {
                    if (!TryParseAmount(DonationAmountText, out _))
                        return false;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(DonationItemName) ||
                        string.IsNullOrWhiteSpace(DonationUnitOfMeasure) ||
                        !TryParseAmount(DonationQuantityText, out _))
                        return false;
                }
            }

            if (SelectedReleaseKind == AssistanceReleaseKind.Goods)
            {
                return !string.IsNullOrWhiteSpace(NewProjectItemName) &&
                       !string.IsNullOrWhiteSpace(NewProjectUnitOfMeasure) &&
                       TryParseAmount(NewProjectQuantityText, out _);
            }

            return TryParseAmount(NewProjectUnitAmountText, out _);
        }

        private async Task ConfirmCreateProjectAsync()
        {
            if (IsBusy) return;

            decimal? unitAmount = null;
            decimal? quantity = null;

            if (SelectedReleaseKind == AssistanceReleaseKind.Goods)
            {
                if (!TryParseAmount(NewProjectQuantityText, out var qty) || string.IsNullOrWhiteSpace(NewProjectItemName))
                {
                    SetErrorStatus("Enter valid item details and quantity.");
                    return;
                }
                quantity = qty;
            }
            else
            {
                if (!TryParseAmount(NewProjectUnitAmountText, out var amt))
                {
                    SetErrorStatus("Enter a valid unit amount.");
                    return;
                }
                unitAmount = amt;
            }

            TryParseOptionalAmount(NewProjectBudgetCapText, out var budgetCap);

            if (!IsNewDonationMode
                && !NewProjectSourceDonationId.HasValue
                && !NewProjectSourceGGMSBudgetId.HasValue
                && string.IsNullOrWhiteSpace(NewProjectSourceProjectDetailsId))
            {
                SetErrorStatus("A source fund must be selected to create a project.");
                return;
            }

            // A GGMS project is its own spending envelope: resolve the cap here so the remote
            // (Hostinger) and local writes both persist the same value.
            if (!string.IsNullOrWhiteSpace(NewProjectSourceProjectDetailsId) && NewProjectSourceProjectBudget.HasValue)
            {
                if (budgetCap.HasValue && budgetCap.Value > NewProjectSourceProjectBudget.Value)
                {
                    SetErrorStatus($"Budget cap cannot exceed the GGMS project budget of PHP {NewProjectSourceProjectBudget.Value:N2}.");
                    return;
                }
                budgetCap ??= NewProjectSourceProjectBudget.Value;
            }

            IsBusy = true;

            try
            {
                await using var context = new LocalDbContext();
                var budgetService = new BudgetManagementService(context);

                if (IsNewDonationMode)
                {
                    SetNeutralStatus("Recording donation and creating project...");

                    var donationResult = await RecordDonationCoreAsync(budgetService);
                    if (donationResult == null)
                    {
                        return; // validation error already surfaced
                    }

                    if (!donationResult.IsSuccess || !donationResult.DonationId.HasValue)
                    {
                        SetErrorStatus(donationResult.Message);
                        return;
                    }

                    NewProjectSourceDonationId = donationResult.DonationId.Value;
                    NewProjectSourceGGMSBudgetId = null;
                }
                else
                {
                    SetNeutralStatus("Creating project linked to source fund...");
                }

                var result = await budgetService.CreateProgramAsync(
                    new AyudaProgramRequest(
                        NewProjectCode,
                        NewProjectName,
                        SelectedProgramType,
                        NormalizeNullable(NewProjectDescription),
                        NormalizeNullable(NewProjectName), // Using name as assistance type for now
                        SelectedReleaseKind,
                        unitAmount,
                        NormalizeNullable(NewProjectItemDescription),
                        NewProjectItemName,
                        quantity,
                        NewProjectUnitOfMeasure,
                        NewProjectStartDate,
                        NewProjectEndDate,
                        budgetCap,
                        AyudaProgramDistributionStatus.Draft,
                        NewProjectSourceDonationId,
                        NewProjectSourceGGMSBudgetId,
                        NormalizeNullable(NewProjectSourceProjectDetailsId)),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(IsNewDonationMode
                        ? $"Donation was recorded, but project creation failed: {result.Message}"
                        : result.Message);
                    return;
                }

                var createdName = NewProjectName;
                var enrollmentMessage = string.Empty;

                // Selection lives in the id set (not the visible page) so search-narrowed
                // and SELECT ALL FILTERED picks are all enrolled.
                var selectedIds = _selectedEnrollmentStagingIds.ToList();

                if (selectedIds.Count > 0 && result.ProgramId.HasValue)
                {
                    SetNeutralStatus("Enrolling selected beneficiaries...");
                    try
                    {
                        await using var enrollContext = new LocalDbContext();
                        var distributionService = new ProjectDistributionService(enrollContext);
                        var enrollResult = await distributionService.BulkAddBeneficiariesAsync(
                            result.ProgramId.Value,
                            selectedIds,
                            _currentUser.Id);

                        enrollmentMessage = enrollResult.IsSuccess
                            ? $" {selectedIds.Count} beneficiar{(selectedIds.Count == 1 ? "y" : "ies")} enrolled."
                            : $" Beneficiary enrollment failed ({enrollResult.Message}) — use ADD BENEFICIARIES in the Distribution module.";
                    }
                    catch (Exception enrollEx)
                    {
                        enrollmentMessage = $" Beneficiary enrollment failed ({enrollEx.Message}) — use ADD BENEFICIARIES in the Distribution module.";
                    }
                }

                CloseProjectCreationPanel();
                await LoadAsync();
                SetSuccessStatus($"Project '{createdName}' created successfully.{enrollmentMessage}");
                ProjectCreatedGoToDistribution?.Invoke(createdName);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to create project: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Validates and records the donation entered in the combined modal. Returns null when input validation failed (status already set).</summary>
        private async Task<PrivateDonationOperationResult?> RecordDonationCoreAsync(BudgetManagementService budgetService)
        {
            decimal amount = 0m;
            string? itemName = null;
            decimal? quantity = null;
            string? unitOfMeasure = null;
            DonationType donationType = DonationType.Cash;

            if (IsCashDonation)
            {
                if (!TryParseAmount(DonationAmountText, out amount))
                {
                    SetErrorStatus("Enter a valid donation amount greater than zero.");
                    return null;
                }
            }
            else
            {
                donationType = DonationType.Goods;
                itemName = NormalizeNullable(DonationItemName);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    SetErrorStatus("Enter the item name for the goods donation.");
                    return null;
                }

                if (!TryParseAmount(DonationQuantityText, out var qty))
                {
                    SetErrorStatus("Enter a valid quantity greater than zero.");
                    return null;
                }
                quantity = qty;

                unitOfMeasure = NormalizeNullable(DonationUnitOfMeasure);
                if (string.IsNullOrWhiteSpace(unitOfMeasure))
                {
                    SetErrorStatus("Enter the unit of measure (e.g. Sacks, Boxes).");
                    return null;
                }
            }

            return await budgetService.RecordPrivateDonationAsync(
                new PrivateDonationRequest(
                    SelectedDonorType,
                    DonorName,
                    donationType,
                    amount,
                    itemName,
                    quantity,
                    unitOfMeasure,
                    DonationDateReceived,
                    NormalizeNullable(DonationReferenceNumber),
                    NormalizeNullable(DonationRemarks),
                    SelectedProofType,
                    NormalizeNullable(ProofReferenceNumber),
                    NormalizeNullable(ProofFilePath)),
                _currentUser.Id);
        }

        private void ResetProjectCreationForm()
        {
            NewProjectName = string.Empty;
            NewProjectCode = string.Empty;
            NewProjectDescription = string.Empty;
            NewProjectUnitAmountText = string.Empty;
            NewProjectItemDescription = string.Empty;
            NewProjectBudgetCapText = string.Empty;
            NewProjectStartDate = DateTime.Today;
            NewProjectEndDate = DateTime.Today.AddMonths(1);
            SelectedProgramType = AyudaProgramType.GeneralPurpose;
            SelectedReleaseKind = AssistanceReleaseKind.Cash;
        }

        public BudgetViewModel(User currentUser)
        {
            _currentUser = currentUser;
            DonorTypes = new ObservableCollection<PrivateDonationDonorType>(Enum.GetValues<PrivateDonationDonorType>());
            ProofTypes = new ObservableCollection<DonationProofType>(Enum.GetValues<DonationProofType>());
            Donations = new ObservableCollection<PrivateDonation>();
            LedgerEntries = new ObservableCollection<BudgetLedgerEntryListItem>();
            LedgerSourceFilters = new ObservableCollection<string> { AllLedgerSourceFilter };
            _ledgerEntriesView = CollectionViewSource.GetDefaultView(LedgerEntries);

            AllBudgets = new ObservableCollection<BudgetRecordListItem>();
            TypeFilters = new ObservableCollection<string> { AllTypeFilter, "Global Aid Cap", "Global CFW Cap", "Private Donation", "Government Fund", "GGMS Project" };
            _budgetsView = CollectionViewSource.GetDefaultView(AllBudgets);
            _budgetsView.Filter = FilterBudgetRecord;

            _openDashboardPanelCommand = new RelayCommand(_ => ClosePanel(), _ => !IsBusy && _activePanel != BudgetWorkspacePanel.Dashboard);
            _openGovernmentSyncPanelCommand = new RelayCommand(_ => OpenLedgerPanel(), _ => !IsBusy && _activePanel != BudgetWorkspacePanel.Ledger);
            _openLedgerPanelCommand = new RelayCommand(_ => OpenLedgerPanel(), _ => !IsBusy && _activePanel != BudgetWorkspacePanel.Ledger);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _syncGovernmentBudgetCommand = new RelayCommand(async _ => await SyncGovernmentBudgetAsync(), _ => !IsBusy);
            _closePanelCommand = new RelayCommand(_ => ClosePanel(), _ => !IsBusy);
            _closeLedgerHistoryCardCommand = new RelayCommand(_ => CloseLedgerHistoryCard(), _ => SelectedLedgerEntry != null);
            _clearSelectedBudgetCommand = new RelayCommand(_ =>
            {
                SearchText = string.Empty;
                SelectedBudget = null;
                ClosePanel();
                _budgetsView?.Refresh();
            }, _ => SelectedBudget != null);
            _browseProofCommand = new RelayCommand(_ => BrowseProof());
            _exportLedgerCommand = new RelayCommand(async _ => await ExportLedgerAsync(), _ => !IsBusy && LedgerEntries.Any());
            
            _unlockFundsCommand = new RelayCommand(async _ => await UnlockFundsAsync(), _ => !IsBusy && SelectedBudget != null && SelectedBudgetRemaining > 0);

            _navigatePreviousCommand = new RelayCommand(_ => NavigatePrevious(), _ => CanNavigatePrevious());
            _navigateNextCommand = new RelayCommand(_ => NavigateNext(), _ => CanNavigateNext());

            _openProjectCreationPanelCommand = new RelayCommand(source => OpenProjectCreationPanel(source), _ => !IsBusy);
            _openNewDonationProjectCommand = new RelayCommand(_ => OpenNewDonationProjectPanel(), _ => !IsBusy && !IsProjectCreationPanelOpen);
            _closeProjectCreationPanelCommand = new RelayCommand(_ => CloseProjectCreationPanel());
            _confirmCreateProjectCommand = new RelayCommand(async _ => await ConfirmCreateProjectAsync(), _ => !IsBusy && CanConfirmCreateProject());

            _selectAllFilteredEnrollmentCommand = new RelayCommand(async _ => await SelectAllFilteredEnrollmentAsync());
            _deselectAllEnrollmentCommand = new RelayCommand(_ => DeselectAllEnrollment());
            _previousEnrollmentPageCommand = new RelayCommand(
                _ => { CurrentEnrollmentPage--; _ = QueryEnrollmentBeneficiariesAsync(); },
                _ => CurrentEnrollmentPage > 1);
            _nextEnrollmentPageCommand = new RelayCommand(
                _ => { CurrentEnrollmentPage++; _ = QueryEnrollmentBeneficiariesAsync(); },
                _ => CurrentEnrollmentPage < TotalEnrollmentPages);

            _nextLedgerPageCommand = new RelayCommand(async _ => await NextLedgerPageAsync(), _ => !IsBusy && CurrentLedgerPage < TotalLedgerPages);
            _previousLedgerPageCommand = new RelayCommand(async _ => await PreviousLedgerPageAsync(), _ => !IsBusy && CurrentLedgerPage > 1);

            _ = LoadAsync();
        }

        public ObservableCollection<PrivateDonationDonorType> DonorTypes { get; }
        public ObservableCollection<DonationProofType> ProofTypes { get; }
        public ObservableCollection<PrivateDonation> Donations { get; }
        public ObservableCollection<BudgetLedgerEntryListItem> LedgerEntries { get; }
        public ObservableCollection<string> LedgerSourceFilters { get; }

        public ObservableCollection<BudgetRecordListItem> AllBudgets { get; }
        public ObservableCollection<string> TypeFilters { get; }

        public ICommand OpenDashboardPanelCommand => _openDashboardPanelCommand;
        public ICommand OpenGovernmentSyncPanelCommand => _openGovernmentSyncPanelCommand;
        public ICommand OpenLedgerPanelCommand => _openLedgerPanelCommand;
        public ICommand RefreshCommand => _refreshCommand;
        public ICommand SyncGovernmentBudgetCommand => _syncGovernmentBudgetCommand;
        public ICommand OpenNewDonationProjectCommand => _openNewDonationProjectCommand;
        public ICommand ClosePanelCommand => _closePanelCommand;
        public ICommand CloseLedgerHistoryCardCommand => _closeLedgerHistoryCardCommand;
        public ICommand ClearSelectedBudgetCommand => _clearSelectedBudgetCommand;
        public ICommand CloseHistoryDetailCommand => _closeLedgerHistoryCardCommand;
        public ICommand BrowseProofCommand => _browseProofCommand;
        public ICommand ExportLedgerCommand => _exportLedgerCommand;
        public ICommand UnlockFundsCommand => _unlockFundsCommand;
        public ICommand SelectAllFilteredEnrollmentCommand => _selectAllFilteredEnrollmentCommand;
        public ICommand DeselectAllEnrollmentCommand => _deselectAllEnrollmentCommand;
        public ICommand PreviousEnrollmentPageCommand => _previousEnrollmentPageCommand;
        public ICommand NextEnrollmentPageCommand => _nextEnrollmentPageCommand;

        public ICommand NavigatePreviousCommand => _navigatePreviousCommand;
        public ICommand NavigateNextCommand => _navigateNextCommand;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _openDashboardPanelCommand.RaiseCanExecuteChanged();
                    _openGovernmentSyncPanelCommand.RaiseCanExecuteChanged();
                    _openLedgerPanelCommand.RaiseCanExecuteChanged();
                    _refreshCommand.RaiseCanExecuteChanged();
                    _syncGovernmentBudgetCommand.RaiseCanExecuteChanged();
                    _openProjectCreationPanelCommand.RaiseCanExecuteChanged();
                    _openNewDonationProjectCommand.RaiseCanExecuteChanged();
                    _confirmCreateProjectCommand.RaiseCanExecuteChanged();
                    _closePanelCommand.RaiseCanExecuteChanged();
                    _exportLedgerCommand.RaiseCanExecuteChanged();
                    _unlockFundsCommand.RaiseCanExecuteChanged();
                    _navigatePreviousCommand.RaiseCanExecuteChanged();
                    _navigateNextCommand.RaiseCanExecuteChanged();
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

        public decimal CombinedAvailable
        {
            get => _combinedAvailable;
            private set => SetProperty(ref _combinedAvailable, value);
        }

        public decimal WeeklySpent
        {
            get => _weeklySpent;
            private set => SetProperty(ref _weeklySpent, value);
        }

        public decimal MonthlySpent
        {
            get => _monthlySpent;
            private set => SetProperty(ref _monthlySpent, value);
        }

        public decimal GovernmentAvailable
        {
            get => _governmentAvailable;
            private set => SetProperty(ref _governmentAvailable, value);
        }

        public decimal PrivateAvailable
        {
            get => _privateAvailable;
            private set => SetProperty(ref _privateAvailable, value);
        }

        public decimal UnrestrictedAvailable
        {
            get => _unrestrictedAvailable;
            private set => SetProperty(ref _unrestrictedAvailable, value);
        }

        public decimal LockedAvailable
        {
            get => _lockedAvailable;
            private set => SetProperty(ref _lockedAvailable, value);
        }

        public decimal ReleasedTotal
        {
            get => _releasedTotal;
            private set => SetProperty(ref _releasedTotal, value);
        }

        public decimal GovernmentAllocated
        {
            get => _governmentAllocated;
            private set => SetProperty(ref _governmentAllocated, value);
        }

        public decimal GovernmentSpentReference
        {
            get => _governmentSpentReference;
            private set => SetProperty(ref _governmentSpentReference, value);
        }

        public decimal AssistanceCaseBudgetCapTotal
        {
            get => _assistanceCaseBudgetCapTotal;
            private set => SetProperty(ref _assistanceCaseBudgetCapTotal, value);
        }

        public decimal CashForWorkBudgetCapTotal
        {
            get => _cashForWorkBudgetCapTotal;
            private set => SetProperty(ref _cashForWorkBudgetCapTotal, value);
        }

        public string GovernmentOfficeCode
        {
            get => _governmentOfficeCode;
            private set => SetProperty(ref _governmentOfficeCode, value);
        }

        public string GovernmentOfficeName
        {
            get => _governmentOfficeName;
            private set => SetProperty(ref _governmentOfficeName, value);
        }

        public string LatestGovernmentSyncLabel
        {
            get => _latestGovernmentSyncLabel;
            private set => SetProperty(ref _latestGovernmentSyncLabel, value);
        }

        public string CurrentPanelTitle
        {
            get => _currentPanelTitle;
            private set => SetProperty(ref _currentPanelTitle, value);
        }

        public string CurrentPanelSubtitle
        {
            get => _currentPanelSubtitle;
            private set => SetProperty(ref _currentPanelSubtitle, value);
        }

        public Visibility DashboardVisibility => GetPanelVisibility(BudgetWorkspacePanel.Dashboard);

        public Visibility GovernmentSyncVisibility => GetPanelVisibility(BudgetWorkspacePanel.GovernmentSync);

        public Visibility LedgerVisibility => GetPanelVisibility(BudgetWorkspacePanel.Ledger);

        public Visibility BackToDashboardVisibility => _activePanel == BudgetWorkspacePanel.Ledger
            ? Visibility.Collapsed
            : Visibility.Visible;

        public Visibility HistoryDetailVisibility => _activePanel == BudgetWorkspacePanel.Ledger && SelectedLedgerEntry != null
            ? Visibility.Visible
            : Visibility.Collapsed;

        public ICollectionView LedgerEntriesView
        {
            get => _ledgerEntriesView;
            private set => SetProperty(ref _ledgerEntriesView, value);
        }

        public BudgetLedgerEntryListItem? SelectedLedgerEntry
        {
            get => _selectedLedgerEntry;
            set
            {
                if (SetProperty(ref _selectedLedgerEntry, value))
                {
                    OnPropertyChanged(nameof(IsLedgerHistoryCardOpen));
                    OnPropertyChanged(nameof(HistoryDetailVisibility));
                    _closeLedgerHistoryCardCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLedgerHistoryCardOpen => SelectedLedgerEntry != null;

        public string LedgerSearchText
        {
            get => _ledgerSearchText;
            set
            {
                if (SetProperty(ref _ledgerSearchText, value))
                {
                    RefreshLedgerFilters();
                }
            }
        }

        public string SelectedLedgerSourceFilter
        {
            get => _selectedLedgerSourceFilter;
            set
            {
                if (SetProperty(ref _selectedLedgerSourceFilter, value))
                {
                    RefreshLedgerFilters();
                }
            }
        }

        public BudgetRecordListItem? SelectedBudget
        {
            get => _selectedBudget;
            set
            {
                if (SetProperty(ref _selectedBudget, value))
                {
                    SyncWithSelectedBudget();
                    UpdateNavigationState();
                    OnPropertyChanged(nameof(EmptyStateVisibility));
                    OnPropertyChanged(nameof(DetailVisibility));
                    OnPropertyChanged(nameof(SearchText));
                }
            }
        }

        public ICollectionView BudgetsView => _budgetsView;

        public Visibility EmptyStateVisibility => SelectedBudget == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DetailVisibility => SelectedBudget != null ? Visibility.Visible : Visibility.Collapsed;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _budgetsView.Refresh();
                }
            }
        }

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    _budgetsView.Refresh();
                }
            }
        }

        public string CurrentPosition
        {
            get
            {
                var viewList = _budgetsView.Cast<BudgetRecordListItem>().ToList();
                if (SelectedBudget == null || !viewList.Contains(SelectedBudget))
                {
                    return "0 / 0";
                }
                return $"{viewList.IndexOf(SelectedBudget) + 1} / {viewList.Count}";
            }
        }

        public decimal SelectedBudgetRemaining
        {
            get => _selectedBudgetRemaining;
            private set => SetProperty(ref _selectedBudgetRemaining, value);
        }

        public bool IsGlobalCapSelected
        {
            get => _isGlobalCapSelected;
            private set => SetProperty(ref _isGlobalCapSelected, value);
        }

        public string UnlockRemarks
        {
            get => _unlockRemarks;
            set => SetProperty(ref _unlockRemarks, value);
        }

        public bool IsAnyOverlayOpen => _activePanel == BudgetWorkspacePanel.Ledger || IsProjectCreationPanelOpen;

        private async void SyncWithSelectedBudget()
        {
            if (SelectedBudget == null)
            {
                _currentIndex = -1;
                IsGlobalCapSelected = false;
                return;
            }

            var viewList = _budgetsView.Cast<BudgetRecordListItem>().ToList();
            _currentIndex = viewList.IndexOf(SelectedBudget);
            IsGlobalCapSelected = SelectedBudget.Category is "Global Aid Cap" or "Global CFW Cap";

            // Calculate Remaining
            try
            {
                await using var context = new LocalDbContext();
                decimal spend;

                if (SelectedBudget.Category == "GGMS Project" && SelectedBudget.OriginalItem is GgmsProjectCache ggmsProject)
                {
                    // Releases attributed to this GGMS envelope via the linked local program.
                    var linkedProgramIds = await context.AyudaPrograms
                        .AsNoTracking()
                        .Where(p => p.SourceProjectDetailsId == ggmsProject.ProjectDetailsId)
                        .Select(p => p.Id)
                        .ToListAsync();

                    spend = linkedProgramIds.Count == 0
                        ? 0m
                        : await context.BudgetLedgerEntries
                            .AsNoTracking()
                            .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                            entry.ProgramId != null &&
                                            linkedProgramIds.Contains(entry.ProgramId.Value))
                            .SumAsync(entry => (decimal?)entry.TotalAmount) ?? 0m;
                }
                else
                {
                    spend = await context.BudgetLedgerEntries
                        .AsNoTracking()
                        .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                       (entry.AssistanceCaseBudgetId == SelectedBudget.Id && SelectedBudget.Category == "Global Aid Cap" ||
                                        entry.CashForWorkBudgetId == SelectedBudget.Id && SelectedBudget.Category == "Global CFW Cap"))
                        .SumAsync(entry => (decimal?)entry.TotalAmount) ?? 0m;
                }

                SelectedBudgetRemaining = (SelectedBudget.BudgetCap ?? 0m) - spend;
            }
            catch
            {
                SelectedBudgetRemaining = 0m;
            }

            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(DetailVisibility));
            _unlockFundsCommand.RaiseCanExecuteChanged();
        }

        private void UpdateNavigationState()
        {
            _navigatePreviousCommand.RaiseCanExecuteChanged();
            _navigateNextCommand.RaiseCanExecuteChanged();
            _clearSelectedBudgetCommand.RaiseCanExecuteChanged();
        }

        private bool CanNavigatePrevious() => _currentIndex > 0;

        private void NavigatePrevious()
        {
            var viewList = _budgetsView.Cast<BudgetRecordListItem>().ToList();
            if (_currentIndex > 0)
            {
                SelectedBudget = viewList[_currentIndex - 1];
            }
        }

        private bool CanNavigateNext()
        {
            var viewList = _budgetsView.Cast<BudgetRecordListItem>().ToList();
            return _currentIndex >= 0 && _currentIndex < viewList.Count - 1;
        }

        private void NavigateNext()
        {
            var viewList = _budgetsView.Cast<BudgetRecordListItem>().ToList();
            if (_currentIndex >= 0 && _currentIndex < viewList.Count - 1)
            {
                SelectedBudget = viewList[_currentIndex + 1];
            }
        }

        private bool FilterBudgetRecord(object item)
        {
            if (item is not BudgetRecordListItem record) return false;

            if (!string.Equals(SelectedTypeFilter, AllTypeFilter) && !string.Equals(record.Category, SelectedTypeFilter))
                return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var search = SearchText.Trim();
            return record.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   record.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   record.Category.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadBudgetsViewAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            
            var acBudgets = await budgetService.GetAssistanceCaseBudgetsAsync();
            var cfwBudgets = await budgetService.GetCashForWorkBudgetsAsync();

            var currentSelectedId = SelectedBudget?.Id;
            var currentCategory = SelectedBudget?.Category;

            AllBudgets.Clear();

            foreach (var b in acBudgets)
            {
                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = b.BudgetCode,
                    Name = b.BudgetName,
                    Category = "Global Aid Cap",
                    BudgetCap = b.BudgetCap,
                    Status = b.IsActive ? "Active" : "Inactive",
                    OriginalItem = b
                });
            }

            foreach (var b in cfwBudgets)
            {
                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = b.BudgetCode,
                    Name = b.BudgetName,
                    Category = "Global CFW Cap",
                    BudgetCap = b.BudgetCap,
                    Status = b.IsActive ? "Active" : "Inactive",
                    OriginalItem = b
                });
            }

            var donations = await budgetService.GetPrivateDonationsAsync();
            await using var ggmsContext = new LocalDbContext();
            var allProjects = await ggmsContext.AyudaPrograms.AsNoTracking().ToListAsync();

            foreach (var b in donations)
            {
                var linkedProject = allProjects.FirstOrDefault(p => p.SourceDonationId == b.Id);
                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = $"DON-{b.Id}",
                    Name = b.DonorName,
                    Category = "Private Donation",
                    BudgetCap = b.Amount,
                    Status = "Active",
                    OriginalItem = b,
                    HasLinkedProject = linkedProject != null,
                    LinkedProjectName = linkedProject?.ProgramName ?? string.Empty
                });
            }

            var snapshots = await ggmsContext.GovernmentBudgetSnapshots.AsNoTracking().ToListAsync();
            foreach (var b in snapshots)
            {
                var linkedProject = allProjects.FirstOrDefault(p => p.SourceGGMSBudgetId == b.Id);
                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = b.OfficeCode,
                    Name = b.OfficeName,
                    Category = "Government Fund",
                    BudgetCap = b.AllocatedAmount,
                    Status = "Active",
                    OriginalItem = b,
                    HasLinkedProject = linkedProject != null,
                    LinkedProjectName = linkedProject?.ProgramName ?? string.Empty
                });
            }

            // Mirrored GGMS project sub-allocations (refreshed by Sync GGMS / module open).
            var ggmsProjects = await ggmsContext.GgmsProjectCache
                .AsNoTracking()
                .OrderByDescending(p => p.SourceCreatedAt)
                .ToListAsync();
            foreach (var b in ggmsProjects)
            {
                var linkedProject = allProjects.FirstOrDefault(p => p.SourceProjectDetailsId == b.ProjectDetailsId);
                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.GgmsProjectCacheId,
                    Code = b.ProjectDetailsId,
                    Name = b.ProjectName,
                    Category = "GGMS Project",
                    BudgetCap = b.TotalBudget,
                    Status = string.Equals(b.Status, "archived", StringComparison.OrdinalIgnoreCase) ? "Archived" : "Active",
                    OriginalItem = b,
                    HasLinkedProject = linkedProject != null || b.IsLinked,
                    LinkedProjectName = linkedProject?.ProgramName ?? string.Empty
                });
            }

            if (currentSelectedId.HasValue)
            {
                SelectedBudget = AllBudgets.FirstOrDefault(b => b.Id == currentSelectedId && b.Category == currentCategory);
            }
        }

        public PrivateDonationDonorType SelectedDonorType
        {
            get => _selectedDonorType;
            set => SetProperty(ref _selectedDonorType, value);
        }

        public DonationProofType SelectedProofType
        {
            get => _selectedProofType;
            set => SetProperty(ref _selectedProofType, value);
        }

        public string DonorName
        {
            get => _donorName;
            set => SetProperty(ref _donorName, value);
        }

        public string DonationAmountText
        {
            get => _donationAmountText;
            set => SetProperty(ref _donationAmountText, value);
        }

        public bool IsCashDonation
        {
            get => _isCashDonation;
            set
            {
                if (SetProperty(ref _isCashDonation, value))
                {
                    OnPropertyChanged(nameof(IsGoodsDonation));
                    if (value)
                    {
                        DonationItemName = string.Empty;
                        DonationQuantityText = string.Empty;
                        DonationUnitOfMeasure = string.Empty;
                    }
                }
            }
        }

        public bool IsGoodsDonation
        {
            get => !_isCashDonation;
            set
            {
                if (SetProperty(ref _isCashDonation, !value))
                {
                    OnPropertyChanged(nameof(IsCashDonation));
                    if (value)
                    {
                        DonationAmountText = string.Empty;
                    }
                }
            }
        }

        public string DonationItemName
        {
            get => _donationItemName;
            set => SetProperty(ref _donationItemName, value);
        }

        public string DonationQuantityText
        {
            get => _donationQuantityText;
            set => SetProperty(ref _donationQuantityText, value);
        }

        public string DonationUnitOfMeasure
        {
            get => _donationUnitOfMeasure;
            set => SetProperty(ref _donationUnitOfMeasure, value);
        }

        public DateTime DonationDateReceived
        {
            get => _donationDateReceived;
            set => SetProperty(ref _donationDateReceived, value);
        }

        public string DonationReferenceNumber
        {
            get => _donationReferenceNumber;
            set => SetProperty(ref _donationReferenceNumber, value);
        }

        public string DonationRemarks
        {
            get => _donationRemarks;
            set => SetProperty(ref _donationRemarks, value);
        }

        public string ProofReferenceNumber
        {
            get => _proofReferenceNumber;
            set => SetProperty(ref _proofReferenceNumber, value);
        }

        public string ProofFilePath
        {
            get => _proofFilePath;
            set => SetProperty(ref _proofFilePath, value);
        }

        public bool IsNewDonationMode
        {
            get => _isNewDonationMode;
            set => SetProperty(ref _isNewDonationMode, value);
        }

        public string EnrollmentSearchText
        {
            get => _enrollmentSearchText;
            set
            {
                if (SetProperty(ref _enrollmentSearchText, value))
                {
                    _currentEnrollmentPage = 1;
                    OnPropertyChanged(nameof(CurrentEnrollmentPage));
                    _ = QueryEnrollmentBeneficiariesAsync();
                }
            }
        }

        public int CurrentEnrollmentPage
        {
            get => _currentEnrollmentPage;
            private set
            {
                if (SetProperty(ref _currentEnrollmentPage, value))
                {
                    _previousEnrollmentPageCommand.RaiseCanExecuteChanged();
                    _nextEnrollmentPageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int TotalEnrollmentPages
        {
            get => _totalEnrollmentPages;
            private set
            {
                if (SetProperty(ref _totalEnrollmentPages, value))
                {
                    _previousEnrollmentPageCommand.RaiseCanExecuteChanged();
                    _nextEnrollmentPageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<EnrollmentBeneficiaryOption> EnrollmentBeneficiaries { get; } = new();
        public ObservableCollection<EnrollmentBeneficiaryOption> FilteredEnrollmentBeneficiaries { get; } = new();

        /// <summary>e.g. "Showing first 200 of 40,152 — refine the search" so the capped list is never mistaken for the whole registry.</summary>
        public string EnrollmentResultSummary
        {
            get => _enrollmentResultSummary;
            private set => SetProperty(ref _enrollmentResultSummary, value);
        }

        public int SelectedEnrollmentCount
        {
            get => _selectedEnrollmentCount;
            private set => SetProperty(ref _selectedEnrollmentCount, value);
        }

        private async Task LoadEnrollmentBeneficiariesAsync()
        {
            try
            {
                _enrollmentSearchText = string.Empty;
                OnPropertyChanged(nameof(EnrollmentSearchText));
                _selectedEnrollmentStagingIds.Clear();
                SelectedEnrollmentCount = 0;
                CurrentEnrollmentPage = 1;

                // Show the first page from the local masterlist immediately...
                await QueryEnrollmentBeneficiariesAsync();

                // ...then pull any beneficiaries missing locally in the background
                // (fail-soft offline) and refresh the visible page if new rows landed.
                // The masterlist mirrors the municipal registry. Task.Run keeps the
                // CRS fetch + dedup scan off the UI thread.
                var mirror = await Task.Run(() => new CrsMasterlistMirrorService().MirrorValidatedBeneficiariesAsync());
                if (mirror.IsSuccess && mirror.AddedCount > 0)
                {
                    await QueryEnrollmentBeneficiariesAsync();
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load approved beneficiaries: {ex.Message}");
            }
        }

        private sealed record EnrollmentBeneficiaryRow(
            int StagingID, string? BeneficiaryId, string? FullName, string? LastName, string? FirstName);

        private static IQueryable<BeneficiaryStaging> BuildEnrollmentQuery(LocalDbContext context, string? search)
        {
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

            return query;
        }

        /// <summary>
        /// Filters and pages DB-side: only the first <see cref="EnrollmentDisplayLimit"/> matches are
        /// materialized so opening the panel stays instant even with the full municipal registry local.
        /// </summary>
        private async Task QueryEnrollmentBeneficiariesAsync()
        {
            var version = ++_enrollmentSearchVersion;
            var search = EnrollmentSearchText?.Trim();
            var page = Math.Max(1, _currentEnrollmentPage);

            try
            {
                // SQLite executes "async" queries synchronously — run them on the
                // thread pool so the UI never blocks while the registry is scanned.
                var (totalCount, rows) = await Task.Run(async () =>
                {
                    await using var context = new LocalDbContext();
                    var query = BuildEnrollmentQuery(context, search);

                    var count = await query.CountAsync();
                    var lastPage = Math.Max(1, (int)Math.Ceiling(count / (double)EnrollmentDisplayLimit));
                    var boundedPage = Math.Min(page, lastPage);
                    var pageRows = await query
                        .OrderBy(item => item.FullName ?? item.LastName)
                        .Skip((boundedPage - 1) * EnrollmentDisplayLimit)
                        .Take(EnrollmentDisplayLimit)
                        .Select(item => new EnrollmentBeneficiaryRow(
                            item.StagingID,
                            item.BeneficiaryId,
                            item.FullName,
                            item.LastName,
                            item.FirstName))
                        .ToListAsync();
                    return (count, pageRows);
                });

                if (version != _enrollmentSearchVersion)
                {
                    return; // a newer search superseded this one
                }

                foreach (var stale in EnrollmentBeneficiaries)
                {
                    stale.PropertyChanged -= OnEnrollmentOptionPropertyChanged;
                }
                EnrollmentBeneficiaries.Clear();
                FilteredEnrollmentBeneficiaries.Clear();

                foreach (var b in rows)
                {
                    var option = new EnrollmentBeneficiaryOption
                    {
                        StagingId = b.StagingID,
                        BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                        FullName = string.IsNullOrWhiteSpace(b.FullName)
                            ? $"{b.LastName}, {b.FirstName}".Trim(',', ' ')
                            : b.FullName,
                        IsSelected = _selectedEnrollmentStagingIds.Contains(b.StagingID)
                    };
                    option.PropertyChanged += OnEnrollmentOptionPropertyChanged;
                    EnrollmentBeneficiaries.Add(option);
                    FilteredEnrollmentBeneficiaries.Add(option);
                }

                var lastPageForCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)EnrollmentDisplayLimit));
                TotalEnrollmentPages = lastPageForCount;
                CurrentEnrollmentPage = Math.Min(page, lastPageForCount);

                EnrollmentResultSummary = totalCount > EnrollmentDisplayLimit
                    ? $"{totalCount:N0} match{(totalCount == 1 ? "" : "es")} — page {CurrentEnrollmentPage:N0} of {TotalEnrollmentPages:N0}. Type a name or ID to narrow the list."
                    : totalCount == 0
                        ? "No approved beneficiaries match."
                        : $"{totalCount:N0} beneficiar{(totalCount == 1 ? "y" : "ies")} shown.";
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load approved beneficiaries: {ex.Message}");
            }
        }

        private void OnEnrollmentOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EnrollmentBeneficiaryOption.IsSelected) &&
                sender is EnrollmentBeneficiaryOption option)
            {
                if (option.IsSelected)
                {
                    _selectedEnrollmentStagingIds.Add(option.StagingId);
                }
                else
                {
                    _selectedEnrollmentStagingIds.Remove(option.StagingId);
                }

                SelectedEnrollmentCount = _selectedEnrollmentStagingIds.Count;
            }
        }

        /// <summary>Selects every beneficiary matching the current search DB-side (ids only), not just the visible page.</summary>
        private async Task SelectAllFilteredEnrollmentAsync()
        {
            try
            {
                var search = EnrollmentSearchText?.Trim();
                var ids = await Task.Run(async () =>
                {
                    await using var context = new LocalDbContext();
                    return await BuildEnrollmentQuery(context, search)
                        .Select(item => item.StagingID)
                        .ToListAsync();
                });

                foreach (var id in ids)
                {
                    _selectedEnrollmentStagingIds.Add(id);
                }

                foreach (var option in EnrollmentBeneficiaries)
                {
                    if (_selectedEnrollmentStagingIds.Contains(option.StagingId))
                    {
                        option.IsSelected = true;
                    }
                }

                SelectedEnrollmentCount = _selectedEnrollmentStagingIds.Count;
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to select beneficiaries: {ex.Message}");
            }
        }

        private void DeselectAllEnrollment()
        {
            _selectedEnrollmentStagingIds.Clear();
            foreach (var option in EnrollmentBeneficiaries)
            {
                option.IsSelected = false;
            }

            SelectedEnrollmentCount = 0;
        }

        private void ClearEnrollmentSelection()
        {
            foreach (var option in EnrollmentBeneficiaries)
            {
                option.PropertyChanged -= OnEnrollmentOptionPropertyChanged;
            }
            EnrollmentBeneficiaries.Clear();
            FilteredEnrollmentBeneficiaries.Clear();
            _selectedEnrollmentStagingIds.Clear();
            _enrollmentSearchText = string.Empty;
            OnPropertyChanged(nameof(EnrollmentSearchText));
            EnrollmentResultSummary = string.Empty;
            CurrentEnrollmentPage = 1;
            TotalEnrollmentPages = 1;
            SelectedEnrollmentCount = 0;
        }

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading budget controls...");

            try
            {
                await AutoSyncGgmsProjectsAsync();
                await LoadAssistanceCaseBudgetsAsync();
                await LoadCashForWorkBudgetsAsync();
                await LoadOverviewAsync();
                await LoadDonationsAsync();
                await LoadLedgerAsync();
                await LoadBudgetsViewAsync();
                SetSuccessStatus("Budget controls refreshed.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load budget controls: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Pulls new GGMS project sub-allocations once when the module opens; offline or GGMS
        /// failures are swallowed so the Budget module still loads from the local cache.
        /// </summary>
        private async Task AutoSyncGgmsProjectsAsync()
        {
            if (_hasAutoSyncedGgmsProjects)
            {
                return;
            }

            _hasAutoSyncedGgmsProjects = true;

            try
            {
                await using var context = new LocalDbContext();
                await new GgmsProjectSyncService().RefreshProjectCacheAsync(context);
            }
            catch
            {
                // Offline / unreachable GGMS — the cached mirror is still served.
            }
        }

        private async Task LoadAssistanceCaseBudgetsAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            var globalBudget = await budgetService.GetGlobalAssistanceCaseBudgetAsync();

            AssistanceCaseBudgetCapTotal = globalBudget?.BudgetCap ?? 0m;
        }

        private async Task LoadCashForWorkBudgetsAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            var globalBudget = await budgetService.GetGlobalCashForWorkBudgetAsync();

            CashForWorkBudgetCapTotal = globalBudget?.BudgetCap ?? 0m;
        }

        private async Task LoadOverviewAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            var overview = await budgetService.GetOverviewAsync();
            CombinedAvailable = overview.CombinedAvailable;
            GovernmentAvailable = overview.GovernmentAvailable;
            PrivateAvailable = overview.PrivateAvailable;
            UnrestrictedAvailable = overview.GovernmentUnrestrictedAvailable + overview.PrivateUnrestrictedAvailable;
            LockedAvailable = overview.GovernmentLockedAvailable + overview.PrivateLockedAvailable;
            ReleasedTotal = overview.ReleasedTotal;
            WeeklySpent = overview.WeeklySpent;
            MonthlySpent = overview.MonthlySpent;
            GovernmentAllocated = overview.GovernmentAllocated;
            GovernmentSpentReference = overview.GovernmentSpentReference;
            GovernmentOfficeCode = string.IsNullOrWhiteSpace(overview.OfficeCode) ? "Not configured" : overview.OfficeCode;
            GovernmentOfficeName = string.IsNullOrWhiteSpace(overview.OfficeName) ? "Not configured" : overview.OfficeName;
            LatestGovernmentSyncLabel = overview.LastGovernmentSyncAt.HasValue
                ? $"Last GGMS sync: {overview.LastGovernmentSyncAt:MMM dd, yyyy hh:mm tt}"
                : "No government sync yet.";
        }

        private async Task LoadDonationsAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            Donations.Clear();
            foreach (var donation in await budgetService.GetPrivateDonationsAsync())
            {
                Donations.Add(donation);
            }
        }

        private async Task LoadLedgerAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            
            var total = await budgetService.GetLedgerCountAsync(LedgerSearchText, SelectedLedgerSourceFilter);
            TotalLedgerEntries = total;
            TotalLedgerPages = (int)Math.Ceiling(total / (double)LedgerPageSize);
            if (TotalLedgerPages == 0) TotalLedgerPages = 1;

            if (CurrentLedgerPage > TotalLedgerPages) CurrentLedgerPage = TotalLedgerPages;
            if (CurrentLedgerPage < 1) CurrentLedgerPage = 1;

            var skip = (CurrentLedgerPage - 1) * LedgerPageSize;
            var entries = await budgetService.GetRecentLedgerEntriesAsync(skip, LedgerPageSize, LedgerSearchText, SelectedLedgerSourceFilter);

            LedgerEntries.Clear();

            foreach (var entry in entries)
            {
                LedgerEntries.Add(new BudgetLedgerEntryListItem
                {
                    EntryDate = entry.EntryDate,
                    EntryType = entry.EntryType.ToString(),
                    FeatureSource = entry.FeatureSource.ToString(),
                    ReleaseKind = entry.ReleaseKind?.ToString() ?? "--",
                    ProgramName = entry.Program?.ProgramName 
                            ?? entry.AssistanceCaseBudget?.BudgetName 
                            ?? entry.CashForWorkBudget?.BudgetName 
                            ?? "--",
                    RecipientCount = entry.RecipientCount,
                    TotalAmount = entry.TotalAmount,
                    GovernmentPortion = entry.GovernmentPortion,
                    PrivatePortion = entry.PrivatePortion,
                    Remarks = entry.Remarks ?? string.Empty
                });
            }

            SelectedLedgerEntry = null;
            RefreshLedgerSourceFilters();
            _nextLedgerPageCommand.RaiseCanExecuteChanged();
            _previousLedgerPageCommand.RaiseCanExecuteChanged();
        }

        private async Task NextLedgerPageAsync()
        {
            if (CurrentLedgerPage < TotalLedgerPages)
            {
                CurrentLedgerPage++;
                await LoadLedgerAsync();
            }
        }

        private async Task PreviousLedgerPageAsync()
        {
            if (CurrentLedgerPage > 1)
            {
                CurrentLedgerPage--;
                await LoadLedgerAsync();
            }
        }

        private async Task SyncGovernmentBudgetAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Syncing government budget from GGMS...");

            try
            {
                // STEP 1 — office-level allocation snapshot.
                await using var context = new LocalDbContext();
                var result = await new GgmsBudgetSyncService().SyncAyudaBudgetAsync(context, _currentUser.Id);
                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                // STEP 2 — mirror project_details sub-allocations for our office code.
                // The allocation snapshot above is already committed; a project read failure
                // must not undo it, so it degrades to a partial-success warning.
                SetNeutralStatus("Checking GGMS projects for new sub-allocations...");
                var projectResult = await new GgmsProjectSyncService().RefreshProjectCacheAsync(context);

                await LoadOverviewAsync();
                await LoadBudgetsViewAsync();

                if (!projectResult.IsSuccess)
                {
                    SetErrorStatus($"Government budget synced, but the GGMS project check failed: {projectResult.Message}");
                    return;
                }

                SetSuccessStatus($"Government budget sync completed. {projectResult.Message}");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to sync GGMS budget: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task UnlockFundsAsync()
        {
            if (IsBusy || SelectedBudget == null)
            {
                return;
            }

            if (SelectedBudgetRemaining <= 0)
            {
                SetErrorStatus("There are no remaining funds to unlock for this budget.");
                return;
            }

            if (SelectedBudget.Category == "GGMS Project")
            {
                SetErrorStatus("GGMS project budgets are controlled by GGMS and cannot be unlocked locally.");
                return;
            }

            string targetType;

            if (SelectedBudget.Category == "Global Aid Cap")
            {
                targetType = "AssistanceCaseBudget";
            }
            else if (SelectedBudget.Category == "Global CFW Cap")
            {
                targetType = "CashForWorkBudget";
            }
            else
            {
                targetType = "AyudaProgram";
            }

            IsBusy = true;
            SetNeutralStatus($"Unlocking remaining funds for {SelectedBudget.Code}...");

            try
            {
                await using var context = new LocalDbContext();
                var budgetService = new BudgetManagementService(context);

                var remarks = string.IsNullOrWhiteSpace(UnlockRemarks) ? "Manual unlock by admin" : UnlockRemarks.Trim();

                var result = await budgetService.ReallocateEarmarkAsync(
                    SelectedBudget.Id,
                    targetType,
                    remarks,
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                UnlockRemarks = string.Empty;
                await LoadOverviewAsync();
                await LoadLedgerAsync();
                SyncWithSelectedBudget(); // Refresh selected budget remaining

                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to unlock funds: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenLedgerPanel()
        {
            if (IsBusy)
            {
                return;
            }

            SetActivePanel(BudgetWorkspacePanel.Ledger);
        }

        private void ClosePanel()
        {
            SetActivePanel(BudgetWorkspacePanel.Dashboard);
        }

        private void CloseAllSetupPanels()
        {
            IsProjectCreationPanelOpen = false;
        }

        private void CloseLedgerHistoryCard()
        {
            SelectedLedgerEntry = null;
        }

        private void SetActivePanel(BudgetWorkspacePanel panel)
        {
            if (_activePanel == panel &&
                IsProjectCreationPanelOpen == (panel == BudgetWorkspacePanel.ProjectCreation))
            {
                return;
            }

            _activePanel = panel;
            CloseAllSetupPanels();
            OnPropertyChanged(nameof(IsAnyOverlayOpen));

            (CurrentPanelTitle, CurrentPanelSubtitle) = panel switch
            {
                BudgetWorkspacePanel.Dashboard => (
                    "Budget Ledger",
                    "Search the unified release history, export liquidation-ready rows, and inspect the full detail of the selected entry."),
                BudgetWorkspacePanel.GovernmentSync => (
                    "Budget Ledger",
                    "Search the unified release history, export liquidation-ready rows, and inspect the full detail of the selected entry."),
                BudgetWorkspacePanel.Ledger => (
                    "Budget Ledger",
                    "Search the unified release history, export liquidation-ready rows, and inspect the full detail of the selected entry."),
                _ => (CurrentPanelTitle, CurrentPanelSubtitle)
            };

            OnPropertyChanged(nameof(DashboardVisibility));
            OnPropertyChanged(nameof(GovernmentSyncVisibility));
            OnPropertyChanged(nameof(LedgerVisibility));
            OnPropertyChanged(nameof(ProjectCreationPanelVisibility));
            OnPropertyChanged(nameof(BackToDashboardVisibility));
            OnPropertyChanged(nameof(HistoryDetailVisibility));

            _openDashboardPanelCommand.RaiseCanExecuteChanged();
            _openGovernmentSyncPanelCommand.RaiseCanExecuteChanged();
            _openLedgerPanelCommand.RaiseCanExecuteChanged();
            _openNewDonationProjectCommand.RaiseCanExecuteChanged();
            _closePanelCommand.RaiseCanExecuteChanged();
        }

        private Visibility GetPanelVisibility(BudgetWorkspacePanel panel)
        {
            return _activePanel == panel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseProof()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files|*.*",
                Title = "Select Donation Proof File"
            };

            if (dialog.ShowDialog() == true)
            {
                ProofFilePath = dialog.FileName;
            }
        }

        private async Task ExportLedgerAsync()
        {
            if (IsBusy)
            {
                return;
            }

            var rows = LedgerEntriesView.Cast<BudgetLedgerEntryListItem>().ToList();
            if (rows.Count == 0)
            {
                SetErrorStatus("No ledger rows are available to export.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                AddExtension = true,
                DefaultExt = ".csv",
                FileName = $"budget_ledger_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Exporting budget ledger...");

            try
            {
                var lines = new List<string>(rows.Count + 1)
                {
                    "Date,Entry,Source,Release,Program,Recipients,Total,Government,Private,Remarks"
                };

                lines.AddRange(rows.Select(row => string.Join(",",
                    EscapeCsv(row.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.EntryType),
                    EscapeCsv(row.FeatureSource),
                    EscapeCsv(row.ReleaseKind),
                    EscapeCsv(row.ProgramName),
                    EscapeCsv(row.RecipientCount.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(row.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.GovernmentPortion.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.PrivatePortion.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.Remarks))));

                await File.WriteAllLinesAsync(dialog.FileName, lines);
                SetSuccessStatus($"Budget ledger exported to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to export budget ledger: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetDonationForm()
        {
            SelectedDonorType = PrivateDonationDonorType.Person;
            SelectedProofType = DonationProofType.Cash;
            DonorName = string.Empty;
            DonationAmountText = string.Empty;
            DonationDateReceived = DateTime.Today;
            DonationReferenceNumber = string.Empty;
            DonationRemarks = string.Empty;
            ProofReferenceNumber = string.Empty;
            ProofFilePath = string.Empty;
            IsCashDonation = true;
            DonationItemName = string.Empty;
            DonationQuantityText = string.Empty;
            DonationUnitOfMeasure = string.Empty;
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

        private static bool TryParseAmount(string text, out decimal amount)
        {
            amount = 0m;

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                return amount > 0;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return amount > 0;
            }

            return false;
        }

        private static bool TryParseOptionalAmount(string text, out decimal? amount)
        {
            amount = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (TryParseAmount(text, out var parsedAmount))
            {
                amount = parsedAmount;
                return true;
            }

            return false;
        }

        private void RefreshLedgerFilters()
        {
            LedgerEntriesView.Refresh();
            _exportLedgerCommand.RaiseCanExecuteChanged();
        }

        private void RefreshLedgerSourceFilters()
        {
            var selectedFilter = SelectedLedgerSourceFilter;
            var availableFilters = LedgerEntries
                .Select(entry => entry.FeatureSource)
                .Where(filter => !string.IsNullOrWhiteSpace(filter))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(filter => filter, StringComparer.OrdinalIgnoreCase)
                .ToList();

            LedgerSourceFilters.Clear();
            LedgerSourceFilters.Add(AllLedgerSourceFilter);

            foreach (var filter in availableFilters)
            {
                LedgerSourceFilters.Add(filter);
            }

            if (!LedgerSourceFilters.Any(filter => string.Equals(filter, selectedFilter, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedLedgerSourceFilter = AllLedgerSourceFilter;
            }
        }

        private bool FilterLedgerEntry(object item)
        {
            if (item is not BudgetLedgerEntryListItem entry)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SelectedLedgerSourceFilter) &&
                !string.Equals(SelectedLedgerSourceFilter, AllLedgerSourceFilter, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.FeatureSource, SelectedLedgerSourceFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(LedgerSearchText))
            {
                return true;
            }

            var searchText = LedgerSearchText.Trim();
            return ContainsFilterText(entry.EntryType, searchText)
                || ContainsFilterText(entry.FeatureSource, searchText)
                || ContainsFilterText(entry.ReleaseKind, searchText)
                || ContainsFilterText(entry.ProgramName, searchText)
                || ContainsFilterText(entry.Remarks, searchText)
                || ContainsFilterText(entry.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), searchText);
        }

        private static bool ContainsFilterText(string? source, string searchText)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string EscapeCsv(string? value)
        {
            var normalized = value ?? string.Empty;
            if (normalized.Contains(',') || normalized.Contains('"') || normalized.Contains('\r') || normalized.Contains('\n'))
            {
                return $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            }

            return normalized;
        }
    }

    public sealed class ProjectBudgetSourceViewModel : ObservableObject
    {
        private bool _isEnabled;
        private int _priority;

        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public decimal RemainingCap { get; init; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }
    }

    public sealed class BudgetLedgerEntryListItem
    {
        public DateTime EntryDate { get; init; }
        public string EntryType { get; init; } = string.Empty;
        public string FeatureSource { get; init; } = string.Empty;
        public string ReleaseKind { get; init; } = string.Empty;
        public string ProgramName { get; init; } = string.Empty;
        public int RecipientCount { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal GovernmentPortion { get; init; }
        public decimal PrivatePortion { get; init; }
        public string Remarks { get; init; } = string.Empty;
    }
}

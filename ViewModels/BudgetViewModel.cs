using System.Linq;
using System.Linq.Expressions;
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
        public bool IsProject => Category is "Cash for Work Project" or "Seminar Project" or "Distribution Project";
        public bool IsCashForWorkOrSeminar => Category is "Cash for Work Project" or "Seminar Project";
        public bool IsDistributionProject => Category == "Distribution Project";

        // Donor & Funding Source Details
        public string FundingSourceSummary { get; init; } = string.Empty;
        public string? DonorName { get; init; }
        public string? DonorType { get; init; }
        public DateTime? DonationDate { get; init; }
        public string? DonationReferenceNumber { get; init; }
        public string? DonationProofType { get; init; }
        public string? DonationProofReference { get; init; }
        public decimal? DonationAmount { get; init; }
        public string? DonationRemarks { get; init; }
        public string? DonationGoodsSummary { get; init; }

        // Operational & Event Details
        public string? ReleaseKind { get; init; }
        public decimal? DailyRate { get; init; }
        public string? ItemName { get; init; }
        public decimal? QuantityPerBeneficiary { get; init; }
        public string? UnitOfMeasure { get; init; }
        public string? BenefitDescription { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public string? Location { get; init; }
        public int? LinkedEventId { get; init; }
        public string? LinkedEventTitle { get; init; }
        public string? Description { get; init; }
    }

        public sealed class FundingSourceOption
    {
        public string DisplayText { get; init; } = string.Empty;
        public string SourceCode { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string FundingType { get; init; } = string.Empty;
        public decimal AllocatedAmount { get; init; }
        public decimal DisbursedAmount { get; init; }
        public decimal AvailableBalance { get; init; }
        public bool IsNewDonation { get; init; }
        public object? SourceItem { get; init; }

        public override string ToString() => DisplayText;
    }

    public sealed class TargetBarangayOption : ObservableObject
    {
        private bool _isSelected;
        public string Name { get; init; } = string.Empty;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private RelayCommand? _toggleCommand;
        public ICommand ToggleCommand => _toggleCommand ??= new RelayCommand(_ => IsSelected = !IsSelected);
    }

    public class EnrollmentBeneficiaryOption : ObservableObject
    {
        private bool _isSelected;
        public int StagingId { get; init; }
        public string BeneficiaryId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Barangay { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>Row in the household-records modal shown before a beneficiary is added to the project.</summary>
    public sealed class HouseholdBenefitRecordRow
    {
        public string FullName { get; init; } = string.Empty;
        public string RelationshipToHead { get; init; } = string.Empty;
        public bool IsCandidateBeneficiary { get; init; }
        public int BenefitsReceivedCount { get; init; }
        public string BenefitsReceivedText => BenefitsReceivedCount == 0
            ? "No benefits yet"
            : $"{BenefitsReceivedCount} benefit{(BenefitsReceivedCount == 1 ? "" : "s")} received";
    }

    public sealed class BudgetViewModel : ObservableObject
    {
        private const string AllLedgerSourceFilter = "All Sources";
        private const string AllTypeFilter = "All Categories";
        private static readonly string[] SulopBarangays = new[]
        {
            "Balasinon", "Buguis", "Carre", "Clib", "Harada Butai",
            "Katipunan", "Kiblagon", "Labon", "Laperas", "Lapla",
            "Litos", "Luparan", "Mckinley", "New Cebu", "Osmeña",
            "Palili", "Parami", "Poblacion", "Roxas", "Solongvale",
            "Tagolilong", "Talao", "Talas", "Tanwalang", "Waterfall"
        };
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
        private readonly RelayCommand _nextProjectWizardStepCommand;
        private readonly RelayCommand _previousProjectWizardStepCommand;
        private readonly RelayCommand _goToProjectWizardStepCommand;
        private readonly RelayCommand _toggleAllTargetBarangaysCommand;
        private readonly RelayCommand _selectAllTargetBarangaysCommand;
        private readonly RelayCommand _clearTargetBarangaysCommand;
        private readonly RelayCommand _toggleTargetBarangayCommand;
        private readonly RelayCommand _setBudgetCapToMaxCommand;
        private readonly RelayCommand _autoFillCandidatesCommand;
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

        private readonly RelayCommand _openRecordDonationModalCommand;
        private readonly RelayCommand _closeRecordDonationModalCommand;
        private readonly RelayCommand _saveDonationCommand;


        // Walkthrough Tour Commands & Fields
        private readonly RelayCommand _openOnboardingCommand;
        private readonly RelayCommand _closeOnboardingCommand;
        private readonly RelayCommand _nextOnboardingStepCommand;
        private readonly RelayCommand _previousOnboardingStepCommand;
        private readonly RelayCommand _setOnboardingStepCommand;
        private bool _isOnboardingOpen;
        private int _onboardingStep = 1;

        // Create Project Modal Walkthrough Tour Commands & Fields
        private readonly RelayCommand _openCreateProjectTourCommand;
        private readonly RelayCommand _closeCreateProjectTourCommand;
        private readonly RelayCommand _nextCreateProjectTourStepCommand;
        private readonly RelayCommand _previousCreateProjectTourStepCommand;
        private readonly RelayCommand _setCreateProjectTourStepCommand;
        private bool _isCreateProjectTourOpen;
        private int _createProjectTourStep = 1;

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
        private bool _isCreateProjectGuided;
        private string? _projectCreationErrorMessage;
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

        /// <summary>Set only for the creation that just completed; consumed by BudgetPage to route CFW projects to the payout console.</summary>
        internal int? _createdCfwBudgetId;
        /// <summary>Set only for the creation that just completed; consumed by BudgetPage to route Seminar projects to the Seminar Attendance page.</summary>
        internal int? _createdSeminarBudgetId;
        private const int EnrollmentDisplayLimit = 200;
        private int _currentEnrollmentPage = 1;
        private int _totalEnrollmentPages = 1;
        private readonly RelayCommand _previousEnrollmentPageCommand;
        private readonly RelayCommand _nextEnrollmentPageCommand;

        // "Add Beneficiaries" picker modal + household-records confirmation modal state.
        private bool _isBeneficiaryPickerOpen;
        private bool _isHouseholdRecordsOpen;
        private EnrollmentBeneficiaryOption? _householdRecordsCandidate;
        private string _householdRecordsCode = string.Empty;
        private string _householdRecordsHeadName = string.Empty;
        private string _householdRecordsSummary = string.Empty;
        private bool _householdRecordsHasHousehold;
        private string _householdRecordsDemographics = string.Empty;

        private int _currentLedgerPage = 1;
        private int _totalLedgerPages = 1;
        private int _totalLedgerEntries = 0;
        private const int LedgerPageSize = 25;

        private bool _isEditProjectPanelOpen;
        private string _editProjectCode = string.Empty;
        private string _editProjectName = string.Empty;
        private string _editProjectDescription = string.Empty;
        private string _editProjectCapText = string.Empty;
        private string _editDailyRateText = string.Empty;
        private string _editItemName = string.Empty;
        private string _editQuantityText = string.Empty;
        private string _editUnitOfMeasure = string.Empty;
        private string _editBenefitDescription = string.Empty;
        private DateTime? _editStartDate = DateTime.Today;
        private DateTime? _editEndDate = DateTime.Today.AddMonths(1);
        private bool _isEditCash = true;
        private bool _isEditCfwOrSeminar;
        private bool _isEditDistribution;
        private bool _hasEditProjectError;
        private string? _editProjectErrorMessage;
        private decimal _selectedBudgetDisbursed;
        private int _selectedBudgetBeneficiariesCount;

        private readonly RelayCommand _openEditProjectCommand;
        private readonly RelayCommand _closeEditProjectCommand;
        private readonly RelayCommand _saveEditProjectCommand;

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
        public ICommand NextProjectWizardStepCommand => _nextProjectWizardStepCommand;
        public ICommand PreviousProjectWizardStepCommand => _previousProjectWizardStepCommand;
        public ICommand GoToProjectWizardStepCommand => _goToProjectWizardStepCommand;
        public ICommand ToggleAllTargetBarangaysCommand => _toggleAllTargetBarangaysCommand;
        public ICommand SelectAllTargetBarangaysCommand => _selectAllTargetBarangaysCommand;
        public ICommand ClearTargetBarangaysCommand => _clearTargetBarangaysCommand;
        public ICommand ToggleTargetBarangayCommand => _toggleTargetBarangayCommand;
        public ICommand SetBudgetCapToMaxCommand => _setBudgetCapToMaxCommand;
        public ICommand AutoFillCandidatesCommand => _autoFillCandidatesCommand;

        private int _projectWizardStep = 1;
        public int ProjectWizardStep
        {
            get => _projectWizardStep;
            set
            {
                if (SetProperty(ref _projectWizardStep, value))
                {
                    OnPropertyChanged(nameof(IsStep1));
                    OnPropertyChanged(nameof(IsStep2));
                    OnPropertyChanged(nameof(IsStep3));
                    OnPropertyChanged(nameof(IsStep4));
                    OnPropertyChanged(nameof(IsFirstWizardStep));
                    OnPropertyChanged(nameof(IsLastWizardStep));
                    OnPropertyChanged(nameof(WizardStepTitle));
                    OnPropertyChanged(nameof(WizardStepSubtitle));
                }
            }
        }

        public bool IsStep1 => ProjectWizardStep == 1;
        public bool IsStep2 => ProjectWizardStep == 2;
        public bool IsStep3 => ProjectWizardStep == 3;
        public bool IsStep4 => ProjectWizardStep == 4;
        public bool IsFirstWizardStep => ProjectWizardStep == 1;
        public bool IsLastWizardStep => ProjectWizardStep == 4;

        public string WizardStepTitle => ProjectWizardStep switch
        {
            1 => "1. Funding Source & Benefit Type",
            2 => "2. Project Particulars & Target Coverage",
            3 => "3. Financial Earmark & Timeline Review",
            4 => "4. Candidate Beneficiary Pre-Enrollment",
            _ => "Create Ayuda Project"
        };

        public string WizardStepSubtitle => ProjectWizardStep switch
        {
            1 => "Select the 1:1 parent funding source pool and specify whether aid is cash or in-kind goods.",
            2 => "Configure project particulars, package details, unit multiplier, and target barangays.",
            3 => "Review financial earmark allocations, budget cap constraints, and schedule dates.",
            4 => "Screen and pre-enroll eligible citizens from targeted barangays using live municipal registry records.",
            _ => string.Empty
        };

        private decimal _sourceAllocatedAmount;
        public decimal SourceAllocatedAmount
        {
            get => _sourceAllocatedAmount;
            set => SetProperty(ref _sourceAllocatedAmount, value);
        }

        private decimal _sourceDisbursedAmount;
        public decimal SourceDisbursedAmount
        {
            get => _sourceDisbursedAmount;
            set => SetProperty(ref _sourceDisbursedAmount, value);
        }

        private decimal _sourceAvailableBalance;
        public decimal SourceAvailableBalance
        {
            get => _sourceAvailableBalance;
            set
            {
                if (SetProperty(ref _sourceAvailableBalance, value))
                {
                    NotifyEstimationCalculations();
                }
            }
        }

        private string _sourceCode = string.Empty;
        public string SourceCode
        {
            get => _sourceCode;
            set => SetProperty(ref _sourceCode, value);
        }

        private string _sourceTitle = string.Empty;
        public string SourceTitle
        {
            get => _sourceTitle;
            set => SetProperty(ref _sourceTitle, value);
        }

        private string _sourceFundingType = string.Empty;
        public string SourceFundingType
        {
            get => _sourceFundingType;
            set => SetProperty(ref _sourceFundingType, value);
        }

        private string _sourceSubtitle = string.Empty;
        public string SourceSubtitle
        {
            get => _sourceSubtitle;
            set => SetProperty(ref _sourceSubtitle, value);
        }

        public ObservableCollection<FundingSourceOption> FundingSourceOptions { get; } = new();

        private FundingSourceOption? _selectedFundingSourceOption;
        public FundingSourceOption? SelectedFundingSourceOption
        {
            get => _selectedFundingSourceOption;
            set
            {
                if (SetProperty(ref _selectedFundingSourceOption, value) && value != null)
                {
                    ApplyFundingSourceOption(value);
                }
            }
        }

        public ICommand SelectCashBenefitTypeCommand { get; }
        public ICommand SelectGoodsBenefitTypeCommand { get; }


        private string _newProjectTargetCountText = "50";
        public string NewProjectTargetCountText
        {
            get => _newProjectTargetCountText;
            set
            {
                if (SetProperty(ref _newProjectTargetCountText, value))
                {
                    NotifyEstimationCalculations();
                }
            }
        }

        public string CalculatedTotalCostText
        {
            get
            {
                var targetCount = 0;
                int.TryParse(NewProjectTargetCountText, out targetCount);

                if (SelectedReleaseKind == AssistanceReleaseKind.Goods)
                {
                    var qty = 0m;
                    TryParseAmount(NewProjectQuantityText, out qty);
                    var totalQty = targetCount * qty;
                    var unit = string.IsNullOrWhiteSpace(NewProjectUnitOfMeasure) ? "units" : NewProjectUnitOfMeasure;
                    return $"{targetCount} recipients × {qty:G29} {unit} = {totalQty:G29} {unit} total";
                }
                else
                {
                    var unitAmt = 0m;
                    TryParseAmount(NewProjectUnitAmountText, out unitAmt);
                    var totalAmt = targetCount * unitAmt;
                    return $"{targetCount} recipients × PHP {unitAmt:N2} = PHP {totalAmt:N2} total";
                }
            }
        }

        public decimal CalculatedTotalCostAmount
        {
            get
            {
                if (!int.TryParse(NewProjectTargetCountText, out var targetCount) || targetCount <= 0)
                    return 0m;

                if (SelectedReleaseKind == AssistanceReleaseKind.Goods)
                {
                    TryParseAmount(NewProjectQuantityText, out var qty);
                    return targetCount * qty;
                }
                else
                {
                    TryParseAmount(NewProjectUnitAmountText, out var unitAmt);
                    return targetCount * unitAmt;
                }
            }
        }

        public bool IsCostExceedingSourceBalance
        {
            get
            {
                if (IsNewDonationMode) return false;
                if (SourceAvailableBalance <= 0) return false;

                if (SelectedReleaseKind == AssistanceReleaseKind.Cash)
                {
                    var cost = CalculatedTotalCostAmount;
                    return cost > 0 && cost > SourceAvailableBalance;
                }

                return false;
            }
        }

        public decimal CostExceededDifference =>
            IsCostExceedingSourceBalance ? (CalculatedTotalCostAmount - SourceAvailableBalance) : 0m;

        public string CostExceededWarningText =>
            IsCostExceedingSourceBalance
                ? $"⚠️ Exceeds source available limit by ₱{CostExceededDifference:N2}! (Available: ₱{SourceAvailableBalance:N2})"
                : string.Empty;

        public bool IsBudgetCapExceedingSource
        {
            get
            {
                if (IsNewDonationMode) return false;
                if (SourceAvailableBalance <= 0) return false;
                if (TryParseOptionalAmount(NewProjectBudgetCapText, out var cap) && cap.HasValue)
                {
                    return cap.Value > SourceAvailableBalance;
                }
                return false;
            }
        }

        public void NotifyEstimationCalculations()
        {
            OnPropertyChanged(nameof(CalculatedTotalCostText));
            OnPropertyChanged(nameof(CalculatedTotalCostAmount));
            OnPropertyChanged(nameof(IsCostExceedingSourceBalance));
            OnPropertyChanged(nameof(CostExceededDifference));
            OnPropertyChanged(nameof(CostExceededWarningText));
            OnPropertyChanged(nameof(IsBudgetCapExceedingSource));
        }

        public ObservableCollection<TargetBarangayOption> TargetBarangayOptions { get; } = new();

        public int SelectedTargetBarangayCount => TargetBarangayOptions.Count(b => b.IsSelected);
        public bool IsAllTargetBarangaysSelected => TargetBarangayOptions.Count > 0 && TargetBarangayOptions.All(b => b.IsSelected);
        public bool HasTargetBarangaysFilter => SelectedTargetBarangayCount > 0 && !IsAllTargetBarangaysSelected;

        public string TargetBarangayCoverageText
        {
            get
            {
                var count = SelectedTargetBarangayCount;
                if (count == 0)
                    return "Municipality-Wide Scope: All 24 Sulop barangays are eligible for aid enrollment.";
                if (count == TargetBarangayOptions.Count)
                    return $"All {count} Barangays Selected: Full municipal coverage enabled.";
                var list = string.Join(", ", TargetBarangayOptions.Where(b => b.IsSelected).Select(b => b.Name));
                return $"{count} Barangay(s) Targeted: {list}";
            }
        }

        public ObservableCollection<AyudaProgramType> ProgramTypes { get; } = new(Enum.GetValues<AyudaProgramType>().Where(type => type != AyudaProgramType.AssistanceCase));
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
            set
            {
                if (SetProperty(ref _newProjectUnitAmountText, value))
                {
                    _confirmCreateProjectCommand.RaiseCanExecuteChanged();
                    NotifyEstimationCalculations();
                }
            }
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
                if (SetProperty(ref _newProjectQuantityText, value))
                {
                    ((RelayCommand)_confirmCreateProjectCommand).RaiseCanExecuteChanged();
                    NotifyEstimationCalculations();
                }
            }
        }

        public string NewProjectUnitOfMeasure
        {
            get => _newProjectUnitOfMeasure;
            set
            {
                if (SetProperty(ref _newProjectUnitOfMeasure, value))
                {
                    ((RelayCommand)_confirmCreateProjectCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(CalculatedTotalCostText));
                }
            }
        }

        public string NewProjectBudgetCapText
        {
            get => _newProjectBudgetCapText;
            set
            {
                if (SetProperty(ref _newProjectBudgetCapText, value))
                {
                    NotifyEstimationCalculations();
                }
            }
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
            set
            {
                if (SetProperty(ref _selectedProgramType, value))
                {
                    OnPropertyChanged(nameof(IsEnrollBeneficiariesVisible));
                    OnPropertyChanged(nameof(IsAttendanceBasedProgram));
                }
            }
        }

        public bool IsEnrollBeneficiariesVisible => _selectedProgramType != AyudaProgramType.CashForWork && _selectedProgramType != AyudaProgramType.Seminar;
        public bool IsAttendanceBasedProgram => _selectedProgramType == AyudaProgramType.CashForWork || _selectedProgramType == AyudaProgramType.Seminar;

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
                    NotifyEstimationCalculations();
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

        public string? ProjectCreationErrorMessage
        {
            get => _projectCreationErrorMessage;
            set
            {
                if (SetProperty(ref _projectCreationErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasProjectCreationError));
                }
            }
        }

        public bool HasProjectCreationError => !string.IsNullOrWhiteSpace(ProjectCreationErrorMessage);

        public bool IsCreateProjectGuided
        {
            get => _isCreateProjectGuided;
            set => SetProperty(ref _isCreateProjectGuided, value);
        }

        private void OpenProjectCreationPanel(object? sourceObj)
        {
            if (IsBusy) return;
            IsCreateProjectGuided = false;
            ProjectCreationErrorMessage = null;
            ProjectWizardStep = 1;

            ResetProjectCreationForm();
            IsNewDonationMode = false;

            RefreshFundingSourceOptions(sourceObj);

            var shouldOpenTour = IsOnboardingOpen && OnboardingStep == 4;
            if (shouldOpenTour)
            {
                CloseOnboarding();
            }

            SetActivePanel(BudgetWorkspacePanel.ProjectCreation);
            IsProjectCreationPanelOpen = true;
            _ = LoadEnrollmentBeneficiariesAsync();

            if (shouldOpenTour)
            {
                OpenCreateProjectTour();
            }
        }

        
        private bool _isDonationModalOpen;
        public bool IsDonationModalOpen
        {
            get => _isDonationModalOpen;
            private set
            {
                if (SetProperty(ref _isDonationModalOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                    OnPropertyChanged(nameof(DonationModalVisibility));
                    OnPropertyChanged(nameof(IsStandardModalOpen));
                }
            }
        }

        public Visibility DonationModalVisibility => IsDonationModalOpen ? Visibility.Visible : Visibility.Collapsed;

        private string? _donationModalErrorMessage;
        public string? DonationModalErrorMessage
        {
            get => _donationModalErrorMessage;
            set
            {
                if (SetProperty(ref _donationModalErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasDonationModalError));
                }
            }
        }

        public bool HasDonationModalError => !string.IsNullOrWhiteSpace(DonationModalErrorMessage);

        private string _goodsEstimatedValueText = string.Empty;
        public string GoodsEstimatedValueText
        {
            get => _goodsEstimatedValueText;
            set => SetProperty(ref _goodsEstimatedValueText, value);
        }

        public ObservableCollection<string> DonationUnits { get; } = new() { "Sacks", "Boxes", "Kits", "Pieces", "Packs" };

        public void OpenRecordDonationModal()
        {
            if (IsBusy) return;
            DonationModalErrorMessage = null;
            ResetDonationForm();
            IsDonationModalOpen = true;
        }

        public void CloseRecordDonationModal()
        {
            IsDonationModalOpen = false;
            DonationModalErrorMessage = null;
        }

        public async Task SaveDonationAsync()
        {
            if (IsBusy) return;
            DonationModalErrorMessage = null;

            if (string.IsNullOrWhiteSpace(DonorName))
            {
                DonationModalErrorMessage = "Donor name is required.";
                return;
            }

            decimal amount = 0m;
            string? itemName = null;
            decimal? quantity = null;
            string? unitOfMeasure = null;
            DonationType donationType = DonationType.Cash;

            if (IsCashDonation)
            {
                if (!TryParseAmount(DonationAmountText, out amount) || amount <= 0)
                {
                    DonationModalErrorMessage = "Enter a valid cash donation amount greater than zero.";
                    return;
                }
            }
            else
            {
                donationType = DonationType.Goods;
                itemName = NormalizeNullable(DonationItemName);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    DonationModalErrorMessage = "Please enter the item name for the goods donation.";
                    return;
                }

                if (!TryParseAmount(DonationQuantityText, out var qty) || qty <= 0)
                {
                    DonationModalErrorMessage = "Enter a valid donation quantity greater than zero.";
                    return;
                }
                quantity = qty;

                unitOfMeasure = NormalizeNullable(DonationUnitOfMeasure);
                if (string.IsNullOrWhiteSpace(unitOfMeasure))
                {
                    DonationModalErrorMessage = "Please specify the unit of measure (e.g. Sacks, Boxes).";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(GoodsEstimatedValueText) && TryParseAmount(GoodsEstimatedValueText, out var estVal))
                {
                    amount = estVal;
                }
                else if (!string.IsNullOrWhiteSpace(DonationAmountText) && TryParseAmount(DonationAmountText, out var altEstVal))
                {
                    amount = altEstVal;
                }
            }

            IsBusy = true;
            SetNeutralStatus("Posting donation to immutable ledger...");

            try
            {
                await using var context = new LocalDbContext();
                var budgetService = new BudgetManagementService(context);

                var req = new PrivateDonationRequest(
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
                    NormalizeNullable(ProofFilePath)
                );

                var result = await budgetService.RecordPrivateDonationAsync(req, _currentUser.Id);
                if (!result.IsSuccess)
                {
                    DonationModalErrorMessage = result.Message;
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadDonationsAsync();
                await LoadOverviewAsync();
                await LoadLedgerAsync();
                await LoadBudgetsViewAsync();
                RefreshFundingSourceOptions();

                CloseRecordDonationModal();
                SetSuccessStatus(donationType == DonationType.Cash
                    ? $"Private cash donation of ₱{amount:N2} from {DonorName} posted to ledger."
                    : $"Private goods donation ({quantity} {unitOfMeasure} of {itemName}) from {DonorName} posted to ledger.");
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
                DonationModalErrorMessage = message;
                SetErrorStatus($"Failed to record donation: {message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenNewDonationProjectPanel()
        {
            if (IsBusy) return;
            IsCreateProjectGuided = false;
            ProjectCreationErrorMessage = null;
            ProjectWizardStep = 1;

            var shouldOpenTour = IsOnboardingOpen && OnboardingStep == 4;
            if (shouldOpenTour)
            {
                CloseOnboarding();
            }

            ResetProjectCreationForm();
            ResetDonationForm();
            IsNewDonationMode = false;

            RefreshFundingSourceOptions();
            SelectedFundingSourceOption = FundingSourceOptions.FirstOrDefault(o => !o.IsNewDonation) ?? FundingSourceOptions.FirstOrDefault();

            SetActivePanel(BudgetWorkspacePanel.ProjectCreation);
            IsProjectCreationPanelOpen = true;
            _ = LoadEnrollmentBeneficiariesAsync();

            if (shouldOpenTour)
            {
                OpenCreateProjectTour();
            }
        }

        public void RefreshFundingSourceOptions(object? preselectObj = null)
        {
            FundingSourceOptions.Clear();

            foreach (var b in AllBudgets)
            {
                if (b.Category == "Government Fund" && b.OriginalItem is GovernmentBudgetSnapshot ggms)
                {
                    var rem = Math.Max(0, ggms.AllocatedAmount - ggms.SpentAmount);
                    FundingSourceOptions.Add(new FundingSourceOption
                    {
                        DisplayText = $"[Government] {ggms.OfficeCode} - {ggms.OfficeName} (Available: ₱{rem:N2})",
                        SourceCode = ggms.OfficeCode ?? $"OFF-{ggms.Id:D4}",
                        Title = ggms.OfficeName ?? "Government Allocation",
                        Subtitle = $"{ggms.OfficeCode} · FY {DateTime.Now.Year}",
                        FundingType = "Government Fund",
                        AllocatedAmount = ggms.AllocatedAmount,
                        DisbursedAmount = ggms.SpentAmount,
                        AvailableBalance = rem,
                        IsNewDonation = false,
                        SourceItem = ggms
                    });
                }
                else if (b.Category == "GGMS Project" && b.OriginalItem is GgmsProjectCache ggmsProj && !ggmsProj.IsLinked)
                {
                    FundingSourceOptions.Add(new FundingSourceOption
                    {
                        DisplayText = $"[Government] {ggmsProj.ProjectDetailsId} - {ggmsProj.ProjectName} (Available: ₱{ggmsProj.TotalBudget:N2})",
                        SourceCode = ggmsProj.ProjectDetailsId ?? "GGMS Project",
                        Title = ggmsProj.ProjectName ?? "GGMS Project",
                        Subtitle = $"{ggmsProj.ProjectDetailsId} · GGMS Central",
                        FundingType = "Government Fund",
                        AllocatedAmount = ggmsProj.TotalBudget,
                        DisbursedAmount = 0,
                        AvailableBalance = ggmsProj.TotalBudget,
                        IsNewDonation = false,
                        SourceItem = ggmsProj
                    });
                }
                else if (b.Category == "Private Donation" && b.OriginalItem is PrivateDonation donation && !b.HasLinkedProject)
                {
                    FundingSourceOptions.Add(new FundingSourceOption
                    {
                        DisplayText = $"[Donation] {donation.ReferenceNumber ?? $"DON-{donation.Id:D4}"} - {donation.DonorName} (Available: ₱{donation.Amount:N2})",
                        SourceCode = donation.ReferenceNumber ?? $"DON-{donation.Id:D4}",
                        Title = donation.DonorName ?? "Private Donation",
                        Subtitle = $"Received: {donation.DateReceived:MMM dd, yyyy} · {donation.DonorType}",
                        FundingType = "Private Donation",
                        AllocatedAmount = donation.Amount,
                        DisbursedAmount = 0,
                        AvailableBalance = donation.Amount,
                        IsNewDonation = false,
                        SourceItem = donation
                    });
                }
            }

            // Always add the "+ Record New Private Donation..." option
            FundingSourceOptions.Add(new FundingSourceOption
            {
                DisplayText = "+ Record New Private Donation...",
                SourceCode = "NEW DONATION",
                Title = "New Private Donation",
                Subtitle = "Ingest a new donation record",
                FundingType = "Private Donation",
                AllocatedAmount = 0,
                DisbursedAmount = 0,
                AvailableBalance = 0,
                IsNewDonation = true,
                SourceItem = null
            });

            // Handle pre-selection
            if (preselectObj != null)
            {
                var target = preselectObj is BudgetRecordListItem rec ? rec.OriginalItem : preselectObj;
                var found = FundingSourceOptions.FirstOrDefault(o => o.SourceItem != null && ReferenceEquals(o.SourceItem, target))
                         ?? FundingSourceOptions.FirstOrDefault(o => o.SourceItem != null && o.SourceItem.Equals(target));
                if (found != null)
                {
                    SelectedFundingSourceOption = found;
                    return;
                }
            }

            if (SelectedFundingSourceOption == null || !FundingSourceOptions.Contains(SelectedFundingSourceOption))
            {
                SelectedFundingSourceOption = FundingSourceOptions.FirstOrDefault(o => !o.IsNewDonation) ?? FundingSourceOptions.FirstOrDefault();
            }
        }

        private void ApplyFundingSourceOption(FundingSourceOption option)
        {
            if (option.IsNewDonation)
            {
                IsNewDonationMode = true;
                NewProjectSourceDonationId = null;
                NewProjectSourceGGMSBudgetId = null;
                NewProjectSourceProjectDetailsId = null;
                NewProjectSourceProjectBudget = null;
                NewProjectSourceDescription = "Source: New Private Donation (recorded on confirm)";
                SourceAllocatedAmount = 0;
                SourceDisbursedAmount = 0;
                SourceAvailableBalance = 0;
                SourceCode = "NEW DONATION";
                SourceTitle = "New Private Donation";
                SourceSubtitle = "Enter donation details below";
                SourceFundingType = "Private Donation";
                return;
            }

            IsNewDonationMode = false;
            SourceAllocatedAmount = option.AllocatedAmount;
            SourceDisbursedAmount = option.DisbursedAmount;
            SourceAvailableBalance = option.AvailableBalance;
            SourceCode = option.SourceCode;
            SourceTitle = option.Title;
            SourceSubtitle = option.Subtitle;
            SourceFundingType = option.FundingType;

            if (option.SourceItem is PrivateDonation donation)
            {
                NewProjectSourceDonationId = donation.Id;
                NewProjectSourceGGMSBudgetId = null;
                NewProjectSourceProjectDetailsId = null;
                NewProjectSourceProjectBudget = null;
                NewProjectSourceDescription = $"Source: Private Donation - {donation.DonorName} (PHP {donation.Amount:N2})";
                if (donation.DonationType == DonationType.Goods)
                {
                    SelectedReleaseKind = AssistanceReleaseKind.Goods;
                    NewProjectItemName = donation.ItemName;
                    NewProjectQuantityText = donation.Quantity?.ToString() ?? string.Empty;
                    NewProjectUnitOfMeasure = donation.UnitOfMeasure;
                }
            }
            else if (option.SourceItem is GovernmentBudgetSnapshot ggms)
            {
                NewProjectSourceGGMSBudgetId = ggms.Id;
                NewProjectSourceDonationId = null;
                NewProjectSourceProjectDetailsId = null;
                NewProjectSourceProjectBudget = null;
                NewProjectSourceDescription = $"Source: GGMS Allocation - {ggms.OfficeName} (PHP {ggms.AllocatedAmount:N2})";
            }
            else if (option.SourceItem is GgmsProjectCache ggmsProject)
            {
                NewProjectSourceProjectDetailsId = ggmsProject.ProjectDetailsId;
                NewProjectSourceProjectBudget = ggmsProject.TotalBudget;
                NewProjectSourceDonationId = null;
                NewProjectSourceGGMSBudgetId = null;
                NewProjectName = ggmsProject.ProjectName;
                NewProjectDescription = ggmsProject.Description ?? string.Empty;
                NewProjectSourceDescription = $"Source: GGMS Project {ggmsProject.ProjectDetailsId} - {ggmsProject.ProjectName} (PHP {ggmsProject.TotalBudget:N2})";
            }

            NotifyEstimationCalculations();
        }

        private void CloseProjectCreationPanel()
        {
            CloseCreateProjectTour();
            IsProjectCreationPanelOpen = false;
            IsNewDonationMode = false;
            ProjectWizardStep = 1;
            ClearEnrollmentSelection();
            SetActivePanel(BudgetWorkspacePanel.Dashboard);
        }

        private void NextProjectWizardStep()
        {
            ProjectCreationErrorMessage = null;

            if (ProjectWizardStep == 1)
            {
                if (IsNewDonationMode)
                {
                    if (string.IsNullOrWhiteSpace(DonorName))
                    {
                        ProjectCreationErrorMessage = "Please enter the donor's name.";
                        return;
                    }

                    if (IsCashDonation)
                    {
                        if (!TryParseAmount(DonationAmountText, out var amt) || amt <= 0)
                        {
                            ProjectCreationErrorMessage = "Please enter a valid cash donation amount greater than 0.";
                            return;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(DonationItemName) ||
                            string.IsNullOrWhiteSpace(DonationUnitOfMeasure) ||
                            !TryParseAmount(DonationQuantityText, out var qty) || qty <= 0)
                        {
                            ProjectCreationErrorMessage = "Please enter valid donation item name, quantity, and unit of measure.";
                            return;
                        }
                    }
                }
                else
                {
                    if (!NewProjectSourceDonationId.HasValue && !NewProjectSourceGGMSBudgetId.HasValue && string.IsNullOrWhiteSpace(NewProjectSourceProjectDetailsId))
                    {
                        ProjectCreationErrorMessage = "A source fund must be selected before proceeding.";
                        return;
                    }
                }

                ProjectWizardStep = 2;
            }
            else if (ProjectWizardStep == 2)
            {
                if (string.IsNullOrWhiteSpace(NewProjectName))
                {
                    ProjectCreationErrorMessage = "Please enter the project title / name.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewProjectCode))
                {
                    ProjectCreationErrorMessage = "Please enter the project code.";
                    return;
                }

                if (SelectedReleaseKind == AssistanceReleaseKind.Goods)
                {
                    if (string.IsNullOrWhiteSpace(NewProjectItemName))
                    {
                        ProjectCreationErrorMessage = "Enter an item name for goods distribution.";
                        return;
                    }

                    if (!TryParseAmount(NewProjectQuantityText, out var qty) || qty <= 0)
                    {
                        ProjectCreationErrorMessage = "Enter a valid quantity per beneficiary (greater than 0).";
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(NewProjectUnitOfMeasure))
                    {
                        ProjectCreationErrorMessage = "Enter a unit of measure (e.g. Sacks, Boxes, Packs).";
                        return;
                    }
                }
                else
                {
                    if (!TryParseAmount(NewProjectUnitAmountText, out var amt) || amt <= 0)
                    {
                        ProjectCreationErrorMessage = "Enter a valid unit payout amount (greater than 0).";
                        return;
                    }
                }

                if (SelectedReleaseKind == AssistanceReleaseKind.Cash && !IsNewDonationMode && SourceAvailableBalance > 0)
                {
                    if (IsCostExceedingSourceBalance)
                    {
                        ProjectCreationErrorMessage = $"Project estimated cost of ₱{CalculatedTotalCostAmount:N2} exceeds available source balance of ₱{SourceAvailableBalance:N2} by ₱{CostExceededDifference:N2}. Please adjust recipient count or unit payout.";
                        return;
                    }
                }

                ProjectWizardStep = 3;
            }
            else if (ProjectWizardStep == 3)
            {
                if (TryParseOptionalAmount(NewProjectBudgetCapText, out var cap) && cap.HasValue)
                {
                    if (!IsNewDonationMode && SourceAvailableBalance > 0 && cap.Value > SourceAvailableBalance)
                    {
                        ProjectCreationErrorMessage = $"Hard budget cap of ₱{cap.Value:N2} exceeds available source balance of ₱{SourceAvailableBalance:N2}.";
                        return;
                    }
                }

                if (IsAttendanceBasedProgram)
                {
                    _ = ConfirmCreateProjectAsync();
                }
                else
                {
                    ProjectWizardStep = 4;
                    _ = QueryEnrollmentBeneficiariesAsync();
                }
            }
        }

        private void PreviousProjectWizardStep()
        {
            ProjectCreationErrorMessage = null;
            if (ProjectWizardStep > 1)
            {
                ProjectWizardStep--;
            }
        }

        private void SetBudgetCapToMax()
        {
            if (SourceAvailableBalance > 0)
            {
                NewProjectBudgetCapText = SourceAvailableBalance.ToString("F2");
            }
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

            ProjectCreationErrorMessage = null;
            _createdCfwBudgetId = null;
            _createdSeminarBudgetId = null;

            decimal? unitAmount = null;
            decimal? quantity = null;

            if (NewProjectStartDate.HasValue && NewProjectEndDate.HasValue && NewProjectEndDate.Value < NewProjectStartDate.Value)
            {
                ProjectCreationErrorMessage = "Project end date cannot be earlier than start date.";
                SetErrorStatus(ProjectCreationErrorMessage);
                return;
            }

            // Cash-for-work pays a daily cash rate per attendance day; a Goods release
            // kind has no meaning there and its item/quantity inputs would be discarded.
            if (SelectedProgramType == AyudaProgramType.CashForWork &&
                SelectedReleaseKind == AssistanceReleaseKind.Goods)
            {
                ProjectCreationErrorMessage = "Cash-for-work projects pay a daily cash rate. Switch the release method to Cash and enter the daily rate.";
                SetErrorStatus(ProjectCreationErrorMessage);
                return;
            }

            if (SelectedReleaseKind == AssistanceReleaseKind.Goods)
            {
                if (string.IsNullOrWhiteSpace(NewProjectItemName))
                {
                    ProjectCreationErrorMessage = "Enter an item name for the goods distribution.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return;
                }

                if (!TryParseAmount(NewProjectQuantityText, out var qty))
                {
                    ProjectCreationErrorMessage = "Enter a valid quantity per beneficiary (greater than 0).";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return;
                }
                quantity = qty;
            }
            else
            {
                if (!TryParseAmount(NewProjectUnitAmountText, out var amt))
                {
                    ProjectCreationErrorMessage = "Enter a valid unit payout amount (greater than 0).";
                    SetErrorStatus(ProjectCreationErrorMessage);
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
                ProjectCreationErrorMessage = "A source fund must be selected to create a project.";
                SetErrorStatus(ProjectCreationErrorMessage);
                return;
            }

            if (!IsNewDonationMode && SelectedBudget != null && SelectedBudget.HasLinkedProject)
            {
                ProjectCreationErrorMessage = $"Source fund '{SelectedBudget.Name}' already has a linked project ('{SelectedBudget.LinkedProjectName}'). Duplicate project creation is blocked.";
                SetErrorStatus(ProjectCreationErrorMessage);
                return;
            }

            // A GGMS project is its own spending envelope: resolve the cap here so the remote
            // (Hostinger) and local writes both persist the same value.
            if (!string.IsNullOrWhiteSpace(NewProjectSourceProjectDetailsId) && NewProjectSourceProjectBudget.HasValue)
            {
                if (budgetCap.HasValue && budgetCap.Value > NewProjectSourceProjectBudget.Value)
                {
                    ProjectCreationErrorMessage = $"Budget cap cannot exceed the GGMS project budget of PHP {NewProjectSourceProjectBudget.Value:N2}.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return;
                }
                budgetCap ??= NewProjectSourceProjectBudget.Value;
            }

            if (!IsNewDonationMode && SourceAvailableBalance > 0)
            {
                if (SelectedReleaseKind == AssistanceReleaseKind.Cash && CalculatedTotalCostAmount > SourceAvailableBalance)
                {
                    ProjectCreationErrorMessage = $"Project estimated cost of ₱{CalculatedTotalCostAmount:N2} exceeds available source balance of ₱{SourceAvailableBalance:N2}.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return;
                }

                if (budgetCap.HasValue && budgetCap.Value > SourceAvailableBalance)
                {
                    ProjectCreationErrorMessage = $"Budget cap of ₱{budgetCap.Value:N2} exceeds available source balance of ₱{SourceAvailableBalance:N2}.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return;
                }
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
                        ProjectCreationErrorMessage = donationResult.Message;
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

                var createdName = NewProjectName;
                var enrollmentMessage = string.Empty;
                var selectedIds = _selectedEnrollmentStagingIds.ToList();

                var projectDescriptionToSave = NewProjectDescription;
                if (HasTargetBarangaysFilter)
                {
                    var targetedList = TargetBarangayOptions.Where(b => b.IsSelected).Select(b => b.Name).ToList();
                    var coveragePrefix = $"[Coverage: {string.Join(", ", targetedList)}]";
                    projectDescriptionToSave = string.IsNullOrWhiteSpace(projectDescriptionToSave)
                        ? coveragePrefix
                        : $"{coveragePrefix} {projectDescriptionToSave}";
                }

                // Branch: CFW projects go through a different creation path
                if (SelectedProgramType == AyudaProgramType.CashForWork)
                {
                    SetNeutralStatus("Creating cash-for-work project...");

                    var cfwRequest = new CashForWorkProjectRequest(
                        BudgetCode: $"CFW-{NewProjectCode}",
                        BudgetName: NewProjectName,
                        Description: NormalizeNullable(projectDescriptionToSave),
                        DailyRate: unitAmount,
                        BudgetCap: budgetCap,
                        StartDate: NewProjectStartDate ?? DateTime.Now,
                        EndDate: NewProjectEndDate ?? DateTime.Now,
                        SourceDonationId: NewProjectSourceDonationId,
                        SourceGGMSBudgetId: NewProjectSourceGGMSBudgetId,
                        SourceProjectDetailsId: NormalizeNullable(NewProjectSourceProjectDetailsId));

                    var cfwResult = await budgetService.CreateCashForWorkProjectAsync(cfwRequest, _currentUser.Id);
                    if (!cfwResult.Success)
                    {
                        ProjectCreationErrorMessage = IsNewDonationMode
                            ? $"Donation was recorded, but CFW project creation failed: {cfwResult.Message}"
                            : cfwResult.Message;
                        SetErrorStatus(ProjectCreationErrorMessage);
                        return;
                    }

                    _createdCfwBudgetId = cfwResult.BudgetId;

                    // The linked CFW event is auto-created with the project; details are refined in the CFW module
                    enrollmentMessage = " Its cash-for-work event was created; refine details via Edit Event in the Cash-for-Work module.";
                }
                else if (SelectedProgramType == AyudaProgramType.Seminar)
                {
                    SetNeutralStatus("Creating seminar project...");

                    var benefitType = SelectedReleaseKind == AssistanceReleaseKind.Goods
                        ? CashForWorkBenefitType.Goods
                        : CashForWorkBenefitType.Cash;

                    var benefitDescription = SelectedReleaseKind == AssistanceReleaseKind.Goods
                        ? $"{NewProjectQuantityText} {NewProjectUnitOfMeasure} of {NewProjectItemName}"
                        : null;

                    var semRequest = new CashForWorkProjectRequest(
                        BudgetCode: $"SEM-{NewProjectCode}",
                        BudgetName: NewProjectName,
                        Description: NormalizeNullable(projectDescriptionToSave),
                        DailyRate: unitAmount,
                        BudgetCap: budgetCap,
                        StartDate: NewProjectStartDate ?? DateTime.Now,
                        EndDate: NewProjectEndDate ?? DateTime.Now,
                        SourceDonationId: NewProjectSourceDonationId,
                        SourceGGMSBudgetId: NewProjectSourceGGMSBudgetId,
                        SourceProjectDetailsId: NormalizeNullable(NewProjectSourceProjectDetailsId),
                        EventKind: CashForWorkEventKind.Seminar,
                        BenefitType: benefitType,
                        BenefitDescription: benefitDescription);

                    var semResult = await budgetService.CreateCashForWorkProjectAsync(semRequest, _currentUser.Id);
                    if (!semResult.Success)
                    {
                        ProjectCreationErrorMessage = IsNewDonationMode
                            ? $"Donation was recorded, but seminar project creation failed: {semResult.Message}"
                            : semResult.Message;
                        SetErrorStatus(ProjectCreationErrorMessage);
                        return;
                    }

                    _createdSeminarBudgetId = semResult.BudgetId;
                    enrollmentMessage = " Its seminar event was created; refine details in the Seminar Attendance module.";
                }
                else
                {
                    // Distribution project path (existing logic)
                    var result = await budgetService.CreateProgramAsync(
                        new AyudaProgramRequest(
                            NewProjectCode,
                            NewProjectName,
                            SelectedProgramType,
                            NormalizeNullable(projectDescriptionToSave),
                            NormalizeNullable(NewProjectName),
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
                        ProjectCreationErrorMessage = IsNewDonationMode
                            ? $"Donation was recorded, but project creation failed: {result.Message}"
                            : result.Message;
                        SetErrorStatus(ProjectCreationErrorMessage);
                        return;
                    }

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
                }

                CloseProjectCreationPanel();
                await LoadAsync();
                SetSuccessStatus($"Project '{createdName}' created successfully.{enrollmentMessage}");
                ProjectCreatedGoToDistribution?.Invoke(createdName);
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message ?? ex.Message;
                ProjectCreationErrorMessage = $"Failed to create project: {detail}";
                SetErrorStatus(ProjectCreationErrorMessage);
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

            if (string.IsNullOrWhiteSpace(DonorName))
            {
                ProjectCreationErrorMessage = "Donor name is required for recording private donations.";
                SetErrorStatus(ProjectCreationErrorMessage);
                return null;
            }

            if (IsCashDonation)
            {
                if (!TryParseAmount(DonationAmountText, out amount))
                {
                    ProjectCreationErrorMessage = "Enter a valid donation amount greater than zero.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return null;
                }
            }
            else
            {
                donationType = DonationType.Goods;
                itemName = NormalizeNullable(DonationItemName);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    ProjectCreationErrorMessage = "Enter the item name for the goods donation.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return null;
                }

                if (!TryParseAmount(DonationQuantityText, out var qty))
                {
                    ProjectCreationErrorMessage = "Enter a valid donation quantity greater than zero.";
                    SetErrorStatus(ProjectCreationErrorMessage);
                    return null;
                }
                quantity = qty;

                unitOfMeasure = NormalizeNullable(DonationUnitOfMeasure);
                if (string.IsNullOrWhiteSpace(unitOfMeasure))
                {
                    ProjectCreationErrorMessage = "Enter the unit of measure (e.g. Sacks, Boxes).";
                    SetErrorStatus(ProjectCreationErrorMessage);
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

        private void InitializeTargetBarangays()
        {
            TargetBarangayOptions.Clear();
            foreach (var b in SulopBarangays)
            {
                var opt = new TargetBarangayOption { Name = b, IsSelected = false };
                opt.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TargetBarangayOption.IsSelected))
                    {
                        OnPropertyChanged(nameof(IsAllTargetBarangaysSelected));
                        OnPropertyChanged(nameof(SelectedTargetBarangayCount));
                        OnPropertyChanged(nameof(TargetBarangayCoverageText));
                        OnPropertyChanged(nameof(HasTargetBarangaysFilter));
                        _ = QueryEnrollmentBeneficiariesAsync();
                    }
                };
                TargetBarangayOptions.Add(opt);
            }
        }

        private void ToggleAllTargetBarangays()
        {
            var selectAll = !IsAllTargetBarangaysSelected;
            foreach (var b in TargetBarangayOptions)
            {
                b.IsSelected = selectAll;
            }
        }

        private void SelectAllTargetBarangays()
        {
            foreach (var b in TargetBarangayOptions)
            {
                b.IsSelected = true;
            }
        }

        private void ClearTargetBarangays()
        {
            foreach (var b in TargetBarangayOptions)
            {
                b.IsSelected = false;
            }
        }

        private void ResetProjectCreationForm()
        {
            ProjectCreationErrorMessage = null;
            ProjectWizardStep = 1;
            NewProjectName = string.Empty;
            NewProjectCode = string.Empty;
            NewProjectDescription = string.Empty;
            NewProjectUnitAmountText = string.Empty;
            NewProjectItemDescription = string.Empty;
            NewProjectItemName = string.Empty;
            NewProjectQuantityText = string.Empty;
            NewProjectUnitOfMeasure = string.Empty;
            NewProjectTargetCountText = "50";
            NewProjectBudgetCapText = string.Empty;
            NewProjectStartDate = DateTime.Today;
            NewProjectEndDate = DateTime.Today.AddMonths(1);
            SelectedProgramType = AyudaProgramType.GeneralPurpose;
            SelectedReleaseKind = AssistanceReleaseKind.Cash;
            ClearTargetBarangays();
        }

        public BudgetViewModel(User currentUser)
        {
            _currentUser = currentUser;
            InitializeTargetBarangays();
            DonorTypes = new ObservableCollection<PrivateDonationDonorType>(Enum.GetValues<PrivateDonationDonorType>());
            ProofTypes = new ObservableCollection<DonationProofType>(Enum.GetValues<DonationProofType>());
            Donations = new ObservableCollection<PrivateDonation>();
            LedgerEntries = new ObservableCollection<BudgetLedgerEntryListItem>();
            LedgerSourceFilters = new ObservableCollection<string> { AllLedgerSourceFilter };
            _ledgerEntriesView = CollectionViewSource.GetDefaultView(LedgerEntries);

            AllBudgets = new ObservableCollection<BudgetRecordListItem>();
            TypeFilters = new ObservableCollection<string> { AllTypeFilter, "All Projects", "Cash for Work Projects", "Seminar Projects", "Distribution Projects", "Private Donations", "Government Funds", "GGMS Projects", "Global Aid Cap" };
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
            _openRecordDonationModalCommand = new RelayCommand(_ => OpenRecordDonationModal(), _ => !IsBusy);
            _closeRecordDonationModalCommand = new RelayCommand(_ => CloseRecordDonationModal());
            _saveDonationCommand = new RelayCommand(async _ => await SaveDonationAsync(), _ => !IsBusy);
            SelectCashDonationCommand = new RelayCommand(_ => IsCashDonation = true);
            SelectGoodsDonationCommand = new RelayCommand(_ => IsGoodsDonation = true);

            _closeProjectCreationPanelCommand = new RelayCommand(_ => CloseProjectCreationPanel());
            _confirmCreateProjectCommand = new RelayCommand(async _ => await ConfirmCreateProjectAsync(), _ => !IsBusy && CanConfirmCreateProject());
            _nextProjectWizardStepCommand = new RelayCommand(_ => NextProjectWizardStep());
            _previousProjectWizardStepCommand = new RelayCommand(_ => PreviousProjectWizardStep());
            _goToProjectWizardStepCommand = new RelayCommand(param =>
            {
                if (param is int step && step >= 1 && step <= 4)
                {
                    ProjectWizardStep = step;
                }
                else if (int.TryParse(param?.ToString(), out var parsedStep) && parsedStep >= 1 && parsedStep <= 4)
                {
                    ProjectWizardStep = parsedStep;
                }
            });
            _toggleAllTargetBarangaysCommand = new RelayCommand(_ => ToggleAllTargetBarangays());
            _selectAllTargetBarangaysCommand = new RelayCommand(_ => SelectAllTargetBarangays());
            _clearTargetBarangaysCommand = new RelayCommand(_ => ClearTargetBarangays());
            _toggleTargetBarangayCommand = new RelayCommand(param =>
            {
                if (param is TargetBarangayOption opt)
                {
                    opt.IsSelected = !opt.IsSelected;
                }
                else if (param is string brgyName)
                {
                    var found = TargetBarangayOptions.FirstOrDefault(b => b.Name.Equals(brgyName, StringComparison.OrdinalIgnoreCase));
                    if (found != null) found.IsSelected = !found.IsSelected;
                }
            });
            _setBudgetCapToMaxCommand = new RelayCommand(_ => SetBudgetCapToMax());
            _autoFillCandidatesCommand = new RelayCommand(async _ => await AutoFillCandidatesAsync(), _ => !IsBusy);
            SelectCashBenefitTypeCommand = new RelayCommand(_ => SelectedReleaseKind = AssistanceReleaseKind.Cash);
            SelectGoodsBenefitTypeCommand = new RelayCommand(_ => SelectedReleaseKind = AssistanceReleaseKind.Goods);

            _openEditProjectCommand = new RelayCommand(param => OpenEditProject(param as BudgetRecordListItem ?? SelectedBudget), _ => !IsBusy && (SelectedBudget?.IsProject == true || _isEditProjectPanelOpen));
            _closeEditProjectCommand = new RelayCommand(_ => CloseEditProjectPanel());
            _saveEditProjectCommand = new RelayCommand(async _ => await SaveEditProjectAsync(), _ => !IsBusy);

            _selectAllFilteredEnrollmentCommand = new RelayCommand(async _ => await SelectAllFilteredEnrollmentAsync());
            _deselectAllEnrollmentCommand = new RelayCommand(_ => DeselectAllEnrollment());
            _previousEnrollmentPageCommand = new RelayCommand(
                _ => { CurrentEnrollmentPage--; _ = QueryEnrollmentBeneficiariesAsync(); },
                _ => CurrentEnrollmentPage > 1);
            _nextEnrollmentPageCommand = new RelayCommand(
                _ => { CurrentEnrollmentPage++; _ = QueryEnrollmentBeneficiariesAsync(); },
                _ => CurrentEnrollmentPage < TotalEnrollmentPages);

            OpenBeneficiaryPickerCommand = new RelayCommand(_ => IsBeneficiaryPickerOpen = true, _ => !IsBusy);
            CloseBeneficiaryPickerCommand = new RelayCommand(_ => IsBeneficiaryPickerOpen = false);
            RequestAddBeneficiaryCommand = new RelayCommand(async parameter => await OpenHouseholdRecordsAsync(parameter as EnrollmentBeneficiaryOption));
            ConfirmAddBeneficiaryCommand = new RelayCommand(_ => ConfirmAddBeneficiary(), _ => _householdRecordsCandidate != null);
            CancelHouseholdRecordsCommand = new RelayCommand(_ => CloseHouseholdRecords());
            RemoveSelectedBeneficiaryCommand = new RelayCommand(parameter => RemoveSelectedBeneficiary(parameter as EnrollmentBeneficiaryOption));

            _nextLedgerPageCommand = new RelayCommand(async _ => await NextLedgerPageAsync(), _ => !IsBusy && CurrentLedgerPage < TotalLedgerPages);
            _previousLedgerPageCommand = new RelayCommand(async _ => await PreviousLedgerPageAsync(), _ => !IsBusy && CurrentLedgerPage > 1);

            _openOnboardingCommand = new RelayCommand(_ => OpenOnboarding());
            _closeOnboardingCommand = new RelayCommand(_ => CloseOnboarding());
            _nextOnboardingStepCommand = new RelayCommand(_ => NextOnboardingStep());
            _previousOnboardingStepCommand = new RelayCommand(_ => PreviousOnboardingStep());
            _setOnboardingStepCommand = new RelayCommand(param => SetOnboardingStep(param));

            _openCreateProjectTourCommand = new RelayCommand(_ => OpenCreateProjectTour());
            _closeCreateProjectTourCommand = new RelayCommand(_ => CloseCreateProjectTour());
            _nextCreateProjectTourStepCommand = new RelayCommand(_ => NextCreateProjectTourStep());
            _previousCreateProjectTourStepCommand = new RelayCommand(_ => PreviousCreateProjectTourStep());
            _setCreateProjectTourStepCommand = new RelayCommand(param => SetCreateProjectTourStep(param));

            _ = LoadAsync();
        }

        public ICommand OpenOnboardingCommand => _openOnboardingCommand;
        public ICommand CloseOnboardingCommand => _closeOnboardingCommand;
        public ICommand NextOnboardingStepCommand => _nextOnboardingStepCommand;
        public ICommand PreviousOnboardingStepCommand => _previousOnboardingStepCommand;
        public ICommand SetOnboardingStepCommand => _setOnboardingStepCommand;

        public ICommand OpenCreateProjectTourCommand => _openCreateProjectTourCommand;
        public ICommand CloseCreateProjectTourCommand => _closeCreateProjectTourCommand;
        public ICommand NextCreateProjectTourStepCommand => _nextCreateProjectTourStepCommand;
        public ICommand PreviousCreateProjectTourStepCommand => _previousCreateProjectTourStepCommand;
        public ICommand SetCreateProjectTourStepCommand => _setCreateProjectTourStepCommand;

        // Unified Tour Commands
        public ICommand CloseActiveTourCommand => new RelayCommand(_ => { if (IsCreateProjectTourOpen) CloseCreateProjectTour(); else CloseOnboarding(); });
        public ICommand NextActiveTourStepCommand => new RelayCommand(_ => { if (IsCreateProjectTourOpen) NextCreateProjectTourStep(); else NextOnboardingStep(); });
        public ICommand PreviousActiveTourStepCommand => new RelayCommand(_ => { if (IsCreateProjectTourOpen) PreviousCreateProjectTourStep(); else PreviousOnboardingStep(); });
        public ICommand SetActiveTourStepCommand => new RelayCommand(param => { if (IsCreateProjectTourOpen) SetCreateProjectTourStep(param); else SetOnboardingStep(param); });

        public bool IsOnboardingOpen
        {
            get => _isOnboardingOpen;
            set
            {
                if (SetProperty(ref _isOnboardingOpen, value))
                {
                    if (value)
                    {
                        _isCreateProjectTourOpen = false;
                        OnPropertyChanged(nameof(IsCreateProjectTourOpen));
                    }
                    OnPropertyChanged(nameof(IsAnyTourOpen));
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                    NotifyActiveTourChanged();
                }
            }
        }

        public int OnboardingStep
        {
            get => _onboardingStep;
            set
            {
                if (SetProperty(ref _onboardingStep, Math.Clamp(value, 1, 4)))
                {
                    NotifyActiveTourChanged();
                }
            }
        }

        public bool IsCreateProjectTourOpen
        {
            get => _isCreateProjectTourOpen;
            set
            {
                if (SetProperty(ref _isCreateProjectTourOpen, value))
                {
                    if (value)
                    {
                        _isOnboardingOpen = false;
                        OnPropertyChanged(nameof(IsOnboardingOpen));
                    }
                    OnPropertyChanged(nameof(IsAnyTourOpen));
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                    NotifyActiveTourChanged();
                }
            }
        }

        public int CreateProjectTourStep
        {
            get => _createProjectTourStep;
            set
            {
                if (SetProperty(ref _createProjectTourStep, Math.Clamp(value, 1, 5)))
                {
                    NotifyActiveTourChanged();
                }
            }
        }

        public bool IsAnyTourOpen => _isOnboardingOpen || _isCreateProjectTourOpen;

        public int ActiveTourStep => _isCreateProjectTourOpen ? _createProjectTourStep : _onboardingStep;
        public int ActiveTourTotalSteps => _isCreateProjectTourOpen ? 5 : 4;
        public string ActiveTourHeaderTitle => _isCreateProjectTourOpen ? "PROJECT CREATION TOUR" : "BUDGET & FINANCE TOUR";

        public string ActiveTourTitle => _isCreateProjectTourOpen
            ? CreateProjectTourStep switch
            {
                1 => "Project Identity & Purpose",
                2 => "Release Method & Amounts",
                3 => "Funding Source & Donor",
                4 => "Beneficiary Enrollment",
                5 => "Finalize & Launch",
                _ => "Create Project Guide"
            }
            : OnboardingStep switch
            {
                1 => "Financial Overview & Balances",
                2 => "Sync Government Funds (GGMS)",
                3 => "Budget & Funding Registry",
                4 => "Spawn Community Project",
                _ => "Budget Management Tour"
            };

        public string ActiveTourInstruction => _isCreateProjectTourOpen
            ? CreateProjectTourStep switch
            {
                1 => "Specify the official project name, unique project code, and select its operational purpose (Cash for Work, Seminar, or Assistance Distribution).",
                2 => "Configure how benefits are disbursed: as Cash (with daily wage rate / unit payout) or physical Goods (specifying item name, quantity, and unit of measure).",
                3 => "Verify the allocated government budget or record a new private donation in-place with donor type, proof type, and receipt reference.",
                4 => "For distribution projects, pre-enroll approved municipal residents via ADD BENEFICIARIES. For Cash-for-Work and Seminars, participants register dynamically via QR scanning on-site.",
                5 => "Review all configured parameters and click CREATE PROJECT to lock the budget allocation and spawn the project across the system.",
                _ => string.Empty
            }
            : OnboardingStep switch
            {
                1 => "Monitor overall budget allocations, active private donations, and available operational balances. Track general vs. earmarked amounts and velocity metrics.",
                2 => "Synchronize certified municipal and national allocations directly from the Government Grants Management System (GGMS).",
                3 => "Browse all registered funding sources and active community projects. Filter by category, search by fund code, and select items for allocation.",
                4 => "Allocate funds to create Cash for Work, Seminar Training, or In-Kind Distribution projects with defined worker rates or goods allocations.",
                _ => string.Empty
            };

        public string ActiveTourActionHint => _isCreateProjectTourOpen
            ? CreateProjectTourStep switch
            {
                1 => "Fill in Project Name and Code in the first column, then click NEXT STEP.",
                2 => "Select Release Method (Cash or Goods) and set unit amounts.",
                3 => "Review the linked funding source in the middle column.",
                4 => "Optionally click ADD BENEFICIARIES to select validated residents.",
                5 => "Click CREATE PROJECT in the bottom right to finalize or click FINISH TOUR.",
                _ => string.Empty
            }
            : OnboardingStep switch
            {
                1 => "Review the financial metric cards above. Click NEXT STEP to continue.",
                2 => "Click SYNC GGMS to pull updated allocations or click NEXT STEP.",
                3 => SelectedBudget != null
                    ? $"Selected: {SelectedBudget.Code} - {SelectedBudget.Name}. Click NEXT STEP to continue."
                    : "Click any fund row in the table to inspect details or click NEXT STEP.",
                4 => "Click CREATE PROJECT to spawn an event or click FINISH.",
                _ => string.Empty
            };

        public string ActiveTourTargetName => _isCreateProjectTourOpen
            ? CreateProjectTourStep switch
            {
                1 => "ProjectBasicInfoSection",
                2 => "ReleaseSettingsSection",
                3 => "FundingSourceSection",
                4 => "BeneficiariesEnrollmentSection",
                5 => "ConfirmCreateProjectButton",
                _ => string.Empty
            }
            : OnboardingStep switch
            {
                1 => "FinancialSummaryGrid",
                2 => "SyncGgmsButton",
                3 => "BudgetBrowserCard",
                4 => "CreateProjectButton",
                _ => string.Empty
            };

        // Backward-compatible properties
        public string OnboardingTitle => ActiveTourTitle;
        public string OnboardingInstruction => ActiveTourInstruction;
        public string OnboardingActionHint => ActiveTourActionHint;
        public string OnboardingTargetName => ActiveTourTargetName;

        private void NotifyActiveTourChanged()
        {
            OnPropertyChanged(nameof(ActiveTourStep));
            OnPropertyChanged(nameof(ActiveTourTotalSteps));
            OnPropertyChanged(nameof(ActiveTourHeaderTitle));
            OnPropertyChanged(nameof(ActiveTourTitle));
            OnPropertyChanged(nameof(ActiveTourInstruction));
            OnPropertyChanged(nameof(ActiveTourActionHint));
            OnPropertyChanged(nameof(ActiveTourTargetName));
            OnPropertyChanged(nameof(OnboardingTitle));
            OnPropertyChanged(nameof(OnboardingInstruction));
            OnPropertyChanged(nameof(OnboardingActionHint));
            OnPropertyChanged(nameof(OnboardingTargetName));
        }

        public void OpenOnboarding()
        {
            OnboardingStep = 1;
            IsOnboardingOpen = true;
        }

        public void CloseOnboarding()
        {
            IsOnboardingOpen = false;
        }

        public void NextOnboardingStep()
        {
            if (OnboardingStep < 4)
            {
                OnboardingStep++;
            }
            else
            {
                CloseOnboarding();
            }
        }

        public void PreviousOnboardingStep()
        {
            if (OnboardingStep > 1)
            {
                OnboardingStep--;
            }
        }

        public void SetOnboardingStep(object? stepParam)
        {
            if (stepParam is int step)
            {
                OnboardingStep = step;
            }
            else if (stepParam is string s && int.TryParse(s, out var parsedStep))
            {
                OnboardingStep = parsedStep;
            }
        }

        public void OpenCreateProjectTour()
        {
            CreateProjectTourStep = 1;
            IsCreateProjectTourOpen = true;
        }

        public void CloseCreateProjectTour()
        {
            IsCreateProjectTourOpen = false;
        }

        public void NextCreateProjectTourStep()
        {
            if (CreateProjectTourStep < 5)
            {
                CreateProjectTourStep++;
            }
            else
            {
                CloseCreateProjectTour();
            }
        }

        public void PreviousCreateProjectTourStep()
        {
            if (CreateProjectTourStep > 1)
            {
                CreateProjectTourStep--;
            }
        }

        public void SetCreateProjectTourStep(object? stepParam)
        {
            if (stepParam is int step)
            {
                CreateProjectTourStep = step;
            }
            else if (stepParam is string s && int.TryParse(s, out var parsedStep))
            {
                CreateProjectTourStep = parsedStep;
            }
        }

        public bool IsStandardModalOpen => _activePanel == BudgetWorkspacePanel.Ledger || IsProjectCreationPanelOpen || IsEditProjectPanelOpen || IsBeneficiaryPickerOpen || IsHouseholdRecordsOpen || IsDonationModalOpen;

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
        public ICommand OpenRecordDonationModalCommand => _openRecordDonationModalCommand;
        public ICommand CloseRecordDonationModalCommand => _closeRecordDonationModalCommand;
        public ICommand SaveDonationCommand => _saveDonationCommand;
        public ICommand SelectCashDonationCommand { get; }
        public ICommand SelectGoodsDonationCommand { get; }

        public ICommand ClosePanelCommand => _closePanelCommand;
        public ICommand CloseLedgerHistoryCardCommand => _closeLedgerHistoryCardCommand;
        public ICommand ClearSelectedBudgetCommand => _clearSelectedBudgetCommand;
        public ICommand CloseHistoryDetailCommand => _closeLedgerHistoryCardCommand;
        public ICommand BrowseProofCommand => _browseProofCommand;
        public ICommand ExportLedgerCommand => _exportLedgerCommand;
        public ICommand UnlockFundsCommand => _unlockFundsCommand;
        public ICommand OpenEditProjectCommand => _openEditProjectCommand;
        public ICommand CloseEditProjectCommand => _closeEditProjectCommand;
        public ICommand SaveEditProjectCommand => _saveEditProjectCommand;
        public ICommand SelectAllFilteredEnrollmentCommand => _selectAllFilteredEnrollmentCommand;
        public ICommand DeselectAllEnrollmentCommand => _deselectAllEnrollmentCommand;
        public ICommand PreviousEnrollmentPageCommand => _previousEnrollmentPageCommand;
        public ICommand NextEnrollmentPageCommand => _nextEnrollmentPageCommand;

        public ICommand OpenBeneficiaryPickerCommand { get; }
        public ICommand CloseBeneficiaryPickerCommand { get; }
        public ICommand RequestAddBeneficiaryCommand { get; }
        public ICommand ConfirmAddBeneficiaryCommand { get; }
        public ICommand CancelHouseholdRecordsCommand { get; }
        public ICommand RemoveSelectedBeneficiaryCommand { get; }

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

                    if (IsOnboardingOpen && OnboardingStep == 3 && value != null)
                    {
                        OnboardingStep = 4;
                    }
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

        public bool IsAnyOverlayOpen => IsStandardModalOpen || _isOnboardingOpen;

        private async void SyncWithSelectedBudget()
        {
            if (SelectedBudget == null)
            {
                _currentIndex = -1;
                IsGlobalCapSelected = false;
                SelectedBudgetDisbursed = 0m;
                SelectedBudgetRemaining = 0m;
                SelectedBudgetBeneficiariesCount = 0;
                return;
            }

            var viewList = _budgetsView.Cast<BudgetRecordListItem>().ToList();
            _currentIndex = viewList.IndexOf(SelectedBudget);
            IsGlobalCapSelected = SelectedBudget.Category is "Global Aid Cap" or "Global CFW Cap";

            try
            {
                await using var context = new LocalDbContext();
                decimal spend = 0m;
                int beneficiaries = 0;

                if (SelectedBudget.Category is "Cash for Work Project" or "Seminar Project")
                {
                    var releaseEntries = await context.BudgetLedgerEntries
                        .AsNoTracking()
                        .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                        entry.CashForWorkBudgetId == SelectedBudget.Id)
                        .ToListAsync();

                    spend = releaseEntries.Sum(e => e.TotalAmount);
                    beneficiaries = releaseEntries.Sum(e => e.RecipientCount);

                    if (beneficiaries == 0 && SelectedBudget.LinkedEventId.HasValue)
                    {
                        beneficiaries = await context.CashForWorkParticipants
                            .AsNoTracking()
                            .CountAsync(p => !p.IsDeleted && p.EventId == SelectedBudget.LinkedEventId.Value);
                    }
                }
                else if (SelectedBudget.Category == "Distribution Project")
                {
                    var releaseEntries = await context.BudgetLedgerEntries
                        .AsNoTracking()
                        .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                        entry.ProgramId == SelectedBudget.Id)
                        .ToListAsync();

                    spend = releaseEntries.Sum(e => e.TotalAmount);
                    beneficiaries = releaseEntries.Sum(e => e.RecipientCount);

                    if (beneficiaries == 0)
                    {
                        beneficiaries = await context.AyudaProjectBeneficiaries
                            .AsNoTracking()
                            .CountAsync(b => b.AyudaProgramId == SelectedBudget.Id);
                    }
                }
                else if (SelectedBudget.Category == "GGMS Project" && SelectedBudget.OriginalItem is GgmsProjectCache ggmsProject)
                {
                    var linkedProgramIds = await context.AyudaPrograms
                        .AsNoTracking()
                        .Where(p => p.SourceProjectDetailsId == ggmsProject.ProjectDetailsId)
                        .Select(p => p.Id)
                        .ToListAsync();

                    var releaseEntries = linkedProgramIds.Count == 0
                        ? new List<BudgetLedgerEntry>()
                        : await context.BudgetLedgerEntries
                            .AsNoTracking()
                            .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                            entry.ProgramId != null &&
                                            linkedProgramIds.Contains(entry.ProgramId.Value))
                            .ToListAsync();

                    spend = releaseEntries.Sum(e => e.TotalAmount);
                    beneficiaries = releaseEntries.Sum(e => e.RecipientCount);
                }
                else if (SelectedBudget.Category == "Private Donation")
                {
                    var linkedProgramIds = await context.AyudaPrograms
                        .AsNoTracking()
                        .Where(p => p.SourceDonationId == SelectedBudget.Id)
                        .Select(p => p.Id)
                        .ToListAsync();

                    var linkedCfwIds = await context.CashForWorkBudgets
                        .AsNoTracking()
                        .Where(p => p.SourceDonationId == SelectedBudget.Id)
                        .Select(p => p.Id)
                        .ToListAsync();

                    var releaseEntries = await context.BudgetLedgerEntries
                        .AsNoTracking()
                        .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                       ((entry.ProgramId != null && linkedProgramIds.Contains(entry.ProgramId.Value)) ||
                                        (entry.CashForWorkBudgetId != null && linkedCfwIds.Contains(entry.CashForWorkBudgetId.Value))))
                        .ToListAsync();

                    spend = releaseEntries.Sum(e => e.TotalAmount);
                    beneficiaries = releaseEntries.Sum(e => e.RecipientCount);
                }
                else
                {
                    var releaseEntries = await context.BudgetLedgerEntries
                        .AsNoTracking()
                        .Where(entry => entry.EntryType == BudgetLedgerEntryType.Release &&
                                       (entry.AssistanceCaseBudgetId == SelectedBudget.Id && SelectedBudget.Category == "Global Aid Cap"))
                        .ToListAsync();

                    spend = releaseEntries.Sum(e => e.TotalAmount);
                    beneficiaries = releaseEntries.Sum(e => e.RecipientCount);
                }

                SelectedBudgetDisbursed = spend;
                SelectedBudgetRemaining = (SelectedBudget.BudgetCap ?? 0m) - spend;
                SelectedBudgetBeneficiariesCount = beneficiaries;
            }
            catch
            {
                SelectedBudgetDisbursed = 0m;
                SelectedBudgetRemaining = SelectedBudget.BudgetCap ?? 0m;
                SelectedBudgetBeneficiariesCount = 0;
            }

            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(DetailVisibility));
            _unlockFundsCommand.RaiseCanExecuteChanged();
            _openEditProjectCommand.RaiseCanExecuteChanged();
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

            if (!string.Equals(SelectedTypeFilter, AllTypeFilter))
            {
                if (SelectedTypeFilter == "All Projects")
                {
                    if (!record.IsProject) return false;
                }
                else if (SelectedTypeFilter == "Cash for Work Projects")
                {
                    if (record.Category != "Cash for Work Project") return false;
                }
                else if (SelectedTypeFilter == "Seminar Projects")
                {
                    if (record.Category != "Seminar Project") return false;
                }
                else if (SelectedTypeFilter == "Distribution Projects")
                {
                    if (record.Category != "Distribution Project") return false;
                }
                else if (SelectedTypeFilter == "Private Donations")
                {
                    if (record.Category != "Private Donation") return false;
                }
                else if (SelectedTypeFilter == "Government Funds")
                {
                    if (record.Category != "Government Fund") return false;
                }
                else if (SelectedTypeFilter == "GGMS Projects")
                {
                    if (record.Category != "GGMS Project") return false;
                }
                else if (SelectedTypeFilter == "Global Aid Cap")
                {
                    if (record.Category != "Global Aid Cap") return false;
                }
                else if (!string.Equals(record.Category, SelectedTypeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var search = SearchText.Trim();
            return record.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   record.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   record.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(record.DonorName) && record.DonorName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(record.FundingSourceSummary) && record.FundingSourceSummary.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private async Task LoadBudgetsViewAsync()
        {
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            
            var acBudgets = await budgetService.GetAssistanceCaseBudgetsAsync();
            var cfwBudgets = await budgetService.GetCashForWorkBudgetsAsync();
            var ayudaPrograms = await context.AyudaPrograms.AsNoTracking().ToListAsync();
            var donations = await budgetService.GetPrivateDonationsAsync(take: 200);
            var snapshots = await context.GovernmentBudgetSnapshots.AsNoTracking().ToListAsync();
            var cfwEvents = await context.CashForWorkEvents.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();

            var currentSelectedId = SelectedBudget?.Id;
            var currentCategory = SelectedBudget?.Category;

            AllBudgets.Clear();

            // Global Aid Cap
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
                    Description = b.Description,
                    FundingSourceSummary = "General Assistance Pool",
                    OriginalItem = b
                });
            }

            // Cash for Work & Seminar Projects
            foreach (var b in cfwBudgets)
            {
                var isSeminar = b.BudgetCode.StartsWith("SEM-", StringComparison.OrdinalIgnoreCase);
                var category = isSeminar ? "Seminar Project" : "Cash for Work Project";

                var donor = b.SourceDonationId.HasValue ? donations.FirstOrDefault(d => d.Id == b.SourceDonationId.Value) : null;
                var linkedEvent = cfwEvents.FirstOrDefault(e => e.CashForWorkBudgetId == b.Id);

                var fundingSummary = donor != null
                    ? $"Private Donation ({donor.DonorName})"
                    : (!string.IsNullOrEmpty(b.SourceProjectDetailsId)
                        ? $"GGMS Project ({b.SourceProjectDetailsId})"
                        : (b.SourceGGMSBudgetId.HasValue ? "Government Budget Snapshot" : "General Municipality Fund"));

                var releaseKindStr = linkedEvent != null
                    ? (linkedEvent.BenefitType == CashForWorkBenefitType.Goods ? "Goods" : "Cash")
                    : (b.DailyRate.HasValue ? "Cash" : "Goods");

                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = b.BudgetCode,
                    Name = b.BudgetName,
                    Category = category,
                    BudgetCap = b.BudgetCap ?? donor?.Amount,
                    Status = b.IsActive ? "Active" : "Inactive",
                    Description = b.Description,
                    OriginalItem = b,
                    FundingSourceSummary = fundingSummary,
                    DonorName = donor?.DonorName,
                    DonorType = donor?.DonorType.ToString(),
                    DonationDate = donor?.DateReceived,
                    DonationReferenceNumber = donor?.ReferenceNumber,
                    DonationProofType = donor?.ProofType.ToString(),
                    DonationProofReference = donor?.ProofReferenceNumber,
                    DonationAmount = donor?.Amount,
                    DonationRemarks = donor?.Remarks,
                    DonationGoodsSummary = donor != null && donor.DonationType == DonationType.Goods ? $"{donor.Quantity} {donor.UnitOfMeasure} of {donor.ItemName}" : null,
                    ReleaseKind = releaseKindStr,
                    DailyRate = b.DailyRate,
                    ItemName = donor?.ItemName,
                    QuantityPerBeneficiary = donor?.Quantity,
                    UnitOfMeasure = donor?.UnitOfMeasure,
                    BenefitDescription = linkedEvent?.BenefitDescription,
                    StartDate = b.StartDate ?? linkedEvent?.EventDate,
                    EndDate = b.EndDate ?? linkedEvent?.FinishDate,
                    Location = linkedEvent?.Location,
                    LinkedEventId = linkedEvent?.Id,
                    LinkedEventTitle = linkedEvent?.Title
                });
            }

            // Distribution Projects (Ayuda Programs)
            foreach (var p in ayudaPrograms)
            {
                var donor = p.SourceDonationId.HasValue ? donations.FirstOrDefault(d => d.Id == p.SourceDonationId.Value) : null;
                var fundingSummary = donor != null
                    ? $"Private Donation ({donor.DonorName})"
                    : (!string.IsNullOrEmpty(p.SourceProjectDetailsId)
                        ? $"GGMS Project ({p.SourceProjectDetailsId})"
                        : (p.SourceGGMSBudgetId.HasValue ? "Government Budget Snapshot" : "General Municipality Fund"));

                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = p.Id,
                    Code = p.ProgramCode,
                    Name = p.ProgramName,
                    Category = "Distribution Project",
                    BudgetCap = p.BudgetCap ?? donor?.Amount,
                    Status = p.IsActive ? p.DistributionStatus.ToString() : "Inactive",
                    Description = p.Description,
                    OriginalItem = p,
                    FundingSourceSummary = fundingSummary,
                    DonorName = donor?.DonorName,
                    DonorType = donor?.DonorType.ToString(),
                    DonationDate = donor?.DateReceived,
                    DonationReferenceNumber = donor?.ReferenceNumber,
                    DonationProofType = donor?.ProofType.ToString(),
                    DonationProofReference = donor?.ProofReferenceNumber,
                    DonationAmount = donor?.Amount,
                    DonationRemarks = donor?.Remarks,
                    DonationGoodsSummary = donor != null && donor.DonationType == DonationType.Goods ? $"{donor.Quantity} {donor.UnitOfMeasure} of {donor.ItemName}" : null,
                    ReleaseKind = p.ReleaseKind.ToString(),
                    DailyRate = p.UnitAmount,
                    ItemName = p.ItemName ?? donor?.ItemName,
                    QuantityPerBeneficiary = p.QuantityPerBeneficiary ?? donor?.Quantity,
                    UnitOfMeasure = p.UnitOfMeasure ?? donor?.UnitOfMeasure,
                    BenefitDescription = p.ItemDescription,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Location = p.ProgramName
                });
            }

            // Private Donations (Fund Sources)
            foreach (var b in donations)
            {
                var linkedProject = ayudaPrograms.FirstOrDefault(p => p.SourceDonationId == b.Id);
                var linkedCfwProject = cfwBudgets.FirstOrDefault(p => p.SourceDonationId == b.Id);
                var linkedName = linkedProject?.ProgramName ?? linkedCfwProject?.BudgetName ?? string.Empty;
                var isLinked = linkedProject != null || linkedCfwProject != null;

                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = $"DON-{b.Id}",
                    Name = b.DonorName,
                    Category = "Private Donation",
                    BudgetCap = b.Amount,
                    Status = "Active",
                    OriginalItem = b,
                    HasLinkedProject = isLinked,
                    LinkedProjectName = linkedName,
                    DonorName = b.DonorName,
                    DonorType = b.DonorType.ToString(),
                    DonationDate = b.DateReceived,
                    DonationReferenceNumber = b.ReferenceNumber,
                    DonationProofType = b.ProofType.ToString(),
                    DonationProofReference = b.ProofReferenceNumber,
                    DonationAmount = b.Amount,
                    DonationRemarks = b.Remarks,
                    DonationGoodsSummary = b.DonationType == DonationType.Goods ? $"{b.Quantity} {b.UnitOfMeasure} of {b.ItemName}" : null,
                    ReleaseKind = b.DonationType == DonationType.Goods ? "Goods" : "Cash",
                    ItemName = b.ItemName,
                    QuantityPerBeneficiary = b.Quantity,
                    UnitOfMeasure = b.UnitOfMeasure
                });
            }

            // Government Budget Snapshots
            foreach (var b in snapshots)
            {
                var linkedProject = ayudaPrograms.FirstOrDefault(p => p.SourceGGMSBudgetId == b.Id);
                var linkedCfwProject = cfwBudgets.FirstOrDefault(p => p.SourceGGMSBudgetId == b.Id);
                var linkedName = linkedProject?.ProgramName ?? linkedCfwProject?.BudgetName ?? string.Empty;
                var isLinked = linkedProject != null || linkedCfwProject != null;

                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.Id,
                    Code = b.OfficeCode,
                    Name = b.OfficeName,
                    Category = "Government Fund",
                    BudgetCap = b.AllocatedAmount,
                    Status = "Active",
                    OriginalItem = b,
                    HasLinkedProject = isLinked,
                    LinkedProjectName = linkedName
                });
            }

            // GGMS Projects (local-only mirror — skipped on shared MySQL connections).
            await LoadGgmsProjectBudgetsIntoViewAsync(context, ayudaPrograms, cfwBudgets);

            if (currentSelectedId.HasValue)
            {
                SelectedBudget = AllBudgets.FirstOrDefault(b => b.Id == currentSelectedId && b.Category == currentCategory);
            }

            RefreshFundingSourceOptions();
        }

        /// <summary>
        /// Appends GGMS Project rows from the local-only ggms_project_cache mirror.
        /// The mirror lives in ams.db and is absent from legacy MySQL databases, so on
        /// the MySQL provider this is a no-op instead of breaking the whole Budget load.
        /// </summary>
        private async Task LoadGgmsProjectBudgetsIntoViewAsync(
            LocalDbContext context,
            IReadOnlyList<AyudaProgram> ayudaPrograms,
            IReadOnlyList<CashForWorkBudget> cfwBudgets)
        {
            if (context.Database.ProviderName == "Pomelo.EntityFrameworkCore.MySql")
            {
                return;
            }

            var ggmsProjects = await context.GgmsProjectCache
                .AsNoTracking()
                .OrderByDescending(p => p.SourceCreatedAt)
                .ToListAsync();

            var staleLinkIds = ggmsProjects
                .Where(p => p.IsLinked && ayudaPrograms.All(a => a.SourceProjectDetailsId != p.ProjectDetailsId) && cfwBudgets.All(c => c.SourceProjectDetailsId != p.ProjectDetailsId))
                .Select(p => p.GgmsProjectCacheId)
                .ToList();
            if (staleLinkIds.Count > 0)
            {
                await context.GgmsProjectCache
                    .Where(p => staleLinkIds.Contains(p.GgmsProjectCacheId))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsLinked, false));
                foreach (var p in ggmsProjects)
                {
                    if (staleLinkIds.Contains(p.GgmsProjectCacheId))
                    {
                        p.IsLinked = false;
                    }
                }
            }

            foreach (var b in ggmsProjects)
            {
                var linkedProject = ayudaPrograms.FirstOrDefault(p => p.SourceProjectDetailsId == b.ProjectDetailsId);
                var linkedCfwProject = cfwBudgets.FirstOrDefault(p => p.SourceProjectDetailsId == b.ProjectDetailsId);
                var linkedName = linkedProject?.ProgramName ?? linkedCfwProject?.BudgetName ?? string.Empty;
                var isLinked = linkedProject != null || linkedCfwProject != null || b.IsLinked;

                AllBudgets.Add(new BudgetRecordListItem
                {
                    Id = b.GgmsProjectCacheId,
                    Code = b.ProjectDetailsId,
                    Name = b.ProjectName,
                    Category = "GGMS Project",
                    BudgetCap = b.TotalBudget,
                    Status = string.Equals(b.Status, "archived", StringComparison.OrdinalIgnoreCase) ? "Archived" : "Active",
                    OriginalItem = b,
                    HasLinkedProject = isLinked,
                    LinkedProjectName = linkedName
                });
            }
        }

        public void OpenEditProject(BudgetRecordListItem? item)
        {
            item ??= SelectedBudget;
            if (item == null || !item.IsProject) return;

            HasEditProjectError = false;
            EditProjectErrorMessage = null;

            EditProjectCode = item.Code;
            EditProjectName = item.Name;
            EditProjectDescription = item.Description ?? string.Empty;
            EditProjectCapText = item.BudgetCap.HasValue ? item.BudgetCap.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
            EditDailyRateText = item.DailyRate.HasValue ? item.DailyRate.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
            EditItemName = item.ItemName ?? string.Empty;
            EditQuantityText = item.QuantityPerBeneficiary.HasValue ? item.QuantityPerBeneficiary.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
            EditUnitOfMeasure = item.UnitOfMeasure ?? string.Empty;
            EditBenefitDescription = item.BenefitDescription ?? string.Empty;
            EditStartDate = item.StartDate ?? DateTime.Today;
            EditEndDate = item.EndDate ?? DateTime.Today.AddMonths(1);

            IsEditCash = string.Equals(item.ReleaseKind, "Cash", StringComparison.OrdinalIgnoreCase) || item.DailyRate.HasValue;
            IsEditCfwOrSeminar = item.IsCashForWorkOrSeminar;
            IsEditDistribution = item.IsDistributionProject;

            IsEditProjectPanelOpen = true;
        }

        public void CloseEditProjectPanel()
        {
            IsEditProjectPanelOpen = false;
            HasEditProjectError = false;
            EditProjectErrorMessage = null;
        }

        public async Task SaveEditProjectAsync()
        {
            if (SelectedBudget == null || !SelectedBudget.IsProject) return;

            if (string.IsNullOrWhiteSpace(EditProjectName))
            {
                HasEditProjectError = true;
                EditProjectErrorMessage = "Project name is required.";
                return;
            }

            if (EditStartDate.HasValue && EditEndDate.HasValue && EditStartDate > EditEndDate)
            {
                HasEditProjectError = true;
                EditProjectErrorMessage = "Start date cannot be after end date.";
                return;
            }

            decimal? budgetCap = null;
            if (!string.IsNullOrWhiteSpace(EditProjectCapText))
            {
                if (!TryParseAmount(EditProjectCapText, out var cap))
                {
                    HasEditProjectError = true;
                    EditProjectErrorMessage = "Budget cap must be a valid positive amount.";
                    return;
                }
                budgetCap = cap;
            }

            decimal? dailyRate = null;
            if (IsEditCash && !string.IsNullOrWhiteSpace(EditDailyRateText))
            {
                if (!TryParseAmount(EditDailyRateText, out var rate))
                {
                    HasEditProjectError = true;
                    EditProjectErrorMessage = "Daily rate / unit amount must be a valid positive number.";
                    return;
                }
                dailyRate = rate;
            }

            decimal? quantity = null;
            if (!IsEditCash)
            {
                if (string.IsNullOrWhiteSpace(EditItemName))
                {
                    HasEditProjectError = true;
                    EditProjectErrorMessage = "Goods item name is required.";
                    return;
                }

                if (IsEditDistribution)
                {
                    if (string.IsNullOrWhiteSpace(EditQuantityText) || !decimal.TryParse(EditQuantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                    {
                        HasEditProjectError = true;
                        EditProjectErrorMessage = "Goods quantity must be greater than zero.";
                        return;
                    }
                    quantity = qty;

                    if (string.IsNullOrWhiteSpace(EditUnitOfMeasure))
                    {
                        HasEditProjectError = true;
                        EditProjectErrorMessage = "Unit of measure is required.";
                        return;
                    }
                }
            }

            IsBusy = true;
            SetNeutralStatus($"Saving changes to project '{EditProjectName}'...");

            try
            {
                await using var context = new LocalDbContext();
                var budgetService = new BudgetManagementService(context);

                if (SelectedBudget.IsCashForWorkOrSeminar)
                {
                    var benefitType = IsEditCash ? CashForWorkBenefitType.Cash : CashForWorkBenefitType.Goods;
                    var benefitDesc = !IsEditCash
                        ? (!string.IsNullOrWhiteSpace(EditBenefitDescription) ? EditBenefitDescription : $"{EditQuantityText} {EditUnitOfMeasure} of {EditItemName}".Trim())
                        : null;

                    var request = new CashForWorkProjectRequest(
                        BudgetCode: SelectedBudget.Code,
                        BudgetName: EditProjectName.Trim(),
                        Description: NormalizeNullable(EditProjectDescription),
                        DailyRate: dailyRate,
                        BudgetCap: budgetCap,
                        StartDate: EditStartDate ?? DateTime.Today,
                        EndDate: EditEndDate ?? DateTime.Today.AddMonths(1),
                        BenefitType: benefitType,
                        BenefitDescription: benefitDesc);

                    var result = await budgetService.UpdateCashForWorkProjectAsync(SelectedBudget.Id, request, _currentUser.Id);
                    if (!result.Success)
                    {
                        HasEditProjectError = true;
                        EditProjectErrorMessage = result.Message;
                        SetErrorStatus(result.Message);
                        return;
                    }
                }
                else if (SelectedBudget.IsDistributionProject)
                {
                    var releaseKind = IsEditCash ? AssistanceReleaseKind.Cash : AssistanceReleaseKind.Goods;
                    var request = new AyudaProgramRequest(
                        ProgramCode: SelectedBudget.Code,
                        ProgramName: EditProjectName.Trim(),
                        ProgramType: (SelectedBudget.OriginalItem as AyudaProgram)?.ProgramType ?? AyudaProgramType.GeneralPurpose,
                        Description: NormalizeNullable(EditProjectDescription),
                        AssistanceType: NormalizeNullable(EditProjectName),
                        ReleaseKind: releaseKind,
                        UnitAmount: dailyRate,
                        ItemDescription: NormalizeNullable(EditBenefitDescription),
                        ItemName: EditItemName,
                        QuantityPerBeneficiary: quantity,
                        UnitOfMeasure: EditUnitOfMeasure,
                        StartDate: EditStartDate,
                        EndDate: EditEndDate,
                        BudgetCap: budgetCap,
                        DistributionStatus: (SelectedBudget.OriginalItem as AyudaProgram)?.DistributionStatus ?? AyudaProgramDistributionStatus.Open);

                    var result = await budgetService.UpdateProgramAsync(SelectedBudget.Id, request, _currentUser.Id);
                    if (!result.IsSuccess)
                    {
                        HasEditProjectError = true;
                        EditProjectErrorMessage = result.Message;
                        SetErrorStatus(result.Message);
                        return;
                    }
                }

                CloseEditProjectPanel();
                await LoadBudgetsViewAsync();
                await LoadOverviewAsync();
                SetSuccessStatus($"Project '{EditProjectName}' updated successfully.");
            }
            catch (Exception ex)
            {
                HasEditProjectError = true;
                EditProjectErrorMessage = $"Failed to save project: {ex.Message}";
                SetErrorStatus(EditProjectErrorMessage);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public bool IsEditProjectPanelOpen
        {
            get => _isEditProjectPanelOpen;
            set
            {
                if (SetProperty(ref _isEditProjectPanelOpen, value))
                {
                    OnPropertyChanged(nameof(EditProjectPanelVisibility));
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }

        public Visibility EditProjectPanelVisibility => IsEditProjectPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public string EditProjectCode
        {
            get => _editProjectCode;
            set => SetProperty(ref _editProjectCode, value);
        }

        public string EditProjectName
        {
            get => _editProjectName;
            set => SetProperty(ref _editProjectName, value);
        }

        public string EditProjectDescription
        {
            get => _editProjectDescription;
            set => SetProperty(ref _editProjectDescription, value);
        }

        public string EditProjectCapText
        {
            get => _editProjectCapText;
            set => SetProperty(ref _editProjectCapText, value);
        }

        public string EditDailyRateText
        {
            get => _editDailyRateText;
            set => SetProperty(ref _editDailyRateText, value);
        }

        public string EditItemName
        {
            get => _editItemName;
            set => SetProperty(ref _editItemName, value);
        }

        public string EditQuantityText
        {
            get => _editQuantityText;
            set => SetProperty(ref _editQuantityText, value);
        }

        public string EditUnitOfMeasure
        {
            get => _editUnitOfMeasure;
            set => SetProperty(ref _editUnitOfMeasure, value);
        }

        public string EditBenefitDescription
        {
            get => _editBenefitDescription;
            set => SetProperty(ref _editBenefitDescription, value);
        }

        public DateTime? EditStartDate
        {
            get => _editStartDate;
            set => SetProperty(ref _editStartDate, value);
        }

        public DateTime? EditEndDate
        {
            get => _editEndDate;
            set => SetProperty(ref _editEndDate, value);
        }

        public bool IsEditCash
        {
            get => _isEditCash;
            set
            {
                if (SetProperty(ref _isEditCash, value))
                {
                    OnPropertyChanged(nameof(IsEditGoods));
                }
            }
        }

        public bool IsEditGoods
        {
            get => !_isEditCash;
            set => IsEditCash = !value;
        }

        public bool IsEditCfwOrSeminar
        {
            get => _isEditCfwOrSeminar;
            set => SetProperty(ref _isEditCfwOrSeminar, value);
        }

        public bool IsEditDistribution
        {
            get => _isEditDistribution;
            set => SetProperty(ref _isEditDistribution, value);
        }

        public bool HasEditProjectError
        {
            get => _hasEditProjectError;
            set => SetProperty(ref _hasEditProjectError, value);
        }

        public string? EditProjectErrorMessage
        {
            get => _editProjectErrorMessage;
            set => SetProperty(ref _editProjectErrorMessage, value);
        }

        public decimal SelectedBudgetDisbursed
        {
            get => _selectedBudgetDisbursed;
            private set => SetProperty(ref _selectedBudgetDisbursed, value);
        }

        public int SelectedBudgetBeneficiariesCount
        {
            get => _selectedBudgetBeneficiariesCount;
            private set => SetProperty(ref _selectedBudgetBeneficiariesCount, value);
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

        /// <summary>Right list of the picker modal — beneficiaries confirmed for the project (mirrors _selectedEnrollmentStagingIds).</summary>
        public ObservableCollection<EnrollmentBeneficiaryOption> SelectedEnrollmentBeneficiaries { get; } = new();

        /// <summary>Household roster shown in the confirmation modal before a beneficiary is added.</summary>
        public ObservableCollection<HouseholdBenefitRecordRow> HouseholdRecordsMembers { get; } = new();

        public bool IsBeneficiaryPickerOpen
        {
            get => _isBeneficiaryPickerOpen;
            private set => SetProperty(ref _isBeneficiaryPickerOpen, value);
        }

        public bool IsHouseholdRecordsOpen
        {
            get => _isHouseholdRecordsOpen;
            private set => SetProperty(ref _isHouseholdRecordsOpen, value);
        }

        public string HouseholdRecordsCandidateName => _householdRecordsCandidate?.FullName ?? string.Empty;

        public string HouseholdRecordsCode
        {
            get => _householdRecordsCode;
            private set => SetProperty(ref _householdRecordsCode, value);
        }

        public string HouseholdRecordsHeadName
        {
            get => _householdRecordsHeadName;
            private set => SetProperty(ref _householdRecordsHeadName, value);
        }

        public string HouseholdRecordsSummary
        {
            get => _householdRecordsSummary;
            private set => SetProperty(ref _householdRecordsSummary, value);
        }

        public bool HouseholdRecordsHasHousehold
        {
            get => _householdRecordsHasHousehold;
            private set => SetProperty(ref _householdRecordsHasHousehold, value);
        }

        /// <summary>CRS demographics line (marital status · ethnicity · tribe) from crs_demographics_cache.</summary>
        public string HouseholdRecordsDemographics
        {
            get => _householdRecordsDemographics;
            private set => SetProperty(ref _householdRecordsDemographics, value);
        }

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
            int StagingID, string? BeneficiaryId, string? FullName, string? LastName, string? FirstName, string? Address);

        private static IQueryable<BeneficiaryStaging> BuildEnrollmentQuery(
            LocalDbContext context, 
            string? search, 
            IReadOnlyList<string>? targetBarangays = null)
        {
            // Mirrors MasterListService.LoadPageAsync: the local staging registry is the
            // source of truth and is listed without a VerificationStatus gate (MasterList
            // shows every local row). Scoping to targeted barangays is address-substring
            // only, case-insensitive via lower() like the MasterList search.
            var query = context.BeneficiaryStaging.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLowerInvariant();
                query = query.Where(item =>
                    (item.FullName != null && item.FullName.ToLower().Contains(searchLower)) ||
                    (item.BeneficiaryId != null && item.BeneficiaryId.ToLower().Contains(searchLower)) ||
                    (item.CivilRegistryId != null && item.CivilRegistryId.ToLower().Contains(searchLower)) ||
                    (item.LastName != null && item.LastName.ToLower().Contains(searchLower)) ||
                    (item.FirstName != null && item.FirstName.ToLower().Contains(searchLower)) ||
                    (item.Address != null && item.Address.ToLower().Contains(searchLower)));
            }

            if (targetBarangays != null && targetBarangays.Count > 0 && targetBarangays.Count < SulopBarangays.Length)
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(BeneficiaryStaging), "item");
                var addressProp = System.Linq.Expressions.Expression.Property(parameter, nameof(BeneficiaryStaging.Address));
                var notNullExp = System.Linq.Expressions.Expression.NotEqual(addressProp, System.Linq.Expressions.Expression.Constant(null, typeof(string)));
                var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
                var addressLowerExp = System.Linq.Expressions.Expression.Call(addressProp, toLowerMethod);
                var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

                var searchTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var brgy in targetBarangays)
                {
                    searchTerms.Add(brgy);
                    if (brgy.Equals("Osmeña", StringComparison.OrdinalIgnoreCase))
                    {
                        searchTerms.Add("Osme");
                    }
                    else if (brgy.Equals("Talao", StringComparison.OrdinalIgnoreCase))
                    {
                        searchTerms.Add("Tala-o");
                    }
                    else if (brgy.Equals("Harada Butai", StringComparison.OrdinalIgnoreCase))
                    {
                        searchTerms.Add("Harada Yano");
                    }
                    else if (brgy.Equals("Parami", StringComparison.OrdinalIgnoreCase))
                    {
                        searchTerms.Add("Parame");
                    }
                }

                System.Linq.Expressions.Expression? orExp = null;
                foreach (var term in searchTerms)
                {
                    var termLower = term.ToLowerInvariant();
                    var call = System.Linq.Expressions.Expression.Call(addressLowerExp, containsMethod, System.Linq.Expressions.Expression.Constant(termLower, typeof(string)));
                    orExp = orExp == null ? call : System.Linq.Expressions.Expression.OrElse(orExp, call);
                }

                if (orExp != null)
                {
                    var combined = System.Linq.Expressions.Expression.AndAlso(notNullExp, orExp);
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<BeneficiaryStaging, bool>>(combined, parameter);
                    query = query.Where(lambda);
                }
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
            var targetBarangays = TargetBarangayOptions
                .Where(b => b.IsSelected)
                .Select(b => b.Name)
                .ToList();

            try
            {
                // SQLite executes "async" queries synchronously — run them on the
                // thread pool so the UI never blocks while the registry is scanned.
                var (totalCount, rows) = await Task.Run(async () =>
                {
                    await using var context = new LocalDbContext();
                    var query = BuildEnrollmentQuery(context, search, targetBarangays);

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
                            item.FirstName,
                            item.Address))
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
                    var brgy = GgmsConsolidatedTransactionService.ParseBarangayFromAddress(b.Address) ?? string.Empty;
                    var option = new EnrollmentBeneficiaryOption
                    {
                        StagingId = b.StagingID,
                        BeneficiaryId = b.BeneficiaryId ?? string.Empty,
                        FullName = string.IsNullOrWhiteSpace(b.FullName)
                            ? $"{b.LastName}, {b.FirstName}".Trim(',', ' ')
                            : b.FullName,
                        Barangay = brgy,
                        Address = b.Address ?? string.Empty,
                        IsSelected = _selectedEnrollmentStagingIds.Contains(b.StagingID)
                    };
                    option.PropertyChanged += OnEnrollmentOptionPropertyChanged;
                    EnrollmentBeneficiaries.Add(option);
                    FilteredEnrollmentBeneficiaries.Add(option);
                }

                var lastPageForCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)EnrollmentDisplayLimit));
                TotalEnrollmentPages = lastPageForCount;
                CurrentEnrollmentPage = Math.Min(page, lastPageForCount);

                var scopeSuffix = targetBarangays.Count > 0 && targetBarangays.Count < SulopBarangays.Length
                    ? $" [Scoped to {targetBarangays.Count} targeted barangay(s)]"
                    : string.Empty;

                EnrollmentResultSummary = totalCount > EnrollmentDisplayLimit
                    ? $"{totalCount:N0} match{(totalCount == 1 ? "" : "es")} — page {CurrentEnrollmentPage:N0} of {TotalEnrollmentPages:N0}. Type a name or ID to narrow the list.{scopeSuffix}"
                    : totalCount == 0
                        ? $"No approved beneficiaries match.{scopeSuffix}"
                        : $"{totalCount:N0} beneficiar{(totalCount == 1 ? "y" : "ies")} shown.{scopeSuffix}";
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
                    if (!SelectedEnrollmentBeneficiaries.Any(item => item.StagingId == option.StagingId))
                    {
                        SelectedEnrollmentBeneficiaries.Add(option);
                    }
                }
                else
                {
                    _selectedEnrollmentStagingIds.Remove(option.StagingId);
                    var listed = SelectedEnrollmentBeneficiaries.FirstOrDefault(item => item.StagingId == option.StagingId);
                    if (listed != null)
                    {
                        SelectedEnrollmentBeneficiaries.Remove(listed);
                    }
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
                var targetBarangays = TargetBarangayOptions
                    .Where(b => b.IsSelected)
                    .Select(b => b.Name)
                    .ToList();

                var ids = await Task.Run(async () =>
                {
                    await using var context = new LocalDbContext();
                    return await BuildEnrollmentQuery(context, search, targetBarangays)
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

        private async Task AutoFillCandidatesAsync()
        {
            if (IsBusy) return;
            try
            {
                var targetSlots = 50;
                if (int.TryParse(NewProjectTargetCountText, out var parsedCount) && parsedCount > 0)
                {
                    targetSlots = parsedCount;
                }

                var needed = targetSlots - _selectedEnrollmentStagingIds.Count;
                if (needed <= 0)
                {
                    SetNeutralStatus($"Candidate roster is already at or above target slots ({_selectedEnrollmentStagingIds.Count}/{targetSlots}).");
                    return;
                }

                var targetBarangays = TargetBarangayOptions
                    .Where(b => b.IsSelected)
                    .Select(b => b.Name)
                    .ToList();

                var candidatesToAdd = await Task.Run(async () =>
                {
                    await using var context = new LocalDbContext();
                    var existingIds = _selectedEnrollmentStagingIds.ToList();
                    return await BuildEnrollmentQuery(context, null, targetBarangays)
                        .Where(b => !existingIds.Contains(b.StagingID))
                        .OrderBy(b => b.FullName ?? b.LastName)
                        .Take(needed)
                        .Select(b => new EnrollmentBeneficiaryRow(
                            b.StagingID,
                            b.BeneficiaryId,
                            b.FullName,
                            b.LastName,
                            b.FirstName,
                            b.Address))
                        .ToListAsync();
                });

                foreach (var c in candidatesToAdd)
                {
                    if (_selectedEnrollmentStagingIds.Add(c.StagingID))
                    {
                        var brgy = GgmsConsolidatedTransactionService.ParseBarangayFromAddress(c.Address) ?? string.Empty;
                        var opt = new EnrollmentBeneficiaryOption
                        {
                            StagingId = c.StagingID,
                            BeneficiaryId = c.BeneficiaryId ?? string.Empty,
                            FullName = string.IsNullOrWhiteSpace(c.FullName) ? $"{c.LastName}, {c.FirstName}".Trim(',', ' ') : c.FullName,
                            Barangay = brgy,
                            Address = c.Address ?? string.Empty,
                            IsSelected = true
                        };
                        SelectedEnrollmentBeneficiaries.Add(opt);
                    }
                }

                foreach (var opt in EnrollmentBeneficiaries)
                {
                    if (_selectedEnrollmentStagingIds.Contains(opt.StagingId))
                    {
                        opt.IsSelected = true;
                    }
                }

                SelectedEnrollmentCount = _selectedEnrollmentStagingIds.Count;
                SetSuccessStatus($"Auto-filled {candidatesToAdd.Count} candidate(s) into project roster ({SelectedEnrollmentCount}/{targetSlots} slots).");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Auto-fill candidates failed: {ex.Message}");
            }
        }

        private void DeselectAllEnrollment()
        {
            _selectedEnrollmentStagingIds.Clear();
            foreach (var option in EnrollmentBeneficiaries)
            {
                option.IsSelected = false;
            }
            SelectedEnrollmentBeneficiaries.Clear();

            SelectedEnrollmentCount = 0;
        }

        /// <summary>Step 1 of adding: load the candidate's household + per-member benefit history, then show the confirmation modal.</summary>
        private async Task OpenHouseholdRecordsAsync(EnrollmentBeneficiaryOption? candidate)
        {
            if (candidate == null || _selectedEnrollmentStagingIds.Contains(candidate.StagingId))
            {
                return;
            }

            _householdRecordsCandidate = candidate;
            HouseholdRecordsMembers.Clear();
            HouseholdRecordsCode = string.Empty;
            HouseholdRecordsHeadName = string.Empty;
            HouseholdRecordsSummary = "Loading household records...";
            HouseholdRecordsHasHousehold = false;
            HouseholdRecordsDemographics = string.Empty;
            OnPropertyChanged(nameof(HouseholdRecordsCandidateName));
            IsHouseholdRecordsOpen = true;

            try
            {
                var (records, demographics) = await Task.Run(async () =>
                {
                    await using var context = new LocalDbContext();
                    var service = new ProjectDistributionService(context);
                    var benefitRecords = await service.GetHouseholdBenefitRecordsAsync(candidate.StagingId);

                    var cachedDemographics = string.IsNullOrWhiteSpace(candidate.BeneficiaryId)
                        ? null
                        : await context.CrsDemographicsCaches
                            .AsNoTracking()
                            .FirstOrDefaultAsync(row => row.BeneficiaryId == candidate.BeneficiaryId);

                    return (benefitRecords, cachedDemographics);
                });

                if (_householdRecordsCandidate != candidate || !IsHouseholdRecordsOpen)
                {
                    return; // modal was cancelled or superseded while loading
                }

                HouseholdRecordsDemographics = FormatDemographicsLine(demographics);
                HouseholdRecordsHasHousehold = records.HasHousehold;
                if (!records.HasHousehold)
                {
                    HouseholdRecordsSummary = "No linked household on record for this beneficiary.";
                    return;
                }

                HouseholdRecordsCode = records.HouseholdCode;
                HouseholdRecordsHeadName = records.HeadName;
                foreach (var member in records.Members)
                {
                    HouseholdRecordsMembers.Add(new HouseholdBenefitRecordRow
                    {
                        FullName = member.FullName,
                        RelationshipToHead = member.RelationshipToHead,
                        IsCandidateBeneficiary = member.IsCandidateBeneficiary,
                        BenefitsReceivedCount = member.BenefitsReceivedCount
                    });
                }

                HouseholdRecordsSummary = records.TotalHouseholdClaims == 0
                    ? "This household has not received any benefits yet."
                    : $"This household has received {records.TotalHouseholdClaims} benefit{(records.TotalHouseholdClaims == 1 ? "" : "s")} in total across all projects.";
            }
            catch (Exception ex)
            {
                HouseholdRecordsSummary = $"Unable to load household records: {ex.Message}";
            }
        }

        /// <summary>One-line demographics summary from the CRS cache, or a hint when nothing is cached yet.</summary>
        private static string FormatDemographicsLine(CrsDemographicsCache? demographics)
        {
            if (demographics == null)
            {
                return "No CRS demographics cached yet — refresh the masterlist while online.";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(demographics.MaritalStatus))
            {
                parts.Add($"Marital status: {demographics.MaritalStatus}");
            }
            if (!string.IsNullOrWhiteSpace(demographics.Ethnicity))
            {
                parts.Add($"Ethnicity: {demographics.Ethnicity}");
            }
            if (!string.IsNullOrWhiteSpace(demographics.Tribe))
            {
                parts.Add($"Tribe: {demographics.Tribe}");
            }

            return parts.Count > 0
                ? string.Join("  ·  ", parts)
                : "CRS demographics on file, but no marital status, ethnicity, or tribe recorded.";
        }

        /// <summary>Step 2: operator reviewed the household and confirmed — move the candidate into the selected list.</summary>
        private void ConfirmAddBeneficiary()
        {
            var candidate = _householdRecordsCandidate;
            if (candidate == null)
            {
                return;
            }

            if (_selectedEnrollmentStagingIds.Add(candidate.StagingId))
            {
                candidate.IsSelected = true;
                if (!SelectedEnrollmentBeneficiaries.Any(item => item.StagingId == candidate.StagingId))
                {
                    SelectedEnrollmentBeneficiaries.Add(candidate);
                }
                SelectedEnrollmentCount = _selectedEnrollmentStagingIds.Count;
            }

            CloseHouseholdRecords();
        }

        private void CloseHouseholdRecords()
        {
            IsHouseholdRecordsOpen = false;
            _householdRecordsCandidate = null;
            HouseholdRecordsMembers.Clear();
            OnPropertyChanged(nameof(HouseholdRecordsCandidateName));
        }

        private void RemoveSelectedBeneficiary(EnrollmentBeneficiaryOption? option)
        {
            if (option == null)
            {
                return;
            }

            _selectedEnrollmentStagingIds.Remove(option.StagingId);
            option.IsSelected = false;
            SelectedEnrollmentBeneficiaries.Remove(option);
            SelectedEnrollmentCount = _selectedEnrollmentStagingIds.Count;
        }

        private void ClearEnrollmentSelection()
        {
            foreach (var option in EnrollmentBeneficiaries)
            {
                option.PropertyChanged -= OnEnrollmentOptionPropertyChanged;
            }
            EnrollmentBeneficiaries.Clear();
            FilteredEnrollmentBeneficiaries.Clear();
            SelectedEnrollmentBeneficiaries.Clear();
            _selectedEnrollmentStagingIds.Clear();
            _enrollmentSearchText = string.Empty;
            OnPropertyChanged(nameof(EnrollmentSearchText));
            EnrollmentResultSummary = string.Empty;
            CurrentEnrollmentPage = 1;
            TotalEnrollmentPages = 1;
            SelectedEnrollmentCount = 0;
            IsBeneficiaryPickerOpen = false;
            CloseHouseholdRecords();
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
                await CheckFundIntegrityAsync();
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
        /// Monitoring probe (not a gate): flags earmarked envelopes whose ledger
        /// releases exceed the envelope — evidence of a past multi-PC race or a
        /// manual edit. Surfaces through the existing status line; never blocks loading.
        /// </summary>
        private async Task CheckFundIntegrityAsync()
        {
            try
            {
                await using var context = new LocalDbContext();
                var service = new BudgetManagementService(context);
                var issues = await service.GetOverdrawnEnvelopesAsync();
                var duplicates = await service.GetDuplicateLedgerReleaseKeysAsync();
                if (issues.Count == 0 && duplicates.Count == 0)
                {
                    return;
                }

                var parts = issues.Select(issue =>
                    $"{issue.EnvelopeKind} '{issue.EnvelopeName}' overdrawn by PHP {issue.OverdrawnBy:N2}").ToList();
                if (duplicates.Count > 0)
                {
                    parts.Add($"{duplicates.Count} duplicate ledger key(s): " + string.Join("; ", duplicates.Take(3).Select(d => $"{d.FeatureSource}:{d.SourceRecordId} x{d.Count}")));
                }

                SetErrorStatus($"FUND INTEGRITY: {string.Join("; ", parts)}.");
            }
            catch
            {
                // The probe must never break the Budget module.
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
                if (context.Database.ProviderName == "Pomelo.EntityFrameworkCore.MySql")
                {
                    // The ggms_project_cache mirror is local-only (ams.db) — nothing to
                    // sync into on a shared MySQL connection.
                    return;
                }

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

                if (IsOnboardingOpen && OnboardingStep == 2)
                {
                    OnboardingStep = 3;
                }
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

        public void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        }

        public void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
        }

        public void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BE123C"));
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

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class AssistanceValidatedBeneficiaryOption
    {
        public int Id { get; set; }
        public string? BeneficiaryId { get; set; }
        public string? CivilRegistryId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Barangay { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;

        public string DisplayText => string.IsNullOrWhiteSpace(CivilRegistryId)
            ? $"{FullName} (Brgy. {Barangay})"
            : $"{FullName} • CRN: {CivilRegistryId} (Brgy. {Barangay})";

        public static AssistanceValidatedBeneficiaryOption FromApprovedStaging(BeneficiaryStaging staging)
        {
            var first = staging.FirstName?.Trim() ?? string.Empty;
            var middle = staging.MiddleName?.Trim();
            var last = staging.LastName?.Trim() ?? string.Empty;
            var fullName = string.IsNullOrWhiteSpace(middle)
                ? $"{last}, {first}".Trim(',', ' ')
                : $"{last}, {first} {middle}".Trim(',', ' ');

            return new AssistanceValidatedBeneficiaryOption
            {
                Id = staging.StagingID,
                BeneficiaryId = staging.BeneficiaryId,
                CivilRegistryId = staging.CivilRegistryId,
                FullName = string.IsNullOrWhiteSpace(fullName) ? "Unknown Beneficiary" : fullName,
                Barangay = staging.Address ?? string.Empty,
                Address = staging.Address ?? string.Empty,
                ContactNumber = string.Empty
            };
        }
    }

    public sealed class AssistanceBudgetOption
    {
        public int Id { get; set; }
        public string BudgetCode { get; set; } = string.Empty;
        public string BudgetName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal? BudgetCap { get; set; }
        public int? TargetAyudaProgramId { get; set; }
        public int? TargetCashForWorkBudgetId { get; set; }

        public string DisplayText
        {
            get
            {
                var capStr = BudgetCap.HasValue && BudgetCap.Value > 0 ? $" - Cap: ₱{BudgetCap.Value:N2}" : string.Empty;
                var catStr = !string.IsNullOrWhiteSpace(Category) ? $" [{Category}]" : string.Empty;
                return $"{BudgetName} ({BudgetCode}){catStr}{capStr}";
            }
        }
    }

    public sealed class CitizenRequestItemViewModel : ObservableObject
    {
        private string _description = string.Empty;
        private AssistanceCasePriority _priority = AssistanceCasePriority.Medium;
        private AssistanceCaseStatus _status = AssistanceCaseStatus.Pending;
        private decimal? _approvedAmount;
        private decimal? _disbursedAmount;
        private string _budgetName = "General Municipal Assistance Fund";
        private string _budgetCode = "GLOBAL_AID_BUDGET";
        private string? _resolutionNotes;
        private string? _rejectionReason;
        private int? _budgetLedgerEntryId;
        private int? _assistanceCaseBudgetId;

        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string IntakeSource { get; set; } = "Walk-in";
        public string CitizenName { get; set; } = string.Empty;
        public string? CivilRegistryId { get; set; }
        public string? BeneficiaryId { get; set; }
        public string Barangay { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = "General Inquiry / Complaint";
        public string Subject { get; set; } = string.Empty;

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public AssistanceCasePriority Priority
        {
            get => _priority;
            set
            {
                if (SetProperty(ref _priority, value))
                {
                    OnPropertyChanged(nameof(PriorityLabel));
                    OnPropertyChanged(nameof(PriorityBadgeBg));
                    OnPropertyChanged(nameof(PriorityBadgeFg));
                }
            }
        }

        public AssistanceCaseStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusLabel));
                    OnPropertyChanged(nameof(StatusBadgeBg));
                    OnPropertyChanged(nameof(StatusBadgeFg));
                    OnPropertyChanged(nameof(IsDisbursed));
                }
            }
        }

        public AssistanceReleaseKind ReleaseKind { get; set; } = AssistanceReleaseKind.Cash;
        public decimal? RequestedAmount { get; set; }

        public decimal? ApprovedAmount
        {
            get => _approvedAmount;
            set => SetProperty(ref _approvedAmount, value);
        }

        public decimal? DisbursedAmount
        {
            get => _disbursedAmount;
            set
            {
                if (SetProperty(ref _disbursedAmount, value))
                {
                    OnPropertyChanged(nameof(IsDisbursed));
                }
            }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime RequestedOn { get; set; } = DateTime.Today;
        public string DepartmentName { get; set; } = "MSWDO";
        public string HandlerName { get; set; } = "Unassigned";

        public string BudgetName
        {
            get => _budgetName;
            set => SetProperty(ref _budgetName, value);
        }

        public string BudgetCode
        {
            get => _budgetCode;
            set => SetProperty(ref _budgetCode, value);
        }

        public string? ResolutionNotes
        {
            get => _resolutionNotes;
            set => SetProperty(ref _resolutionNotes, value);
        }

        public string? RejectionReason
        {
            get => _rejectionReason;
            set => SetProperty(ref _rejectionReason, value);
        }

        public int? BudgetLedgerEntryId
        {
            get => _budgetLedgerEntryId;
            set
            {
                if (SetProperty(ref _budgetLedgerEntryId, value))
                {
                    OnPropertyChanged(nameof(IsDisbursed));
                }
            }
        }

        public int? AssistanceCaseBudgetId
        {
            get => _assistanceCaseBudgetId;
            set => SetProperty(ref _assistanceCaseBudgetId, value);
        }

        public bool IsDisbursed => Status == AssistanceCaseStatus.Released || BudgetLedgerEntryId.HasValue || (DisbursedAmount.HasValue && DisbursedAmount.Value > 0);

        public string PriorityLabel => Priority switch
        {
            AssistanceCasePriority.Low => "Low",
            AssistanceCasePriority.Medium => "Normal",
            AssistanceCasePriority.High => "Urgent",
            AssistanceCasePriority.Critical => "Emergency",
            _ => Priority.ToString()
        };

        public string PriorityBadgeBg => Priority switch
        {
            AssistanceCasePriority.Low => "#DCFCE7",
            AssistanceCasePriority.Medium => "#E0F2FE",
            AssistanceCasePriority.High => "#FEF3C7",
            AssistanceCasePriority.Critical => "#FEE2E2",
            _ => "#F1F5F9"
        };

        public string PriorityBadgeFg => Priority switch
        {
            AssistanceCasePriority.Low => "#15803D",
            AssistanceCasePriority.Medium => "#0284C7",
            AssistanceCasePriority.High => "#B45309",
            AssistanceCasePriority.Critical => "#BE123C",
            _ => "#475569"
        };

        public string StatusLabel => Status switch
        {
            AssistanceCaseStatus.Pending => "Pending Triage",
            AssistanceCaseStatus.UnderReview => "In Review / Processing",
            AssistanceCaseStatus.Approved => "Approved / Endorsed",
            AssistanceCaseStatus.Released => "Assistance Disbursed",
            AssistanceCaseStatus.Closed => "Resolved & Closed",
            AssistanceCaseStatus.Rejected => "Declined / Ineligible",
            AssistanceCaseStatus.Cancelled => "Cancelled",
            _ => Status.ToString()
        };

        public string StatusBadgeBg => Status switch
        {
            AssistanceCaseStatus.Pending => "#FEF3C7",
            AssistanceCaseStatus.UnderReview => "#E0F2FE",
            AssistanceCaseStatus.Approved => "#E0E7FF",
            AssistanceCaseStatus.Released => "#DCFCE7",
            AssistanceCaseStatus.Closed => "#F1F5F9",
            AssistanceCaseStatus.Rejected => "#FEE2E2",
            _ => "#F1F5F9"
        };

        public string StatusBadgeFg => Status switch
        {
            AssistanceCaseStatus.Pending => "#B45309",
            AssistanceCaseStatus.UnderReview => "#0369A1",
            AssistanceCaseStatus.Approved => "#4338CA",
            AssistanceCaseStatus.Released => "#15803D",
            AssistanceCaseStatus.Closed => "#475569",
            AssistanceCaseStatus.Rejected => "#BE123C",
            _ => "#475569"
        };
    }

    public sealed class AssistanceCaseManagementViewModel : ObservableObject
    {
        private static readonly string[] SulopBarangays = new[]
        {
            "Balasinon", "Buguis", "Carre", "Clib", "Harada Butai",
            "Katipunan", "Kiblagon", "Labon", "Laperas", "Lapla",
            "Litos", "Luparan", "Mckinley", "New Cebu", "Osmeña",
            "Palili", "Parami", "Poblacion", "Roxas", "Solongvale",
            "Tagolilong", "Talao", "Talas", "Tanwalang", "Waterfall"
        };

        private readonly User _currentUser;
        private readonly LocalDbContext _context;
        private readonly AssistanceCaseManagementService _caseService;
        private readonly AuditService _auditService;

        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "ALL";
        private string _selectedCategoryFilter = "ALL";
        private string _selectedPriorityFilter = "ALL";
        private string _selectedDateFilter = "ALL";

        private int _totalCount;
        private int _pendingTriageCount;
        private int _inFlightCount;
        private int _resolvedCount;

        private int _currentPage = 1;
        private int _pageSize = 15;
        private int _totalFilteredCount;

        // Intake Modal State
        private bool _isIntakeModalOpen;
        private string _beneficiarySearchText = string.Empty;
        private bool _isBeneficiarySearching;
        private AssistanceValidatedBeneficiaryOption? _selectedBeneficiaryOption;
        private bool _isUnregisteredResident;

        private string _intakeCitizenName = string.Empty;
        private string _intakeCivilRegistryId = string.Empty;
        private string _intakeBarangay = string.Empty;
        private string _intakeContactNumber = string.Empty;
        private string _intakeAddress = string.Empty;
        private string _intakeCategory = "Social Assistance";
        private AssistanceCasePriority _intakePriority = AssistanceCasePriority.Medium;
        private string _intakeDepartment = "MSWDO";
        private string _intakeSubject = string.Empty;
        private string _intakeDescription = string.Empty;
        private AssistanceReleaseKind _intakeReleaseKind = AssistanceReleaseKind.Cash;
        private string _intakeAmount = string.Empty;
        private string _intakeGoodsDescription = string.Empty;

        // Inspect & Action Modal State
        private bool _isInspectModalOpen;
        private CitizenRequestItemViewModel? _selectedRequest;

        private bool _isRejectModalOpen;
        private string _rejectionReasonText = string.Empty;

        private bool _isResolveModalOpen;
        private string _resolutionClassification = "Assistance Released";
        private string _resolutionSummary = string.Empty;

        private bool _isInternalNoteModalOpen;
        private string _internalNoteText = string.Empty;

        private bool _isDisburseModalOpen;
        private string _disburseAmountText = string.Empty;
        private string _disburseRemarks = string.Empty;

        // Toast Status
        private string _statusMessage = string.Empty;
        private string _statusType = "Success";
        private bool _isStatusVisible;

        public ObservableCollection<CitizenRequestItemViewModel> Requests { get; } = new();
        public ObservableCollection<CitizenRequestItemViewModel> PagedRequests { get; } = new();
        public ObservableCollection<AssistanceValidatedBeneficiaryOption> BeneficiarySearchResults { get; } = new();

        public ObservableCollection<string> StatusFilters { get; } = new()
        {
            "ALL", "Pending Triage", "In Review / Processing", "Approved / Endorsed", "Assistance Disbursed", "Resolved & Closed", "Declined / Ineligible"
        };

        public ObservableCollection<string> CategoryFilters { get; } = new()
        {
            "ALL", "Social Assistance", "Legal / Notarial Aid", "Medical / Burial Endorsement", "Document Issuance / Clearance", "General Inquiry / Complaint"
        };

        public ObservableCollection<string> DepartmentOptions { get; } = new()
        {
            "MSWDO", "Mayor's Office", "Municipal Health Office", "Legal Services", "General Services / Admin"
        };

        public ObservableCollection<string> Barangays { get; } = new(SulopBarangays);

        public RelayCommand SearchCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand OpenIntakeModalCommand { get; }
        public RelayCommand CloseIntakeModalCommand { get; }
        public RelayCommand SearchBeneficiariesCommand { get; }
        public RelayCommand SelectBeneficiaryOptionCommand { get; }
        public RelayCommand ClearBeneficiarySelectionCommand { get; }
        public RelayCommand SubmitIntakeCommand { get; }

        public RelayCommand InspectRequestCommand { get; }
        public RelayCommand CloseInspectModalCommand { get; }
        public RelayCommand MarkInReviewCommand { get; }
        public RelayCommand EndorseDepartmentCommand { get; }
        public RelayCommand StartProcessingCommand { get; }

        public RelayCommand OpenResolveModalCommand { get; }
        public RelayCommand CloseResolveModalCommand { get; }
        public RelayCommand ConfirmResolveCommand { get; }

        public RelayCommand OpenRejectModalCommand { get; }
        public RelayCommand CloseRejectModalCommand { get; }
        public RelayCommand ConfirmRejectCommand { get; }

        public RelayCommand OpenDisburseModalCommand { get; }
        public RelayCommand CloseDisburseModalCommand { get; }
        public RelayCommand ConfirmDisburseCommand { get; }

        public RelayCommand OpenInternalNoteModalCommand { get; }
        public RelayCommand CloseInternalNoteModalCommand { get; }
        public RelayCommand SaveInternalNoteCommand { get; }

        public RelayCommand DismissStatusCommand { get; }

        public AssistanceCaseManagementViewModel(User currentUser, LocalDbContext context)
        {
            _currentUser = currentUser;
            _context = context;
            _auditService = new AuditService(context);
            _caseService = new AssistanceCaseManagementService(
                context,
                _auditService,
                new GgmsConsolidatedTransactionService());

            SearchCommand = new RelayCommand(_ => FilterAndPaginate());
            NextPageCommand = new RelayCommand(_ => GoToPage(CurrentPage + 1), _ => CanGoNext);
            PreviousPageCommand = new RelayCommand(_ => GoToPage(CurrentPage - 1), _ => CanGoPrevious);

            OpenIntakeModalCommand = new RelayCommand(_ => OpenIntakeModal());
            CloseIntakeModalCommand = new RelayCommand(_ => IsIntakeModalOpen = false);
            SearchBeneficiariesCommand = new RelayCommand(async _ => await SearchBeneficiariesAsync());
            SelectBeneficiaryOptionCommand = new RelayCommand(param => SelectBeneficiaryOption(param as AssistanceValidatedBeneficiaryOption));
            ClearBeneficiarySelectionCommand = new RelayCommand(_ => ClearBeneficiarySelection());
            SubmitIntakeCommand = new RelayCommand(async _ => await SubmitIntakeAsync(), _ => CanSubmitIntake());

            InspectRequestCommand = new RelayCommand(param => InspectRequest(param as CitizenRequestItemViewModel));
            CloseInspectModalCommand = new RelayCommand(_ => IsInspectModalOpen = false);

            MarkInReviewCommand = new RelayCommand(async _ => await ChangeSelectedStatusAsync(AssistanceCaseStatus.UnderReview, "Accepted for triage and review."), _ => CanMarkInReview);
            EndorseDepartmentCommand = new RelayCommand(async _ => await ChangeSelectedStatusAsync(AssistanceCaseStatus.UnderReview, $"Endorsed to {SelectedRequest?.DepartmentName ?? "department"}."), _ => CanEndorse);
            StartProcessingCommand = new RelayCommand(async _ => await ChangeSelectedStatusAsync(AssistanceCaseStatus.Approved, "Approved and moving to processing."), _ => CanStartProcessing);

            OpenResolveModalCommand = new RelayCommand(_ => { if (CanResolve) { IsResolveModalOpen = true; ResolutionSummary = string.Empty; } }, _ => CanResolve);
            CloseResolveModalCommand = new RelayCommand(_ => IsResolveModalOpen = false);
            ConfirmResolveCommand = new RelayCommand(async _ => await ConfirmResolveAsync());

            OpenRejectModalCommand = new RelayCommand(_ => { if (CanReject) { IsRejectModalOpen = true; RejectionReasonText = string.Empty; } }, _ => CanReject);
            CloseRejectModalCommand = new RelayCommand(_ => IsRejectModalOpen = false);
            ConfirmRejectCommand = new RelayCommand(async _ => await ConfirmRejectAsync());

            OpenDisburseModalCommand = new RelayCommand(_ => OpenDisburseModal(), _ => CanDisburse);
            CloseDisburseModalCommand = new RelayCommand(_ => IsDisburseModalOpen = false);
            ConfirmDisburseCommand = new RelayCommand(async _ => await ConfirmDisburseAsync());

            OpenInternalNoteModalCommand = new RelayCommand(_ => { if (CanAddInternalNote) { IsInternalNoteModalOpen = true; InternalNoteText = string.Empty; } }, _ => CanAddInternalNote);
            CloseInternalNoteModalCommand = new RelayCommand(_ => IsInternalNoteModalOpen = false);
            SaveInternalNoteCommand = new RelayCommand(async _ => await SaveInternalNoteAsync());

            DismissStatusCommand = new RelayCommand(_ => IsStatusVisible = false);

            _ = LoadRequestsAsync();
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _currentPage = 1;
                    FilterAndPaginate();
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
                    _currentPage = 1;
                    FilterAndPaginate();
                }
            }
        }

        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (SetProperty(ref _selectedCategoryFilter, value))
                {
                    _currentPage = 1;
                    FilterAndPaginate();
                }
            }
        }

        public string SelectedPriorityFilter
        {
            get => _selectedPriorityFilter;
            set
            {
                if (SetProperty(ref _selectedPriorityFilter, value))
                {
                    _currentPage = 1;
                    FilterAndPaginate();
                }
            }
        }

        public string SelectedDateFilter
        {
            get => _selectedDateFilter;
            set
            {
                if (SetProperty(ref _selectedDateFilter, value))
                {
                    _currentPage = 1;
                    FilterAndPaginate();
                }
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        public int PendingTriageCount
        {
            get => _pendingTriageCount;
            private set => SetProperty(ref _pendingTriageCount, value);
        }

        public int InFlightCount
        {
            get => _inFlightCount;
            private set => SetProperty(ref _inFlightCount, value);
        }

        public int ResolvedCount
        {
            get => _resolvedCount;
            private set => SetProperty(ref _resolvedCount, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoPrevious));
                }
            }
        }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalFilteredCount / (double)_pageSize));
        public string PageInfo => $"Page {CurrentPage} of {TotalPages} ({TotalFilteredCount} records)";
        public bool CanGoNext => CurrentPage < TotalPages;
        public bool CanGoPrevious => CurrentPage > 1;

        public int TotalFilteredCount
        {
            get => _totalFilteredCount;
            private set
            {
                if (SetProperty(ref _totalFilteredCount, value))
                {
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoPrevious));
                }
            }
        }

        // Intake Modal Properties
        public bool IsIntakeModalOpen
        {
            get => _isIntakeModalOpen;
            set => SetProperty(ref _isIntakeModalOpen, value);
        }

        public string BeneficiarySearchText
        {
            get => _beneficiarySearchText;
            set
            {
                if (SetProperty(ref _beneficiarySearchText, value))
                {
                    _ = SearchBeneficiariesAsync();
                }
            }
        }

        public bool IsBeneficiarySearching
        {
            get => _isBeneficiarySearching;
            set => SetProperty(ref _isBeneficiarySearching, value);
        }

        public AssistanceValidatedBeneficiaryOption? SelectedBeneficiaryOption
        {
            get => _selectedBeneficiaryOption;
            set
            {
                if (SetProperty(ref _selectedBeneficiaryOption, value))
                {
                    OnPropertyChanged(nameof(IsBeneficiarySelected));
                    SubmitIntakeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsBeneficiarySelected => SelectedBeneficiaryOption != null;

        public bool IsUnregisteredResident
        {
            get => _isUnregisteredResident;
            set
            {
                if (SetProperty(ref _isUnregisteredResident, value))
                {
                    SubmitIntakeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string IntakeCitizenName
        {
            get => _intakeCitizenName;
            set
            {
                if (SetProperty(ref _intakeCitizenName, value))
                    SubmitIntakeCommand.RaiseCanExecuteChanged();
            }
        }

        public string IntakeCivilRegistryId
        {
            get => _intakeCivilRegistryId;
            set => SetProperty(ref _intakeCivilRegistryId, value);
        }

        public string IntakeBarangay
        {
            get => _intakeBarangay;
            set
            {
                if (SetProperty(ref _intakeBarangay, value))
                    SubmitIntakeCommand.RaiseCanExecuteChanged();
            }
        }

        public string IntakeContactNumber
        {
            get => _intakeContactNumber;
            set => SetProperty(ref _intakeContactNumber, value);
        }

        public string IntakeAddress
        {
            get => _intakeAddress;
            set => SetProperty(ref _intakeAddress, value);
        }

        public string IntakeCategory
        {
            get => _intakeCategory;
            set => SetProperty(ref _intakeCategory, value);
        }

        public AssistanceCasePriority IntakePriority
        {
            get => _intakePriority;
            set => SetProperty(ref _intakePriority, value);
        }

        public IEnumerable<AssistanceCasePriority> Priorities => Enum.GetValues<AssistanceCasePriority>();

        public string IntakeDepartment
        {
            get => _intakeDepartment;
            set => SetProperty(ref _intakeDepartment, value);
        }

        public string IntakeSubject
        {
            get => _intakeSubject;
            set
            {
                if (SetProperty(ref _intakeSubject, value))
                    SubmitIntakeCommand.RaiseCanExecuteChanged();
            }
        }

        public string IntakeDescription
        {
            get => _intakeDescription;
            set
            {
                if (SetProperty(ref _intakeDescription, value))
                    SubmitIntakeCommand.RaiseCanExecuteChanged();
            }
        }

        public AssistanceReleaseKind IntakeReleaseKind
        {
            get => _intakeReleaseKind;
            set
            {
                if (SetProperty(ref _intakeReleaseKind, value))
                {
                    OnPropertyChanged(nameof(IsCashReleaseKind));
                    OnPropertyChanged(nameof(IsGoodsReleaseKind));
                }
            }
        }

        public bool IsCashReleaseKind
        {
            get => IntakeReleaseKind == AssistanceReleaseKind.Cash;
            set
            {
                if (value)
                {
                    IntakeReleaseKind = AssistanceReleaseKind.Cash;
                }
            }
        }

        public bool IsGoodsReleaseKind
        {
            get => IntakeReleaseKind == AssistanceReleaseKind.Goods;
            set
            {
                if (value)
                {
                    IntakeReleaseKind = AssistanceReleaseKind.Goods;
                }
            }
        }

        public string IntakeAmount
        {
            get => _intakeAmount;
            set => SetProperty(ref _intakeAmount, value);
        }

        public string IntakeGoodsDescription
        {
            get => _intakeGoodsDescription;
            set => SetProperty(ref _intakeGoodsDescription, value);
        }

        // Inspect Modal Properties
        public bool IsInspectModalOpen
        {
            get => _isInspectModalOpen;
            set => SetProperty(ref _isInspectModalOpen, value);
        }

        public CitizenRequestItemViewModel? SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                if (SetProperty(ref _selectedRequest, value))
                {
                    NotifySelectedRequestStateChanged();
                }
            }
        }

        public bool CanMarkInReview => SelectedRequest?.Status == AssistanceCaseStatus.Pending;
        public bool CanEndorse => SelectedRequest?.Status == AssistanceCaseStatus.Pending || SelectedRequest?.Status == AssistanceCaseStatus.UnderReview;
        public bool CanStartProcessing => SelectedRequest?.Status == AssistanceCaseStatus.UnderReview;
        public bool CanResolve => SelectedRequest != null && (SelectedRequest.Status == AssistanceCaseStatus.Approved || SelectedRequest.Status == AssistanceCaseStatus.UnderReview);
        public bool CanReject => SelectedRequest != null 
            && SelectedRequest.Status != AssistanceCaseStatus.Rejected 
            && SelectedRequest.Status != AssistanceCaseStatus.Closed 
            && SelectedRequest.Status != AssistanceCaseStatus.Released 
            && SelectedRequest.Status != AssistanceCaseStatus.Cancelled;
        public bool CanDisburse => SelectedRequest != null 
            && !SelectedRequest.IsDisbursed 
            && SelectedRequest.Status != AssistanceCaseStatus.Closed 
            && SelectedRequest.Status != AssistanceCaseStatus.Released 
            && SelectedRequest.Status != AssistanceCaseStatus.Rejected 
            && SelectedRequest.Status != AssistanceCaseStatus.Cancelled;
        public bool CanAddInternalNote => SelectedRequest != null 
            && SelectedRequest.Status != AssistanceCaseStatus.Closed 
            && SelectedRequest.Status != AssistanceCaseStatus.Rejected 
            && SelectedRequest.Status != AssistanceCaseStatus.Cancelled;
        public bool IsRequestClosed => SelectedRequest?.Status == AssistanceCaseStatus.Closed;
        public bool IsRequestRejected => SelectedRequest?.Status == AssistanceCaseStatus.Rejected;

        public void NotifySelectedRequestStateChanged()
        {
            OnPropertyChanged(nameof(CanMarkInReview));
            OnPropertyChanged(nameof(CanEndorse));
            OnPropertyChanged(nameof(CanStartProcessing));
            OnPropertyChanged(nameof(CanResolve));
            OnPropertyChanged(nameof(CanReject));
            OnPropertyChanged(nameof(CanDisburse));
            OnPropertyChanged(nameof(CanAddInternalNote));
            OnPropertyChanged(nameof(IsRequestClosed));
            OnPropertyChanged(nameof(IsRequestRejected));

            MarkInReviewCommand?.RaiseCanExecuteChanged();
            EndorseDepartmentCommand?.RaiseCanExecuteChanged();
            StartProcessingCommand?.RaiseCanExecuteChanged();
            OpenResolveModalCommand?.RaiseCanExecuteChanged();
            OpenRejectModalCommand?.RaiseCanExecuteChanged();
            OpenDisburseModalCommand?.RaiseCanExecuteChanged();
            OpenInternalNoteModalCommand?.RaiseCanExecuteChanged();
        }

        // Sub-modal Properties
        public bool IsRejectModalOpen
        {
            get => _isRejectModalOpen;
            set => SetProperty(ref _isRejectModalOpen, value);
        }

        public string RejectionReasonText
        {
            get => _rejectionReasonText;
            set => SetProperty(ref _rejectionReasonText, value);
        }

        public bool IsResolveModalOpen
        {
            get => _isResolveModalOpen;
            set => SetProperty(ref _isResolveModalOpen, value);
        }

        public string ResolutionClassification
        {
            get => _resolutionClassification;
            set => SetProperty(ref _resolutionClassification, value);
        }

        public string ResolutionSummary
        {
            get => _resolutionSummary;
            set => SetProperty(ref _resolutionSummary, value);
        }

        public bool IsInternalNoteModalOpen
        {
            get => _isInternalNoteModalOpen;
            set => SetProperty(ref _isInternalNoteModalOpen, value);
        }

        public string InternalNoteText
        {
            get => _internalNoteText;
            set => SetProperty(ref _internalNoteText, value);
        }

        public bool IsDisburseModalOpen
        {
            get => _isDisburseModalOpen;
            set => SetProperty(ref _isDisburseModalOpen, value);
        }

        public string DisburseAmountText
        {
            get => _disburseAmountText;
            set => SetProperty(ref _disburseAmountText, value);
        }

        public string DisburseRemarks
        {
            get => _disburseRemarks;
            set => SetProperty(ref _disburseRemarks, value);
        }

        public ObservableCollection<AssistanceBudgetOption> AvailableBudgets { get; } = new();

        private AssistanceBudgetOption? _selectedDisburseBudget;
        public AssistanceBudgetOption? SelectedDisburseBudget
        {
            get => _selectedDisburseBudget;
            set => SetProperty(ref _selectedDisburseBudget, value);
        }

        private AssistanceBudgetOption? _selectedIntakeBudget;
        public AssistanceBudgetOption? SelectedIntakeBudget
        {
            get => _selectedIntakeBudget;
            set => SetProperty(ref _selectedIntakeBudget, value);
        }

        // Status Toast
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

        // --- Core Data Methods ---

        public async Task LoadRequestsAsync()
        {
            IsLoading = true;
            try
            {
                await LoadBudgetsAsync();

                var cases = await _context.AssistanceCases
                    .AsNoTracking()
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                Requests.Clear();
                foreach (var c in cases)
                {
                    var item = MapCaseToViewModel(c);
                    Requests.Add(item);
                }

                UpdateMetrics();
                FilterAndPaginate();
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load citizen requests: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadBudgetsAsync()
        {
            try
            {
                var acBudgets = await _context.AssistanceCaseBudgets.ToListAsync();
                var cfwBudgets = await _context.CashForWorkBudgets.AsNoTracking().Where(b => b.IsActive).ToListAsync();
                var ayudaPrograms = await _context.AyudaPrograms.AsNoTracking().Where(p => p.IsActive).ToListAsync();
                var donations = await _context.PrivateDonations.AsNoTracking().ToListAsync();
                var snapshots = await _context.GovernmentBudgetSnapshots.AsNoTracking().ToListAsync();
                var ggmsProjects = await _context.GgmsProjectCache.AsNoTracking().ToListAsync();

                bool hasAnyRealBudget = acBudgets.Any(b => b.BudgetCode != "GLOBAL_AID_BUDGET" && b.IsActive)
                                        || cfwBudgets.Any()
                                        || ayudaPrograms.Any()
                                        || donations.Any()
                                        || snapshots.Any()
                                        || ggmsProjects.Any();

                // Deactivate the dummy/fallback General Municipal Assistance Fund if other real budgets exist
                var dummyBudget = acBudgets.FirstOrDefault(b => b.BudgetCode == "GLOBAL_AID_BUDGET");
                if (dummyBudget != null && hasAnyRealBudget)
                {
                    dummyBudget.IsActive = false;
                    dummyBudget.UpdatedAt = DateTime.Now;
                }

                // Sync CashForWorkBudgets into AssistanceCaseBudgets
                foreach (var cfw in cfwBudgets)
                {
                    var existing = acBudgets.FirstOrDefault(b => b.BudgetCode == cfw.BudgetCode);
                    var isSeminar = cfw.BudgetCode.StartsWith("SEM-", StringComparison.OrdinalIgnoreCase);
                    var category = isSeminar ? "Seminar Project" : "Cash for Work Project";
                    if (existing == null)
                    {
                        existing = new AssistanceCaseBudget
                        {
                            BudgetCode = cfw.BudgetCode,
                            BudgetName = cfw.BudgetName,
                            Description = cfw.Description,
                            AssistanceType = category,
                            BudgetCap = cfw.BudgetCap,
                            IsActive = cfw.IsActive,
                            CreatedByUserId = _currentUser.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(existing);
                        acBudgets.Add(existing);
                    }
                    else
                    {
                        existing.BudgetName = cfw.BudgetName;
                        existing.BudgetCap = cfw.BudgetCap;
                        existing.AssistanceType = category;
                        existing.IsActive = cfw.IsActive;
                        existing.UpdatedAt = DateTime.Now;
                    }
                }

                // Sync AyudaPrograms (Distribution Projects) into AssistanceCaseBudgets
                foreach (var ayuda in ayudaPrograms)
                {
                    var existing = acBudgets.FirstOrDefault(b => b.BudgetCode == ayuda.ProgramCode);
                    if (existing == null)
                    {
                        existing = new AssistanceCaseBudget
                        {
                            BudgetCode = ayuda.ProgramCode,
                            BudgetName = ayuda.ProgramName,
                            Description = ayuda.Description,
                            AssistanceType = "Distribution Project",
                            BudgetCap = ayuda.BudgetCap,
                            IsActive = ayuda.IsActive,
                            CreatedByUserId = _currentUser.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(existing);
                        acBudgets.Add(existing);
                    }
                    else
                    {
                        existing.BudgetName = ayuda.ProgramName;
                        existing.BudgetCap = ayuda.BudgetCap;
                        existing.AssistanceType = "Distribution Project";
                        existing.IsActive = ayuda.IsActive;
                        existing.UpdatedAt = DateTime.Now;
                    }
                }

                // Sync PrivateDonations into AssistanceCaseBudgets
                foreach (var don in donations)
                {
                    var donCode = $"DON-{don.Id}";
                    var existing = acBudgets.FirstOrDefault(b => b.BudgetCode == donCode);
                    var donName = string.IsNullOrWhiteSpace(don.DonorName) ? $"Donation #{don.Id}" : don.DonorName;
                    if (existing == null)
                    {
                        existing = new AssistanceCaseBudget
                        {
                            BudgetCode = donCode,
                            BudgetName = donName,
                            Description = don.Remarks,
                            AssistanceType = "Private Donation",
                            BudgetCap = don.Amount,
                            IsActive = true,
                            CreatedByUserId = _currentUser.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(existing);
                        acBudgets.Add(existing);
                    }
                    else
                    {
                        existing.BudgetName = donName;
                        existing.BudgetCap = don.Amount;
                        existing.AssistanceType = "Private Donation";
                        existing.IsActive = true;
                        existing.UpdatedAt = DateTime.Now;
                    }
                }

                // Sync GovernmentBudgetSnapshots into AssistanceCaseBudgets
                foreach (var snap in snapshots)
                {
                    var snapCode = !string.IsNullOrWhiteSpace(snap.OfficeCode) ? $"GOV-{snap.OfficeCode}" : $"GOV-{snap.Id}";
                    var existing = acBudgets.FirstOrDefault(b => b.BudgetCode == snapCode);
                    var snapName = !string.IsNullOrWhiteSpace(snap.OfficeName) ? snap.OfficeName : snapCode;
                    if (existing == null)
                    {
                        existing = new AssistanceCaseBudget
                        {
                            BudgetCode = snapCode,
                            BudgetName = snapName,
                            Description = $"Government fund allocation for {snapName}",
                            AssistanceType = "Government Fund",
                            BudgetCap = snap.AllocatedAmount,
                            IsActive = true,
                            CreatedByUserId = _currentUser.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(existing);
                        acBudgets.Add(existing);
                    }
                    else
                    {
                        existing.BudgetName = snapName;
                        existing.BudgetCap = snap.AllocatedAmount;
                        existing.AssistanceType = "Government Fund";
                        existing.IsActive = true;
                        existing.UpdatedAt = DateTime.Now;
                    }
                }

                // Sync GgmsProjectCache into AssistanceCaseBudgets
                foreach (var ggms in ggmsProjects)
                {
                    var ggmsCode = $"GGMS-{ggms.ProjectDetailsId}";
                    var existing = acBudgets.FirstOrDefault(b => b.BudgetCode == ggmsCode);
                    var ggmsName = !string.IsNullOrWhiteSpace(ggms.ProjectName) ? ggms.ProjectName : ggmsCode;
                    if (existing == null)
                    {
                        existing = new AssistanceCaseBudget
                        {
                            BudgetCode = ggmsCode,
                            BudgetName = ggmsName,
                            Description = ggms.Description,
                            AssistanceType = "GGMS Project",
                            BudgetCap = ggms.TotalBudget,
                            IsActive = true,
                            CreatedByUserId = _currentUser.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(existing);
                        acBudgets.Add(existing);
                    }
                    else
                    {
                        existing.BudgetName = ggmsName;
                        existing.BudgetCap = ggms.TotalBudget;
                        existing.AssistanceType = "GGMS Project";
                        existing.IsActive = true;
                        existing.UpdatedAt = DateTime.Now;
                    }
                }

                // Fallback only if absolutely no active budget exists anywhere
                if (!acBudgets.Any(b => b.IsActive))
                {
                    var defaultBudget = acBudgets.FirstOrDefault(b => b.BudgetCode == "GLOBAL_AID_BUDGET");
                    if (defaultBudget != null)
                    {
                        defaultBudget.BudgetName = "Global Aid Request Budget";
                        defaultBudget.IsActive = true;
                        defaultBudget.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        defaultBudget = new AssistanceCaseBudget
                        {
                            BudgetCode = "GLOBAL_AID_BUDGET",
                            BudgetName = "Global Aid Request Budget",
                            Description = "Default municipal frontline aid fund pool",
                            AssistanceType = "General Aid",
                            BudgetCap = null,
                            IsActive = true,
                            CreatedByUserId = _currentUser.Id,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.AssistanceCaseBudgets.Add(defaultBudget);
                        acBudgets.Add(defaultBudget);
                    }
                }

                if (_context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                }

                AvailableBudgets.Clear();
                foreach (var b in acBudgets.Where(b => b.IsActive).OrderBy(b => b.BudgetName))
                {
                    var cat = b.AssistanceType ?? "Aid Budget";
                    if (b.BudgetCode == "GLOBAL_AID_BUDGET") cat = "Global Aid Cap";

                    var linkedAyuda = ayudaPrograms.FirstOrDefault(p => p.ProgramCode == b.BudgetCode);
                    var linkedCfw = cfwBudgets.FirstOrDefault(c => c.BudgetCode == b.BudgetCode);

                    AvailableBudgets.Add(new AssistanceBudgetOption
                    {
                        Id = b.Id,
                        BudgetCode = b.BudgetCode,
                        BudgetName = b.BudgetName,
                        Category = cat,
                        BudgetCap = b.BudgetCap,
                        TargetAyudaProgramId = linkedAyuda?.Id,
                        TargetCashForWorkBudgetId = linkedCfw?.Id
                    });
                }

                if (SelectedDisburseBudget == null || !AvailableBudgets.Any(b => b.Id == SelectedDisburseBudget.Id))
                {
                    SelectedDisburseBudget = AvailableBudgets.FirstOrDefault();
                }

                if (SelectedIntakeBudget == null || !AvailableBudgets.Any(b => b.Id == SelectedIntakeBudget.Id))
                {
                    SelectedIntakeBudget = AvailableBudgets.FirstOrDefault();
                }
            }
            catch
            {
                // Fallback graceful
            }
        }

        private void UpdateMetrics()
        {
            TotalCount = Requests.Count;
            PendingTriageCount = Requests.Count(r => r.Status == AssistanceCaseStatus.Pending);
            InFlightCount = Requests.Count(r => r.Status == AssistanceCaseStatus.UnderReview || r.Status == AssistanceCaseStatus.Approved);
            ResolvedCount = Requests.Count(r => r.Status == AssistanceCaseStatus.Released || r.Status == AssistanceCaseStatus.Closed);
        }

        private void FilterAndPaginate()
        {
            var query = Requests.AsEnumerable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLowerInvariant();
                query = query.Where(r =>
                    r.TicketNumber.ToLowerInvariant().Contains(term) ||
                    r.CitizenName.ToLowerInvariant().Contains(term) ||
                    (!string.IsNullOrEmpty(r.CivilRegistryId) && r.CivilRegistryId.ToLowerInvariant().Contains(term)) ||
                    r.Subject.ToLowerInvariant().Contains(term) ||
                    r.Barangay.ToLowerInvariant().Contains(term));
            }

            // Status filter
            if (SelectedStatusFilter != "ALL")
            {
                query = SelectedStatusFilter switch
                {
                    "Pending Triage" => query.Where(r => r.Status == AssistanceCaseStatus.Pending),
                    "In Review / Processing" => query.Where(r => r.Status == AssistanceCaseStatus.UnderReview),
                    "Approved / Endorsed" => query.Where(r => r.Status == AssistanceCaseStatus.Approved),
                    "Assistance Disbursed" => query.Where(r => r.Status == AssistanceCaseStatus.Released),
                    "Resolved & Closed" => query.Where(r => r.Status == AssistanceCaseStatus.Closed),
                    "Declined / Ineligible" => query.Where(r => r.Status == AssistanceCaseStatus.Rejected),
                    _ => query
                };
            }

            // Category filter
            if (SelectedCategoryFilter != "ALL")
            {
                query = query.Where(r => string.Equals(r.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Priority filter
            if (SelectedPriorityFilter != "ALL")
            {
                query = SelectedPriorityFilter switch
                {
                    "Low" => query.Where(r => r.Priority == AssistanceCasePriority.Low),
                    "Medium" => query.Where(r => r.Priority == AssistanceCasePriority.Medium),
                    "High" => query.Where(r => r.Priority == AssistanceCasePriority.High),
                    "Critical" => query.Where(r => r.Priority == AssistanceCasePriority.Critical),
                    _ => query
                };
            }

            // Date filter
            if (SelectedDateFilter == "TODAY")
            {
                var today = DateTime.Today;
                query = query.Where(r => r.CreatedAt.Date == today);
            }
            else if (SelectedDateFilter == "THIS_WEEK")
            {
                var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                query = query.Where(r => r.CreatedAt.Date >= startOfWeek);
            }

            var filteredList = query.ToList();
            TotalFilteredCount = filteredList.Count;

            // Ensure CurrentPage valid
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = filteredList
                .Skip((CurrentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            PagedRequests.Clear();
            foreach (var item in paged)
            {
                PagedRequests.Add(item);
            }

            NextPageCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();
        }

        private void GoToPage(int page)
        {
            if (page >= 1 && page <= TotalPages)
            {
                CurrentPage = page;
                FilterAndPaginate();
            }
        }

        // --- Intake Methods ---

        private void OpenIntakeModal()
        {
            SelectedBeneficiaryOption = null;
            IsUnregisteredResident = false;
            BeneficiarySearchText = string.Empty;
            BeneficiarySearchResults.Clear();

            IntakeCitizenName = string.Empty;
            IntakeCivilRegistryId = string.Empty;
            IntakeBarangay = SulopBarangays.FirstOrDefault() ?? "Poblacion";
            IntakeContactNumber = string.Empty;
            IntakeAddress = string.Empty;
            IntakeCategory = "Social Assistance";
            IntakePriority = AssistanceCasePriority.Medium;
            IntakeDepartment = "MSWDO";
            IntakeSubject = string.Empty;
            IntakeDescription = string.Empty;
            IntakeReleaseKind = AssistanceReleaseKind.Cash;
            IntakeAmount = string.Empty;
            IntakeGoodsDescription = string.Empty;
            SelectedIntakeBudget = AvailableBudgets.FirstOrDefault();

            IsIntakeModalOpen = true;
        }

        public async Task SearchBeneficiariesAsync()
        {
            if (string.IsNullOrWhiteSpace(BeneficiarySearchText) || BeneficiarySearchText.Trim().Length < 2)
            {
                BeneficiarySearchResults.Clear();
                return;
            }

            IsBeneficiarySearching = true;
            try
            {
                var term = BeneficiarySearchText.Trim().ToLowerInvariant();
                
                // Requirement test check: context.BeneficiaryStaging with VerificationStatus.Approved and FromApprovedStaging
                var stagingMatches = await _context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(item => item.VerificationStatus == VerificationStatus.Approved)
                    .Where(item =>
                        (item.FirstName != null && item.FirstName.ToLower().Contains(term)) ||
                        (item.LastName != null && item.LastName.ToLower().Contains(term)) ||
                        (item.CivilRegistryId != null && item.CivilRegistryId.ToLower().Contains(term)) ||
                        (item.Address != null && item.Address.ToLower().Contains(term)))
                    .Take(10)
                    .ToListAsync();

                BeneficiarySearchResults.Clear();
                foreach (var s in stagingMatches)
                {
                    BeneficiarySearchResults.Add(AssistanceValidatedBeneficiaryOption.FromApprovedStaging(s));
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error searching registry: {ex.Message}", "Warning");
            }
            finally
            {
                IsBeneficiarySearching = false;
            }
        }

        private void SelectBeneficiaryOption(AssistanceValidatedBeneficiaryOption? option)
        {
            if (option == null) return;

            SelectedBeneficiaryOption = option;
            IsUnregisteredResident = false;
            IntakeCitizenName = option.FullName;
            IntakeCivilRegistryId = option.CivilRegistryId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(option.Barangay) && SulopBarangays.Contains(option.Barangay))
            {
                IntakeBarangay = option.Barangay;
            }
            IntakeContactNumber = option.ContactNumber;
            IntakeAddress = option.Address;

            BeneficiarySearchResults.Clear();
        }

        private void ClearBeneficiarySelection()
        {
            SelectedBeneficiaryOption = null;
            IsUnregisteredResident = true;
            BeneficiarySearchResults.Clear();
        }

        private bool CanSubmitIntake()
        {
            return !string.IsNullOrWhiteSpace(IntakeCitizenName) &&
                   !string.IsNullOrWhiteSpace(IntakeSubject) &&
                   !string.IsNullOrWhiteSpace(IntakeDescription);
        }

        private async Task SubmitIntakeAsync()
        {
            if (!CanSubmitIntake()) return;

            IsLoading = true;
            try
            {
                decimal? amount = null;
                if (!string.IsNullOrWhiteSpace(IntakeAmount) && decimal.TryParse(IntakeAmount, out var parsedAmount))
                {
                    amount = parsedAmount;
                }

                var budgetId = SelectedIntakeBudget?.Id;
                var budgetName = SelectedIntakeBudget?.BudgetName ?? "Global Aid Request Budget";
                var targetAyudaId = SelectedIntakeBudget?.TargetAyudaProgramId;
                var notesComposite = $"[Desk Intake - Budget: {budgetName}] {IntakeDescription.Trim()}";
                if (!string.IsNullOrWhiteSpace(IntakeContactNumber))
                    notesComposite += $"\nContact: {IntakeContactNumber.Trim()}";
                if (!string.IsNullOrWhiteSpace(IntakeAddress))
                    notesComposite += $"\nAddress: {IntakeAddress.Trim()}";
                if (IntakeReleaseKind == AssistanceReleaseKind.Goods && !string.IsNullOrWhiteSpace(IntakeGoodsDescription))
                    notesComposite += $"\nGoods Package: {IntakeGoodsDescription.Trim()}";

                var requestDto = new AssistanceCaseUpsertRequest(
                    HouseholdId: null,
                    HouseholdMemberId: null,
                    ValidatedBeneficiaryName: IntakeCitizenName.Trim(),
                    ValidatedBeneficiaryId: SelectedBeneficiaryOption?.BeneficiaryId,
                    ValidatedCivilRegistryId: string.IsNullOrWhiteSpace(IntakeCivilRegistryId) ? null : IntakeCivilRegistryId.Trim(),
                    AssistanceType: IntakeCategory,
                    Priority: IntakePriority,
                    ReleaseKind: IntakeReleaseKind,
                    AssistanceAmount: amount,
                    RequestedOn: DateTime.Today,
                    ScheduledReleaseDate: null,
                    Summary: IntakeSubject.Trim(),
                    AyudaProgramId: targetAyudaId);

                var result = await _caseService.CreateAsync(requestDto, _currentUser.Id, budgetId);

                if (result.IsSuccess && result.AssistanceCaseId.HasValue)
                {
                    // Update notes and budget on the newly created case
                    var createdCase = await _context.AssistanceCases.FindAsync(result.AssistanceCaseId.Value);
                    if (createdCase != null)
                    {
                        createdCase.Notes = notesComposite;
                        if (budgetId.HasValue)
                        {
                            createdCase.AssistanceCaseBudgetId = budgetId.Value;
                        }
                        if (targetAyudaId.HasValue)
                        {
                            createdCase.AyudaProgramId = targetAyudaId.Value;
                        }
                        await _context.SaveChangesAsync();
                    }

                    IsIntakeModalOpen = false;
                    ShowStatus($"Request successfully registered with ticket {createdCase?.CaseNumber ?? "created"} under {budgetName}.", "Success");
                    await LoadRequestsAsync();
                }
                else
                {
                    ShowStatus(result.Message ?? "Failed to register request.", "Error");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error registering request: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --- Inspection & Lifecycle Actions ---

        private void InspectRequest(CitizenRequestItemViewModel? item)
        {
            if (item == null) return;
            SelectedRequest = item;
            IsInspectModalOpen = true;
        }

        private async Task ChangeSelectedStatusAsync(AssistanceCaseStatus targetStatus, string auditReason)
        {
            if (SelectedRequest == null) return;

            IsLoading = true;
            try
            {
                var result = await _caseService.ChangeStatusAsync(SelectedRequest.Id, targetStatus, _currentUser.Id, auditReason);
                if (result.IsSuccess)
                {
                    SelectedRequest.Status = targetStatus;
                    NotifySelectedRequestStateChanged();
                    ShowStatus($"Request status updated to {targetStatus}.", "Success");
                    UpdateMetrics();
                    FilterAndPaginate();
                }
                else
                {
                    ShowStatus(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Status update failed: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ConfirmResolveAsync()
        {
            if (SelectedRequest == null || string.IsNullOrWhiteSpace(ResolutionSummary))
            {
                ShowStatus("Resolution summary is required.", "Warning");
                return;
            }

            IsLoading = true;
            try
            {
                var outcomeNotes = $"[{ResolutionClassification}] {ResolutionSummary.Trim()}";
                var result = await _caseService.ChangeStatusAsync(SelectedRequest.Id, AssistanceCaseStatus.Closed, _currentUser.Id, outcomeNotes);

                if (result.IsSuccess)
                {
                    var caseEntity = await _context.AssistanceCases.FindAsync(SelectedRequest.Id);
                    if (caseEntity != null)
                    {
                        caseEntity.ResolutionNotes = outcomeNotes;
                        await _context.SaveChangesAsync();
                    }

                    SelectedRequest.Status = AssistanceCaseStatus.Closed;
                    SelectedRequest.ResolutionNotes = outcomeNotes;
                    NotifySelectedRequestStateChanged();

                    IsResolveModalOpen = false;
                    ShowStatus("Citizen request has been successfully resolved and closed.", "Success");
                    UpdateMetrics();
                    FilterAndPaginate();
                }
                else
                {
                    ShowStatus(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Resolve failed: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ConfirmRejectAsync()
        {
            if (SelectedRequest == null || string.IsNullOrWhiteSpace(RejectionReasonText))
            {
                ShowStatus("Please specify the reason for rejection.", "Warning");
                return;
            }

            IsLoading = true;
            try
            {
                var reason = RejectionReasonText.Trim();
                var result = await _caseService.RejectCaseAsync(SelectedRequest.Id, reason, _currentUser.Id);

                if (result.IsSuccess)
                {
                    SelectedRequest.Status = AssistanceCaseStatus.Rejected;
                    SelectedRequest.RejectionReason = reason;
                    NotifySelectedRequestStateChanged();

                    IsRejectModalOpen = false;
                    ShowStatus("Citizen request has been marked as declined/ineligible.", "Warning");
                    UpdateMetrics();
                    FilterAndPaginate();
                }
                else
                {
                    ShowStatus(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Reject operation failed: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenDisburseModal()
        {
            if (SelectedRequest == null || !CanDisburse) return;
            DisburseAmountText = SelectedRequest.RequestedAmount?.ToString("N2", CultureInfo.InvariantCulture) ?? "1000.00";
            DisburseRemarks = $"Disbursement payout for Ticket {SelectedRequest.TicketNumber} ({SelectedRequest.CitizenName}).";

            if (SelectedRequest.AssistanceCaseBudgetId.HasValue)
            {
                SelectedDisburseBudget = AvailableBudgets.FirstOrDefault(b => b.Id == SelectedRequest.AssistanceCaseBudgetId.Value)
                                         ?? AvailableBudgets.FirstOrDefault();
            }
            else
            {
                SelectedDisburseBudget ??= AvailableBudgets.FirstOrDefault();
            }

            IsDisburseModalOpen = true;
        }

        private async Task ConfirmDisburseAsync()
        {
            if (SelectedRequest == null || !CanDisburse) return;

            if (!decimal.TryParse(DisburseAmountText, out var amount) || amount <= 0)
            {
                ShowStatus("Enter a valid disbursement amount greater than zero.", "Warning");
                return;
            }

            IsLoading = true;
            try
            {
                var targetBudgetId = SelectedDisburseBudget?.Id;
                var result = await _caseService.FastTrackReleaseAsync(
                    SelectedRequest.Id,
                    amount,
                    _currentUser.Id,
                    DisburseRemarks,
                    targetBudgetId);

                if (result.IsSuccess)
                {
                    SelectedRequest.Status = AssistanceCaseStatus.Released;
                    SelectedRequest.ApprovedAmount = amount;
                    SelectedRequest.DisbursedAmount = amount;
                    SelectedRequest.AssistanceCaseBudgetId = targetBudgetId;
                    if (SelectedDisburseBudget != null)
                    {
                        SelectedRequest.BudgetName = SelectedDisburseBudget.BudgetName;
                        SelectedRequest.BudgetCode = SelectedDisburseBudget.BudgetCode;
                    }
                    NotifySelectedRequestStateChanged();

                    IsDisburseModalOpen = false;
                    var budgetName = SelectedDisburseBudget?.BudgetName ?? "Office Budget";
                    ShowStatus($"Disbursement of ₱{amount:N2} recorded to {budgetName}.", "Success");
                    UpdateMetrics();
                    FilterAndPaginate();
                }
                else
                {
                    ShowStatus(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Disbursement error: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveInternalNoteAsync()
        {
            if (SelectedRequest == null || string.IsNullOrWhiteSpace(InternalNoteText)) return;

            try
            {
                var caseEntity = await _context.AssistanceCases.FindAsync(SelectedRequest.Id);
                if (caseEntity != null)
                {
                    var timestamp = DateTime.Now.ToString("MMM dd, yyyy h:mm tt");
                    var newNote = $"[Note {timestamp} by {_currentUser.Username}]: {InternalNoteText.Trim()}";
                    caseEntity.Notes = string.IsNullOrWhiteSpace(caseEntity.Notes)
                        ? newNote
                        : $"{caseEntity.Notes}\n\n{newNote}";

                    await _context.SaveChangesAsync();
                    SelectedRequest.Description = caseEntity.Notes;
                }

                IsInternalNoteModalOpen = false;
                ShowStatus("Internal note recorded.", "Success");
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to save note: {ex.Message}", "Error");
            }
        }

        private void ShowStatus(string message, string type)
        {
            StatusMessage = message;
            StatusType = type;
            IsStatusVisible = true;
        }

        private CitizenRequestItemViewModel MapCaseToViewModel(AssistanceCase c)
        {
            var bgy = "Poblacion";
            if (!string.IsNullOrWhiteSpace(c.Notes))
            {
                foreach (var b in SulopBarangays)
                {
                    if (c.Notes.Contains(b, StringComparison.OrdinalIgnoreCase))
                    {
                        bgy = b;
                        break;
                    }
                }
            }

            var dept = c.AssistanceType switch
            {
                "Social Assistance" => "MSWDO",
                "Legal / Notarial Aid" => "Legal Services",
                "Medical / Burial Endorsement" => "Municipal Health Office",
                "Document Issuance / Clearance" => "Mayor's Office",
                _ => "General Services / Admin"
            };

            var linkedBudget = AvailableBudgets.FirstOrDefault(b => b.Id == c.AssistanceCaseBudgetId)
                               ?? (c.AyudaProgramId.HasValue ? AvailableBudgets.FirstOrDefault(b => b.TargetAyudaProgramId == c.AyudaProgramId.Value) : null)
                               ?? AvailableBudgets.FirstOrDefault();
            var budgetName = linkedBudget?.BudgetName ?? "Global Aid Request Budget";
            var budgetCode = linkedBudget?.BudgetCode ?? "GLOBAL_AID_BUDGET";

            return new CitizenRequestItemViewModel
            {
                Id = c.Id,
                TicketNumber = string.IsNullOrWhiteSpace(c.CaseNumber) ? $"CR-{c.CreatedAt.Year}-{c.Id:D4}" : c.CaseNumber,
                CitizenName = string.IsNullOrWhiteSpace(c.ValidatedBeneficiaryName) ? "Unregistered Resident" : c.ValidatedBeneficiaryName,
                CivilRegistryId = c.ValidatedCivilRegistryId,
                BeneficiaryId = c.ValidatedBeneficiaryId,
                Barangay = bgy,
                Category = string.IsNullOrWhiteSpace(c.AssistanceType) ? "General Inquiry / Complaint" : c.AssistanceType,
                Subject = string.IsNullOrWhiteSpace(c.Summary) ? "Walk-in Desk Assistance" : c.Summary,
                Description = c.Notes ?? string.Empty,
                Priority = c.Priority,
                Status = c.Status,
                ReleaseKind = c.ReleaseKind,
                RequestedAmount = c.RequestedAmount,
                ApprovedAmount = c.ApprovedAmount,
                DisbursedAmount = c.Status == AssistanceCaseStatus.Released ? (c.ApprovedAmount ?? c.RequestedAmount) : null,
                DepartmentName = dept,
                BudgetName = budgetName,
                BudgetCode = budgetCode,
                CreatedAt = c.CreatedAt,
                RequestedOn = c.RequestedOn,
                ResolutionNotes = c.ResolutionNotes,
                BudgetLedgerEntryId = c.BudgetLedgerEntryId,
                AssistanceCaseBudgetId = c.AssistanceCaseBudgetId
            };
        }
    }
}

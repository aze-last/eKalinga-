using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class AssistanceCaseManagementViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly ObservableCollection<AssistanceCaseListItem> _cases = new();
        private readonly ObservableCollection<AssistanceHouseholdOption> _households = new();
        private readonly ObservableCollection<AssistanceHouseholdMemberOption> _availableHouseholdMembers = new();
        private readonly ObservableCollection<AssistanceAyudaProgramOption> _ayudaPrograms = new();
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
        private readonly RelayCommand _deleteCommand;
        private ICollectionView _casesView;
        private AssistanceCaseListItem? _selectedCase;
        private AssistanceHouseholdOption? _selectedHousehold;
        private AssistanceHouseholdMemberOption? _selectedHouseholdMember;
        private AssistanceAyudaProgramOption? _selectedAyudaProgram;
        private AssistanceValidatedBeneficiaryOption? _selectedValidatedBeneficiary;
        private string _searchText = string.Empty;
        private string _validatedBeneficiarySearchText = string.Empty;
        private string _selectedStatusFilter = "All";
        private bool _isBusy;
        private string _statusMessage = "Loading aid requests...";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private int _totalCases;
        private int _pendingCount;
        private int _approvedCount;
        private int _releasedCount;
        private int _closedCount;
        private string _editableCaseNumber = "New aid request";
        private string _editableAssistanceType = string.Empty;
        private AssistanceReleaseKind _selectedReleaseKind = AssistanceReleaseKind.Cash;
        private AssistanceCasePriority _selectedPriority = AssistanceCasePriority.Medium;
        private string _editableAssistanceAmount = string.Empty;
        private DateTime _requestedOnDate = DateTime.Today;
        private DateTime? _scheduledReleaseDate;
        private string _editableSummary = string.Empty;

        public AssistanceCaseManagementViewModel(User currentUser)
        {
            _currentUser = currentUser;
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

            PriorityOptions = new ObservableCollection<AssistanceCasePriority>(Enum.GetValues<AssistanceCasePriority>());
            ReleaseKindOptions = new ObservableCollection<AssistanceReleaseKind>(Enum.GetValues<AssistanceReleaseKind>());

            _casesView = CollectionViewSource.GetDefaultView(_cases);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _newCaseCommand = new RelayCommand(_ => BeginNewCase(), _ => !IsBusy);
            _saveCaseCommand = new RelayCommand(async _ => await SaveCaseAsync(), _ => CanSaveCase());
            _markUnderReviewCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.UnderReview, "under review"), _ => CanChangeStatus(AssistanceCaseStatus.UnderReview));
            _approveCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Approved, "approved"), _ => CanChangeStatus(AssistanceCaseStatus.Approved));
            _releaseCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Released, "released"), _ => CanChangeStatus(AssistanceCaseStatus.Released));
            _closeCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Closed, "closed"), _ => CanChangeStatus(AssistanceCaseStatus.Closed));
            _rejectCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Rejected, "rejected"), _ => CanChangeStatus(AssistanceCaseStatus.Rejected));
            _cancelCommand = new RelayCommand(async _ => await ChangeStatusAsync(AssistanceCaseStatus.Cancelled, "cancelled"), _ => CanChangeStatus(AssistanceCaseStatus.Cancelled));
            _deleteCommand = new RelayCommand(async _ => await DeleteCaseAsync(), _ => CanDeleteCase());

            ApplyFilter();
            _ = LoadAsync();
            _ = LoadValidatedBeneficiariesAsync();
        }

        public ObservableCollection<string> StatusFilters { get; }

        public ObservableCollection<AssistanceCasePriority> PriorityOptions { get; }

        public ObservableCollection<AssistanceReleaseKind> ReleaseKindOptions { get; }

        public ObservableCollection<AssistanceHouseholdOption> Households => _households;

        public ObservableCollection<AssistanceHouseholdMemberOption> AvailableHouseholdMembers => _availableHouseholdMembers;

        public ObservableCollection<AssistanceAyudaProgramOption> AyudaPrograms => _ayudaPrograms;

        public ObservableCollection<AssistanceValidatedBeneficiaryOption> ValidatedBeneficiaries => _validatedBeneficiaries;

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
                    RaiseCommandStates();
                }
            }
        }

        public AssistanceHouseholdOption? SelectedHousehold
        {
            get => _selectedHousehold;
            set
            {
                if (SetProperty(ref _selectedHousehold, value))
                {
                    RefreshAvailableHouseholdMembers();
                    if (value != null && SelectedValidatedBeneficiary != null)
                    {
                        SelectedValidatedBeneficiary = null;
                    }

                    OnPropertyChanged(nameof(ApplicantSummary));
                    RaiseCommandStates();
                }
            }
        }

        public AssistanceHouseholdMemberOption? SelectedHouseholdMember
        {
            get => _selectedHouseholdMember;
            set
            {
                if (SetProperty(ref _selectedHouseholdMember, value))
                {
                    OnPropertyChanged(nameof(ApplicantSummary));
                }
            }
        }

        public AssistanceAyudaProgramOption? SelectedAyudaProgram
        {
            get => _selectedAyudaProgram;
            set
            {
                if (SetProperty(ref _selectedAyudaProgram, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public AssistanceValidatedBeneficiaryOption? SelectedValidatedBeneficiary
        {
            get => _selectedValidatedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedValidatedBeneficiary, value))
                {
                    if (value != null)
                    {
                        SelectedHouseholdMember = null;
                        SelectedHousehold = null;
                    }

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
                    ApplyFilter();
                }
            }
        }

        public string ValidatedBeneficiarySearchText
        {
            get => _validatedBeneficiarySearchText;
            set
            {
                if (SetProperty(ref _validatedBeneficiarySearchText, value))
                {
                    _ = LoadValidatedBeneficiariesAsync();
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
                    RaiseCommandStates();
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

        public string ApplicantSummary =>
            SelectedHouseholdMember?.DisplayLabel
            ?? SelectedValidatedBeneficiary?.DisplayLabel
            ?? SelectedHousehold?.HeadName
            ?? "Select a validated beneficiary or household/member";

        public string ReleaseKindSummary => $"Release kind: {SelectedReleaseKind}";

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand NewCaseCommand => _newCaseCommand;
        public ICommand SaveCaseCommand => _saveCaseCommand;
        public ICommand MarkUnderReviewCommand => _markUnderReviewCommand;
        public ICommand ApproveCommand => _approveCommand;
        public ICommand ReleaseCommand => _releaseCommand;
        public ICommand CloseCommand => _closeCommand;
        public ICommand RejectCommand => _rejectCommand;
        public ICommand CancelCommand => _cancelCommand;
        public ICommand DeleteCommand => _deleteCommand;

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
            var preferredHouseholdId = SelectedHousehold?.Id;
            var preferredMemberId = SelectedHouseholdMember?.Id;
            var preferredProgramId = SelectedAyudaProgram?.Id;
            var preferredValidatedBeneficiary = SelectedValidatedBeneficiary;

            await using var context = new AppDbContext();

            var assistanceCases = await context.AssistanceCases
                .AsNoTracking()
                .Include(item => item.Household)
                .Include(item => item.HouseholdMember)
                .Include(item => item.AyudaProgram)
                .OrderByDescending(item => item.RequestedOn)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();

            var households = await context.Households
                .AsNoTracking()
                .Include(item => item.Members)
                .OrderBy(item => item.HouseholdCode)
                .ThenBy(item => item.HeadName)
                .ToListAsync();

            var ayudaPrograms = await context.AyudaPrograms
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.ProgramName)
                .ToListAsync();

            _cases.Clear();
            foreach (var assistanceCase in assistanceCases)
            {
                _cases.Add(AssistanceCaseListItem.FromEntity(assistanceCase));
            }

            _households.Clear();
            foreach (var household in households)
            {
                _households.Add(AssistanceHouseholdOption.FromEntity(household));
            }

            _ayudaPrograms.Clear();
            foreach (var ayudaProgram in ayudaPrograms)
            {
                _ayudaPrograms.Add(AssistanceAyudaProgramOption.FromEntity(ayudaProgram));
            }

            UpdateCounts();

            CasesView = CollectionViewSource.GetDefaultView(_cases);
            ApplyFilter();

            SelectedCase = _cases.FirstOrDefault(item => item.Id == preferredCaseId)
                ?? _cases.FirstOrDefault();

            var selectedHouseholdId = SelectedCase?.HouseholdId ?? preferredHouseholdId;
            SelectedHousehold = selectedHouseholdId.HasValue
                ? _households.FirstOrDefault(item => item.Id == selectedHouseholdId.Value)
                : null;

            var selectedMemberId = SelectedCase?.HouseholdMemberId ?? preferredMemberId;
            if (selectedMemberId.HasValue)
            {
                SelectedHouseholdMember = _availableHouseholdMembers.FirstOrDefault(item => item.Id == selectedMemberId.Value);
            }

            var selectedProgramId = SelectedCase?.AyudaProgramId ?? preferredProgramId;
            if (selectedProgramId.HasValue)
            {
                SelectedAyudaProgram = _ayudaPrograms.FirstOrDefault(item => item.Id == selectedProgramId.Value);
            }
            else
            {
                SelectedAyudaProgram = null;
            }

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
            PendingCount = _cases.Count(item => item.Status is AssistanceCaseStatus.Pending or AssistanceCaseStatus.UnderReview);
            ApprovedCount = _cases.Count(item => item.Status == AssistanceCaseStatus.Approved);
            ReleasedCount = _cases.Count(item => item.Status == AssistanceCaseStatus.Released);
            ClosedCount = _cases.Count(item => item.Status is AssistanceCaseStatus.Closed or AssistanceCaseStatus.Rejected or AssistanceCaseStatus.Cancelled);
        }

        private void ApplyFilter()
        {
            CasesView.Filter = item =>
            {
                if (item is not AssistanceCaseListItem assistanceCase)
                {
                    return false;
                }

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
                    || Contains(assistanceCase.HouseholdLabel, SearchText)
                    || Contains(assistanceCase.ApplicantLabel, SearchText)
                    || Contains(assistanceCase.Summary, SearchText);
            };

            CasesView.Refresh();
        }

        private void BeginNewCase()
        {
            SelectedCase = null;
            EditableCaseNumber = "New aid request";
            EditableAssistanceType = string.Empty;
            SelectedReleaseKind = AssistanceReleaseKind.Cash;
            SelectedPriority = AssistanceCasePriority.Medium;
            EditableAssistanceAmount = string.Empty;
            RequestedOnDate = DateTime.Today;
            ScheduledReleaseDate = null;
            EditableSummary = string.Empty;
            SelectedHousehold = null;
            SelectedHouseholdMember = null;
            SelectedValidatedBeneficiary = null;
            SelectedAyudaProgram = null;
            RaiseCommandStates();
            OnPropertyChanged(nameof(ApplicantSummary));
        }

        private void SyncEditorFromSelection()
        {
            if (SelectedCase == null)
            {
                OnPropertyChanged(nameof(ApplicantSummary));
                return;
            }

            EditableCaseNumber = SelectedCase.CaseNumber;
            EditableAssistanceType = SelectedCase.AssistanceType;
            SelectedReleaseKind = SelectedCase.ReleaseKind;
            SelectedPriority = SelectedCase.Priority;
            EditableAssistanceAmount = SelectedCase.AssistanceAmountText;
            RequestedOnDate = SelectedCase.RequestedOn;
            ScheduledReleaseDate = SelectedCase.ScheduledReleaseDate;
            EditableSummary = SelectedCase.Summary;

            SelectedHousehold = SelectedCase.HouseholdId.HasValue
                ? _households.FirstOrDefault(item => item.Id == SelectedCase.HouseholdId.Value)
                : null;

            if (SelectedCase.HouseholdMemberId.HasValue)
            {
                SelectedHouseholdMember = _availableHouseholdMembers.FirstOrDefault(item => item.Id == SelectedCase.HouseholdMemberId.Value);
            }
            else
            {
                SelectedHouseholdMember = null;
            }

            if (SelectedCase.AyudaProgramId.HasValue)
            {
                SelectedAyudaProgram = _ayudaPrograms.FirstOrDefault(item => item.Id == SelectedCase.AyudaProgramId.Value);
            }
            else
            {
                SelectedAyudaProgram = null;
            }

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

            OnPropertyChanged(nameof(ApplicantSummary));
        }

        private void RefreshAvailableHouseholdMembers()
        {
            var preferredMemberId = SelectedCase?.HouseholdId == SelectedHousehold?.Id
                ? SelectedCase?.HouseholdMemberId
                : SelectedHouseholdMember?.Id;

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
                SelectedHouseholdMember = _availableHouseholdMembers.FirstOrDefault(item => item.Id == preferredMemberId.Value);
            }
            else
            {
                SelectedHouseholdMember = null;
            }

            OnPropertyChanged(nameof(ApplicantSummary));
        }

        private bool CanSaveCase()
        {
            return !IsBusy
                && (SelectedHousehold != null || SelectedValidatedBeneficiary != null)
                && !string.IsNullOrWhiteSpace(EditableAssistanceType);
        }

        private bool CanChangeStatus(AssistanceCaseStatus targetStatus)
        {
            if (IsBusy || SelectedCase == null || SelectedCase.Status == targetStatus)
            {
                return false;
            }

            return targetStatus switch
            {
                AssistanceCaseStatus.Released => SelectedCase.ApprovedAmount.HasValue
                    && SelectedCase.ApprovedAmount.Value > 0
                    && SelectedCase.AyudaProgramId.HasValue,
                _ => true
            };
        }

        private async Task SaveCaseAsync()
        {
            if (!CanSaveCase())
            {
                return;
            }

            if (!TryParseAmount(EditableAssistanceAmount, out var assistanceAmount, out var amountError))
            {
                SetErrorStatus(amountError);
                return;
            }

            if (!assistanceAmount.HasValue || assistanceAmount.Value <= 0)
            {
                SetErrorStatus("Enter the assistance amount before saving this aid request.");
                return;
            }

            var editingCaseId = SelectedCase?.Id;
            IsBusy = true;
            SetNeutralStatus(editingCaseId.HasValue ? $"Saving {EditableCaseNumber}..." : "Creating aid request...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(context);
                var request = new AssistanceCaseUpsertRequest(
                    SelectedHousehold?.Id,
                    SelectedHouseholdMember?.Id,
                    SelectedValidatedBeneficiary?.FullName,
                    SelectedValidatedBeneficiary?.BeneficiaryId,
                    SelectedValidatedBeneficiary?.CivilRegistryId,
                    EditableAssistanceType.Trim(),
                    SelectedPriority,
                    SelectedReleaseKind,
                    assistanceAmount,
                    RequestedOnDate,
                    ScheduledReleaseDate,
                    NormalizeNullable(EditableSummary),
                    SelectedAyudaProgram?.Id);

                AssistanceCaseOperationResult result = editingCaseId.HasValue
                    ? await service.UpdateAsync(editingCaseId.Value, request, _currentUser.Id)
                    : await service.CreateAsync(request, _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(result.AssistanceCaseId);
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
                var service = new AssistanceCaseManagementService(context);
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
                var service = new AssistanceCaseManagementService(context);
                var result = await service.DeleteAsync(caseId, _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(null);
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
                var searchText = ValidatedBeneficiarySearchText.Trim();

                await using var context = new AppDbContext();
                var query = context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(item => item.VerificationStatus == VerificationStatus.Approved);

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    query = query.Where(item =>
                        (item.FullName ?? string.Empty).Contains(searchText) ||
                        (item.BeneficiaryId ?? string.Empty).Contains(searchText) ||
                        (item.CivilRegistryId ?? string.Empty).Contains(searchText) ||
                        (item.Address ?? string.Empty).Contains(searchText));
                }

                var beneficiaries = await query
                    .OrderBy(item => item.FullName ?? item.LastName)
                    .ThenBy(item => item.FirstName)
                    .Take(50)
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
            _deleteCommand.RaiseCanExecuteChanged();
        }

        private void ClearLoadedState()
        {
            _cases.Clear();
            _households.Clear();
            _availableHouseholdMembers.Clear();
            _ayudaPrograms.Clear();
            _validatedBeneficiaries.Clear();
            CasesView = CollectionViewSource.GetDefaultView(_cases);
            TotalCases = 0;
            PendingCount = 0;
            ApprovedCount = 0;
            ReleasedCount = 0;
            ClosedCount = 0;
            BeginNewCase();
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

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }
    }

    public sealed class AssistanceCaseListItem
    {
        public int Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public int? HouseholdId { get; init; }
        public int? HouseholdMemberId { get; init; }
        public int? AyudaProgramId { get; init; }
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
        public string HouseholdLabel { get; init; } = string.Empty;
        public string ApplicantLabel { get; init; } = string.Empty;
        public string AyudaProgramLabel { get; init; } = string.Empty;

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
                HouseholdId = assistanceCase.HouseholdId,
                HouseholdMemberId = assistanceCase.HouseholdMemberId,
                AyudaProgramId = assistanceCase.AyudaProgramId,
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
                HouseholdLabel = assistanceCase.Household != null
                    ? $"{assistanceCase.Household.HouseholdCode} - {assistanceCase.Household.HeadName}"
                    : "Validated beneficiary",
                ApplicantLabel = assistanceCase.HouseholdMember?.FullName
                    ?? assistanceCase.ValidatedBeneficiaryName
                    ?? assistanceCase.Household?.HeadName
                    ?? "No applicant selected",
                AyudaProgramLabel = assistanceCase.AyudaProgram?.ProgramName ?? "Program not set"
            };
        }

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }
    }

    public sealed class AssistanceHouseholdOption
    {
        public int Id { get; init; }
        public string HouseholdCode { get; init; } = string.Empty;
        public string HeadName { get; init; } = string.Empty;
        public string AddressLine { get; init; } = string.Empty;
        public IReadOnlyList<AssistanceHouseholdMemberOption> Members { get; init; } = Array.Empty<AssistanceHouseholdMemberOption>();
        public string DisplayLabel => $"{HouseholdCode} - {HeadName}";

        public static AssistanceHouseholdOption FromEntity(Household household)
        {
            return new AssistanceHouseholdOption
            {
                Id = household.Id,
                HouseholdCode = household.HouseholdCode,
                HeadName = household.HeadName,
                AddressLine = household.AddressLine,
                Members = household.Members
                    .OrderBy(member => member.FullName)
                    .Select(AssistanceHouseholdMemberOption.FromEntity)
                    .ToList()
            };
        }
    }

    public sealed class AssistanceHouseholdMemberOption
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string RelationshipToHead { get; init; } = string.Empty;
        public string DisplayLabel =>
            string.IsNullOrWhiteSpace(RelationshipToHead)
                ? FullName
                : $"{FullName} ({RelationshipToHead})";

        public static AssistanceHouseholdMemberOption FromEntity(HouseholdMember member)
        {
            return new AssistanceHouseholdMemberOption
            {
                Id = member.Id,
                FullName = member.FullName,
                RelationshipToHead = member.RelationshipToHead
            };
        }
    }

    public sealed class AssistanceAyudaProgramOption
    {
        public int Id { get; init; }
        public string ProgramCode { get; init; } = string.Empty;
        public string ProgramName { get; init; } = string.Empty;
        public string DisplayLabel => $"{ProgramCode} - {ProgramName}";

        public static AssistanceAyudaProgramOption FromEntity(AyudaProgram program)
        {
            return new AssistanceAyudaProgramOption
            {
                Id = program.Id,
                ProgramCode = program.ProgramCode,
                ProgramName = program.ProgramName
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

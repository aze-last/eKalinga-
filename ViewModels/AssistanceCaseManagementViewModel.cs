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
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _newCaseCommand;
        private readonly RelayCommand _saveCaseCommand;
        private readonly RelayCommand _markUnderReviewCommand;
        private readonly RelayCommand _approveCommand;
        private readonly RelayCommand _releaseCommand;
        private readonly RelayCommand _closeCommand;
        private readonly RelayCommand _rejectCommand;
        private readonly RelayCommand _cancelCommand;
        private ICollectionView _casesView;
        private AssistanceCaseListItem? _selectedCase;
        private AssistanceHouseholdOption? _selectedHousehold;
        private AssistanceHouseholdMemberOption? _selectedHouseholdMember;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "All";
        private bool _isBusy;
        private string _statusMessage = "Loading assistance cases...";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private int _totalCases;
        private int _pendingCount;
        private int _approvedCount;
        private int _releasedCount;
        private int _closedCount;
        private string _editableCaseNumber = "New case";
        private string _editableAssistanceType = string.Empty;
        private AssistanceCasePriority _selectedPriority = AssistanceCasePriority.Medium;
        private string _editableRequestedAmount = string.Empty;
        private string _editableApprovedAmount = string.Empty;
        private DateTime _requestedOnDate = DateTime.Today;
        private DateTime? _scheduledReleaseDate;
        private string _editableSummary = string.Empty;
        private string _editableNotes = string.Empty;
        private string _editableResolutionNotes = string.Empty;

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

            ApplyFilter();
            _ = LoadAsync();
        }

        public ObservableCollection<string> StatusFilters { get; }

        public ObservableCollection<AssistanceCasePriority> PriorityOptions { get; }

        public ObservableCollection<AssistanceHouseholdOption> Households => _households;

        public ObservableCollection<AssistanceHouseholdMemberOption> AvailableHouseholdMembers => _availableHouseholdMembers;

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
                    RaiseCommandStates();
                }
            }
        }

        public AssistanceHouseholdMemberOption? SelectedHouseholdMember
        {
            get => _selectedHouseholdMember;
            set => SetProperty(ref _selectedHouseholdMember, value);
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
            set => SetProperty(ref _editableAssistanceType, value);
        }

        public AssistanceCasePriority SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        public string EditableRequestedAmount
        {
            get => _editableRequestedAmount;
            set => SetProperty(ref _editableRequestedAmount, value);
        }

        public string EditableApprovedAmount
        {
            get => _editableApprovedAmount;
            set => SetProperty(ref _editableApprovedAmount, value);
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

        public string EditableNotes
        {
            get => _editableNotes;
            set => SetProperty(ref _editableNotes, value);
        }

        public string EditableResolutionNotes
        {
            get => _editableResolutionNotes;
            set
            {
                if (SetProperty(ref _editableResolutionNotes, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string ApplicantSummary =>
            SelectedHouseholdMember?.DisplayLabel
            ?? SelectedHousehold?.HeadName
            ?? "Select a household or member";

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand NewCaseCommand => _newCaseCommand;
        public ICommand SaveCaseCommand => _saveCaseCommand;
        public ICommand MarkUnderReviewCommand => _markUnderReviewCommand;
        public ICommand ApproveCommand => _approveCommand;
        public ICommand ReleaseCommand => _releaseCommand;
        public ICommand CloseCommand => _closeCommand;
        public ICommand RejectCommand => _rejectCommand;
        public ICommand CancelCommand => _cancelCommand;

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading assistance cases...");

            try
            {
                await LoadCoreAsync(SelectedCase?.Id);
                SetSuccessStatus($"Loaded {TotalCases:N0} assistance case(s).");
            }
            catch (Exception ex)
            {
                ClearLoadedState();
                SetErrorStatus($"Unable to load assistance cases: {ex.Message}");
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

            await using var context = new AppDbContext();

            var assistanceCases = await context.AssistanceCases
                .AsNoTracking()
                .Include(item => item.Household)
                .Include(item => item.HouseholdMember)
                .OrderByDescending(item => item.RequestedOn)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();

            var households = await context.Households
                .AsNoTracking()
                .Include(item => item.Members)
                .OrderBy(item => item.HouseholdCode)
                .ThenBy(item => item.HeadName)
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

            UpdateCounts();

            CasesView = CollectionViewSource.GetDefaultView(_cases);
            ApplyFilter();

            SelectedCase = _cases.FirstOrDefault(item => item.Id == preferredCaseId)
                ?? _cases.FirstOrDefault();

            var selectedHouseholdId = SelectedCase?.HouseholdId ?? preferredHouseholdId;
            SelectedHousehold = _households.FirstOrDefault(item => item.Id == selectedHouseholdId)
                ?? _households.FirstOrDefault();

            var selectedMemberId = SelectedCase?.HouseholdMemberId ?? preferredMemberId;
            if (selectedMemberId.HasValue)
            {
                SelectedHouseholdMember = _availableHouseholdMembers.FirstOrDefault(item => item.Id == selectedMemberId.Value);
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
                    || Contains(assistanceCase.HouseholdLabel, SearchText)
                    || Contains(assistanceCase.ApplicantLabel, SearchText)
                    || Contains(assistanceCase.Summary, SearchText);
            };

            CasesView.Refresh();
        }

        private void BeginNewCase()
        {
            SelectedCase = null;
            EditableCaseNumber = "New case";
            EditableAssistanceType = string.Empty;
            SelectedPriority = AssistanceCasePriority.Medium;
            EditableRequestedAmount = string.Empty;
            EditableApprovedAmount = string.Empty;
            RequestedOnDate = DateTime.Today;
            ScheduledReleaseDate = null;
            EditableSummary = string.Empty;
            EditableNotes = string.Empty;
            EditableResolutionNotes = string.Empty;
            SelectedHousehold = _households.FirstOrDefault();
            SelectedHouseholdMember = null;
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
            SelectedPriority = SelectedCase.Priority;
            EditableRequestedAmount = SelectedCase.RequestedAmountText;
            EditableApprovedAmount = SelectedCase.ApprovedAmountText;
            RequestedOnDate = SelectedCase.RequestedOn;
            ScheduledReleaseDate = SelectedCase.ScheduledReleaseDate;
            EditableSummary = SelectedCase.Summary;
            EditableNotes = SelectedCase.Notes;
            EditableResolutionNotes = SelectedCase.ResolutionNotes;

            SelectedHousehold = _households.FirstOrDefault(item => item.Id == SelectedCase.HouseholdId)
                ?? SelectedHousehold
                ?? _households.FirstOrDefault();

            if (SelectedCase.HouseholdMemberId.HasValue)
            {
                SelectedHouseholdMember = _availableHouseholdMembers.FirstOrDefault(item => item.Id == SelectedCase.HouseholdMemberId.Value);
            }
            else
            {
                SelectedHouseholdMember = null;
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
                && SelectedHousehold != null
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
                AssistanceCaseStatus.Rejected or AssistanceCaseStatus.Closed or AssistanceCaseStatus.Cancelled
                    => !string.IsNullOrWhiteSpace(EditableResolutionNotes),
                _ => true
            };
        }

        private async Task SaveCaseAsync()
        {
            if (!CanSaveCase() || SelectedHousehold == null)
            {
                return;
            }

            if (!TryParseAmount(EditableRequestedAmount, out var requestedAmount, out var requestedAmountError))
            {
                SetErrorStatus(requestedAmountError);
                return;
            }

            if (!TryParseAmount(EditableApprovedAmount, out var approvedAmount, out var approvedAmountError))
            {
                SetErrorStatus(approvedAmountError);
                return;
            }

            var editingCaseId = SelectedCase?.Id;
            IsBusy = true;
            SetNeutralStatus(editingCaseId.HasValue ? $"Saving {EditableCaseNumber}..." : "Creating assistance case...");

            try
            {
                await using var context = new AppDbContext();
                var service = new AssistanceCaseManagementService(context);
                var request = new AssistanceCaseUpsertRequest(
                    SelectedHousehold.Id,
                    SelectedHouseholdMember?.Id,
                    EditableAssistanceType.Trim(),
                    SelectedPriority,
                    requestedAmount,
                    approvedAmount,
                    RequestedOnDate,
                    ScheduledReleaseDate,
                    NormalizeNullable(EditableSummary),
                    NormalizeNullable(EditableNotes));

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
                SetErrorStatus($"Could not save assistance case: {ex.Message}");
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
                    "Update Assistance Case",
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
                    NormalizeNullable(EditableResolutionNotes));

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
                SetErrorStatus($"Could not update assistance case status: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
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
        }

        private void ClearLoadedState()
        {
            _cases.Clear();
            _households.Clear();
            _availableHouseholdMembers.Clear();
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
        public int HouseholdId { get; init; }
        public int? HouseholdMemberId { get; init; }
        public string AssistanceType { get; init; } = string.Empty;
        public AssistanceCasePriority Priority { get; init; }
        public AssistanceCaseStatus Status { get; init; }
        public decimal? RequestedAmount { get; init; }
        public decimal? ApprovedAmount { get; init; }
        public DateTime RequestedOn { get; init; }
        public DateTime? ScheduledReleaseDate { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string Notes { get; init; } = string.Empty;
        public string ResolutionNotes { get; init; } = string.Empty;
        public string HouseholdLabel { get; init; } = string.Empty;
        public string ApplicantLabel { get; init; } = string.Empty;

        public string PriorityText => Priority.ToString();
        public string StatusText => Status switch
        {
            AssistanceCaseStatus.UnderReview => "Under review",
            _ => Status.ToString()
        };

        public string RequestedAmountText => RequestedAmount?.ToString("N2") ?? string.Empty;
        public string ApprovedAmountText => ApprovedAmount?.ToString("N2") ?? string.Empty;
        public string AmountsSummary =>
            $"Requested: {(RequestedAmount?.ToString("N2") ?? "--")} | Approved: {(ApprovedAmount?.ToString("N2") ?? "--")}";

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
                AssistanceType = assistanceCase.AssistanceType,
                Priority = assistanceCase.Priority,
                Status = assistanceCase.Status,
                RequestedAmount = assistanceCase.RequestedAmount,
                ApprovedAmount = assistanceCase.ApprovedAmount,
                RequestedOn = assistanceCase.RequestedOn,
                ScheduledReleaseDate = assistanceCase.ScheduledReleaseDate,
                Summary = assistanceCase.Summary ?? string.Empty,
                Notes = assistanceCase.Notes ?? string.Empty,
                ResolutionNotes = assistanceCase.ResolutionNotes ?? string.Empty,
                HouseholdLabel = $"{assistanceCase.Household.HouseholdCode} - {assistanceCase.Household.HeadName}",
                ApplicantLabel = assistanceCase.HouseholdMember?.FullName ?? assistanceCase.Household.HeadName
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
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BeneficiaryVerificationViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly ObservableCollection<StagedBeneficiaryItem> _records = new();
        private readonly ObservableCollection<HouseholdOption> _households = new();
        private readonly ObservableCollection<HouseholdMemberOption> _availableHouseholdMembers = new();
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _saveCorrectionsCommand;
        private readonly RelayCommand _verifyCommand;
        private readonly RelayCommand _approveCommand;
        private readonly RelayCommand _markDuplicateCommand;
        private readonly RelayCommand _markInactiveCommand;
        private readonly RelayCommand _rejectCommand;
        private ICollectionView _recordsView;
        private StagedBeneficiaryItem? _selectedBeneficiary;
        private HouseholdOption? _selectedHousehold;
        private HouseholdMemberOption? _selectedHouseholdMember;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "Pending";
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

        public BeneficiaryVerificationViewModel(User currentUser)
        {
            _currentUser = currentUser;
            StatusFilters = new ObservableCollection<string>
            {
                "Pending",
                "Verified",
                "Approved",
                "Duplicate",
                "Inactive",
                "Rejected",
                "All"
            };

            _recordsView = CollectionViewSource.GetDefaultView(_records);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _saveCorrectionsCommand = new RelayCommand(async _ => await SaveCorrectionsAsync(), _ => CanSaveCorrectionsSelected());
            _verifyCommand = new RelayCommand(async _ => await VerifySelectedAsync(), _ => CanVerifySelected());
            _approveCommand = new RelayCommand(async _ => await ApproveSelectedAsync(), _ => CanApproveSelected());
            _markDuplicateCommand = new RelayCommand(async _ => await MarkDuplicateSelectedAsync(), _ => CanMarkDuplicateSelected());
            _markInactiveCommand = new RelayCommand(async _ => await MarkInactiveSelectedAsync(), _ => CanMarkInactiveSelected());
            _rejectCommand = new RelayCommand(async _ => await RejectSelectedAsync(), _ => CanRejectSelected());

            ApplyFilter();
            _ = LoadAsync();
        }

        public ObservableCollection<string> StatusFilters { get; }

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
                    OnPropertyChanged(nameof(ApprovalActionLabel));
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
                    RaiseActionCanExecuteChanged();
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

        public string ApprovalActionLabel =>
            SelectedHouseholdMember == null
                ? "Approve and create a new household member"
                : "Approve and link the selected existing member";

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand SaveCorrectionsCommand => _saveCorrectionsCommand;
        public ICommand VerifyCommand => _verifyCommand;
        public ICommand ApproveCommand => _approveCommand;
        public ICommand MarkDuplicateCommand => _markDuplicateCommand;
        public ICommand MarkInactiveCommand => _markInactiveCommand;
        public ICommand RejectCommand => _rejectCommand;

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading staged beneficiaries...");

            try
            {
                await LoadCoreAsync(SelectedBeneficiary?.StagingId);
                SetSuccessStatus($"Loaded {TotalCount:N0} staged beneficiary record(s).");
            }
            catch (Exception ex)
            {
                ClearLoadedState();
                SetErrorStatus($"Unable to load staged beneficiaries: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadCoreAsync(int? preferredStagingId)
        {
            var previouslySelectedHouseholdId = SelectedHousehold?.Id;
            var previouslySelectedHouseholdMemberId = SelectedHouseholdMember?.Id;

            await using var context = new AppDbContext();

            var stagingRows = await context.BeneficiaryStaging
                .AsNoTracking()
                .OrderByDescending(row => row.ImportedAt)
                .ToListAsync();

            var households = await context.Households
                .AsNoTracking()
                .Include(household => household.Members)
                .OrderBy(household => household.HouseholdCode)
                .ThenBy(household => household.HeadName)
                .ToListAsync();

            _records.Clear();
            foreach (var row in stagingRows)
            {
                _records.Add(StagedBeneficiaryItem.FromEntity(row));
            }

            _households.Clear();
            foreach (var household in households)
            {
                _households.Add(HouseholdOption.FromEntity(household));
            }

            UpdateCounts();

            RecordsView = CollectionViewSource.GetDefaultView(_records);
            ApplyFilter();

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
        }

        private void UpdateCounts()
        {
            TotalCount = _records.Count;
            PendingCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Pending);
            VerifiedCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Verified);
            ApprovedCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Approved);
            DuplicateCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Duplicate);
            InactiveCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Inactive);
            RejectedCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Rejected);
        }

        private void ApplyFilter()
        {
            RecordsView.Filter = item =>
            {
                if (item is not StagedBeneficiaryItem row)
                {
                    return false;
                }

                if (SelectedStatusFilter != "All" &&
                    !string.Equals(row.StatusText, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                return Contains(row.FullName, SearchText)
                    || Contains(row.BeneficiaryId, SearchText)
                    || Contains(row.CivilRegistryId, SearchText)
                    || Contains(row.Address, SearchText)
                    || Contains(row.ReviewNotes, SearchText);
            };

            RecordsView.Refresh();
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

                await LoadCoreAsync(stagingId);
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

                await LoadCoreAsync(stagingId);
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
                        NormalizeNullable(EditableReviewNotes)),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadCoreAsync(stagingId);
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

                await LoadCoreAsync(stagingId);
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

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
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

        public static StagedBeneficiaryItem FromEntity(BeneficiaryStaging row)
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
                LinkedHouseholdMemberId = row.LinkedHouseholdMemberId
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

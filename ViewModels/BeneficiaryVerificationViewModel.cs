using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
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
        private readonly ObservableCollection<StagedBeneficiaryItem> _records = new();
        private readonly ObservableCollection<HouseholdOption> _households = new();
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _approveCommand;
        private readonly RelayCommand _rejectCommand;
        private ICollectionView _recordsView;
        private StagedBeneficiaryItem? _selectedBeneficiary;
        private HouseholdOption? _selectedHousehold;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "Pending";
        private bool _isBusy;
        private string _statusMessage = "Loading staged beneficiaries...";
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private int _totalCount;
        private int _pendingCount;
        private int _approvedCount;
        private int _rejectedCount;

        public BeneficiaryVerificationViewModel()
        {
            StatusFilters = new ObservableCollection<string>
            {
                "Pending",
                "Approved",
                "Rejected",
                "All"
            };

            _recordsView = CollectionViewSource.GetDefaultView(_records);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _approveCommand = new RelayCommand(async _ => await ApproveSelectedAsync(), _ => CanApproveSelected());
            _rejectCommand = new RelayCommand(async _ => await RejectSelectedAsync(), _ => CanRejectSelected());

            ApplyFilter();
            _ = LoadAsync();
        }

        public ObservableCollection<string> StatusFilters { get; }

        public ObservableCollection<HouseholdOption> Households => _households;

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
                    if (value != null && SelectedHousehold == null)
                    {
                        SelectedHousehold = _households.FirstOrDefault();
                    }

                    _approveCommand.RaiseCanExecuteChanged();
                    _rejectCommand.RaiseCanExecuteChanged();
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
                    _approveCommand.RaiseCanExecuteChanged();
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
                    _refreshCommand.RaiseCanExecuteChanged();
                    _approveCommand.RaiseCanExecuteChanged();
                    _rejectCommand.RaiseCanExecuteChanged();
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

        public int ApprovedCount
        {
            get => _approvedCount;
            private set => SetProperty(ref _approvedCount, value);
        }

        public int RejectedCount
        {
            get => _rejectedCount;
            private set => SetProperty(ref _rejectedCount, value);
        }

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand ApproveCommand => _approveCommand;
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
                await using var context = new AppDbContext();

                var stagingRows = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .OrderByDescending(row => row.ImportedAt)
                    .ToListAsync();

                var households = await context.Households
                    .AsNoTracking()
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
                    _households.Add(new HouseholdOption
                    {
                        Id = household.Id,
                        HouseholdCode = household.HouseholdCode,
                        HeadName = household.HeadName,
                        AddressLine = household.AddressLine
                    });
                }

                TotalCount = _records.Count;
                PendingCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Pending);
                ApprovedCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Approved);
                RejectedCount = _records.Count(row => row.VerificationStatus == VerificationStatus.Rejected);

                RecordsView = CollectionViewSource.GetDefaultView(_records);
                ApplyFilter();

                SelectedBeneficiary = _records.FirstOrDefault(row => row.VerificationStatus == VerificationStatus.Pending)
                    ?? _records.FirstOrDefault();

                if (SelectedHousehold == null)
                {
                    SelectedHousehold = _households.FirstOrDefault();
                }

                SetSuccessStatus($"Loaded {TotalCount:N0} staged beneficiary record(s).");
            }
            catch (Exception ex)
            {
                _records.Clear();
                _households.Clear();
                RecordsView = CollectionViewSource.GetDefaultView(_records);
                SelectedBeneficiary = null;
                SelectedHousehold = null;
                TotalCount = 0;
                PendingCount = 0;
                ApprovedCount = 0;
                RejectedCount = 0;
                SetErrorStatus($"Unable to load staged beneficiaries: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
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
                    || Contains(row.CivilRegistryId, SearchText)
                    || Contains(row.Address, SearchText);
            };

            RecordsView.Refresh();
        }

        private bool CanApproveSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus == VerificationStatus.Pending
                && SelectedHousehold != null;
        }

        private bool CanRejectSelected()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && SelectedBeneficiary.VerificationStatus == VerificationStatus.Pending;
        }

        private async Task ApproveSelectedAsync()
        {
            if (!CanApproveSelected() || SelectedBeneficiary == null || SelectedHousehold == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Approve {SelectedBeneficiary.FullName} and add the beneficiary to household {SelectedHousehold.HouseholdCode}?",
                "Approve Beneficiary",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Approving {SelectedBeneficiary.FullName}...");

            try
            {
                await using var context = new AppDbContext();
                var stagingRow = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(row => row.StagingID == SelectedBeneficiary.StagingId);

                if (stagingRow == null)
                {
                    SetErrorStatus("The selected staging record no longer exists.");
                    return;
                }

                if (stagingRow.VerificationStatus != VerificationStatus.Pending)
                {
                    SetErrorStatus("Only pending records can be approved.");
                    return;
                }

                var fullName = BuildDisplayName(stagingRow);

                context.HouseholdMembers.Add(new HouseholdMember
                {
                    HouseholdId = SelectedHousehold.Id,
                    FullName = fullName,
                    RelationshipToHead = "Imported Beneficiary",
                    Occupation = "Unspecified",
                    IsCashForWorkEligible = false,
                    Notes = BuildApprovalNotes(stagingRow),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });

                stagingRow.VerificationStatus = VerificationStatus.Approved;
                await context.SaveChangesAsync();

                SetSuccessStatus($"Approved {fullName} into household {SelectedHousehold.HouseholdCode}.");
                await LoadAsync();
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

        private async Task RejectSelectedAsync()
        {
            if (!CanRejectSelected() || SelectedBeneficiary == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Reject {SelectedBeneficiary.FullName}?\n\nNo household member will be created.",
                "Reject Beneficiary",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Rejecting {SelectedBeneficiary.FullName}...");

            try
            {
                await using var context = new AppDbContext();
                var stagingRow = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(row => row.StagingID == SelectedBeneficiary.StagingId);

                if (stagingRow == null)
                {
                    SetErrorStatus("The selected staging record no longer exists.");
                    return;
                }

                if (stagingRow.VerificationStatus != VerificationStatus.Pending)
                {
                    SetErrorStatus("Only pending records can be rejected.");
                    return;
                }

                stagingRow.VerificationStatus = VerificationStatus.Rejected;
                await context.SaveChangesAsync();

                SetSuccessStatus($"Rejected {SelectedBeneficiary.FullName}.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Rejection failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string BuildDisplayName(BeneficiaryStaging row)
        {
            if (!string.IsNullOrWhiteSpace(row.FullName))
            {
                return row.FullName.Trim();
            }

            return string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string BuildApprovalNotes(BeneficiaryStaging row)
        {
            var notes = new List<string>();

            if (!string.IsNullOrWhiteSpace(row.CivilRegistryId))
            {
                notes.Add($"CivilRegistryId: {row.CivilRegistryId}");
            }

            if (!string.IsNullOrWhiteSpace(row.BeneficiaryId))
            {
                notes.Add($"BeneficiaryId: {row.BeneficiaryId}");
            }

            if (row.IsPwd)
            {
                notes.Add("PWD");
            }

            if (row.IsSenior)
            {
                notes.Add("Senior");
            }

            return string.Join(" | ", notes);
        }

        private static bool Contains(string? source, string searchText)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(searchText, StringComparison.OrdinalIgnoreCase);
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

    public sealed class StagedBeneficiaryItem
    {
        public int StagingId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string CivilRegistryId { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Sex { get; init; } = string.Empty;
        public string Age { get; init; } = string.Empty;
        public bool IsPwd { get; init; }
        public bool IsSenior { get; init; }
        public VerificationStatus VerificationStatus { get; init; }
        public DateTime ImportedAt { get; init; }
        public string BeneficiaryId { get; init; } = string.Empty;
        public string DateOfBirth { get; init; } = string.Empty;
        public string MaritalStatus { get; init; } = string.Empty;
        public string DisabilityType { get; init; } = string.Empty;
        public string SeniorIdNo { get; init; } = string.Empty;
        public string PwdIdNo { get; init; } = string.Empty;

        public string StatusText => VerificationStatus.ToString();
        public string PwdLabel => IsPwd ? "Yes" : "No";
        public string SeniorLabel => IsSenior ? "Yes" : "No";
        public string SexAgeSummary => string.IsNullOrWhiteSpace(Age) ? Sex : $"{Sex} • {Age}";
        public string PwdDetails => string.IsNullOrWhiteSpace(PwdIdNo) ? "No PWD ID" : $"PWD ID: {PwdIdNo}";
        public string SeniorDetails => string.IsNullOrWhiteSpace(SeniorIdNo) ? "No Senior ID" : $"Senior ID: {SeniorIdNo}";
        public Brush StatusBrush => VerificationStatus switch
        {
            VerificationStatus.Approved => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")),
            VerificationStatus.Rejected => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"))
        };

        public Brush StatusTextBrush => VerificationStatus switch
        {
            VerificationStatus.Approved => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534")),
            VerificationStatus.Rejected => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E"))
        };

        public static StagedBeneficiaryItem FromEntity(BeneficiaryStaging row)
        {
            return new StagedBeneficiaryItem
            {
                StagingId = row.StagingID,
                FullName = string.IsNullOrWhiteSpace(row.FullName)
                    ? string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim()))
                    : row.FullName.Trim(),
                CivilRegistryId = row.CivilRegistryId ?? string.Empty,
                Address = row.Address ?? string.Empty,
                Sex = row.Sex ?? string.Empty,
                Age = row.Age ?? string.Empty,
                IsPwd = row.IsPwd,
                IsSenior = row.IsSenior,
                VerificationStatus = row.VerificationStatus,
                ImportedAt = row.ImportedAt,
                BeneficiaryId = row.BeneficiaryId ?? string.Empty,
                DateOfBirth = row.DateOfBirth ?? string.Empty,
                MaritalStatus = row.MaritalStatus ?? string.Empty,
                DisabilityType = row.DisabilityType ?? string.Empty,
                SeniorIdNo = row.SeniorIdNo ?? string.Empty,
                PwdIdNo = row.PwdIdNo ?? string.Empty
            };
        }
    }

    public sealed class HouseholdOption
    {
        public int Id { get; init; }
        public string HouseholdCode { get; init; } = string.Empty;
        public string HeadName { get; init; } = string.Empty;
        public string AddressLine { get; init; } = string.Empty;
        public string DisplayLabel => $"{HouseholdCode} • {HeadName}";
    }
}

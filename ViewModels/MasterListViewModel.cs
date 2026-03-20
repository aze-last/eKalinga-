using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class MasterListViewModel : ObservableObject
    {
        private readonly ObservableCollection<MasterListBeneficiary> _beneficiaries = new();
        private readonly RelayCommand _refreshCommand;
        private ICollectionView _beneficiariesView;
        private MasterListBeneficiary? _selectedBeneficiary;
        private string _searchText = string.Empty;
        private string _selectedQuickFilter = "All beneficiaries";
        private bool _isBusy;
        private string _statusMessage = "Loading local beneficiary snapshot...";
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private int _totalBeneficiaries;
        private int _linkedCivilRegistryCount;
        private int _seniorCount;
        private int _pwdCount;
        private string _snapshotSourceSummary = "Local snapshot";
        private string _lastUpdatedSummary = "Last refresh: --";

        public MasterListViewModel()
        {
            QuickFilters = new ObservableCollection<string>
            {
                "All beneficiaries",
                "Senior citizens",
                "PWD",
                "With civil registry ID",
                "Missing civil registry ID"
            };

            _beneficiariesView = CollectionViewSource.GetDefaultView(_beneficiaries);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);

            ApplyFilter();
            _ = LoadAsync();
        }

        public ObservableCollection<string> QuickFilters { get; }

        public ICollectionView BeneficiariesView
        {
            get => _beneficiariesView;
            private set => SetProperty(ref _beneficiariesView, value);
        }

        public MasterListBeneficiary? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set => SetProperty(ref _selectedBeneficiary, value);
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

        public string SelectedQuickFilter
        {
            get => _selectedQuickFilter;
            set
            {
                if (SetProperty(ref _selectedQuickFilter, value))
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

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading local beneficiary snapshot...");

            try
            {
                var snapshot = await MasterListService.LoadLocalSnapshotAsync();

                _beneficiaries.Clear();
                foreach (var beneficiary in snapshot.Beneficiaries)
                {
                    _beneficiaries.Add(beneficiary);
                }

                TotalBeneficiaries = _beneficiaries.Count;
                LinkedCivilRegistryCount = _beneficiaries.Count(item => !string.IsNullOrWhiteSpace(item.CivilRegistryId));
                SeniorCount = _beneficiaries.Count(item => item.IsSenior);
                PwdCount = _beneficiaries.Count(item => item.IsPwd);

                SnapshotSourceSummary = $"Local snapshot from {snapshot.SourceDatabase} on {snapshot.SourceServer}";
                LastUpdatedSummary = snapshot.LastUpdatedAt.HasValue
                    ? $"Last synced data: {snapshot.LastUpdatedAt.Value:MMMM dd, yyyy hh:mm tt}"
                    : "Last synced data: unavailable";

                BeneficiariesView = CollectionViewSource.GetDefaultView(_beneficiaries);
                ApplyFilter();
                SelectedBeneficiary = _beneficiaries.FirstOrDefault();

                SetSuccessStatus($"Loaded {TotalBeneficiaries:N0} beneficiary record(s) from local snapshot.");
            }
            catch (Exception ex)
            {
                _beneficiaries.Clear();
                BeneficiariesView = CollectionViewSource.GetDefaultView(_beneficiaries);
                SelectedBeneficiary = null;
                TotalBeneficiaries = 0;
                LinkedCivilRegistryCount = 0;
                SeniorCount = 0;
                PwdCount = 0;
                SnapshotSourceSummary = "Local snapshot unavailable";
                LastUpdatedSummary = "Last synced data: unavailable";
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter()
        {
            BeneficiariesView.Filter = item =>
            {
                if (item is not MasterListBeneficiary beneficiary)
                {
                    return false;
                }

                if (!MatchesQuickFilter(beneficiary))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                return Contains(beneficiary.DisplayName, SearchText)
                    || Contains(beneficiary.BeneficiaryId, SearchText)
                    || Contains(beneficiary.CivilRegistryId, SearchText)
                    || Contains(beneficiary.Address, SearchText)
                    || Contains(beneficiary.Sex, SearchText);
            };

            BeneficiariesView.Refresh();
        }

        private bool MatchesQuickFilter(MasterListBeneficiary beneficiary)
        {
            return SelectedQuickFilter switch
            {
                "Senior citizens" => beneficiary.IsSenior,
                "PWD" => beneficiary.IsPwd,
                "With civil registry ID" => !string.IsNullOrWhiteSpace(beneficiary.CivilRegistryId),
                "Missing civil registry ID" => string.IsNullOrWhiteSpace(beneficiary.CivilRegistryId),
                _ => true
            };
        }

        private static bool Contains(string source, string searchText)
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
}

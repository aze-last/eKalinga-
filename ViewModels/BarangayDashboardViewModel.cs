using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BarangayDashboardViewModel : ObservableObject
    {
        private readonly BarangayDashboardService _dashboardService;
        private readonly RelayCommand _refreshCommand;
        private string _todayLabel = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        private string _timeLabel = DateTime.Now.ToString("hh:mm tt");
        private string _activeDatabaseLabel = "Loading active database...";
        private string _lastRefreshLabel = "Last refresh: --";
        private string _statusMessage = "Loading dashboard...";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private bool _isBusy;
        private int _aidRequestCount;
        private int _pendingReviewCount;
        private int _budgetAlertCount;
        private int _distributionCount;
        private int _householdCount;
        private int _masterListCount;
        private int _approvedBeneficiaryCount;
        private int _rejectedBeneficiaryCount;
        private int _cashForWorkBeneficiaryCount;
        private int _openEventCount;
        private int _todayAttendanceCount;
        private int _completedEventsThisMonthCount;
        private int _overdueBorrowingCount;

        public BarangayDashboardViewModel()
            : this(loadImmediately: true)
        {
        }

        private BarangayDashboardViewModel(bool loadImmediately)
        {
            _dashboardService = new BarangayDashboardService();
            RecentActivities = new ObservableCollection<BarangayDashboardRecentActivityItem>();
            TodaySummaries = new ObservableCollection<BarangayDashboardTodaySummaryItem>();
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);

            if (loadImmediately)
            {
                _ = LoadAsync();
            }
        }

        public static BarangayDashboardViewModel CreateDesignTime()
        {
            var viewModel = new BarangayDashboardViewModel(loadImmediately: false)
            {
                _todayLabel = "Thursday, April 23, 2026",
                _timeLabel = "05:32 PM",
                _activeDatabaseLabel = "Local: 127.0.0.1:3306 / attendance_shifting_db",
                _lastRefreshLabel = "Updated April 23, 2026 05:32 PM",
                _statusMessage = "Dashboard preview is ready.",
                _statusBrush = CreateBrush("#1A7A4A"),
                _aidRequestCount = 14,
                _pendingReviewCount = 7,
                _budgetAlertCount = 2,
                _distributionCount = 9,
                _householdCount = 128,
                _masterListCount = 412,
                _approvedBeneficiaryCount = 365,
                _rejectedBeneficiaryCount = 11,
                _cashForWorkBeneficiaryCount = 26,
                _openEventCount = 3,
                _todayAttendanceCount = 18,
                _completedEventsThisMonthCount = 4
            };

            viewModel.RecentActivities.Add(new BarangayDashboardRecentActivityItem
            {
                Title = "Aid request approved",
                Detail = "Funeral assistance release prepared for household CRZ-021.",
                TimeLabel = "04:50 PM"
            });

            viewModel.RecentActivities.Add(new BarangayDashboardRecentActivityItem
            {
                Title = "Budget ledger updated",
                Detail = "Rice support allocation posted to April release ledger.",
                TimeLabel = "03:40 PM"
            });

            viewModel.TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Aid Requests Today",
                Value = "12",
                Note = "New requests recorded today"
            });

            viewModel.TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Distributions Today",
                Value = "9",
                Note = "Claimed project distributions"
            });

            viewModel.TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Attendance Logged",
                Value = "18",
                Note = "Cash-for-work attendance entries"
            });

            viewModel.TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Released Amount",
                Value = "PHP 48,250.00",
                Note = "Total release ledger amount for today"
            });

            return viewModel;
        }

        public ObservableCollection<BarangayDashboardRecentActivityItem> RecentActivities { get; }

        public ObservableCollection<BarangayDashboardTodaySummaryItem> TodaySummaries { get; }

        public string TodayLabel
        {
            get => _todayLabel;
            private set => SetProperty(ref _todayLabel, value);
        }

        public string TimeLabel
        {
            get => _timeLabel;
            private set => SetProperty(ref _timeLabel, value);
        }

        public string ActiveDatabaseLabel
        {
            get => _activeDatabaseLabel;
            private set => SetProperty(ref _activeDatabaseLabel, value);
        }

        public string LastRefreshLabel
        {
            get => _lastRefreshLabel;
            private set => SetProperty(ref _lastRefreshLabel, value);
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

        public int AidRequestCount
        {
            get => _aidRequestCount;
            private set => SetProperty(ref _aidRequestCount, value);
        }

        public int PendingReviewCount
        {
            get => _pendingReviewCount;
            private set => SetProperty(ref _pendingReviewCount, value);
        }

        public int BudgetAlertCount
        {
            get => _budgetAlertCount;
            private set => SetProperty(ref _budgetAlertCount, value);
        }

        public int DistributionCount
        {
            get => _distributionCount;
            private set => SetProperty(ref _distributionCount, value);
        }

        public int HouseholdCount
        {
            get => _householdCount;
            private set => SetProperty(ref _householdCount, value);
        }

        public int MasterListCount
        {
            get => _masterListCount;
            private set => SetProperty(ref _masterListCount, value);
        }

        public int ApprovedBeneficiaryCount
        {
            get => _approvedBeneficiaryCount;
            private set => SetProperty(ref _approvedBeneficiaryCount, value);
        }

        public int RejectedBeneficiaryCount
        {
            get => _rejectedBeneficiaryCount;
            private set => SetProperty(ref _rejectedBeneficiaryCount, value);
        }

        public int CashForWorkBeneficiaryCount
        {
            get => _cashForWorkBeneficiaryCount;
            private set => SetProperty(ref _cashForWorkBeneficiaryCount, value);
        }

        public int OpenEventCount
        {
            get => _openEventCount;
            private set => SetProperty(ref _openEventCount, value);
        }

        public int TodayAttendanceCount
        {
            get => _todayAttendanceCount;
            private set => SetProperty(ref _todayAttendanceCount, value);
        }

        public int CompletedEventsThisMonthCount
        {
            get => _completedEventsThisMonthCount;
            private set => SetProperty(ref _completedEventsThisMonthCount, value);
        }

        public int OverdueBorrowingCount
        {
            get => _overdueBorrowingCount;
            private set => SetProperty(ref _overdueBorrowingCount, value);
        }

        public bool HasRecentActivities => RecentActivities.Count > 0;

        public ICommand RefreshCommand => _refreshCommand;

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            TodayLabel = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            TimeLabel = DateTime.Now.ToString("hh:mm tt");
            SetNeutralStatus("Loading dashboard...");

            try
            {
                var snapshot = await _dashboardService.LoadAsync();

                ActiveDatabaseLabel = snapshot.ActiveDatabaseLabel;
                LastRefreshLabel = $"Updated {snapshot.RetrievedAt:MMMM dd, yyyy hh:mm tt}";

                AidRequestCount = snapshot.AidRequestCount;
                PendingReviewCount = snapshot.PendingBeneficiaries;
                BudgetAlertCount = snapshot.BudgetAlertCount;
                DistributionCount = snapshot.DistributionCount;
                HouseholdCount = snapshot.HouseholdCount;
                MasterListCount = snapshot.MasterListCount;
                ApprovedBeneficiaryCount = snapshot.ApprovedBeneficiaries;
                RejectedBeneficiaryCount = snapshot.RejectedBeneficiaries;
                CashForWorkBeneficiaryCount = snapshot.CashForWorkBeneficiaryCount;
                OpenEventCount = snapshot.OpenCashForWorkEvents;
                TodayAttendanceCount = snapshot.TodayAttendanceCount;
                CompletedEventsThisMonthCount = snapshot.CompletedEventsThisMonth;
                OverdueBorrowingCount = snapshot.OverdueBorrowingCount;

                BuildRecentActivities(snapshot);
                BuildTodaySummaries(snapshot);

                SetSuccessStatus("Dashboard is ready.");
            }
            catch (Exception ex)
            {
                RecentActivities.Clear();
                TodaySummaries.Clear();
                OnPropertyChanged(nameof(HasRecentActivities));
                SetErrorStatus($"Unable to load the dashboard: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BuildRecentActivities(BarangayDashboardSnapshot snapshot)
        {
            RecentActivities.Clear();

            foreach (var item in snapshot.RecentActivities)
            {
                RecentActivities.Add(new BarangayDashboardRecentActivityItem
                {
                    Title = item.Title,
                    Detail = item.Detail,
                    TimeLabel = item.OccurredAt.ToString("hh:mm tt")
                });
            }

            OnPropertyChanged(nameof(HasRecentActivities));
        }

        private void BuildTodaySummaries(BarangayDashboardSnapshot snapshot)
        {
            TodaySummaries.Clear();

            TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Aid Requests Today",
                Value = snapshot.AidRequestsToday.ToString("N0"),
                Note = "New requests recorded today"
            });

            TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Distributions Today",
                Value = snapshot.DistributionsToday.ToString("N0"),
                Note = "Claimed project distributions"
            });

            TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Attendance Logged",
                Value = snapshot.TodayAttendanceCount.ToString("N0"),
                Note = "Cash-for-work attendance entries"
            });

            TodaySummaries.Add(new BarangayDashboardTodaySummaryItem
            {
                Label = "Released Amount",
                Value = $"₱{snapshot.ReleasedAmountToday:N2}",
                Note = "Total release ledger amount for today"
            });
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

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }

    public sealed class BarangayDashboardRecentActivityItem
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string TimeLabel { get; init; } = string.Empty;
    }

    public sealed class BarangayDashboardTodaySummaryItem
    {
        public string Label { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
    }
}

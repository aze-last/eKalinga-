using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
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
        private int _masterListCount;
        private int _pendingReviewCount;
        private int _approvedBeneficiaryCount;
        private int _rejectedBeneficiaryCount;
        private int _cashForWorkBeneficiaryCount;
        private int _openEventCount;
        private int _todayAttendanceCount;
        private int _completedEventsThisMonthCount;

        public BarangayDashboardViewModel()
        {
            _dashboardService = new BarangayDashboardService();
            ModuleCards = new ObservableCollection<BarangayDashboardModuleCard>();
            UpcomingEvents = new ObservableCollection<BarangayDashboardEventCard>();
            RecentImports = new ObservableCollection<BarangayDashboardImportCard>();
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);

            _ = LoadAsync();
        }

        public ObservableCollection<BarangayDashboardModuleCard> ModuleCards { get; }

        public ObservableCollection<BarangayDashboardEventCard> UpcomingEvents { get; }

        public ObservableCollection<BarangayDashboardImportCard> RecentImports { get; }

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

        public int MasterListCount
        {
            get => _masterListCount;
            private set => SetProperty(ref _masterListCount, value);
        }

        public int PendingReviewCount
        {
            get => _pendingReviewCount;
            private set => SetProperty(ref _pendingReviewCount, value);
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

        public bool HasUpcomingEvents => UpcomingEvents.Count > 0;

        public bool HasRecentImports => RecentImports.Count > 0;

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
            SetNeutralStatus("Loading barangay operations dashboard...");

            try
            {
                var snapshot = await _dashboardService.LoadAsync();

                ActiveDatabaseLabel = snapshot.ActiveDatabaseLabel;
                LastRefreshLabel = $"Last refresh: {snapshot.RetrievedAt:MMMM dd, yyyy hh:mm tt}";

                MasterListCount = snapshot.MasterListCount;
                PendingReviewCount = snapshot.PendingBeneficiaries;
                ApprovedBeneficiaryCount = snapshot.ApprovedBeneficiaries;
                RejectedBeneficiaryCount = snapshot.RejectedBeneficiaries;
                CashForWorkBeneficiaryCount = snapshot.CashForWorkBeneficiaryCount;
                OpenEventCount = snapshot.OpenCashForWorkEvents;
                TodayAttendanceCount = snapshot.TodayAttendanceCount;
                CompletedEventsThisMonthCount = snapshot.CompletedEventsThisMonth;

                BuildModuleCards(snapshot);
                BuildUpcomingEvents(snapshot);
                BuildRecentImports(snapshot);

                SetSuccessStatus("Dashboard is ready.");
            }
            catch (Exception ex)
            {
                ModuleCards.Clear();
                UpcomingEvents.Clear();
                RecentImports.Clear();
                OnPropertyChanged(nameof(HasUpcomingEvents));
                OnPropertyChanged(nameof(HasRecentImports));
                SetErrorStatus($"Unable to load the dashboard: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BuildModuleCards(BarangayDashboardSnapshot snapshot)
        {
            ModuleCards.Clear();

            ModuleCards.Add(new BarangayDashboardModuleCard
            {
                Title = "Validated beneficiaries snapshot",
                Summary = snapshot.MasterListAvailable
                    ? $"{snapshot.MasterListCount:N0} record(s) available"
                    : "Snapshot unavailable",
                Detail = snapshot.MasterListAvailable && snapshot.MasterListUpdatedAt.HasValue
                    ? $"Last synced {snapshot.MasterListUpdatedAt.Value:MMM dd, yyyy hh:mm tt}"
                    : snapshot.MasterListStatusText,
                StatusText = snapshot.MasterListAvailable ? "Ready" : "Needs snapshot",
                IconKind = "TableLarge",
                AccentBrush = CreateBrush("#0D2B6E"),
                StatusBackground = snapshot.MasterListAvailable ? CreateBrush("#DBEAFE") : CreateBrush("#FEF3C7"),
                StatusForeground = snapshot.MasterListAvailable ? CreateBrush("#1D4ED8") : CreateBrush("#92400E")
            });

            ModuleCards.Add(new BarangayDashboardModuleCard
            {
                Title = "Validated beneficiaries",
                Summary = $"{snapshot.PendingBeneficiaries:N0} pending approval",
                Detail = $"{snapshot.ApprovedBeneficiaries:N0} approved | {snapshot.RejectedBeneficiaries:N0} rejected",
                StatusText = snapshot.PendingBeneficiaries > 0 ? "Needs approval" : "Up to date",
                IconKind = "AccountCheckOutline",
                AccentBrush = CreateBrush("#F59E0B"),
                StatusBackground = snapshot.PendingBeneficiaries > 0 ? CreateBrush("#FEF3C7") : CreateBrush("#DCFCE7"),
                StatusForeground = snapshot.PendingBeneficiaries > 0 ? CreateBrush("#92400E") : CreateBrush("#166534")
            });

            ModuleCards.Add(new BarangayDashboardModuleCard
            {
                Title = "Cash-for-work",
                Summary = $"{snapshot.OpenCashForWorkEvents:N0} open event(s)",
                Detail = $"{snapshot.TodayAttendanceCount:N0} attendance record(s) today | {snapshot.CompletedEventsThisMonth:N0} completed this month",
                StatusText = snapshot.OpenCashForWorkEvents > 0 ? "Active" : "Monitoring",
                IconKind = "CalendarClock",
                AccentBrush = CreateBrush("#991B1B"),
                StatusBackground = snapshot.OpenCashForWorkEvents > 0 ? CreateBrush("#FEE2E2") : CreateBrush("#E5E7EB"),
                StatusForeground = snapshot.OpenCashForWorkEvents > 0 ? CreateBrush("#991B1B") : CreateBrush("#374151")
            });
        }

        private void BuildUpcomingEvents(BarangayDashboardSnapshot snapshot)
        {
            UpcomingEvents.Clear();

            foreach (var cashForWorkEvent in snapshot.UpcomingEvents)
            {
                UpcomingEvents.Add(new BarangayDashboardEventCard
                {
                    Title = cashForWorkEvent.Title,
                    Location = cashForWorkEvent.Location,
                    ScheduleLabel = $"{cashForWorkEvent.EventDate:dddd, MMM dd} | {cashForWorkEvent.StartTime:hh\\:mm}",
                    ParticipantsLabel = $"{cashForWorkEvent.ParticipantCount:N0} participant(s)",
                    StatusText = cashForWorkEvent.Status.ToString(),
                    StatusBackground = CreateStatusBackground(cashForWorkEvent.Status),
                    StatusForeground = CreateStatusForeground(cashForWorkEvent.Status)
                });
            }

            OnPropertyChanged(nameof(HasUpcomingEvents));
        }

        private void BuildRecentImports(BarangayDashboardSnapshot snapshot)
        {
            RecentImports.Clear();

            foreach (var import in snapshot.RecentImports)
            {
                RecentImports.Add(new BarangayDashboardImportCard
                {
                    FullName = string.IsNullOrWhiteSpace(import.FullName) ? "Unnamed beneficiary" : import.FullName,
                    Address = string.IsNullOrWhiteSpace(import.Address) ? "Address unavailable" : import.Address,
                    ImportedAtLabel = import.ImportedAt.ToString("MMM dd, yyyy hh:mm tt"),
                    StatusText = import.Status.ToString(),
                    StatusBackground = CreateVerificationBackground(import.Status),
                    StatusForeground = CreateVerificationForeground(import.Status)
                });
            }

            OnPropertyChanged(nameof(HasRecentImports));
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

        private static Brush CreateStatusBackground(CashForWorkEventStatus status)
        {
            return status switch
            {
                CashForWorkEventStatus.Open => CreateBrush("#DBEAFE"),
                CashForWorkEventStatus.Completed => CreateBrush("#DCFCE7"),
                CashForWorkEventStatus.Cancelled => CreateBrush("#FEE2E2"),
                _ => CreateBrush("#E5E7EB")
            };
        }

        private static Brush CreateStatusForeground(CashForWorkEventStatus status)
        {
            return status switch
            {
                CashForWorkEventStatus.Open => CreateBrush("#1D4ED8"),
                CashForWorkEventStatus.Completed => CreateBrush("#166534"),
                CashForWorkEventStatus.Cancelled => CreateBrush("#991B1B"),
                _ => CreateBrush("#374151")
            };
        }

        private static Brush CreateVerificationBackground(VerificationStatus status)
        {
            return status switch
            {
                VerificationStatus.Approved => CreateBrush("#DCFCE7"),
                VerificationStatus.Rejected => CreateBrush("#FEE2E2"),
                _ => CreateBrush("#FEF3C7")
            };
        }

        private static Brush CreateVerificationForeground(VerificationStatus status)
        {
            return status switch
            {
                VerificationStatus.Approved => CreateBrush("#166534"),
                VerificationStatus.Rejected => CreateBrush("#991B1B"),
                _ => CreateBrush("#92400E")
            };
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }

    public sealed class BarangayDashboardModuleCard
    {
        public string Title { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string IconKind { get; init; } = string.Empty;
        public Brush AccentBrush { get; init; } = Brushes.Transparent;
        public Brush StatusBackground { get; init; } = Brushes.Transparent;
        public Brush StatusForeground { get; init; } = Brushes.Black;
    }

    public sealed class BarangayDashboardEventCard
    {
        public string Title { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string ScheduleLabel { get; init; } = string.Empty;
        public string ParticipantsLabel { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public Brush StatusBackground { get; init; } = Brushes.Transparent;
        public Brush StatusForeground { get; init; } = Brushes.Black;
    }

    public sealed class BarangayDashboardImportCard
    {
        public string FullName { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string ImportedAtLabel { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public Brush StatusBackground { get; init; } = Brushes.Transparent;
        public Brush StatusForeground { get; init; } = Brushes.Black;
    }
}

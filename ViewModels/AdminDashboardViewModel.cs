using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.ViewModels
{
    public class AdminDashboardViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;
        private ImageSource? _userPhotoImage;
        private string _userDisplayName = "Admin";

        private int _totalEmployees;
        private int _presentCount;
        private int _absentCount;
        private int _lateCount;
        private int _overtimeCount;
        private int _pendingLeaveCount;
        private int _shiftCoveragePercent;
        private string _attendanceSummary = string.Empty;
        private string _shiftCoverageSummary = string.Empty;
        private string _lastMetricsRefresh = "--";
        private string _currentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        private string _currentTime = DateTime.Now.ToString("hh:mm:ss tt");

        public int TotalEmployees
        {
            get => _totalEmployees;
            set => SetProperty(ref _totalEmployees, value);
        }

        public int PresentCount
        {
            get => _presentCount;
            set => SetProperty(ref _presentCount, value);
        }

        public int AbsentCount
        {
            get => _absentCount;
            set => SetProperty(ref _absentCount, value);
        }

        public int LateCount
        {
            get => _lateCount;
            set => SetProperty(ref _lateCount, value);
        }

        public int OvertimeCount
        {
            get => _overtimeCount;
            set => SetProperty(ref _overtimeCount, value);
        }

        public int PendingLeaveCount
        {
            get => _pendingLeaveCount;
            set => SetProperty(ref _pendingLeaveCount, value);
        }

        public int ShiftCoveragePercent
        {
            get => _shiftCoveragePercent;
            set => SetProperty(ref _shiftCoveragePercent, value);
        }

        public string AttendanceSummary
        {
            get => _attendanceSummary;
            set => SetProperty(ref _attendanceSummary, value);
        }

        public string ShiftCoverageSummary
        {
            get => _shiftCoverageSummary;
            set => SetProperty(ref _shiftCoverageSummary, value);
        }

        public string LastMetricsRefresh
        {
            get => _lastMetricsRefresh;
            set => SetProperty(ref _lastMetricsRefresh, value);
        }

        public string CurrentDateTime
        {
            get => _currentDateTime;
            set => SetProperty(ref _currentDateTime, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public ImageSource? UserPhotoImage
        {
            get => _userPhotoImage;
            set => SetProperty(ref _userPhotoImage, value);
        }

        public string UserDisplayName
        {
            get => _userDisplayName;
            set => SetProperty(ref _userDisplayName, value);
        }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ObservableCollection<DashboardAlert> Alerts { get; } = new();
        public ObservableCollection<RecentActivity> RecentActivities { get; } = new();
        public ObservableCollection<AreaDistribution> AreaDistributions { get; } = new();

        private string _areaDistributionSummary = string.Empty;
        public string AreaDistributionSummary
        {
            get => _areaDistributionSummary;
            set => SetProperty(ref _areaDistributionSummary, value);
        }

        public System.Windows.Input.ICommand GeneratePayrollCommand { get; }
        public System.Windows.Input.ICommand AddEmployeeCommand { get; }
        public System.Windows.Input.ICommand ShowDashboardCommand { get; }
        public System.Windows.Input.ICommand ShowUsersCommand { get; }
        public System.Windows.Input.ICommand ShowEmployeesCommand { get; }
        public System.Windows.Input.ICommand ShowHouseholdsCommand { get; }
        public System.Windows.Input.ICommand ShowAllEmployeesCommand { get; }
        public System.Windows.Input.ICommand ShowHolidaysCommand { get; }
        public System.Windows.Input.ICommand ShowPayrollCommand { get; }
        public System.Windows.Input.ICommand ShowPositionsCommand { get; }
        public System.Windows.Input.ICommand ShowCashForWorkOcrCommand { get; }
        public System.Windows.Input.ICommand ShowLeaveApprovalsCommand { get; }
        public System.Windows.Input.ICommand OpenRecruitmentMetricsCommand { get; }
        public System.Windows.Input.ICommand OpenRetentionMetricsCommand { get; }
        public System.Windows.Input.ICommand OpenPerformanceMetricsCommand { get; }
        public System.Windows.Input.ICommand OpenEngagementMetricsCommand { get; }
        public System.Windows.Input.ICommand OpenDeiMetricsCommand { get; }
        public System.Windows.Input.ICommand OpenCompensationMetricsCommand { get; }
        public System.Windows.Input.ICommand OpenWorkforcePlanningMetricsCommand { get; }
        public System.Windows.Input.ICommand ShowProfileSettingsCommand { get; }
        public System.Windows.Input.ICommand ShowAttendanceStatusCommand { get; }
        public System.Windows.Input.ICommand OpenRoleSwitchCommand { get; }
        public System.Windows.Input.ICommand ReturnToAdminCommand { get; }

        public bool CanManageUsers => _currentUser.Role == UserRole.Admin;
        public bool CanManagePayroll => _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.HRStaff;
        public bool CanManageEmployees => _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.HRStaff;
        public bool CanManageLeaveApprovals => _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.HRStaff;
        public string PortalTitle => "Barangay Ayuda System";
        public string CurrentRoleLabel => _currentUser.Role switch
        {
            UserRole.Admin => "Administrator",
            UserRole.HRStaff => "Treasurer / Operations Staff",
            UserRole.ShiftManager => "Cash-for-Work Lead",
            UserRole.Manager => "Barangay Staff",
            UserRole.Crew => "Field Worker",
            _ => _currentUser.Role.ToString()
        };
        public bool CanUseRoleSwitch => RoleSwitchService.CanUseSwitcher(_currentUser);
        public bool CanReturnToAdmin => RoleSwitchService.CanReturnToAdmin;
        public bool IsImpersonating => SessionContext.IsImpersonating;
        public string ImpersonationBannerText =>
            SessionContext.IsImpersonating && SessionContext.LoginUser != null
                ? $"Demo Mode: Viewing as {_currentUser.Role} ({_currentUser.Email}). Original Admin: {SessionContext.LoginUser.Email}"
                : string.Empty;

        public AdminDashboardViewModel(User user)
        {
            _currentUser = user;
            _context = new AppDbContext();
            _currentView = CreateHomeDashboardView();

            LoadDashboardData();
            LoadUserSummary();

            GeneratePayrollCommand = new RelayCommand(_ => ExecuteShowPayroll());
            AddEmployeeCommand = new RelayCommand(ExecuteAddEmployee);

            ShowDashboardCommand = new RelayCommand(_ => CurrentView = CreateHomeDashboardView());
            ShowUsersCommand = new RelayCommand(_ => ExecuteShowUsers());
            ShowEmployeesCommand = new RelayCommand(_ => ExecuteShowHouseholds());
            ShowHouseholdsCommand = new RelayCommand(_ => ExecuteShowHouseholds());
            ShowAllEmployeesCommand = new RelayCommand(_ => ExecuteShowHouseholds());
            ShowHolidaysCommand = new RelayCommand(_ => CurrentView = new HolidaysPage());
            ShowPayrollCommand = new RelayCommand(_ => ExecuteShowPayroll());
            ShowPositionsCommand = new RelayCommand(_ => CurrentView = new PositionsPage());
            ShowCashForWorkOcrCommand = new RelayCommand(_ => ExecuteShowCashForWorkOcr());
            ShowLeaveApprovalsCommand = new RelayCommand(_ => ExecuteShowLeaveApprovals());
            OpenRecruitmentMetricsCommand = new RelayCommand(_ => ExecuteOpenRecruitmentMetrics());
            OpenRetentionMetricsCommand = new RelayCommand(_ => ExecuteOpenRetentionMetrics());
            OpenPerformanceMetricsCommand = new RelayCommand(_ => ExecuteOpenPerformanceMetrics());
            OpenEngagementMetricsCommand = new RelayCommand(_ => ExecuteOpenEngagementMetrics());
            OpenDeiMetricsCommand = new RelayCommand(_ => ExecuteOpenDeiMetrics());
            OpenCompensationMetricsCommand = new RelayCommand(_ => ExecuteOpenCompensationMetrics());
            OpenWorkforcePlanningMetricsCommand = new RelayCommand(_ => ExecuteOpenWorkforcePlanningMetrics());
            ShowProfileSettingsCommand = new RelayCommand(_ =>
            {
                var vm = new ProfileSettingsViewModel(_currentUser);
                vm.ProfileUpdated += LoadUserSummary;
                CurrentView = new ProfileSettingsPage
                {
                    DataContext = vm
                };
            });
            ShowAttendanceStatusCommand = new RelayCommand(param =>
            {
                var filter = param?.ToString() ?? "All";
                CurrentView = new AttendanceLogsPage
                {
                    DataContext = new AttendanceLogsViewModel(filter)
                };
            });
            OpenRoleSwitchCommand = new RelayCommand(_ => ExecuteOpenRoleSwitch());
            ReturnToAdminCommand = new RelayCommand(_ => ExecuteReturnToAdmin());

            WeakEventManager<DashboardEventBus, DashboardDataChangedEventArgs>.AddHandler(
                DashboardEventBus.Instance,
                nameof(DashboardEventBus.DashboardDataChanged),
                OnDashboardDataChanged);

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (_, _) =>
            {
                CurrentTime = DateTime.Now.ToString("hh:mm:ss tt");
                if (DateTime.Now.Second == 0)
                {
                    CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
                }
            };
            timer.Start();
        }

        private object CreateHomeDashboardView()
        {
            return new DashboardPage();
        }

        private void OnDashboardDataChanged(object? sender, DashboardDataChangedEventArgs args)
        {
            if (!IsDashboardRelevant(args.Domain))
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                LoadDashboardData();
                return;
            }

            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(LoadDashboardData));
        }

        private static bool IsDashboardRelevant(DashboardDataDomain domain)
        {
            return domain is DashboardDataDomain.Attendance
                or DashboardDataDomain.Employee
                or DashboardDataDomain.Leave
                or DashboardDataDomain.Payroll
                or DashboardDataDomain.Shift;
        }

        private void LoadDashboardData()
        {
            try
            {
                _context.ChangeTracker.Clear();

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                TotalEmployees = _context.Households
                    .AsNoTracking()
                    .Count(h => h.Status == HouseholdStatus.Active);

                var todayParticipants = _context.CashForWorkParticipants
                    .AsNoTracking()
                    .Count(participant =>
                        participant.Event.EventDate >= today &&
                        participant.Event.EventDate < tomorrow);

                PresentCount = _context.CashForWorkAttendances
                    .AsNoTracking()
                    .Count(attendance =>
                        attendance.AttendanceDate >= today &&
                        attendance.AttendanceDate < tomorrow &&
                        attendance.Status == CashForWorkAttendanceStatus.Present);

                PendingLeaveCount = _context.CashForWorkEvents
                    .AsNoTracking()
                    .Count(evt => evt.Status == CashForWorkEventStatus.Open || evt.Status == CashForWorkEventStatus.Draft);

                AbsentCount = Math.Max(0, todayParticipants - PresentCount);
                LateCount = _context.HouseholdMembers
                    .AsNoTracking()
                    .Count(member => member.IsCashForWorkEligible);
                OvertimeCount = _context.CashForWorkAttendances
                    .AsNoTracking()
                    .Count(attendance => attendance.Source == AttendanceCaptureSource.OcrUpload);

                AttendanceSummary = $"{PresentCount} recorded today | {LateCount} eligible members available";

                ShiftCoveragePercent = todayParticipants == 0
                    ? 0
                    : (int)Math.Round((double)PresentCount / todayParticipants * 100, MidpointRounding.AwayFromZero);

                ShiftCoverageSummary = $"{PresentCount}/{todayParticipants} participant attendance saved";

                BuildAlerts(todayParticipants);
                LoadRecentActivities(today);
                LoadAreaDistribution();

                LastMetricsRefresh = DateTime.Now.ToString("hh:mm:ss tt");
            }
            catch (Exception)
            {
                TotalEmployees = 0;
                PresentCount = 0;
                AbsentCount = 0;
                LateCount = 0;
                OvertimeCount = 0;
                PendingLeaveCount = 0;
                ShiftCoveragePercent = 0;
                AttendanceSummary = "No data";
                ShiftCoverageSummary = "No data";
                LastMetricsRefresh = "--";

                Alerts.Clear();
                Alerts.Add(new DashboardAlert { Message = "Unable to load dashboard metrics. Check database connection." });
                RecentActivities.Clear();
                AreaDistributions.Clear();
                AreaDistributionSummary = string.Empty;
            }
        }

        private void BuildAlerts(int todayParticipants)
        {
            Alerts.Clear();

            if (PendingLeaveCount > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{PendingLeaveCount} cash-for-work event(s) are still open for operations or completion." });
            }

            if (todayParticipants > 0 && AbsentCount > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{AbsentCount} approved participant(s) still have no saved attendance for today." });
            }

            if (TotalEmployees == 0)
            {
                Alerts.Add(new DashboardAlert { Message = "No households are registered yet. Add households before assistance operations." });
            }

            if (LateCount > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{LateCount} household member(s) are marked eligible for cash-for-work." });
            }

            if (Alerts.Count == 0)
            {
                Alerts.Add(new DashboardAlert { Message = "No critical alerts right now." });
            }
        }

        private void LoadRecentActivities(DateTime today)
        {
            RecentActivities.Clear();

            var activities = _context.CashForWorkAttendances
                .AsNoTracking()
                .Include(a => a.Participant)
                .ThenInclude(p => p.HouseholdMember)
                .Where(a =>
                    a.RecordedAt >= today &&
                    a.RecordedAt < today.AddDays(1))
                .OrderByDescending(a => a.RecordedAt)
                .Take(5)
                .ToList();

            foreach (var attendance in activities)
            {
                RecentActivities.Add(new RecentActivity
                {
                    Name = attendance.Participant.HouseholdMember.FullName,
                    Time = attendance.RecordedAt,
                    Status = attendance.Source == AttendanceCaptureSource.Manual ? "Manual" : "OCR",
                    StatusColor = attendance.Source == AttendanceCaptureSource.Manual ? "#0F766E" : "#2563EB"
                });
            }

            if (RecentActivities.Count == 0)
            {
                RecentActivities.Add(new RecentActivity
                {
                    Name = "No attendance saved yet",
                    Time = DateTime.Now,
                    Status = "Pending",
                    StatusColor = "#64748B"
                });
            }
        }

        private void LoadAreaDistribution()
        {
            AreaDistributions.Clear();

            var households = _context.Households
                .AsNoTracking()
                .Where(h => h.Status == HouseholdStatus.Active)
                .ToList();

            var total = Math.Max(1, households.Count);
            var purokCounts = households
                .GroupBy(h => h.Purok)
                .OrderByDescending(group => group.Count())
                .Take(4)
                .Select((group, index) => new
                {
                    Label = string.IsNullOrWhiteSpace(group.Key) ? $"Purok {index + 1}" : group.Key,
                    Count = group.Count(),
                    Color = index switch
                    {
                        0 => "#DA291C",
                        1 => "#F59E0B",
                        2 => "#2563EB",
                        _ => "#0F766E"
                    }
                });

            foreach (var purok in purokCounts)
            {
                AreaDistributions.Add(new AreaDistribution
                {
                    Label = purok.Label,
                    Percent = (double)purok.Count / total * 100.0,
                    Color = purok.Color
                });
            }

            AreaDistributionSummary = string.Join(" | ",
                AreaDistributions.Select(d => $"{d.Label}: {d.Percent:0}%"));
        }

        private void ExecuteAddEmployee(object? parameter)
        {
            if (!CanManageEmployees)
            {
                MessageBox.Show("You are not allowed to manage employees.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new EmployeeDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            dialog.ShowDialog();

            if (dialog.DataContext is EmployeeDialogViewModel vm && vm.DialogResult)
            {
                LoadDashboardData();
            }
        }

        private void ExecuteShowUsers()
        {
            if (!CanManageUsers)
            {
                MessageBox.Show("Only Admin can manage user accounts.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentView = new UsersPage(_currentUser);
        }

        private void ExecuteShowHouseholds()
        {
            if (!CanManageEmployees)
            {
                MessageBox.Show("You are not allowed to manage household records.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentView = new HouseholdRegistryPage();
        }

        private void ExecuteShowPayroll()
        {
            if (!CanManagePayroll)
            {
                MessageBox.Show("You are not allowed to process payroll.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentView = new PayrollPage(_currentUser.Id);
        }

        private void ExecuteShowLeaveApprovals()
        {
            if (!CanManageLeaveApprovals)
            {
                MessageBox.Show("You are not allowed to review leave requests.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentView = new LeaveApprovalPage
            {
                DataContext = new LeaveApprovalViewModel(_currentUser.Id)
            };
        }

        private void ExecuteShowCashForWorkOcr()
        {
            if (!CanManageEmployees)
            {
                MessageBox.Show("You are not allowed to manage cash-for-work attendance.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentView = new CashForWorkOcrPage(_currentUser);
        }

        private void ExecuteOpenRecruitmentMetrics()
        {
            CurrentView = new RecruitmentMetricsPage();
        }

        private void ExecuteOpenRetentionMetrics()
        {
            CurrentView = new RetentionTurnoverPage(_currentUser.Id);
        }

        private void ExecuteOpenPerformanceMetrics()
        {
            CurrentView = new PerformanceMetricsPage(_currentUser.Id);
        }

        private void ExecuteOpenEngagementMetrics()
        {
            CurrentView = new EngagementWellbeingPage(_currentUser.Id);
        }

        private void ExecuteOpenDeiMetrics()
        {
            CurrentView = new DeiMetricsPage();
        }

        private void ExecuteOpenCompensationMetrics()
        {
            CurrentView = new CompensationBenefitsPage();
        }

        private void ExecuteOpenWorkforcePlanningMetrics()
        {
            CurrentView = new WorkforcePlanningPage();
        }

        private void ExecuteOpenRoleSwitch()
        {
            if (!RoleSwitchService.IsEnabled)
            {
                MessageBox.Show("Demo role switch is disabled. Set AppSettings:EnableDemoRoleSwitch=true.", "Feature Disabled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!CanUseRoleSwitch)
            {
                MessageBox.Show("Only Admin can switch roles.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var owner = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w is MainWindow && w.DataContext == this);

            var dialog = new RoleSwitchWindow(_currentUser)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() == true && dialog.SelectedUserId.HasValue)
            {
                RoleSwitchService.SwitchToUser(dialog.SelectedUserId.Value, owner, _currentUser);
            }
        }

        private void ExecuteReturnToAdmin()
        {
            if (!CanReturnToAdmin)
            {
                return;
            }

            var owner = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w is MainWindow && w.DataContext == this);

            RoleSwitchService.ReturnToAdmin(owner);
        }

        private void LoadUserSummary()
        {
            var profile = _context.UserProfiles.FirstOrDefault(p => p.UserId == _currentUser.Id);
            var employee = _context.Employees.FirstOrDefault(e => e.UserId == _currentUser.Id);

            UserDisplayName = !string.IsNullOrWhiteSpace(profile?.FullName)
                ? profile.FullName
                : employee?.FullName ?? _currentUser.Username;

            UserPhotoImage = BuildImage(profile?.PhotoPath);
        }

        private ImageSource? BuildImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }

    public class DashboardAlert
    {
        public string Message { get; set; } = string.Empty;
    }

    public class RecentActivity
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
    }

    public class AreaDistribution
    {
        public string Label { get; set; } = string.Empty;
        public double Percent { get; set; }
        public string Color { get; set; } = "#64748B";
    }
}

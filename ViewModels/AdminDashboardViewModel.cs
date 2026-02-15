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
        public System.Windows.Input.ICommand ShowAllEmployeesCommand { get; }
        public System.Windows.Input.ICommand ShowHolidaysCommand { get; }
        public System.Windows.Input.ICommand ShowPayrollCommand { get; }
        public System.Windows.Input.ICommand ShowPositionsCommand { get; }
        public System.Windows.Input.ICommand ShowLeaveApprovalsCommand { get; }
        public System.Windows.Input.ICommand ShowProfileSettingsCommand { get; }
        public System.Windows.Input.ICommand ShowAttendanceStatusCommand { get; }
        public System.Windows.Input.ICommand OpenRoleSwitchCommand { get; }
        public System.Windows.Input.ICommand ReturnToAdminCommand { get; }

        public bool CanManageUsers => _currentUser.Role == UserRole.Admin;
        public bool CanManagePayroll => _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.HRStaff;
        public bool CanManageEmployees => _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.HRStaff;
        public bool CanManageLeaveApprovals => _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.HRStaff;
        public string PortalTitle => _currentUser.Role == UserRole.HRStaff ? "HR Dashboard" : "Admin Dashboard";
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
            _currentView = new DashboardPage();

            LoadDashboardData();
            LoadUserSummary();

            GeneratePayrollCommand = new RelayCommand(_ => ExecuteShowPayroll());
            AddEmployeeCommand = new RelayCommand(ExecuteAddEmployee);

            ShowDashboardCommand = new RelayCommand(_ => CurrentView = new DashboardPage());
            ShowUsersCommand = new RelayCommand(_ => ExecuteShowUsers());
            ShowEmployeesCommand = new RelayCommand(_ => ExecuteShowEmployees());
            ShowAllEmployeesCommand = new RelayCommand(_ => ExecuteShowEmployees());
            ShowHolidaysCommand = new RelayCommand(_ => CurrentView = new HolidaysPage());
            ShowPayrollCommand = new RelayCommand(_ => ExecuteShowPayroll());
            ShowPositionsCommand = new RelayCommand(_ => CurrentView = new PositionsPage());
            ShowLeaveApprovalsCommand = new RelayCommand(_ => ExecuteShowLeaveApprovals());
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

                var now = DateTime.Now;
                var today = now.Date;
                var tomorrow = today.AddDays(1);

                TotalEmployees = _context.Employees
                    .AsNoTracking()
                    .Count(e => e.Status == EmployeeStatus.Active);

                var statusService = new AttendanceStatusService(_context);
                var statuses = statusService.GetTodayStatuses(now);

                AbsentCount = statuses.Count(s => s.Status == "Absent");
                LateCount = statuses.Count(s => s.Status == "Late");
                OvertimeCount = statuses.Count(s => s.Status == "Overtime");
                PresentCount = statuses.Count(s => s.Status is "On Time" or "Late" or "Overtime" or "Early Leave");
                AttendanceSummary = $"{PresentCount} present | {AbsentCount} absent | {LateCount} late";

                PendingLeaveCount = _context.LeaveRequests
                    .AsNoTracking()
                    .Count(lr => lr.Status == LeaveStatus.Pending);

                var assignedEmployeesToday = _context.ShiftAssignments
                    .AsNoTracking()
                    .Where(sa =>
                        sa.Shift.ShiftDate >= today &&
                        sa.Shift.ShiftDate < tomorrow &&
                        sa.Employee.Status == EmployeeStatus.Active)
                    .Select(sa => sa.EmployeeId)
                    .Distinct()
                    .Count();

                ShiftCoveragePercent = TotalEmployees == 0
                    ? 0
                    : (int)Math.Round((double)assignedEmployeesToday / TotalEmployees * 100, MidpointRounding.AwayFromZero);

                var unassigned = Math.Max(0, TotalEmployees - assignedEmployeesToday);
                ShiftCoverageSummary = $"{assignedEmployeesToday}/{TotalEmployees} assigned | {unassigned} unassigned";

                BuildAlerts(unassigned);
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

        private void BuildAlerts(int unassignedEmployees)
        {
            Alerts.Clear();

            if (PendingLeaveCount > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{PendingLeaveCount} pending leave request(s) waiting for review." });
            }

            if (AbsentCount > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{AbsentCount} employee(s) currently marked absent." });
            }

            if (LateCount > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{LateCount} employee(s) arrived late today." });
            }

            if (unassignedEmployees > 0)
            {
                Alerts.Add(new DashboardAlert { Message = $"{unassignedEmployees} active employee(s) have no shift assignment today." });
            }

            if (Alerts.Count == 0)
            {
                Alerts.Add(new DashboardAlert { Message = "No critical alerts right now." });
            }
        }

        private void LoadRecentActivities(DateTime today)
        {
            RecentActivities.Clear();

            var activities = _context.Attendances
                .AsNoTracking()
                .Include(a => a.Employee)
                .Where(a =>
                    a.TimeIn.HasValue &&
                    a.TimeIn.Value >= today &&
                    a.TimeIn.Value < today.AddDays(1))
                .OrderByDescending(a => a.TimeIn)
                .Take(5)
                .ToList();

            foreach (var attendance in activities)
            {
                var status = attendance.Status == AttendanceStatus.Open
                    ? "On Duty"
                    : attendance.OvertimeHours > 0
                        ? "Overtime"
                        : "Closed";

                var statusColor = status switch
                {
                    "On Duty" => "#10B981",
                    "Overtime" => "#7C3AED",
                    _ => "#64748B"
                };

                RecentActivities.Add(new RecentActivity
                {
                    Name = attendance.Employee?.FullName ?? $"Employee #{attendance.EmployeeId}",
                    Time = attendance.TimeIn ?? DateTime.Now,
                    Status = status,
                    StatusColor = statusColor
                });
            }

            if (RecentActivities.Count == 0)
            {
                RecentActivities.Add(new RecentActivity
                {
                    Name = "No time-ins yet",
                    Time = DateTime.Now,
                    Status = "Pending",
                    StatusColor = "#64748B"
                });
            }
        }

        private void LoadAreaDistribution()
        {
            AreaDistributions.Clear();

            var employees = _context.Employees
                .AsNoTracking()
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();

            var total = Math.Max(1, employees.Count);
            var areaCounts = employees
                .Where(e => e.Position != null)
                .GroupBy(e => e.Position.Area)
                .ToDictionary(g => g.Key, g => g.Count());

            var orderedAreas = new[]
            {
                (PositionArea.Kitchen, "Kitchen", "#DA291C"),
                (PositionArea.POS, "POS", "#FFC72C"),
                (PositionArea.DT, "DT", "#27251F"),
                (PositionArea.Lobby, "Lobby", "#9CA3AF")
            };

            foreach (var (area, label, color) in orderedAreas)
            {
                areaCounts.TryGetValue(area, out var count);
                var percent = (double)count / total * 100.0;
                AreaDistributions.Add(new AreaDistribution
                {
                    Label = label,
                    Percent = percent,
                    Color = color
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

        private void ExecuteShowEmployees()
        {
            if (!CanManageEmployees)
            {
                MessageBox.Show("You are not allowed to manage employees.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentView = new EmployeesPage(_currentUser);
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

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ManagerDashboardViewModel : ObservableObject
    {
        private int _totalCrew;
        private int _onDutyCount;
        private int _pendingShifts;
        private string _totalWeekHours = "0.0";

        public int TotalCrew { get => _totalCrew; set => SetProperty(ref _totalCrew, value); }
        public int OnDutyCount { get => _onDutyCount; set => SetProperty(ref _onDutyCount, value); }
        public int PendingShifts { get => _pendingShifts; set => SetProperty(ref _pendingShifts, value); }
        public string TotalWeekHours { get => _totalWeekHours; set => SetProperty(ref _totalWeekHours, value); }

        public ObservableCollection<AttendanceDto> TodayAttendance { get; }
        public ICollectionView TodayAttendanceView { get; }

        private string _attendanceSearchText = string.Empty;
        public string AttendanceSearchText
        {
            get => _attendanceSearchText;
            set
            {
                if (SetProperty(ref _attendanceSearchText, value))
                {
                    ApplyAttendanceFilter();
                }
            }
        }

        public System.Windows.Input.ICommand TimeInCommand { get; }
        public System.Windows.Input.ICommand TimeOutCommand { get; }
        public System.Windows.Input.ICommand ScanFingerprintCommand { get; }
        public System.Windows.Input.ICommand ExportAttendanceCommand { get; }

        private bool _isTimedIn;
        public bool IsTimedIn
        {
            get => _isTimedIn;
            set => SetProperty(ref _isTimedIn, value);
        }

        private string _currentStatus = "Offline";
        public string CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        private string _statusColor = "#64748B";
        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public bool RequireFingerprintForAttendance => FeatureFlagService.RequireFingerprintForAttendance;
        public bool ShowSeparateScanButton => !RequireFingerprintForAttendance;
        public string TimeInActionLabel => RequireFingerprintForAttendance ? "SCAN TO TIME IN" : "TIME IN";
        public string TimeOutActionLabel => RequireFingerprintForAttendance ? "SCAN TO TIME OUT" : "TIME OUT";
        public string AttendanceActionHint => RequireFingerprintForAttendance
            ? "Fingerprint required. Tap once and scan to complete attendance."
            : "Use the button directly or scan fingerprint below.";

        private readonly AppDbContext _context;
        private readonly AttendanceService _attendanceService;
        private readonly FingerprintService _fingerprintService;
        private readonly User _currentUser;
        private readonly int _employeeId;
        private readonly NotificationService _notificationService;
        private readonly ReportExportService _reportExportService;

        private string _announcementText = string.Empty;
        public string AnnouncementText
        {
            get => _announcementText;
            set => SetProperty(ref _announcementText, value);
        }

        public ObservableCollection<Notification> ManagerNotifications { get; } = new();

        public System.Windows.Input.ICommand SendAnnouncementCommand { get; }
        private readonly DispatcherTimer _notificationTimer;

        public ManagerDashboardViewModel(AppDbContext ctx, User user)
        {
            _context = ctx;
            _currentUser = user;
            _attendanceService = new AttendanceService(ctx);
            _fingerprintService = new FingerprintService(ctx);
            _notificationService = new NotificationService(ctx);
            _reportExportService = new ReportExportService();

            // Fix: Fetch Employee ID linked to this User
            var employee = ctx.Employees.FirstOrDefault(e => e.UserId == user.Id);
            _employeeId = employee?.Id ?? 0;

            TimeInCommand = new RelayCommand(_ => ExecuteTimeIn(), _ => !IsTimedIn);
            TimeOutCommand = new RelayCommand(_ => ExecuteTimeOut(), _ => IsTimedIn);
            ScanFingerprintCommand = new RelayCommand(_ => ExecuteScanFingerprintToggle());
            ExportAttendanceCommand = new RelayCommand(_ => ExecuteExportAttendance());
            SendAnnouncementCommand = new RelayCommand(_ => ExecuteSendAnnouncement());

            CheckCurrentStatus();
            TodayAttendance = new ObservableCollection<AttendanceDto>();
            TodayAttendanceView = CollectionViewSource.GetDefaultView(TodayAttendance);

            RefreshDashboardData();
            ApplyAttendanceFilter();

            LoadManagerNotifications();

            _notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(20)
            };
            _notificationTimer.Tick += (_, __) => LoadManagerNotifications();
            _notificationTimer.Start();
        }

        private void ApplyAttendanceFilter()
        {
            if (TodayAttendanceView == null)
            {
                return;
            }

            var query = AttendanceSearchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                TodayAttendanceView.Filter = null;
            }
            else
            {
                TodayAttendanceView.Filter = item =>
                {
                    if (item is not AttendanceDto row)
                    {
                        return false;
                    }

                    return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || row.Position.Contains(query, StringComparison.OrdinalIgnoreCase);
                };
            }

            TodayAttendanceView.Refresh();
        }

        private void CheckCurrentStatus()
        {
            if (_employeeId == 0) return;

            var attendance = _attendanceService.GetActiveAttendance(_employeeId);
            IsTimedIn = attendance != null;
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            if (IsTimedIn)
            {
                CurrentStatus = "ON DUTY";
                StatusColor = "#10B981"; // Green
            }
            else
            {
                if (HasShiftToday())
                {
                    CurrentStatus = "SCHEDULED";
                    StatusColor = "#F59E0B"; // Amber
                }
                else
                {
                    CurrentStatus = "OFF DUTY";
                    StatusColor = "#64748B"; // Slate
                }
            }
        }

        private bool HasShiftToday()
        {
            if (_employeeId == 0) return false;

            var today = DateTime.Today;
            return _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .Any(sa => sa.EmployeeId == _employeeId && sa.Shift.ShiftDate.Date == today);
        }

        private void ExecuteTimeIn()
        {
            try
            {
                if (_employeeId == 0)
                {
                    System.Windows.MessageBox.Show("Error: No Employee record found for this user.", "Error");
                    return;
                }

                if (RequireFingerprintForAttendance)
                {
                    ExecuteFingerprintAttendanceAction(() => _attendanceService.TimeInByUserId(_currentUser.Id));
                    return;
                }

                _attendanceService.TimeIn(_employeeId);
                IsTimedIn = true;
                UpdateStatusDisplay();
                RefreshDashboardData();
                System.Windows.MessageBox.Show("Timed In Successfully!", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Time In Failed: {ex.Message}", "Error");
            }
        }

        private void ExecuteTimeOut()
        {
            try
            {
                if (_employeeId == 0)
                {
                    System.Windows.MessageBox.Show("Error: No Employee record found for this user.", "Error");
                    return;
                }

                if (RequireFingerprintForAttendance)
                {
                    ExecuteFingerprintAttendanceAction(() => _attendanceService.TimeOutByUserId(_currentUser.Id));
                    return;
                }

                _attendanceService.TimeOut(_employeeId);
                IsTimedIn = false;
                UpdateStatusDisplay();
                RefreshDashboardData();
                System.Windows.MessageBox.Show("Timed Out Successfully!", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Time Out Failed: {ex.Message}", "Error");
            }
        }

        private void ExecuteScanFingerprintToggle()
        {
            try
            {
                if (_employeeId == 0)
                {
                    System.Windows.MessageBox.Show("Error: No Employee record found for this user.", "Error");
                    return;
                }

                ExecuteFingerprintAttendanceAction(() => _attendanceService.ToggleTimeByUserId(_currentUser.Id));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Fingerprint scan failed: {ex.Message}", "Fingerprint Error");
            }
        }

        private void ExecuteFingerprintAttendanceAction(Action attendanceAction)
        {
            var identifyResult = _fingerprintService.IdentifyUserFromCapture();
            if (!identifyResult.IsMatched || !identifyResult.MatchedUserId.HasValue)
            {
                MessageBox.Show("Fingerprint not recognized. Please try again.", "No Match",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (identifyResult.MatchedUserId.Value != _currentUser.Id)
            {
                MessageBox.Show("Scanned fingerprint belongs to a different account.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            attendanceAction();
            CheckCurrentStatus();
            RefreshDashboardData();

            MessageBox.Show("Fingerprint accepted. Attendance updated.", "Success");
        }

        private void ExecuteSendAnnouncement()
        {
            var message = AnnouncementText?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                System.Windows.MessageBox.Show("Please enter an announcement.", "Empty Message");
                return;
            }

            var crewUserIds = _context.Users
                .Where(u => u.Role == UserRole.Crew)
                .Select(u => u.Id)
                .ToList();

            if (crewUserIds.Count == 0)
            {
                System.Windows.MessageBox.Show("No crew users found.", "Info");
                return;
            }

            _notificationService.CreateForUsers(crewUserIds, NotificationType.General, "Announcement", message);
            AnnouncementText = string.Empty;
            System.Windows.MessageBox.Show("Announcement sent.", "Success");
        }

        private void ExecuteExportAttendance()
        {
            try
            {
                if (TodayAttendance.Count == 0)
                {
                    System.Windows.MessageBox.Show("No attendance rows to export.", "No Data");
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"asms_attendance_{DateTime.Today:yyyyMMdd}.csv"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                _reportExportService.ExportAttendanceCsv(TodayAttendance, dialog.FileName, DateTime.Today);
                System.Windows.MessageBox.Show("Attendance CSV exported successfully.", "Export Complete");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Export Error");
            }
        }

        private void LoadManagerNotifications()
        {
            ManagerNotifications.Clear();
            var items = _context.Notifications
                .Where(n => n.UserId == _currentUser.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToList();

            foreach (var n in items)
            {
                ManagerNotifications.Add(n);
            }
        }

        private void RefreshDashboardData()
        {
            TotalCrew = _context.Employees.Count(e => e.Status == EmployeeStatus.Active);
            OnDutyCount = _context.Attendances.Count(a => a.Status == AttendanceStatus.Open);
            PendingShifts = 0;

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            var totalHours = _context.Attendances
                .Where(a => a.TimeIn >= startOfWeek && a.Status == AttendanceStatus.Closed)
                .Sum(a => (double)a.TotalHours);
            TotalWeekHours = totalHours.ToString("N1");

            var attendanceList = _context.Attendances
                .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
                .Where(a => a.TimeIn.HasValue && a.TimeIn.Value.Date == DateTime.Today)
                .OrderByDescending(a => a.TimeIn)
                .Select(a => new AttendanceDto
                {
                    Name = a.Employee.FullName,
                    Position = a.Employee.Position.Name,
                    TimeIn = a.TimeIn,
                    TimeOut = a.TimeOut,
                    Status = a.Status == AttendanceStatus.Open ? "ACTIVE" : "CLOSED",
                    StatusColor = a.Status == AttendanceStatus.Open ? "#10B981" : "#64748B"
                })
                .ToList();

            TodayAttendance.Clear();
            foreach (var row in attendanceList)
            {
                TodayAttendance.Add(row);
            }

            ApplyAttendanceFilter();
        }
    }
}

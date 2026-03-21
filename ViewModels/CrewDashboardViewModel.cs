using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Data;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Windows.Data;
using System.ComponentModel;

namespace AttendanceShiftingManagement.ViewModels
{
    public class CrewDashboardViewModel : ObservableObject
    {
        private readonly AttendanceService _attendance;
        private readonly int _employeeId;
        private readonly ShiftService _shiftService;
        private readonly PayrollService _payrollService;
        private readonly AppDbContext _context;
        private readonly int _userId;

        private string _statusText = "OFF DUTY";
        private string _statusColor = "#E53935";
        private string _statusDetails = "No active shift";
        private string _actionButtonText = "TIME IN";
        private string _totalPay = "? 0.00";
        private string _totalHoursCount = "0.0h";
        private string _overtimeHoursCount = "0.0h";
        private bool _isActionEnabled = true;
        private bool _hasScheduleToday;

        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public string StatusDetails { get => _statusDetails; set => SetProperty(ref _statusDetails, value); }
        public string ActionButtonText { get => _actionButtonText; set => SetProperty(ref _actionButtonText, value); }
        public string TotalPay { get => _totalPay; set => SetProperty(ref _totalPay, value); }
        public string TotalHoursCount { get => _totalHoursCount; set => SetProperty(ref _totalHoursCount, value); }
        public string OvertimeHoursCount { get => _overtimeHoursCount; set => SetProperty(ref _overtimeHoursCount, value); }
        public bool IsActionEnabled { get => _isActionEnabled; set => SetProperty(ref _isActionEnabled, value); }

        public event Action? AttendanceRecorded;
        public event Action? ScheduleRequested;

        public ObservableCollection<CrewShiftItem> WeeklyShifts { get; } = new();
        public ObservableCollection<Notification> Notifications { get; } = new();
        public ICollectionView NotificationsView { get; }

        private string _selectedNotificationType = "All";


        public ObservableCollection<string> NotificationTypes { get; } = new()
        {
            "All",
            "Leave",
            "Shift",
            "Announcement"
        };

        public string SelectedNotificationType
        {
            get => _selectedNotificationType;
            set
            {
                if (SetProperty(ref _selectedNotificationType, value))
                {
                    ApplyNotificationFilter();
                }
            }
        }

        public ICommand TimeInOutCommand { get; }
        public ICommand OpenScheduleCommand { get; }
        public ICommand MarkReadCommand { get; }
        public ICommand MarkAllReadCommand { get; }

        public CrewDashboardViewModel(AttendanceService attendance, ShiftService shifts, PayrollService payroll, int employeeId)
        {
            _attendance = attendance;
            _shiftService = shifts;
            _payrollService = payroll;
            _employeeId = employeeId;
            _context = new AppDbContext();
            _userId = _context.Employees.FirstOrDefault(e => e.Id == _employeeId)?.UserId ?? 0;

            TimeInOutCommand = new RelayCommand(_ => ExecuteTimeToggle());
            OpenScheduleCommand = new RelayCommand(_ => ScheduleRequested?.Invoke());
            MarkReadCommand = new RelayCommand(param => ExecuteMarkRead(param));
            MarkAllReadCommand = new RelayCommand(_ => ExecuteMarkAllRead());

            NotificationsView = CollectionViewSource.GetDefaultView(Notifications);

            LoadRealData();
            SyncAttendanceStatus();
        }

        private void SyncAttendanceStatus()
        {
            var active = _attendance.GetActiveAttendance(_employeeId);
            var todayShift = _shiftService.GetEmployeeShiftForDate(_employeeId, DateTime.Today);

            if (active != null)
            {
                StatusText = "ON DUTY";
                StatusColor = "#43A047";
                ActionButtonText = "TIME OUT";
                StatusDetails = $"Shift: {DateTime.Today.Add(active.Shift.StartTime):hh:mm tt} - {DateTime.Today.Add(active.Shift.EndTime):hh:mm tt} | {active.Shift.Position?.Name}";
                IsActionEnabled = true;
                _hasScheduleToday = true;
            }
            else if (todayShift != null)
            {
                StatusText = "OFF DUTY";
                StatusColor = "#FB8C00"; // Orange/Amber for "About to work"
                ActionButtonText = "TIME IN";
                StatusDetails = $"Scheduled today: {DateTime.Today.Add(todayShift.Shift.StartTime):hh:mm tt} - {DateTime.Today.Add(todayShift.Shift.EndTime):hh:mm tt} | {todayShift.Shift.Position?.Name}";
                IsActionEnabled = true;
                _hasScheduleToday = true;
            }
            else
            {
                StatusText = "RELAX / DAY OFF";
                StatusColor = "#37474F"; // Slate Blue Gray
                ActionButtonText = "NO SCHEDULE";
                StatusDetails = "No shift scheduled for today.";
                IsActionEnabled = true;
                _hasScheduleToday = false;
            }
        }

        private void ExecuteTimeToggle()
        {
            try
            {
                if (!_hasScheduleToday && ActionButtonText == "NO SCHEDULE")
                {
                    System.Windows.MessageBox.Show("You don't have a schedule today.", "No Schedule",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                if (ActionButtonText == "TIME IN")
                {
                    _attendance.TimeIn(_employeeId);
                }
                else
                {
                    _attendance.TimeOut(_employeeId);
                }

                LoadRealData();
                SyncAttendanceStatus();
                AttendanceRecorded?.Invoke();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Attendance Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                SyncAttendanceStatus();
            }
        }

        private void LoadRealData()
        {
            // Load Weekly Schedule
            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(6);

            WeeklyShifts.Clear();
            var shifts = _shiftService.GetEmployeeWeeklySchedule(_employeeId, startOfWeek, endOfWeek);
            foreach (var s in shifts)
            {
                WeeklyShifts.Add(new CrewShiftItem
                {
                    Day = s.Shift.ShiftDate.DayOfWeek.ToString(),
                    Position = s.Shift.Position?.Name ?? "General",
                    TimeRange = $"{DateTime.Today.Add(s.Shift.StartTime):hh:mm tt} - {DateTime.Today.Add(s.Shift.EndTime):hh:mm tt}"
                });
            }

            // Load Earnings Estimate
            var estimate = _payrollService.GetEmployeeEarningsEstimate(_employeeId, startOfWeek, endOfWeek);
            TotalPay = $"? {estimate.TotalPay:N2}";
            TotalHoursCount = $"{estimate.TotalHours:F1}h";
            OvertimeHoursCount = $"{estimate.OvertimeHours:F1}h";

            // Load Notifications (leave approvals/rejections, shift changes, announcements)
            Notifications.Clear();
            if (_userId != 0)
            {
                var notifs = _context.Notifications
                    .Where(n => n.UserId == _userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20)
                    .ToList();

                foreach (var n in notifs)
                {
                    Notifications.Add(n);
                }
            }

            ApplyNotificationFilter();
        }

        private void ApplyNotificationFilter()
        {
            if (NotificationsView == null)
            {
                return;
            }

            var filter = SelectedNotificationType;
            NotificationsView.Filter = item =>
            {
                if (item is not Notification n) return false;
                if (filter == "All") return true;
                if (filter == "Leave") return n.Type == NotificationType.LeaveApproved || n.Type == NotificationType.LeaveRejected;
                if (filter == "Shift") return n.Type == NotificationType.ShiftAssigned || n.Type == NotificationType.ShiftChanged || n.Type == NotificationType.ShiftReminder;
                if (filter == "Announcement") return n.Type == NotificationType.General || n.Type == NotificationType.SchedulePublished;
                return true;
            };
            NotificationsView.Refresh();
        }

        private void ExecuteMarkRead(object? param)
        {
            if (param is not Notification notification) return;
            var notif = _context.Notifications.FirstOrDefault(n => n.Id == notification.Id);
            if (notif == null) return;
            notif.IsRead = true;
            _context.SaveChanges();
            LoadRealData();
        }

        private void ExecuteMarkAllRead()
        {
            if (_userId == 0) return;
            var notifs = _context.Notifications.Where(n => n.UserId == _userId && !n.IsRead).ToList();
            if (notifs.Count == 0) return;
            foreach (var n in notifs) n.IsRead = true;
            _context.SaveChanges();
            LoadRealData();
        }
    }

    public class CrewShiftItem
    {
        public string Day { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
    }
}

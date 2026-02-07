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

        private readonly AttendanceService _attendanceService;
        private readonly User _currentUser;
        private readonly int _employeeId;

        public ManagerDashboardViewModel(AppDbContext ctx, User user)
        {
            _currentUser = user;
            _attendanceService = new AttendanceService(ctx);

            // Fix: Fetch Employee ID linked to this User
            var employee = ctx.Employees.FirstOrDefault(e => e.UserId == user.Id);
            _employeeId = employee?.Id ?? 0;

            TimeInCommand = new RelayCommand(_ => ExecuteTimeIn(), _ => !IsTimedIn);
            TimeOutCommand = new RelayCommand(_ => ExecuteTimeOut(), _ => IsTimedIn);

            CheckCurrentStatus();

            // Calculate Stats
            TotalCrew = ctx.Employees.Count(e => e.Status == EmployeeStatus.Active);
            OnDutyCount = ctx.Attendances.Count(a => a.Status == AttendanceStatus.Open);

            // For now, pending shifts as mock or actual if you have a field for it
            PendingShifts = 0;

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            var totalHours = ctx.Attendances
                .Where(a => a.TimeIn >= startOfWeek && a.Status == AttendanceStatus.Closed)
                .Sum(a => (double)a.TotalHours);
            TotalWeekHours = totalHours.ToString("N1");

            var attendanceList = ctx.Attendances
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
                   }).ToList();

            TodayAttendance = new ObservableCollection<AttendanceDto>(attendanceList);
            TodayAttendanceView = CollectionViewSource.GetDefaultView(TodayAttendance);
            ApplyAttendanceFilter();
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
                CurrentStatus = "OFF DUTY";
                StatusColor = "#64748B"; // Slate
            }
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

                _attendanceService.TimeIn(_employeeId);
                IsTimedIn = true;
                UpdateStatusDisplay();
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

                _attendanceService.TimeOut(_employeeId);
                IsTimedIn = false;
                UpdateStatusDisplay();
                System.Windows.MessageBox.Show("Timed Out Successfully!", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Time Out Failed: {ex.Message}", "Error");
            }
        }
    }
}

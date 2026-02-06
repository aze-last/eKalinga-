using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Models;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace AttendanceShiftingManagement.ViewModels
{
    public class CrewDashboardViewModel : ObservableObject
    {
        private readonly AttendanceService _attendance;
        private readonly int _employeeId;

        private string _statusText = "ON DUTY";
        private string _statusColor = "#43A047";
        private string _statusDetails = "Shift: 08:00 AM - 04:00 PM | Kitchen";
        private string _actionButtonText = "TIME OUT";
        private string _totalPay = "₱ 0.00";
        private string _totalHoursCount = "0.0h";
        private string _overtimeHoursCount = "0.0h";

        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public string StatusDetails { get => _statusDetails; set => SetProperty(ref _statusDetails, value); }
        public string ActionButtonText { get => _actionButtonText; set => SetProperty(ref _actionButtonText, value); }
        public string TotalPay { get => _totalPay; set => SetProperty(ref _totalPay, value); }
        public string TotalHoursCount { get => _totalHoursCount; set => SetProperty(ref _totalHoursCount, value); }
        public string OvertimeHoursCount { get => _overtimeHoursCount; set => SetProperty(ref _overtimeHoursCount, value); }

        public event Action? AttendanceRecorded;

        public ObservableCollection<CrewShiftItem> WeeklyShifts { get; } = new();
        public ObservableCollection<string> Notifications { get; } = new();

        public ICommand TimeInOutCommand { get; }

        public CrewDashboardViewModel(AttendanceService service, int employeeId)
        {
            _attendance = service;
            _employeeId = employeeId;

            TimeInOutCommand = new RelayCommand(_ => ExecuteTimeToggle());

            LoadMockData();
        }

        private void ExecuteTimeToggle()
        {
            if (ActionButtonText == "TIME IN")
            {
                _attendance.TimeIn(_employeeId);
                StatusText = "ON DUTY";
                StatusColor = "#43A047";
                ActionButtonText = "TIME OUT";
            }
            else
            {
                _attendance.TimeOut(_employeeId);
                StatusText = "OFF DUTY";
                StatusColor = "#E53935";
                ActionButtonText = "TIME IN";
            }

            AttendanceRecorded?.Invoke();
        }

        private void LoadMockData()
        {
            WeeklyShifts.Add(new CrewShiftItem { Day = "Monday", Position = "Kitchen", TimeRange = "08:00 AM - 04:00 PM" });
            WeeklyShifts.Add(new CrewShiftItem { Day = "Wednesday", Position = "Kitchen", TimeRange = "08:00 AM - 04:00 PM" });
            WeeklyShifts.Add(new CrewShiftItem { Day = "Friday", Position = "Kitchen", TimeRange = "10:00 AM - 06:00 PM" });

            Notifications.Add("New health and safety protocols updated.");
            Notifications.Add("Manager message: Great work everyone on the busy weekend!");

            TotalPay = "₱ 12,450.00";
            TotalHoursCount = "40.0h";
            OvertimeHoursCount = "2.5h";
        }
    }

    public class CrewShiftItem
    {
        public string Day { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
    }
}

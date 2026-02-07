using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using System.Windows.Input;
using System;

namespace AttendanceShiftingManagement.ViewModels
{
    public class CrewMainViewModel : ObservableObject
    {
        private object? _currentView;
        private readonly int _employeeId;
        private readonly AttendanceService _attendanceService;
        private readonly ShiftService _shiftService;
        private readonly PayrollService _payrollService;
        private CrewDashboardViewModel? _dashboardVm;
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ICommand ShowScheduleCommand { get; }
        public ICommand ShowTimeClockCommand { get; }

        public CrewMainViewModel(User user)
        {
            var context = new Data.AppDbContext();

            // Fix: Fetch the Employee ID associated with this User
            // Since User.Id != Employee.Id
            var employee = context.Employees.FirstOrDefault(e => e.UserId == user.Id);
            _employeeId = employee?.Id ?? 0;

            _attendanceService = new AttendanceService(context);
            _shiftService = new ShiftService(context);
            _payrollService = new PayrollService(context);

            ShowScheduleCommand = new RelayCommand(_ => ExecuteShowSchedule());
            ShowTimeClockCommand = new RelayCommand(_ => ExecuteShowTimeClock());

            // Set default view
            ExecuteShowTimeClock();
        }

        public event Action? ShowSuccessRequest;

        private void ExecuteShowTimeClock()
        {
            _dashboardVm = new CrewDashboardViewModel(_attendanceService, _shiftService, _payrollService, _employeeId);
            _dashboardVm.AttendanceRecorded += () => ShowSuccessRequest?.Invoke();
            _dashboardVm.ScheduleRequested += ExecuteShowSchedule;

            CurrentView = new CrewDashboardPage
            {
                DataContext = _dashboardVm
            };
        }

        private void ExecuteShowSchedule()
        {
            CurrentView = new WeeklyCalendarPage
            {
                DataContext = new WeeklyCalendarViewModel(_employeeId)
            };
        }
    }
}

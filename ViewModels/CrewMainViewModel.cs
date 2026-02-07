using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;

namespace AttendanceShiftingManagement.ViewModels
{
    public class CrewMainViewModel : ObservableObject
    {
        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public CrewMainViewModel(User user)
        {
            var context = new Data.AppDbContext();

            // Fix: Fetch the Employee ID associated with this User
            // Since User.Id != Employee.Id
            var employee = context.Employees.FirstOrDefault(e => e.UserId == user.Id);
            int empId = employee?.Id ?? 0;

            var attendanceService = new AttendanceService(context);
            var shiftService = new ShiftService(context);
            var payrollService = new PayrollService(context);

            var dashboardVm = new CrewDashboardViewModel(attendanceService, shiftService, payrollService, empId);
            dashboardVm.AttendanceRecorded += () => ShowSuccessRequest?.Invoke();

            // Set default view
            CurrentView = new CrewDashboardPage
            {
                DataContext = dashboardVm
            };
        }

        public event Action? ShowSuccessRequest;
    }
}

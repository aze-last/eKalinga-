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
            var attendanceService = new AttendanceService(context);
            var shiftService = new ShiftService(context);
            var payrollService = new PayrollService(context);

            var dashboardVm = new CrewDashboardViewModel(attendanceService, shiftService, payrollService, user.Id);
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

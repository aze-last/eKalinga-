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
            var dashboardVm = new CrewDashboardViewModel(new AttendanceService(new Data.AppDbContext()), user.Id);
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

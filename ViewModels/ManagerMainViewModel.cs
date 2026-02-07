using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ManagerMainViewModel : ObservableObject
    {
        private object _currentView;
        private readonly User _currentUser;

        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowShiftsCommand { get; }
        public ICommand ShowAttendanceCommand { get; }
        public ICommand ShowEmployeesCommand { get; }
        public ICommand ShowPositionsCommand { get; }

        public ManagerMainViewModel(User user)
        {
            _currentUser = user;

            ShowDashboardCommand = new RelayCommand(_ => ExecuteShowDashboard());
            ShowShiftsCommand = new RelayCommand(_ => ExecuteShowShifts());
            ShowAttendanceCommand = new RelayCommand(_ => ExecuteShowAttendance());
            ShowEmployeesCommand = new RelayCommand(_ => ExecuteShowEmployees());
            ShowPositionsCommand = new RelayCommand(_ => ExecuteShowPositions());

            // Initialize with Dashboard
            ExecuteShowDashboard();
        }

        private void ExecuteShowDashboard()
        {
            CurrentView = new ManagerDashboardPage
            {
                DataContext = new ManagerDashboardViewModel(new Data.AppDbContext(), _currentUser)
            };
        }

        private void ExecuteShowShifts()
        {
            CurrentView = new ShiftsManagementPage();
        }

        private void ExecuteShowAttendance()
        {
            CurrentView = new PayrollPage();
        }

        private void ExecuteShowEmployees()
        {
            CurrentView = new EmployeesPage();
        }

        private void ExecuteShowPositions()
        {
            CurrentView = new PositionsPage();
        }
    }
}

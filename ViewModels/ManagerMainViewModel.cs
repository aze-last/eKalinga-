using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;

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
        public ICommand ShowWeeklyCalendarCommand { get; }

        private bool _isSchedulingAllowed;
        public bool IsSchedulingAllowed
        {
            get => _isSchedulingAllowed;
            set => SetProperty(ref _isSchedulingAllowed, value);
        }

        public ManagerMainViewModel(User user)
        {
            _currentUser = user;

            // Check if user is "Scheduling Manager"
            // We need a context to find the employee record and position
            using (var ctx = new Data.AppDbContext())
            {
                var emp = ctx.Employees
                    .Include(e => e.Position)
                    .FirstOrDefault(e => e.UserId == user.Id);

                if (emp != null && emp.Position != null)
                {
                    // Allow if position name contains "Scheduling"
                    IsSchedulingAllowed = emp.Position.Name.IndexOf("Scheduling", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else
                {
                    IsSchedulingAllowed = false;
                }
            }

            ShowDashboardCommand = new RelayCommand(_ => ExecuteShowDashboard());
            ShowShiftsCommand = new RelayCommand(_ => ExecuteShowShifts());
            ShowAttendanceCommand = new RelayCommand(_ => ExecuteShowAttendance());
            ShowEmployeesCommand = new RelayCommand(_ => ExecuteShowEmployees());
            ShowPositionsCommand = new RelayCommand(_ => ExecuteShowPositions());
            ShowWeeklyCalendarCommand = new RelayCommand(_ => ExecuteShowWeeklyCalendar());

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
            CurrentView = new ShiftsManagementPage(_currentUser);
        }

        private void ExecuteShowWeeklyCalendar()
        {
            CurrentView = new WeeklyCalendarPage
            {
                DataContext = new WeeklyCalendarViewModel(null, _currentUser.Id)
            };
        }

        private void ExecuteShowAttendance()
        {
            CurrentView = new PayrollPage();
        }

        private void ExecuteShowEmployees()
        {
            CurrentView = new EmployeesPage(_currentUser);
        }

        private void ExecuteShowPositions()
        {
            CurrentView = new PositionsPage();
        }
    }
}

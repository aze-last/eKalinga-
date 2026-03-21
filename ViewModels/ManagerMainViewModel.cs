using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ManagerMainViewModel : ObservableObject
    {
        private object _currentView = new object();
        private readonly User _currentUser;
        private readonly int _employeeId;
        private ImageSource? _userPhotoImage;
        private string _userDisplayName = "Manager";
        private bool _isMenuOpen;

        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set => SetProperty(ref _isMenuOpen, value);
        }

        public ImageSource? UserPhotoImage
        {
            get => _userPhotoImage;
            set => SetProperty(ref _userPhotoImage, value);
        }

        public string UserDisplayName
        {
            get => _userDisplayName;
            set => SetProperty(ref _userDisplayName, value);
        }

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
        public ICommand ShowLeaveApprovalCommand { get; }
        public ICommand ShowProfileSettingsCommand { get; }
        public ICommand ShowMyScheduleCommand { get; }
        public ICommand ToggleMenuCommand { get; }
        public ICommand CloseMenuCommand { get; }

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
                    _employeeId = emp.Id;
                    // Allow if position name contains "Scheduling"
                    IsSchedulingAllowed = emp.Position.Name.IndexOf("Scheduling", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else
                {
                    _employeeId = 0;
                    IsSchedulingAllowed = false;
                }
            }

            LoadUserSummary();

            ShowDashboardCommand = new RelayCommand(_ => ExecuteShowDashboard());
            ShowShiftsCommand = new RelayCommand(_ => ExecuteShowShifts());
            ShowAttendanceCommand = new RelayCommand(_ => ExecuteShowAttendance());
            ShowEmployeesCommand = new RelayCommand(_ => ExecuteShowEmployees());
            ShowPositionsCommand = new RelayCommand(_ => ExecuteShowPositions());
            ShowWeeklyCalendarCommand = new RelayCommand(_ => ExecuteShowWeeklyCalendar());
            ShowLeaveApprovalCommand = new RelayCommand(_ => ExecuteShowLeaveApproval());
            ShowProfileSettingsCommand = new RelayCommand(_ => ExecuteShowProfileSettings());
            ShowMyScheduleCommand = new RelayCommand(_ => ExecuteShowMySchedule());
            ToggleMenuCommand = new RelayCommand(_ => IsMenuOpen = !IsMenuOpen);
            CloseMenuCommand = new RelayCommand(_ => IsMenuOpen = false);

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
            if (!IsSchedulingAllowed)
            {
                System.Windows.MessageBox.Show("Only Scheduling Managers can access Shift Planning.", "Access Denied",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            CurrentView = new ShiftsManagementPage(_currentUser);
        }

        private void LoadUserSummary()
        {
            using var ctx = new Data.AppDbContext();
            var profile = ctx.UserProfiles.FirstOrDefault(p => p.UserId == _currentUser.Id);
            var employee = ctx.Employees.FirstOrDefault(e => e.UserId == _currentUser.Id);

            UserDisplayName = !string.IsNullOrWhiteSpace(profile?.FullName)
                ? profile.FullName
                : employee?.FullName ?? _currentUser.Username;

            UserPhotoImage = BuildImage(profile?.PhotoPath);
        }

        private ImageSource? BuildImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void ExecuteShowWeeklyCalendar()
        {
            CurrentView = new WeeklyCalendarPage
            {
                DataContext = new WeeklyCalendarViewModel(null, _currentUser.Id, IsSchedulingAllowed)
            };
        }

        private void ExecuteShowMySchedule()
        {
            if (_employeeId == 0)
            {
                System.Windows.MessageBox.Show("No employee record found for this manager.", "Schedule Unavailable",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            CurrentView = new WeeklyCalendarPage
            {
                DataContext = new WeeklyCalendarViewModel(_employeeId)
            };
        }

        private void ExecuteShowAttendance()
        {
            CurrentView = new AttendanceLogsPage
            {
                DataContext = new AttendanceLogsViewModel("All")
            };
        }

        private void ExecuteShowEmployees()
        {
            CurrentView = new EmployeesPage(_currentUser);
        }

        private void ExecuteShowPositions()
        {
            CurrentView = new PositionsPage();
        }

        private void ExecuteShowLeaveApproval()
        {
            CurrentView = new LeaveApprovalPage
            {
                DataContext = new LeaveApprovalViewModel(_currentUser.Id)
            };
        }

        private void ExecuteShowProfileSettings()
        {
            var vm = new ProfileSettingsViewModel(_currentUser);
            vm.ProfileUpdated += LoadUserSummary;
            CurrentView = new ProfileSettingsPage
            {
                DataContext = vm
            };
        }
    }
}

using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using System.Windows.Input;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace AttendanceShiftingManagement.ViewModels
{
    public class CrewMainViewModel : ObservableObject
    {
        private object? _currentView;
        private readonly int _employeeId;
        private readonly AttendanceService _attendanceService;
        private readonly ShiftService _shiftService;
        private readonly PayrollService _payrollService;
        private readonly User _currentUser;
        private ImageSource? _userPhotoImage;
        private string _userDisplayName = "Crew";

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
        private CrewDashboardViewModel? _dashboardVm;
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ICommand ShowScheduleCommand { get; }
        public ICommand ShowTimeClockCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand ShowPayslipCommand { get; }
        public ICommand ShowLeaveRequestCommand { get; }
        public ICommand ShowProfileSettingsCommand { get; }

        public CrewMainViewModel(User user)
        {
            _currentUser = user;
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
            ShowHistoryCommand = new RelayCommand(_ => ExecuteShowHistory());
            ShowPayslipCommand = new RelayCommand(_ => ExecuteShowPayslip());
            ShowLeaveRequestCommand = new RelayCommand(_ => ExecuteShowLeaveRequest());
            ShowProfileSettingsCommand = new RelayCommand(_ => ExecuteShowProfileSettings());

            // Set default view
            ExecuteShowTimeClock();

            LoadUserSummary();
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

        private void ExecuteShowHistory()
        {
            MessageBox.Show("History view is coming soon.", "Coming Soon",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteShowPayslip()
        {
            MessageBox.Show("Payslip view is coming soon.", "Coming Soon",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteShowLeaveRequest()
        {
            CurrentView = new LeaveRequestPage
            {
                DataContext = new LeaveRequestViewModel(_employeeId)
            };
        }

        private void ExecuteShowProfileSettings()
        {
            CurrentView = new ProfileSettingsPage
            {
                DataContext = new ProfileSettingsViewModel(_currentUser)
            };
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

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            return image;
        }
    }
}

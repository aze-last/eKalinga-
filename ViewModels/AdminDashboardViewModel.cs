using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace AttendanceShiftingManagement.ViewModels
{
    public class AdminDashboardViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;
        private ImageSource? _userPhotoImage;
        private string _userDisplayName = "Admin";

        private int _totalEmployees;
        private int _absentCount;
        private int _lateCount;
        private int _overtimeCount;
        private string _currentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        private string _currentTime = DateTime.Now.ToString("hh:mm:ss tt");

        public int TotalEmployees
        {
            get => _totalEmployees;
            set => SetProperty(ref _totalEmployees, value);
        }

        public int AbsentCount
        {
            get => _absentCount;
            set => SetProperty(ref _absentCount, value);
        }

        public int LateCount
        {
            get => _lateCount;
            set => SetProperty(ref _lateCount, value);
        }

        public int OvertimeCount
        {
            get => _overtimeCount;
            set => SetProperty(ref _overtimeCount, value);
        }

        public string CurrentDateTime
        {
            get => _currentDateTime;
            set => SetProperty(ref _currentDateTime, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
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

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ObservableCollection<DashboardAlert> Alerts { get; set; } = new();
        public ObservableCollection<RecentActivity> RecentActivities { get; set; } = new();
        public ObservableCollection<AreaDistribution> AreaDistributions { get; } = new();
        private string _areaDistributionSummary = string.Empty;
        public string AreaDistributionSummary
        {
            get => _areaDistributionSummary;
            set => SetProperty(ref _areaDistributionSummary, value);
        }

        public System.Windows.Input.ICommand GeneratePayrollCommand { get; }
        public System.Windows.Input.ICommand AddEmployeeCommand { get; }
        public System.Windows.Input.ICommand ShowDashboardCommand { get; }
        public System.Windows.Input.ICommand ShowUsersCommand { get; }
        public System.Windows.Input.ICommand ShowEmployeesCommand { get; }
        public System.Windows.Input.ICommand ShowAllEmployeesCommand { get; }
        public System.Windows.Input.ICommand ShowHolidaysCommand { get; }
        public System.Windows.Input.ICommand ShowPayrollCommand { get; }
        public System.Windows.Input.ICommand ShowPositionsCommand { get; }
        public System.Windows.Input.ICommand ShowProfileSettingsCommand { get; }
        public System.Windows.Input.ICommand ShowAttendanceStatusCommand { get; }

        public AdminDashboardViewModel(User user)
        {
            _currentUser = user;
            _context = new AppDbContext();
            LoadDashboardData();
            LoadUserSummary();

            // Set default view
            _currentView = new DashboardPage();

            GeneratePayrollCommand = new RelayCommand(p => MessageBox.Show("Generating Payroll (Feature coming soon!)", "Development", MessageBoxButton.OK, MessageBoxImage.Information));
            AddEmployeeCommand = new RelayCommand(ExecuteAddEmployee);

            // Navigation Commands
            ShowDashboardCommand = new RelayCommand(_ => CurrentView = new DashboardPage());
            ShowUsersCommand = new RelayCommand(_ => CurrentView = new UsersPage());
            ShowEmployeesCommand = new RelayCommand(_ => CurrentView = new EmployeesPage(_currentUser));
            ShowAllEmployeesCommand = new RelayCommand(_ => CurrentView = new EmployeesPage(_currentUser));
            ShowHolidaysCommand = new RelayCommand(_ => CurrentView = new HolidaysPage());
            ShowPayrollCommand = new RelayCommand(_ => CurrentView = new PayrollPage());
            ShowPositionsCommand = new RelayCommand(_ => CurrentView = new PositionsPage());
            ShowProfileSettingsCommand = new RelayCommand(_ =>
            {
                var vm = new ProfileSettingsViewModel(_currentUser);
                vm.ProfileUpdated += LoadUserSummary;
                CurrentView = new ProfileSettingsPage
                {
                    DataContext = vm
                };
            });
            ShowAttendanceStatusCommand = new RelayCommand(param =>
            {
                var filter = param?.ToString() ?? "All";
                CurrentView = new AttendanceLogsPage
                {
                    DataContext = new AttendanceLogsViewModel(filter)
                };
            });

            // Setup timer for clock
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("hh:mm:ss tt");
                if (DateTime.Now.Second == 0)
                    CurrentDateTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            };
            timer.Start();
        }

        private void LoadDashboardData()
        {
            try
            {
                TotalEmployees = _context.Employees.Count();

                var statusService = new AttendanceStatusService(_context);
                var statuses = statusService.GetTodayStatuses(DateTime.Now);

                AbsentCount = statuses.Count(s => s.Status == "Absent");
                LateCount = statuses.Count(s => s.Status == "Late");
                OvertimeCount = statuses.Count(s => s.Status == "Overtime");

                Alerts.Clear();
                Alerts.Add(new DashboardAlert { Message = "3 Employees haven't timed in yet" });
                Alerts.Add(new DashboardAlert { Message = "Overtime limit reached for Kitchen staff" });

                RecentActivities.Clear();
                var employees = _context.Employees.Take(5).ToList();
                foreach (var emp in employees)
                {
                    RecentActivities.Add(new RecentActivity
                    {
                        Name = emp.FullName,
                        Time = DateTime.Now.AddMinutes(-new Random().Next(10, 60)),
                        Status = "Present",
                        StatusColor = "#43A047"
                    });
                }

                LoadAreaDistribution();
            }
            catch (Exception)
            {
                // Fallback for demo
                TotalEmployees = 15;
                AbsentCount = 2;
                LateCount = 3;
                OvertimeCount = 8;
            }
        }

        private void LoadAreaDistribution()
        {
            AreaDistributions.Clear();
            var employees = _context.Employees
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();

            var total = Math.Max(1, employees.Count);
            var areaCounts = employees
                .Where(e => e.Position != null)
                .GroupBy(e => e.Position.Area)
                .ToDictionary(g => g.Key, g => g.Count());

            var orderedAreas = new[]
            {
                (PositionArea.Kitchen, "Kitchen", "#DA291C"),
                (PositionArea.POS, "POS", "#FFC72C"),
                (PositionArea.DT, "DT", "#27251F"),
                (PositionArea.Lobby, "Lobby", "#9CA3AF")
            };

            foreach (var (area, label, color) in orderedAreas)
            {
                areaCounts.TryGetValue(area, out var count);
                var percent = (double)count / total * 100.0;
                AreaDistributions.Add(new AreaDistribution
                {
                    Label = label,
                    Percent = percent,
                    Color = color
                });
            }

            AreaDistributionSummary = string.Join(" | ",
                AreaDistributions.Select(d => $"{d.Label}: {d.Percent:0}%"));
        }

        private void ExecuteAddEmployee(object? parameter)
        {
            var dialog = new UserDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            dialog.ShowDialog();

            if (dialog.DataContext is UserDialogViewModel vm && vm.DialogResult)
            {
                LoadDashboardData();
            }
        }

        private void LoadUserSummary()
        {
            var profile = _context.UserProfiles.FirstOrDefault(p => p.UserId == _currentUser.Id);
            var employee = _context.Employees.FirstOrDefault(e => e.UserId == _currentUser.Id);

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
    }

    public class DashboardAlert
    {
        public string Message { get; set; } = string.Empty;
    }

    public class RecentActivity
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
    }

    public class AreaDistribution
    {
        public string Label { get; set; } = string.Empty;
        public double Percent { get; set; }
        public string Color { get; set; } = "#64748B";
    }
}

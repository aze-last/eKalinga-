using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace AttendanceShiftingManagement.ViewModels
{
    public class AdminDashboardViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;

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

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ObservableCollection<DashboardAlert> Alerts { get; set; } = new();
        public ObservableCollection<RecentActivity> RecentActivities { get; set; } = new();

        public System.Windows.Input.ICommand GeneratePayrollCommand { get; }
        public System.Windows.Input.ICommand AddEmployeeCommand { get; }
        public System.Windows.Input.ICommand CreateShiftCommand { get; }
        public System.Windows.Input.ICommand ShowDashboardCommand { get; }
        public System.Windows.Input.ICommand ShowUsersCommand { get; }
        public System.Windows.Input.ICommand ShowEmployeesCommand { get; }
        public System.Windows.Input.ICommand ShowShiftsCommand { get; }
        public System.Windows.Input.ICommand ShowHolidaysCommand { get; }
        public System.Windows.Input.ICommand ShowPayrollCommand { get; }
        public System.Windows.Input.ICommand ShowPositionsCommand { get; }

        public AdminDashboardViewModel(User user)
        {
            _currentUser = user;
            _context = new AppDbContext();
            LoadDashboardData();

            // Set default view
            _currentView = new DashboardPage();

            GeneratePayrollCommand = new RelayCommand(p => MessageBox.Show("Generating Payroll (Feature coming soon!)", "Development", MessageBoxButton.OK, MessageBoxImage.Information));
            AddEmployeeCommand = new RelayCommand(ExecuteAddEmployee);
            CreateShiftCommand = new RelayCommand(p => MessageBox.Show("Creating New Shift (Feature coming soon!)", "Development", MessageBoxButton.OK, MessageBoxImage.Information));

            // Navigation Commands
            ShowDashboardCommand = new RelayCommand(_ => CurrentView = new DashboardPage());
            ShowUsersCommand = new RelayCommand(_ => CurrentView = new UsersPage());
            ShowEmployeesCommand = new RelayCommand(_ => CurrentView = new EmployeesPage(_currentUser));
            ShowShiftsCommand = new RelayCommand(_ => MessageBox.Show("Shifts Management coming soon!", "Info"));
            ShowHolidaysCommand = new RelayCommand(_ => CurrentView = new HolidaysPage());
            ShowPayrollCommand = new RelayCommand(_ => CurrentView = new PayrollPage());
            ShowPositionsCommand = new RelayCommand(_ => CurrentView = new PositionsPage());

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

                // Mock data for other fields for now as they aren't fully implemented in DB yet
                AbsentCount = 2;
                LateCount = 5;
                OvertimeCount = 12;

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
}

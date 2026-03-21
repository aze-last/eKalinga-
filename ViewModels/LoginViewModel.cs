using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Extensions.Configuration;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private string _usernameOrEmail = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private string _activeConnectionSummary = string.Empty;

        public string UsernameOrEmail
        {
            get => _usernameOrEmail;
            set => SetProperty(ref _usernameOrEmail, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string ActiveConnectionSummary
        {
            get => _activeConnectionSummary;
            set => SetProperty(ref _activeConnectionSummary, value);
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            _authService = new AuthService();
            LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
            RefreshConnectionSummary();
        }

        public void RefreshConnectionSummary()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            ActiveConnectionSummary = $"{preset.DisplayName}: {preset.Server}:{preset.Port} / {preset.Database}";
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(UsernameOrEmail) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private void ExecuteLogin(object? parameter)
        {
            ErrorMessage = string.Empty;

            try
            {
                EnsureDatabaseReady();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Database connection failed. Check Connection Settings. {ex.Message}";
                return;
            }

            var user = _authService.Login(UsernameOrEmail.Trim(), Password);

            if (user == null)
            {
                ErrorMessage = "Invalid username/email or password.";
                return;
            }

            if (user.Role != UserRole.Admin)
            {
                ErrorMessage = "Only barangay admin accounts can access this portal.";
                return;
            }

            var dashboardWindow = new Views.MainWindow(user)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            Application.Current.MainWindow = dashboardWindow;
            dashboardWindow.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is Views.LoginWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        private static void EnsureDatabaseReady()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var resetDb = configuration.GetValue("Database:ResetOnStartup", false);
            var migrateOnStartup = configuration.GetValue("Database:MigrateOnStartup", false);
            DatabaseInitializer.Initialize(resetDb, migrateOnStartup);
        }
    }
}

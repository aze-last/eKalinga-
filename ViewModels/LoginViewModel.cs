using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private string _usernameOrEmail = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private string _connectionProfileSummary = string.Empty;

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

        public string ConnectionProfileSummary
        {
            get => _connectionProfileSummary;
            set => SetProperty(ref _connectionProfileSummary, value);
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
            RefreshConnectionProfileSummary();
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(UsernameOrEmail) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private void ExecuteLogin(object? parameter)
        {
            ErrorMessage = string.Empty;
            RefreshConnectionProfileSummary();

            try
            {
                if (Application.Current is App app)
                {
                    app.EnsureDatabaseInitialized();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Database connection failed: {ex.Message}";
                return;
            }

            var authService = new AuthService();
            var user = authService.Login(UsernameOrEmail, Password);

            if (user != null)
            {
                SessionContext.Start(user);

                Window dashboardWindow = user.Role switch
                {
                    UserRole.Manager or UserRole.ShiftManager => new Views.ManagerMainWindow(user),
                    UserRole.Crew => new Views.CrewMainWindow(user),
                    UserRole.HRStaff => new Views.MainWindow(user),
                    _ => new Views.MainWindow(user)
                };

                Application.Current.MainWindow = dashboardWindow;
                dashboardWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dashboardWindow.Show();

                // Close login window
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Views.LoginWindow)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            else
            {
                ErrorMessage = "Invalid username/email or password";
            }
        }

        public void RefreshConnectionProfileSummary()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            ConnectionProfileSummary = $"{preset.DisplayName} | {preset.Server}:{preset.Port}";
        }
    }
}

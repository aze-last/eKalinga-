using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
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

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            _authService = new AuthService();
            LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(UsernameOrEmail) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private void ExecuteLogin(object? parameter)
        {
            ErrorMessage = string.Empty;

            var user = _authService.Login(UsernameOrEmail, Password);

            if (user != null)
            {
                Window dashboardWindow = user.Role switch
                {
                    UserRole.Manager => new Views.ManagerMainWindow(user),
                    UserRole.Crew => new Views.CrewMainWindow(user),
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
    }
}
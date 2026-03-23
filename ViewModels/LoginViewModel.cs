using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Extensions.Configuration;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly RelayCommand _loginCommand;
        private readonly RelayCommand _createInitialAdminCommand;
        private string _usernameOrEmail = string.Empty;
        private string _password = string.Empty;
        private string _bootstrapFullName = "Barangay Administrator";
        private string _bootstrapUsername = "admin";
        private string _bootstrapEmail = "admin@barangay.local";
        private string _bootstrapPassword = string.Empty;
        private string _bootstrapConfirmPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private string _activeConnectionSummary = string.Empty;
        private bool _isBootstrapMode;

        public string UsernameOrEmail
        {
            get => _usernameOrEmail;
            set
            {
                if (SetProperty(ref _usernameOrEmail, value))
                {
                    _loginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    _loginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string BootstrapFullName
        {
            get => _bootstrapFullName;
            set
            {
                if (SetProperty(ref _bootstrapFullName, value))
                {
                    _createInitialAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string BootstrapUsername
        {
            get => _bootstrapUsername;
            set
            {
                if (SetProperty(ref _bootstrapUsername, value))
                {
                    _createInitialAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string BootstrapEmail
        {
            get => _bootstrapEmail;
            set
            {
                if (SetProperty(ref _bootstrapEmail, value))
                {
                    _createInitialAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string BootstrapPassword
        {
            get => _bootstrapPassword;
            set
            {
                if (SetProperty(ref _bootstrapPassword, value))
                {
                    _createInitialAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string BootstrapConfirmPassword
        {
            get => _bootstrapConfirmPassword;
            set
            {
                if (SetProperty(ref _bootstrapConfirmPassword, value))
                {
                    _createInitialAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        public string ActiveConnectionSummary
        {
            get => _activeConnectionSummary;
            set => SetProperty(ref _activeConnectionSummary, value);
        }

        public bool IsBootstrapMode
        {
            get => _isBootstrapMode;
            private set
            {
                if (SetProperty(ref _isBootstrapMode, value))
                {
                    OnPropertyChanged(nameof(IsLoginMode));
                    OnPropertyChanged(nameof(FormTitle));
                    OnPropertyChanged(nameof(FormSubtitle));
                    _loginCommand.RaiseCanExecuteChanged();
                    _createInitialAdminCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoginMode => !IsBootstrapMode;
        public string FormTitle => IsBootstrapMode ? "Initial Admin Setup" : "Admin Login";
        public string FormSubtitle => IsBootstrapMode
            ? "Create the first admin account for the selected database."
            : "Sign in to manage barangay operations.";

        public ICommand LoginCommand => _loginCommand;
        public ICommand CreateInitialAdminCommand => _createInitialAdminCommand;

        public LoginViewModel()
        {
            _authService = new AuthService();
            _loginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
            _createInitialAdminCommand = new RelayCommand(ExecuteCreateInitialAdmin, CanCreateInitialAdmin);
            RefreshConnectionSummary();
            RefreshStartupState();
        }

        public void RefreshConnectionSummary()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            ActiveConnectionSummary = $"{preset.DisplayName}: {preset.Server}:{preset.Port} / {preset.Database}";
        }

        public void RefreshStartupState()
        {
            RefreshConnectionSummary();

            try
            {
                EnsureDatabaseReady();

                using var context = new AppDbContext();
                var state = InitialAdminSetupService.GetState(context);
                IsBootstrapMode = state.RequiresSetup;

                if (state.RequiresSetup)
                {
                    SetNeutralStatus(state.Message);
                }
                else if (string.IsNullOrWhiteSpace(StatusMessage))
                {
                    SetNeutralStatus("Sign in with an existing admin account.");
                }
            }
            catch (Exception ex)
            {
                IsBootstrapMode = false;
                SetErrorStatus($"Database connection failed. Check App Database settings. {ex.Message}");
            }
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !IsBootstrapMode &&
                   !string.IsNullOrWhiteSpace(UsernameOrEmail) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private bool CanCreateInitialAdmin(object? parameter)
        {
            return IsBootstrapMode &&
                   !string.IsNullOrWhiteSpace(BootstrapFullName) &&
                   !string.IsNullOrWhiteSpace(BootstrapUsername) &&
                   !string.IsNullOrWhiteSpace(BootstrapEmail) &&
                   !string.IsNullOrWhiteSpace(BootstrapPassword) &&
                   !string.IsNullOrWhiteSpace(BootstrapConfirmPassword);
        }

        private void ExecuteLogin(object? parameter)
        {
            SetNeutralStatus(string.Empty);

            try
            {
                EnsureDatabaseReady();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Database connection failed. Check App Database settings. {ex.Message}");
                return;
            }

            var user = _authService.Login(UsernameOrEmail.Trim(), Password);

            if (user == null)
            {
                SetErrorStatus("Invalid username/email or password.");
                return;
            }

            if (user.Role != UserRole.Admin)
            {
                SetErrorStatus("Only barangay admin accounts can access this portal.");
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

        private void ExecuteCreateInitialAdmin(object? parameter)
        {
            SetNeutralStatus(string.Empty);

            if (!string.Equals(BootstrapPassword, BootstrapConfirmPassword, StringComparison.Ordinal))
            {
                SetErrorStatus("The admin passwords do not match.");
                return;
            }

            try
            {
                EnsureDatabaseReady();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Database connection failed. Check App Database settings. {ex.Message}");
                return;
            }

            using var context = new AppDbContext();
            var result = InitialAdminSetupService.CreateInitialAdmin(
                context,
                new InitialAdminSetupRequest(
                    BootstrapUsername,
                    BootstrapEmail,
                    BootstrapPassword,
                    BootstrapFullName));

            if (!result.IsSuccess)
            {
                SetErrorStatus(result.Message);
                return;
            }

            UsernameOrEmail = BootstrapEmail.Trim();
            Password = string.Empty;
            BootstrapPassword = string.Empty;
            BootstrapConfirmPassword = string.Empty;
            IsBootstrapMode = false;
            SetSuccessStatus(result.Message);
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7A4A"));
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
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

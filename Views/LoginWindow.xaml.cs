using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Services;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class LoginWindow : Window
    {
        private LoginViewModel ViewModel => (LoginViewModel)DataContext;

        public LoginWindow()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            DataContext = new LoginViewModel();
            WindowBrandingService.ApplyWindowIcon(this);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.Password = passwordBox.Password;
            }
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsShowingPassword = !ViewModel.IsShowingPassword;

            if (!ViewModel.IsShowingPassword)
            {
                PasswordBox.Password = ViewModel.Password;
            }
        }

        private void BootstrapPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.BootstrapPassword = passwordBox.Password;
            }
        }

        private void BootstrapConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.BootstrapConfirmPassword = passwordBox.Password;
            }
        }

        private void OpenConnectionSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConnectionSettingsWindow(selectionOnly: true)
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshConnectionSummary();
            ViewModel.RefreshStartupState();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

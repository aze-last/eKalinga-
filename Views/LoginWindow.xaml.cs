using AttendanceShiftingManagement.ViewModels;
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
            DataContext = new LoginViewModel();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.Password = passwordBox.Password;
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
    }
}

using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class SettingsWindow : Window
    {
        private SettingsToolsViewModel ViewModel => (SettingsToolsViewModel)DataContext;

        public SettingsWindow(User? currentUser = null)
        {
            InitializeComponent();
            DataContext = new SettingsToolsViewModel(currentUser);
            ViewModel.AdvancedLoadTablesRequested += OpenAdvancedLoadTables;
        }

        private void OpenAdvancedLoadTables()
        {
            var window = new LoadTablesWindow
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshPreviewCommand.Execute(null);
        }

        private void OpenAppDatabaseSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConnectionSettingsWindow(selectionOnly: false)
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshPreviewCommand.Execute(null);
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.CurrentPassword = passwordBox.Password;
            }
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.NewPassword = passwordBox.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.ConfirmPassword = passwordBox.Password;
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.ChangePasswordCommand.CanExecute(null))
            {
                return;
            }

            ViewModel.ChangePasswordCommand.Execute(null);
            if (!ViewModel.LastPasswordChangeSucceeded)
            {
                return;
            }

            if (FindName("CurrentPasswordBox") is PasswordBox currentPasswordBox)
            {
                currentPasswordBox.Clear();
            }

            if (FindName("NewPasswordBox") is PasswordBox newPasswordBox)
            {
                newPasswordBox.Clear();
            }

            if (FindName("ConfirmPasswordBox") is PasswordBox confirmPasswordBox)
            {
                confirmPasswordBox.Clear();
            }
        }
    }
}

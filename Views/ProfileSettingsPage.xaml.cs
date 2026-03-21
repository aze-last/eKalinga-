using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class ProfileSettingsPage : UserControl
    {
        public ProfileSettingsPage()
        {
            InitializeComponent();
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ProfileSettingsViewModel vm && sender is PasswordBox box)
            {
                vm.CurrentPassword = box.Password;
            }
        }

        private void NewPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ProfileSettingsViewModel vm && sender is PasswordBox box)
            {
                vm.NewPassword = box.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ProfileSettingsViewModel vm && sender is PasswordBox box)
            {
                vm.ConfirmPassword = box.Password;
            }
        }
    }
}

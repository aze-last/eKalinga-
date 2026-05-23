using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class UserManagementPage : UserControl
    {
        public UserManagementPage()
        {
            InitializeComponent();
            DataContext = new UserManagementViewModel();
        }

        private void NewPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox && DataContext is UserManagementViewModel viewModel)
            {
                viewModel.NewPassword = passwordBox.Password;
            }
        }
    }
}

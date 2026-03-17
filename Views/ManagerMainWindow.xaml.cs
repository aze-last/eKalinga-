using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class ManagerMainWindow : Window
    {
        private readonly User _currentUser;

        public ManagerMainWindow(User user)
        {
            _currentUser = user;
            InitializeComponent();
            this.DataContext = new ViewModels.ManagerMainViewModel(user);
            SwitchRoleButton.Visibility = RoleSwitchService.CanUseSwitcher(user) ? Visibility.Visible : Visibility.Collapsed;
            ReturnAdminButton.Visibility = RoleSwitchService.CanReturnToAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            RoleSwitchService.HandleLogout();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void ReturnToAdmin_Click(object sender, RoutedEventArgs e)
        {
            RoleSwitchService.ReturnToAdmin(this);
        }

        private void SwitchRole_Click(object sender, RoutedEventArgs e)
        {
            RoleSwitchService.OpenRoleSwitcher(_currentUser, this);
        }
    }
}

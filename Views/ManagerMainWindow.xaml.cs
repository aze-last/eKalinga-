using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class ManagerMainWindow : Window
    {
        public ManagerMainWindow(User user)
        {
            InitializeComponent();
            this.DataContext = new ViewModels.ManagerMainViewModel(user);
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
    }
}

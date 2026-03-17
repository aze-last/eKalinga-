using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class MainWindow : Window
    {
        private readonly User _currentUser;

        public MainWindow(User user)
        {
            _currentUser = user;
            InitializeComponent();
            DataContext = new AdminDashboardViewModel(user);
        }

        private void SwitchRole_Click(object sender, RoutedEventArgs e)
        {
            RoleSwitchService.OpenRoleSwitcher(_currentUser, this);
        }

        private void ReturnToAdmin_Click(object sender, RoutedEventArgs e)
        {
            RoleSwitchService.ReturnToAdmin(this);
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?",
                "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RoleSwitchService.HandleLogout();
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                Close();
            }
        }
    }
}

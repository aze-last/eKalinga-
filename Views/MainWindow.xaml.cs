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
            if (!RoleSwitchService.IsEnabled)
            {
                MessageBox.Show("Demo role switch is disabled. Set AppSettings:EnableDemoRoleSwitch=true.",
                    "Feature Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!RoleSwitchService.CanUseSwitcher(_currentUser))
            {
                MessageBox.Show("Only Admin can switch roles.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new RoleSwitchWindow(_currentUser)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.SelectedUserId.HasValue)
            {
                RoleSwitchService.SwitchToUser(dialog.SelectedUserId.Value, this, _currentUser);
            }
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

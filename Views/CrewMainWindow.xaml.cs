using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class CrewMainWindow : Window
    {
        private readonly User _currentUser;

        public CrewMainWindow(User user)
        {
            _currentUser = user;
            InitializeComponent();
            var vm = new AttendanceShiftingManagement.ViewModels.CrewMainViewModel(user);
            vm.ShowSuccessRequest += ShowSuccess;
            this.DataContext = vm;
            SwitchRoleButton.Visibility = RoleSwitchService.CanUseSwitcher(user) ? Visibility.Visible : Visibility.Collapsed;
            ReturnAdminButton.Visibility = RoleSwitchService.CanReturnToAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowSuccess()
        {
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void CloseDialog_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
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

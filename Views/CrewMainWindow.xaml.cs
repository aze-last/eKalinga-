using AttendanceShiftingManagement.Models;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class CrewMainWindow : Window
    {
        public CrewMainWindow(User user)
        {
            InitializeComponent();
            var vm = new AttendanceShiftingManagement.ViewModels.CrewMainViewModel(user);
            vm.ShowSuccessRequest += ShowSuccess;
            this.DataContext = vm;
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
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}

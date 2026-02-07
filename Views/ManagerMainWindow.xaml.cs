using AttendanceShiftingManagement.Models;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class ManagerMainWindow : Window
    {
        public ManagerMainWindow(User user)
        {
            InitializeComponent();
            this.DataContext = new ViewModels.ManagerMainViewModel(user);
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}

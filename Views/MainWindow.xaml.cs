using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System;
namespace AttendanceShiftingManagement.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(User user)
        {
            if (user.Role == UserRole.Manager)
            {
                var managerWin = new ManagerMainWindow(user);
                Application.Current.MainWindow = managerWin;
                managerWin.Show();
                this.Close();
                return;
            }
            else if (user.Role == UserRole.Crew)
            {
                var crewWin = new CrewMainWindow(user);
                Application.Current.MainWindow = crewWin;
                crewWin.Show();
                this.Close();
                return;
            }

            InitializeComponent();
            this.DataContext = new AdminDashboardViewModel();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?",
                "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }
    }
}
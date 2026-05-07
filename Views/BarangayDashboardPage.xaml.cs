using AttendanceShiftingManagement.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class BarangayDashboardPage : UserControl
    {
        public BarangayDashboardPage()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = BarangayDashboardViewModel.CreateDesignTime();
                return;
            }

            DataContext = new BarangayDashboardViewModel();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow window)
            {
                window.OpenSettingsFromDashboard();
            }
        }

        private async void CheckForUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow window)
            {
                await window.CheckForUpdateFromDashboardAsync();
            }
        }

        private async void SyncRemoteAndLocalButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow window)
            {
                await window.SyncRemoteAndLocalFromDashboardAsync();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow window)
            {
                window.LogoutFromDashboard();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

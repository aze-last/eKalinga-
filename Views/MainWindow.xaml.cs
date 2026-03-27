using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class MainWindow : Window
    {
        private BarangayMainViewModel ViewModel => (BarangayMainViewModel)DataContext;

        public MainWindow(User user)
        {
            InitializeComponent();
            DataContext = new BarangayMainViewModel(user);
            WindowBrandingService.ApplyWindowIcon(this);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(ViewModel.CurrentUser)
            {
                Owner = this
            };

            window.ShowDialog();
            WindowBrandingService.ApplyWindowIcon(this);
            ViewModel.RefreshConnectionSummary();
            ViewModel.RefreshUserSummary();
            ViewModel.ReloadCurrentView();
        }

        private void LoadTables_Click(object sender, RoutedEventArgs e)
        {
            var window = new LoadTablesWindow
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshConnectionSummary();
            ViewModel.ReloadCurrentView();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}

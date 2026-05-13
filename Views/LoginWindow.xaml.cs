using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views
{
    public partial class LoginWindow : Window
    {
        private LoginViewModel ViewModel => (LoginViewModel)DataContext;

        public LoginWindow()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            DataContext = new LoginViewModel();
            WindowBrandingService.ApplyWindowIcon(this);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.Password = passwordBox.Password;
            }
        }

        private void LeftPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ConnectionSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConnectionSettingsWindow(selectionOnly: true)
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshConnectionSummary();
            ViewModel.RefreshStartupStateAsync();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Self-registration is currently disabled. Please contact your administrator.", 
                "Registration", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: Auto-focus username
            if (FindName("UsernameInput") is TextBox usernameInput)
            {
                usernameInput.Focus();
            }
        }
    }
}

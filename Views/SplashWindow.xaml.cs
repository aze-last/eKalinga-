using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Media.Animation;

namespace AttendanceShiftingManagement.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();

            if (DataContext is SplashViewModel vm)
            {
                vm.ReadyToLaunch += OnReadyToLaunch;
            }
        }

        private void OnReadyToLaunch(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(FadeOutAndLaunch);
        }

        private void FadeOutAndLaunch()
        {
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                var login = new LoginWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                Application.Current.MainWindow = login;
                login.Show();
                Close();

                // Start background update check now that DB is ready
                Services.AppUpdateCoordinator.StartBackgroundCheck();
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}

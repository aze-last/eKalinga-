using System.Windows;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                AppThemeService.ApplySavedTheme();
                base.OnStartup(e);

                try
                {
                    AppUpdatePackageService.PerformStartupMaintenance();
                }
                catch
                {
                    // Startup must stay resilient even if update cache cleanup fails.
                }

                AppUpdateCoordinator.StartBackgroundCheck();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application failed to start: {ex.Message}\n\nDetails: {ex.ToString()}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

using System.Windows;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
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
    }
}

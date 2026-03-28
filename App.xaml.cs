using System.Windows;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppUpdateCoordinator.StartBackgroundCheck();
        }
    }
}

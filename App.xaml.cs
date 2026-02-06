using AttendanceShiftingManagement.Data;
using System.Windows;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Seed database on startup
            using (var context = new AppDbContext())
            {
                DbSeeder.Seed(context);
            }
        }
    }
}
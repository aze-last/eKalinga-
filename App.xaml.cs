using AttendanceShiftingManagement.Data;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Windows;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            bool resetDb = configuration.GetValue("Database:ResetOnStartup", false);
            bool migrateOnStartup = configuration.GetValue("Database:MigrateOnStartup", true);

            if (e.Args.Any(arg => arg.Equals("--reset-db", StringComparison.OrdinalIgnoreCase)))
            {
                resetDb = true;
            }

            try
            {
                DatabaseInitializer.Initialize(resetDb, migrateOnStartup);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Database initialization failed.\n\n{ex.Message}",
                    "ASMS Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}

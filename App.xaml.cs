using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Windows;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        private bool _databaseInitialized;
        private string _initializedConnectionString = string.Empty;
        private bool _resetPending;

        public bool MigrateOnStartup { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            MigrateOnStartup = configuration.GetValue("Database:MigrateOnStartup", true);
            _resetPending = configuration.GetValue("Database:ResetOnStartup", false);

            if (e.Args.Any(arg => arg.Equals("--reset-db", StringComparison.OrdinalIgnoreCase)))
            {
                _resetPending = true;
            }

            var loginWindow = new LoginWindow();
            MainWindow = loginWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            loginWindow.Show();
        }

        public void EnsureDatabaseInitialized()
        {
            var connectionString = ConnectionSettingsService.GetEffectiveConnectionString();
            if (_databaseInitialized && string.Equals(_initializedConnectionString, connectionString, StringComparison.Ordinal))
            {
                return;
            }

            DatabaseInitializer.Initialize(_resetPending, MigrateOnStartup);

            _databaseInitialized = true;
            _initializedConnectionString = connectionString;
            _resetPending = false;
        }
    }
}

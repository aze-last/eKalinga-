using System.IO;
using System.Windows;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement
{
    public partial class App : Application
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AttendanceShiftingManagement",
            "startup_log.txt");

        static App()
        {
            // Extremely early logging before anything else happens
            StaticLog("App static constructor started.");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => LogException("AppDomain", ev.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ev) =>
            {
                LogException("Dispatcher", ev.Exception);
                ev.Handled = true;
            };

            Log("Application OnStartup triggered.");

            // Uncomment the line below if we need to force a dialog to see if it even gets here
            // MessageBox.Show("Application is starting...", "Debug", MessageBoxButton.OK, MessageBoxImage.Information);

            try
            {
                Log("Applying theme...");
                AppThemeService.ApplySavedTheme();
                Log("Theme applied.");

                base.OnStartup(e);
                Log("Base OnStartup completed.");

                try
                {
                    Log("Performing startup maintenance...");
                    AppUpdatePackageService.PerformStartupMaintenance();
                    Log("Maintenance completed.");
                }
                catch (Exception ex)
                {
                    Log($"Maintenance failed (non-critical): {ex.Message}");
                }

                Log("Starting background update check...");
                AppUpdateCoordinator.StartBackgroundCheck();
                Log("Background check started.");
            }
            catch (Exception ex)
            {
                LogException("OnStartup Critical", ex);
                MessageBox.Show($"Application failed to start: {ex.Message}\n\nCheck the log at: {LogFilePath}", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void StaticLog(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [STATIC] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        private static void Log(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Cannot log if logging fails
            }
        }

        private static void LogException(string source, Exception? ex)
        {
            var message = ex?.Message ?? "Unknown error";
            var detail = ex?.ToString() ?? "No details available";
            Log($"CRITICAL ERROR ({source}): {message}{Environment.NewLine}{detail}");

            // Show a message box for unhandled exceptions to help the user debug
            if (Application.Current != null)
            {
                MessageBox.Show($"A critical error occurred ({source}): {message}\n\nCheck the log at: {LogFilePath}", 
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

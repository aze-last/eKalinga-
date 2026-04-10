using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            OpenSettingsWindow(SettingsWindowSection.SystemProfile, checkForUpdatesOnOpen: false);
        }

        private async void CheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                await RunUpdateCheckFlowAsync();
                return;
            }

            var originalContent = button.Content;
            button.IsEnabled = false;
            button.Content = "Checking...";

            try
            {
                await RunUpdateCheckFlowAsync();
            }
            finally
            {
                button.Content = originalContent;
                button.IsEnabled = true;
            }
        }

        private void OpenSettingsWindow(SettingsWindowSection initialSection, bool checkForUpdatesOnOpen)
        {
            var window = new SettingsWindow(
                ViewModel.CurrentUser,
                initialSection,
                checkForUpdatesOnOpen)
            {
                Owner = this
            };

            window.ShowDialog();
            WindowBrandingService.ApplyWindowIcon(this);
            ViewModel.RefreshBranding();
            ViewModel.RefreshConnectionSummary();
            ViewModel.RefreshUserSummary();
            ViewModel.ReloadCurrentView();
        }

        private async Task RunUpdateCheckFlowAsync()
        {
            var pendingUpdate = AppUpdatePackageService.LoadPendingUpdate();
            if (pendingUpdate != null && File.Exists(pendingUpdate.InstallerPath))
            {
                await PromptInstallPendingUpdateAsync(pendingUpdate);
                return;
            }

            var preferences = AppPreferencesService.Load();
            if (string.IsNullOrWhiteSpace(preferences.UpdateManifestUrl))
            {
                var openSettings = MessageBox.Show(
                    "The app update manifest URL is not configured yet.\n\nOpen Settings > Updates now?",
                    "Check for Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openSettings == MessageBoxResult.Yes)
                {
                    OpenSettingsWindow(SettingsWindowSection.Updates, checkForUpdatesOnOpen: false);
                }

                return;
            }

            UpdateCheckResult result;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                result = await AppUpdateCoordinator.CheckNowAsync(preferences.UpdateManifestUrl);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            switch (result.Status)
            {
                case UpdateCheckStatus.UpToDate:
                    MessageBox.Show(
                        $"This installation is up to date.\n\nCurrent version: {result.CurrentVersion}",
                        "Check for Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                case UpdateCheckStatus.NotConfigured:
                    var openSettings = MessageBox.Show(
                        $"{result.Message}\n\nOpen Settings > Updates now?",
                        "Check for Update",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (openSettings == MessageBoxResult.Yes)
                    {
                        OpenSettingsWindow(SettingsWindowSection.Updates, checkForUpdatesOnOpen: false);
                    }

                    return;
                case UpdateCheckStatus.Failed:
                    MessageBox.Show(
                        result.Message,
                        "Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                case UpdateCheckStatus.UpdateAvailable:
                    await HandleAvailableUpdateAsync(result);
                    return;
                default:
                    MessageBox.Show(
                        result.Message,
                        "Check for Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
            }
        }

        private async Task HandleAvailableUpdateAsync(UpdateCheckResult result)
        {
            var summary = BuildUpdateSummary(result);

            if (!result.CanDownloadInstaller)
            {
                var openPage = MessageBox.Show(
                    $"{summary}\n\nThis release does not have a downloadable installer in the manifest yet.\n\nOpen the release page now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openPage == MessageBoxResult.Yes)
                {
                    OpenReleasePage(result.ReleasePageUrl);
                }

                return;
            }

            var downloadNow = MessageBox.Show(
                $"{summary}\n\nDownload this update now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (downloadNow != MessageBoxResult.Yes)
            {
                return;
            }

            PendingAppUpdate downloadedUpdate;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                downloadedUpdate = await AppUpdatePackageService.DownloadUpdateAsync(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to download the installer.\n\n{ex.Message}",
                    "Update Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            var installNow = MessageBox.Show(
                $"Version {downloadedUpdate.Version} was downloaded and verified.\n\nInstall it now? The app will close while setup runs.",
                "Update Ready",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (installNow == MessageBoxResult.Yes)
            {
                LaunchPendingInstaller(downloadedUpdate);
                return;
            }

            MessageBox.Show(
                $"Version {downloadedUpdate.Version} is downloaded and ready.\n\nClick 'Check for Update' again anytime to install it.",
                "Update Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task PromptInstallPendingUpdateAsync(PendingAppUpdate pendingUpdate)
        {
            var releaseDateLine = string.IsNullOrWhiteSpace(pendingUpdate.PublishedAt)
                ? string.Empty
                : $"\nPublished: {pendingUpdate.PublishedAt}";
            var notes = pendingUpdate.Notes.Count == 0
                ? string.Empty
                : $"\n\nRelease notes:\n- {string.Join("\n- ", pendingUpdate.Notes)}";

            var installNow = MessageBox.Show(
                $"Version {pendingUpdate.Version} is already downloaded and ready to install.{releaseDateLine}{notes}\n\nInstall it now?",
                "Update Ready",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (installNow == MessageBoxResult.Yes)
            {
                LaunchPendingInstaller(pendingUpdate);
                return;
            }

            if (!string.IsNullOrWhiteSpace(pendingUpdate.ReleasePageUrl))
            {
                var openPage = MessageBox.Show(
                    "Do you want to open the release page instead?",
                    "Update Ready",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openPage == MessageBoxResult.Yes)
                {
                    OpenReleasePage(pendingUpdate.ReleasePageUrl);
                }
            }

            await Task.CompletedTask;
        }

        private static string BuildUpdateSummary(UpdateCheckResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Version {result.LatestVersion} is available.");
            builder.AppendLine($"Current version: {result.CurrentVersion}");

            if (!string.IsNullOrWhiteSpace(result.PublishedAt))
            {
                builder.AppendLine($"Published: {result.PublishedAt}");
            }

            if (result.Notes.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Release notes:");
                foreach (var note in result.Notes)
                {
                    builder.AppendLine($"- {note}");
                }
            }

            return builder.ToString().Trim();
        }

        private static void LaunchPendingInstaller(PendingAppUpdate pendingUpdate)
        {
            try
            {
                AppUpdatePackageService.LaunchInstaller(pendingUpdate);
                Application.Current?.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to start the installer.\n\n{ex.Message}",
                    "Install Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static void OpenReleasePage(string? releasePageUrl)
        {
            if (string.IsNullOrWhiteSpace(releasePageUrl))
            {
                MessageBox.Show(
                    "No release page is available for this update.",
                    "Open Release Page",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = releasePageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to open the release page.\n\n{ex.Message}",
                    "Open Release Page",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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

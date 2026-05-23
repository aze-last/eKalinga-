using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace AttendanceShiftingManagement.Views
{
    public partial class MainWindow : Window
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_NCMOUSEMOVE = 0x00A0;
        private const int WM_NCMOUSELEAVE = 0x02A2;
        private const int HTMAXBUTTON = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        [StructLayout(LayoutKind.Sequential)]
        public struct TRACKMOUSEEVENT
        {
            public int cbSize;
            public uint dwFlags;
            public IntPtr hwndTrack;
            public uint dwHoverTime;
        }

        private const uint TME_LEAVE = 0x00000002;
        private const uint TME_NONCLIENT = 0x00010000;

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        public static readonly DependencyProperty IsMaxButtonHoveredProperty =
            DependencyProperty.Register("IsMaxButtonHovered", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool IsMaxButtonHovered
        {
            get => (bool)GetValue(IsMaxButtonHoveredProperty);
            set => SetValue(IsMaxButtonHoveredProperty, value);
        }

        private bool _isTrackingMouse = false;
        private bool _hasShownSessionAnnouncements;
        private BarangayMainViewModel ViewModel => (BarangayMainViewModel)DataContext;

        public MainWindow(User user)
        {
            InitializeComponent();
            DataContext = new BarangayMainViewModel(user);
            WindowBrandingService.ApplyWindowIcon(this);
            Loaded += MainWindow_Loaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCHITTEST:
                    try
                    {
                        int x = (short)(lParam.ToInt32() & 0xFFFF);
                        int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
                        Point screenPoint = new Point(x, y);
                        Point windowPoint = PointFromScreen(screenPoint);

                        if (MaximizeButton != null && MaximizeButton.IsVisible)
                        {
                            var transform = MaximizeButton.TransformToAncestor(this);
                            var buttonRect = new Rect(transform.Transform(new Point(0, 0)), new Size(MaximizeButton.ActualWidth, MaximizeButton.ActualHeight));

                            if (buttonRect.Contains(windowPoint))
                            {
                                handled = true;
                                return new IntPtr(HTMAXBUTTON);
                            }
                        }
                    }
                    catch { }
                    break;

                case WM_NCMOUSEMOVE:
                    if (wParam.ToInt32() == HTMAXBUTTON)
                    {
                        IsMaxButtonHovered = true;
                    }
                    else
                    {
                        IsMaxButtonHovered = false;
                    }

                    if (!_isTrackingMouse)
                    {
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                            dwFlags = TME_LEAVE | TME_NONCLIENT,
                            hwndTrack = hwnd
                        };
                        TrackMouseEvent(ref tme);
                        _isTrackingMouse = true;
                    }
                    break;

                case WM_NCMOUSELEAVE:
                    IsMaxButtonHovered = false;
                    _isTrackingMouse = false;
                    break;

                case WM_NCLBUTTONDOWN:
                    if (wParam.ToInt32() == HTMAXBUTTON)
                    {
                        MaximizeRestore_Click(this, new RoutedEventArgs());
                        handled = true;
                        return IntPtr.Zero;
                    }
                    break;

                case WM_GETMINMAXINFO:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);

                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = rcWorkArea.Left - rcMonitorArea.Left;
                mmi.ptMaxPosition.y = rcWorkArea.Top - rcMonitorArea.Top;
                mmi.ptMaxSize.x = rcWorkArea.Right - rcWorkArea.Left;
                mmi.ptMaxSize.y = rcWorkArea.Bottom - rcWorkArea.Top;
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hasShownSessionAnnouncements)
            {
                return;
            }

            _hasShownSessionAnnouncements = true;

            try
            {
                var service = new SessionAnnouncementService();
                var snapshot = await service.BuildSnapshotAsync(ViewModel.CurrentUser.Id);
                if (!snapshot.HasUpdates)
                {
                    return;
                }

                var window = new SessionAnnouncementWindow(snapshot)
                {
                    Owner = this
                };

                window.ShowDialog();
            }
            catch
            {
                // Keep login flow resilient if the popup cannot be loaded.
            }
        }

        public void OpenSettingsFromDashboard()
        {
            OpenSettingsWindow(SettingsWindowSection.SystemProfile, checkForUpdatesOnOpen: false);
        }

        public Task CheckForUpdateFromDashboardAsync()
        {
            return RunUpdateCheckFlowAsync();
        }

        public async Task SyncRemoteAndLocalFromDashboardAsync()
        {
            try
            {
                var result = await RemotePhaseOneSyncService.SyncFromRemoteToLocalAsync();
                var icon = result.IsSuccess
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning;

                MessageBox.Show(
                    result.Message,
                    "Sync Remote And Local",
                    MessageBoxButton.OK,
                    icon);

                if (result.IsSuccess)
                {
                    ViewModel.ReloadCurrentView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Sync failed. {ex.Message}",
                    "Sync Remote And Local",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void LogoutFromDashboard()
        {
            ExecuteLogout();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsFromDashboard();
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
            ExecuteLogout();
        }

        private void ExecuteLogout()
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

            try
            {
                using var context = new AppDbContext();
                var auditService = new AuditService(context);
                auditService.LogActivity(
                    ViewModel.CurrentUser.Id,
                    "Logout",
                    "User",
                    ViewModel.CurrentUser.Id,
                    $"User '{ViewModel.CurrentUser.Username}' logged out.");

                var service = new SessionAnnouncementService();
                service.RecordLogoutCheckpoint(ViewModel.CurrentUser.Id);
                
                // Clear user permissions on logout
                UserPermissionService.Clear();
            }
            catch
            {
                // Allow logout even if activity tracking fails.
                UserPermissionService.Clear();
            }

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}

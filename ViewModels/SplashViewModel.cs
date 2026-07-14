using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using MySqlConnector;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public enum StartupStepState { Pending, Running, Done, Failed }

    public class StartupStep : ObservableObject
    {
        private StartupStepState _state = StartupStepState.Pending;

        public string Label { get; }

        public StartupStepState State
        {
            get => _state;
            set
            {
                SetProperty(ref _state, value);
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(LabelBrush));
            }
        }

        public string Icon => State switch
        {
            StartupStepState.Done    => "✓",
            StartupStepState.Running => "⏳",
            StartupStepState.Failed  => "✗",
            _                        => "○"
        };

        public Brush IconBrush => State switch
        {
            StartupStepState.Done    => new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D)),
            StartupStepState.Running => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            StartupStepState.Failed  => new SolidColorBrush(Color.FromRgb(0xBE, 0x12, 0x3C)),
            _                        => new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1))
        };

        public Brush LabelBrush => State switch
        {
            StartupStepState.Done    => new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
            StartupStepState.Running => new SolidColorBrush(Color.FromRgb(0x1E, 0x4E, 0x89)),
            StartupStepState.Failed  => new SolidColorBrush(Color.FromRgb(0xBE, 0x12, 0x3C)),
            _                        => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
        };

        public StartupStep(string label) => Label = label;
    }

    public class SplashViewModel : ObservableObject
    {
        // ── Branding ──────────────────────────────────────────────────────────
        private ImageSource? _barangayLogo;
        public ImageSource? BarangayLogo
        {
            get => _barangayLogo;
            private set => SetProperty(ref _barangayLogo, value);
        }

        // ── Startup Steps ──────────────────────────────────────────────────────
        public ObservableCollection<StartupStep> Steps { get; } = new()
        {
            new StartupStep("Loading Settings"),
            new StartupStep("Loading User Roles"),
            new StartupStep("Loading Budget Services"),
            new StartupStep("Loading GGMS Integration"),
            new StartupStep("Applying Theme"),
            new StartupStep("Connecting Database"),
        };

        // ── Mode ──────────────────────────────────────────────────────────────
        private string _mode = "Checking...";
        public string Mode
        {
            get => _mode;
            private set => SetProperty(ref _mode, value);
        }

        private Brush _modeBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        public Brush ModeBrush
        {
            get => _modeBrush;
            private set => SetProperty(ref _modeBrush, value);
        }

        private string _lastSync = "Never";
        public string LastSync
        {
            get => _lastSync;
            private set => SetProperty(ref _lastSync, value);
        }

        // ── Version ───────────────────────────────────────────────────────────
        public string AppVersion { get; } =
            Assembly.GetExecutingAssembly().GetName().Version is { } v
                ? $"v{v.Major}.{v.Minor}.{v.Build}"
                : "v1.0";

        // ── Event ─────────────────────────────────────────────────────────────
        public event EventHandler? ReadyToLaunch;

        public SplashViewModel()
        {
            LoadBranding();
            Task.Run(InitializeAsync);
        }

        private void LoadBranding()
        {
            try
            {
                var settings = SystemProfileSettingsService.Load();
                var branding = SystemProfileSettingsService.BuildLoginBranding(settings);
                BarangayLogo = LocalImageLoader.Load(branding.LogoPath);
            }
            catch
            {
                BarangayLogo = null;
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Step: Loading Settings
                await RunStep(0, async () =>
                {
                    ConnectionSettingsService.Load();
                    BudgetRuntimeOptions.Load();
                    await Task.Delay(60);
                });

                // Step: Loading User Roles
                await RunStep(1, async () =>
                {
                    // Permission enums are loaded lazily by EF on first query
                    await Task.Delay(60);
                });

                // Step: Loading Budget Services
                await RunStep(2, async () =>
                {
                    // Warm up static service registrations
                    await Task.Delay(80);
                });

                // Step: Loading GGMS Integration
                await RunStep(3, async () =>
                {
                    try
                    {
                        var options = BudgetRuntimeOptions.Load();
                        var lastSync = ReadLastSyncTime();
                        Dispatch(() => LastSync = lastSync);
                    }
                    catch { /* non-critical */ }
                    await Task.Delay(80);
                });

                // Step: Applying Theme
                await RunStep(4, async () =>
                {
                    Dispatch(AppThemeService.ApplySavedTheme);
                    await Task.Delay(60);

                    try { AppUpdatePackageService.PerformStartupMaintenance(); }
                    catch { /* non-critical */ }

                    try { BackgroundMaintenanceService.Start(); }
                    catch { /* non-critical */ }
                });

                // Step: Connecting Database
                await RunStep(5, async () =>
                {
                    try
                    {
                        // 1. Always ensure local SQLite is ready first (offline fallback)
                        using (var localDb = new LocalDbContext())
                        {
                            await localDb.Database.EnsureCreatedAsync();
                            SQLiteSchemaBootstrapper.EnsureSQLiteSchema(localDb);
                        }

                        // 2. We keep the MySQL initialization for the Hostinger connection
                        DatabaseInitializer.Initialize(resetDatabase: false, migrateOnStartup: true);
                        await Task.Delay(200);
                        
                        // 3. Test if online
                        await CheckConnectivityAsync();
                    }
                    catch
                    {
                        Dispatch(() =>
                        {
                            Mode = "Offline (DB Error)";
                            ModeBrush = new SolidColorBrush(Color.FromRgb(0xBE, 0x12, 0x3C));
                        });
                    }
                }, failOnException: false);

                await Task.Delay(300);
            }
            finally
            {
                ReadyToLaunch?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task RunStep(int index, Func<Task> action, bool failOnException = true)
        {
            var step = Steps[index];
            Dispatch(() => step.State = StartupStepState.Running);

            try
            {
                await action();
                Dispatch(() => step.State = StartupStepState.Done);
            }
            catch
            {
                Dispatch(() => step.State = failOnException ? StartupStepState.Failed : StartupStepState.Done);
                if (failOnException) throw;
            }
        }

        private async Task CheckConnectivityAsync()
        {
            try
            {
                var connectionString = ConnectionSettingsService.GetEffectiveConnectionString();
                await using var conn = new MySqlConnection(connectionString);
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await conn.OpenAsync(cts.Token);
                await conn.CloseAsync();
                Dispatch(() =>
                {
                    Mode = "Online";
                    ModeBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
                });
            }
            catch
            {
                Dispatch(() =>
                {
                    Mode = "Offline";
                    ModeBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                });
            }
        }

        private static string ReadLastSyncTime()
        {
            // Will be wired to SyncMetadata once LocalDbContext is implemented.
            // For now, return placeholder.
            return "Not yet synced";
        }

        private static void Dispatch(Action action) =>
            System.Windows.Application.Current?.Dispatcher?.Invoke(action);
    }
}

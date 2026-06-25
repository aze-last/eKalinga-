using MySqlConnector;
using System.Net.Sockets;
using System.Timers;
using Timer = System.Timers.Timer;

namespace AttendanceShiftingManagement.Services
{
    public class ConnectivityStatusChangedEventArgs : EventArgs
    {
        public bool IsOnline { get; }
        public bool IsGgmsAvailable { get; }

        public ConnectivityStatusChangedEventArgs(bool isOnline, bool isGgmsAvailable)
        {
            IsOnline = isOnline;
            IsGgmsAvailable = isGgmsAvailable;
        }
    }

    public sealed class ConnectivityService : IDisposable
    {
        private static readonly Lazy<ConnectivityService> _instance = new(() => new ConnectivityService());
        public static ConnectivityService Instance => _instance.Value;

        private readonly Timer _checkTimer;
        private bool _isOnline;
        private bool _isGgmsAvailable;
        private bool _isChecking;

        public bool IsOnline => _isOnline;
        public bool IsGgmsAvailable => _isGgmsAvailable;

        public event EventHandler<ConnectivityStatusChangedEventArgs>? ConnectivityChanged;

        private ConnectivityService()
        {
            _checkTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _checkTimer.Elapsed += async (s, e) => await CheckConnectivityAsync();
        }

        public void StartPeriodicCheck()
        {
            _checkTimer.Start();
            // Fire off an immediate check without blocking
            _ = Task.Run(CheckConnectivityAsync);
        }

        public void StopPeriodicCheck()
        {
            _checkTimer.Stop();
        }

        public async Task CheckConnectivityAsync()
        {
            if (_isChecking) return;
            _isChecking = true;

            try
            {
                var hostingerOnline = await CheckHostingerMySQLAsync();
                var ggmsOnline = await CheckGgmsMySQLAsync();

                var statusChanged = hostingerOnline != _isOnline || ggmsOnline != _isGgmsAvailable;

                _isOnline = hostingerOnline;
                _isGgmsAvailable = ggmsOnline;

                if (statusChanged)
                {
                    ConnectivityChanged?.Invoke(this, new ConnectivityStatusChangedEventArgs(_isOnline, _isGgmsAvailable));
                }
            }
            finally
            {
                _isChecking = false;
            }
        }

        private static async Task<bool> CheckHostingerMySQLAsync()
        {
            try
            {
                var connString = ConnectionSettingsService.GetEffectiveConnectionString();
                
                // If it's explicitly the local preset but configured for localhost offline testing,
                // we treat it based on whether that DB is reachable.
                if (string.IsNullOrWhiteSpace(connString)) return false;

                // Add short timeout for the connectivity check (3 seconds)
                var builder = new MySqlConnectionStringBuilder(connString)
                {
                    ConnectionTimeout = 3
                };

                await using var conn = new MySqlConnection(builder.ConnectionString);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await conn.OpenAsync(cts.Token);
                return true;
            }
            catch (Exception ex) when (ex is MySqlException || ex is SocketException || ex is TimeoutException || ex is OperationCanceledException)
            {
                return false;
            }
        }

        private static async Task<bool> CheckGgmsMySQLAsync()
        {
            try
            {
                var options = BudgetRuntimeOptions.Load();
                if (string.IsNullOrWhiteSpace(options.GgmsConnection.Server) || string.IsNullOrWhiteSpace(options.GgmsConnection.Username))
                {
                    return false;
                }

                var builder = new MySqlConnectionStringBuilder
                {
                    Server = options.GgmsConnection.Server,
                    Port = (uint)options.GgmsConnection.Port,
                    Database = options.GgmsConnection.Database,
                    UserID = options.GgmsConnection.Username,
                    Password = options.GgmsConnection.Password,
                    ConnectionTimeout = 3
                };

                await using var conn = new MySqlConnection(builder.ConnectionString);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await conn.OpenAsync(cts.Token);
                return true;
            }
            catch (Exception ex) when (ex is MySqlException || ex is SocketException || ex is TimeoutException || ex is OperationCanceledException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            _checkTimer.Dispose();
        }
    }
}

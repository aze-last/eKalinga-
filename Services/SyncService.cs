using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Windows;
using Application = System.Windows.Application;

namespace AttendanceShiftingManagement.Services
{
    public sealed class SyncService : IDisposable
    {
        private static readonly Lazy<SyncService> _instance = new(() => new SyncService());
        public static SyncService Instance => _instance.Value;

        private bool _isSyncing;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

        public event EventHandler<string>? SyncStatusChanged;
        public event EventHandler<DateTime>? LastSyncUpdated;
        public event EventHandler<Exception>? SyncFailed;

        public DateTime? LastSyncTime { get; private set; }

        private SyncService()
        {
            ConnectivityService.Instance.ConnectivityChanged += OnConnectivityChanged;
            _ = LoadLastSyncTimeAsync();
        }

        private async void OnConnectivityChanged(object? sender, ConnectivityStatusChangedEventArgs e)
        {
            if (e.IsOnline)
            {
                await TriggerSyncAsync();
            }
        }

        private async Task LoadLastSyncTimeAsync()
        {
            try
            {
                using var localDb = new LocalDbContext();
                var metadata = await localDb.SyncMetadata.FirstOrDefaultAsync(m => m.TableName == "MainDatabaseSync");
                if (metadata != null)
                {
                    LastSyncTime = metadata.LastSyncAt;
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        LastSyncUpdated?.Invoke(this, LastSyncTime.Value);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading last sync time: {ex.Message}");
            }
        }

        public async Task TriggerSyncAsync()
        {
            if (_isSyncing || !ConnectivityService.Instance.IsOnline) return;

            await _syncSemaphore.WaitAsync();
            try
            {
                if (_isSyncing || !ConnectivityService.Instance.IsOnline) return;
                _isSyncing = true;
                UpdateStatus("Syncing...");

                await PerformSyncCycleAsync();

                UpdateStatus("Up to date");
            }
            catch (Exception ex)
            {
                UpdateStatus("Sync failed");
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SyncFailed?.Invoke(this, ex);
                });
                Debug.WriteLine($"Sync Failed: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
                _syncSemaphore.Release();
            }
        }

        private void UpdateStatus(string status)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                SyncStatusChanged?.Invoke(this, status);
            });
        }

        private async Task PerformSyncCycleAsync()
        {
            var syncStartTime = DateTime.Now;

            using var localDb = new LocalDbContext();
            var lastSync = LastSyncTime ?? DateTime.MinValue;

            // Flush offline GGMS transactions if available
            if (ConnectivityService.Instance.IsGgmsAvailable)
            {
                var ggmsService = new GgmsConsolidatedTransactionService();
                await ggmsService.FlushPendingTransactionsAsync(localDb);
            }

            // Use DatabaseInitializer logic without destroying data
            DatabaseInitializer.Initialize(resetDatabase: false, migrateOnStartup: true);
            using var remoteDb = new AppDbContext();

            // Pull phase: get records from remote where UpdatedAt > lastSync
            // Currently keeping this light. A robust sync would iterate all entities generically.
            // For Phase 4, we establish the pattern. 
            // In a production app, each entity table needs a local-remote comparison, 
            // but we start with a targeted approach per table.
            
            // Example for ActivityLogs:
            var newRemoteLogs = await remoteDb.ActivityLogs
                .Where(l => l.UpdatedAt > lastSync)
                .AsNoTracking()
                .ToListAsync();

            if (newRemoteLogs.Any())
            {
                foreach (var remoteLog in newRemoteLogs)
                {
                    var localLog = await localDb.ActivityLogs.FirstOrDefaultAsync(l => l.SyncId == remoteLog.SyncId);
                    if (localLog == null)
                    {
                        localDb.ActivityLogs.Add(remoteLog);
                    }
                    else if (remoteLog.UpdatedAt > localLog.UpdatedAt)
                    {
                        localDb.Entry(localLog).CurrentValues.SetValues(remoteLog);
                    }
                }
                await localDb.SaveChangesAsync();
            }

            // Push phase: push local records where UpdatedAt > lastSync
            var newLocalLogs = await localDb.ActivityLogs
                .Where(l => l.UpdatedAt > lastSync)
                .AsNoTracking()
                .ToListAsync();

            if (newLocalLogs.Any())
            {
                foreach (var localLog in newLocalLogs)
                {
                    var remoteLog = await remoteDb.ActivityLogs.FirstOrDefaultAsync(l => l.SyncId == localLog.SyncId);
                    if (remoteLog == null)
                    {
                        remoteDb.ActivityLogs.Add(localLog);
                    }
                    else if (localLog.UpdatedAt > remoteLog.UpdatedAt)
                    {
                        remoteDb.Entry(remoteLog).CurrentValues.SetValues(localLog);
                    }
                }
                await remoteDb.SaveChangesAsync();
            }

            // Sync Users
            // Pull Users
            var newRemoteUsers = await remoteDb.Users
                .Where(u => u.UpdatedAt > lastSync)
                .AsNoTracking()
                .ToListAsync();

            if (newRemoteUsers.Any())
            {
                foreach (var remoteUser in newRemoteUsers)
                {
                    var localUser = await localDb.Users.FirstOrDefaultAsync(u => u.SyncId == remoteUser.SyncId);
                    if (localUser == null)
                    {
                        localDb.Users.Add(remoteUser);
                    }
                    else if (remoteUser.UpdatedAt > localUser.UpdatedAt)
                    {
                        localDb.Entry(localUser).CurrentValues.SetValues(remoteUser);
                    }
                }
                await localDb.SaveChangesAsync();
            }

            // Push Users
            var newLocalUsers = await localDb.Users
                .Where(u => u.UpdatedAt > lastSync)
                .AsNoTracking()
                .ToListAsync();

            if (newLocalUsers.Any())
            {
                foreach (var localUser in newLocalUsers)
                {
                    var remoteUser = await remoteDb.Users.FirstOrDefaultAsync(u => u.SyncId == localUser.SyncId);
                    if (remoteUser == null)
                    {
                        remoteDb.Users.Add(localUser);
                    }
                    else if (localUser.UpdatedAt > remoteUser.UpdatedAt)
                    {
                        remoteDb.Entry(remoteUser).CurrentValues.SetValues(localUser);
                    }
                }
                await remoteDb.SaveChangesAsync();
            }

            // Save the sync timestamp
            var metadata = await localDb.SyncMetadata.FirstOrDefaultAsync(m => m.TableName == "MainDatabaseSync");
            if (metadata == null)
            {
                metadata = new SyncMetadata { TableName = "MainDatabaseSync", LastSyncAt = syncStartTime };
                localDb.SyncMetadata.Add(metadata);
            }
            else
            {
                metadata.LastSyncAt = syncStartTime;
                localDb.SyncMetadata.Update(metadata);
            }

            await localDb.SaveChangesAsync();

            LastSyncTime = syncStartTime;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastSyncUpdated?.Invoke(this, LastSyncTime.Value);
            });
        }

        public void Dispose()
        {
            ConnectivityService.Instance.ConnectivityChanged -= OnConnectivityChanged;
            _syncSemaphore.Dispose();
        }
    }
}

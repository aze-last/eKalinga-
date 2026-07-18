using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AttendanceShiftingManagement.Data;

namespace AttendanceShiftingManagement.Services
{
    public static class BackgroundMaintenanceService
    {
        private static readonly SemaphoreSlim MaintenanceGate = new(1, 1);

        public static void Start()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await PerformMaintenanceAsync();
                }
                catch
                {
                    // Fail-safe for background threads
                }

                try
                {
                    await FlushPendingCrsAccessLogsAsync();
                }
                catch
                {
                    // Fail-safe for background threads
                }

                try
                {
                    // CRS is the source of truth for the masterlist — mirror any
                    // beneficiaries it holds that are missing locally (fail-soft
                    // when offline; local data keeps serving).
                    await new CrsMasterlistMirrorService().MirrorValidatedBeneficiariesAsync();
                }
                catch
                {
                    // Fail-safe for background threads
                }

                try
                {
                    // Pre-warm the e-Kard digital ID cache so verification works
                    // offline for every cardholder, not just previously scanned ones.
                    await new CrsDigitalIdCacheSyncService().SyncAsync();
                }
                catch
                {
                    // Fail-safe for background threads
                }
            });
        }

        public static async Task PerformMaintenanceAsync(LocalDbContext? dbContext = null, CancellationToken cancellationToken = default)
        {
            await MaintenanceGate.WaitAsync(cancellationToken);
            try
            {
                var options = new DigitalIdCacheOptions();

                if (dbContext != null)
                {
                    await PerformMaintenanceWithContextAsync(dbContext, options, cancellationToken);
                }
                else
                {
                    using var localDb = new LocalDbContext();
                    await PerformMaintenanceWithContextAsync(localDb, options, cancellationToken);
                }
            }
            finally
            {
                MaintenanceGate.Release();
            }
        }

        /// <summary>
        /// Retries queued e-Kard record_access_logs audit rows that failed to write
        /// because the CRS database was offline (contract: never block a verification
        /// on the audit write — queue and retry). Remove-on-success, keep-on-failure.
        /// </summary>
        public static async Task<int> FlushPendingCrsAccessLogsAsync(
            LocalDbContext? dbContext = null,
            ICrsGateway? gateway = null,
            CancellationToken cancellationToken = default)
        {
            if (dbContext != null)
            {
                return await FlushPendingCrsAccessLogsWithContextAsync(dbContext, gateway ?? new CrsGateway(), cancellationToken);
            }

            using var localDb = new LocalDbContext();
            return await FlushPendingCrsAccessLogsWithContextAsync(localDb, gateway ?? new CrsGateway(), cancellationToken);
        }

        private static async Task<int> FlushPendingCrsAccessLogsWithContextAsync(LocalDbContext localDb, ICrsGateway gateway, CancellationToken cancellationToken)
        {
            var pending = await localDb.CrsPendingAccessLogs
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            var flushed = 0;
            foreach (var item in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CrsAccessLogEntry? entry;
                try
                {
                    entry = JsonSerializer.Deserialize<CrsAccessLogEntry>(item.PayloadJson);
                }
                catch
                {
                    entry = null;
                }

                if (entry == null)
                {
                    // Unreadable payload — drop it so the queue can't wedge forever.
                    localDb.CrsPendingAccessLogs.Remove(item);
                    await localDb.SaveChangesAsync(cancellationToken);
                    continue;
                }

                try
                {
                    await gateway.InsertAccessLogAsync(entry, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Still offline/unreachable — keep the rest for the next pass.
                    break;
                }

                localDb.CrsPendingAccessLogs.Remove(item);
                await localDb.SaveChangesAsync(cancellationToken);
                flushed++;
            }

            return flushed;
        }

        private static async Task PerformMaintenanceWithContextAsync(LocalDbContext localDb, DigitalIdCacheOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var photoExpiryCutoff = DateTime.UtcNow.AddDays(-options.PhotoRetentionDays);
            var expiredPhotos = await localDb.DigitalIdPhotoCaches
                .Where(p => p.UpdatedAt < photoExpiryCutoff)
                .ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (expiredPhotos.Any())
            {
                localDb.DigitalIdPhotoCaches.RemoveRange(expiredPhotos);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var statusExpiryCutoff = DateTime.UtcNow.AddDays(-options.StatusRetentionDays);
            var expiredStatuses = await localDb.DigitalIdStatusCaches
                .Where(s => s.UpdatedAt < statusExpiryCutoff)
                .ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (expiredStatuses.Any())
            {
                localDb.DigitalIdStatusCaches.RemoveRange(expiredStatuses);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (expiredPhotos.Any() || expiredStatuses.Any())
            {
                await localDb.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

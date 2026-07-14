using System;
using System.Linq;
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

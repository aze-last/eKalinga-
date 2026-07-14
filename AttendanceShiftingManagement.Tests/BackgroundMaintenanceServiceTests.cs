using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AttendanceShiftingManagement.Tests
{
    public class BackgroundMaintenanceServiceTests
    {
        [Fact]
        public async Task PerformMaintenanceAsync_PurgesExpiredEntries_AndKeepsFreshEntries()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            
            var expiredPhoto = new DigitalIdPhotoCache
            {
                BeneficiaryIdHash = "HASH_PHOTO_EXPIRED",
                EncryptedPhotoBytes = ConnectionSecretProtector.Protect(Convert.ToBase64String(new byte[] { 1, 2, 3 })),
                UpdatedAt = DateTime.UtcNow.AddDays(-35)
            };
            var freshPhoto = new DigitalIdPhotoCache
            {
                BeneficiaryIdHash = "HASH_PHOTO_FRESH",
                EncryptedPhotoBytes = ConnectionSecretProtector.Protect(Convert.ToBase64String(new byte[] { 4, 5, 6 })),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var expiredStatus = new DigitalIdStatusCache
            {
                BeneficiaryIdHash = "HASH_STATUS_EXPIRED",
                EncryptedBeneficiaryId = ConnectionSecretProtector.Protect("BEN-EXP"),
                EncryptedStatus = ConnectionSecretProtector.Protect("Active"),
                EncryptedExpiryDate = ConnectionSecretProtector.Protect(""),
                EncryptedCardNumber = ConnectionSecretProtector.Protect("BID-EXP"),
                EncryptedQrPayload = ConnectionSecretProtector.Protect("QR-EXP"),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            };
            var freshStatus = new DigitalIdStatusCache
            {
                BeneficiaryIdHash = "HASH_STATUS_FRESH",
                EncryptedBeneficiaryId = ConnectionSecretProtector.Protect("BEN-FRESH"),
                EncryptedStatus = ConnectionSecretProtector.Protect("Active"),
                EncryptedExpiryDate = ConnectionSecretProtector.Protect(""),
                EncryptedCardNumber = ConnectionSecretProtector.Protect("BID-FRESH"),
                EncryptedQrPayload = ConnectionSecretProtector.Protect("QR-FRESH"),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            };

            localDb.DigitalIdPhotoCaches.AddRange(expiredPhoto, freshPhoto);
            localDb.DigitalIdStatusCaches.AddRange(expiredStatus, freshStatus);
            await localDb.SaveChangesAsync();

            await BackgroundMaintenanceService.PerformMaintenanceAsync(localDb, CancellationToken.None);

            var remainingPhotos = await localDb.DigitalIdPhotoCaches.ToListAsync();
            Assert.Single(remainingPhotos);
            Assert.Equal("HASH_PHOTO_FRESH", remainingPhotos[0].BeneficiaryIdHash);

            var remainingStatuses = await localDb.DigitalIdStatusCaches.ToListAsync();
            Assert.Single(remainingStatuses);
            Assert.Equal("HASH_STATUS_FRESH", remainingStatuses[0].BeneficiaryIdHash);
        }

        [Fact]
        public async Task PerformMaintenanceAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            using var localDb = TestDbContextFactory.CreateContext();

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel the token

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await BackgroundMaintenanceService.PerformMaintenanceAsync(localDb, cts.Token);
            });
        }
    }
}

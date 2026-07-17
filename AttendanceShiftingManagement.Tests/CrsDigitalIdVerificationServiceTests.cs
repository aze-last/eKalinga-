using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Xunit;

namespace AttendanceShiftingManagement.Tests
{
    public class CrsDigitalIdVerificationServiceTests
    {
        private class FakeCrsGateway : ICrsGateway
        {
            public CrsDigitalIdRow? DigitalIdRow { get; set; }
            public long? DemographicCharacteristicId { get; set; }
            public DateTime? PhotoUpdatedAt { get; set; }
            public CrsPhotoRow? PhotoRow { get; set; }
            public bool ThrowOnStatus { get; set; }
            public bool ThrowOnAuditInsert { get; set; }
            public int StatusCallCount;
            public int PhotoBlobCallCount;
            public int AuditInsertCount;
            public CrsAccessLogEntry? LastAuditEntry { get; private set; }

            public Task<CrsDigitalIdRow?> GetLatestDigitalIdRowAsync(string beneficiaryId, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref StatusCallCount);
                if (ThrowOnStatus) throw new InvalidOperationException("CRS unreachable");
                return Task.FromResult(DigitalIdRow);
            }

            public Task<long?> GetDemographicCharacteristicIdAsync(string beneficiaryId, CancellationToken cancellationToken)
            {
                if (ThrowOnStatus) throw new InvalidOperationException("CRS unreachable");
                return Task.FromResult(DemographicCharacteristicId);
            }

            public Task<DateTime?> GetPhotoUpdatedAtAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
            {
                return Task.FromResult(PhotoUpdatedAt);
            }

            public Task<CrsPhotoRow?> GetPhotoAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref PhotoBlobCallCount);
                return Task.FromResult(PhotoRow);
            }

            public Task InsertAccessLogAsync(CrsAccessLogEntry entry, CancellationToken cancellationToken)
            {
                if (ThrowOnAuditInsert) throw new InvalidOperationException("CRS unreachable");
                Interlocked.Increment(ref AuditInsertCount);
                LastAuditEntry = entry;
                return Task.CompletedTask;
            }

            public Task<CrsSchemaProbeResult> ProbeSchemaAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new CrsSchemaProbeResult(true, null));
            }
        }

        private class PassthroughResiliencyPolicy : ICrsResiliencyPolicy
        {
            public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
                => action(cancellationToken);
        }

        private static CrsDigitalIdVerificationService CreateService(LocalDbContext localDb, FakeCrsGateway gateway, string dbName)
        {
            return new CrsDigitalIdVerificationService(
                localDb,
                gateway,
                new PassthroughResiliencyPolicy(),
                () => TestDbContextFactory.CreateContext(dbName));
        }

        private static async Task<EKardVerificationResult> VerifyAsync(CrsDigitalIdVerificationService service, string beneficiaryId)
        {
            var result = await service.VerifyAsync(
                new EKardVerificationRequest { BeneficiaryId = beneficiaryId, UserId = 7, UserName = "tester" },
                CancellationToken.None);
            if (service.LastAuditTask != null)
            {
                await service.LastAuditTask;
            }
            return result;
        }

        [Fact]
        public async Task VerifyAsync_ActiveWithNoExpiry_IsValid()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null)
            };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Valid, result.Validity);
            Assert.Equal(EKardSource.LiveCrs, result.Source);
        }

        [Fact]
        public async Task VerifyAsync_ActiveExpiringToday_IsStillValid()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), DateTime.Today, null, null)
            };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Valid, result.Validity);
        }

        [Fact]
        public async Task VerifyAsync_ActiveButExpired_IsExpiredNotValid()
        {
            // Contract: this DB never auto-expires — status alone would accept expired cards.
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-2), DateTime.Today.AddDays(-1), null, null)
            };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Expired, result.Validity);
        }

        [Fact]
        public async Task VerifyAsync_RevokedStatus_IsRevokedWithReason()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Revoked", DateTime.Today.AddYears(-1), null, DateTime.Today.AddDays(-3), "Lost card")
            };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Revoked, result.Validity);
            Assert.Equal("Lost card", result.RevocationReason);
        }

        [Fact]
        public async Task VerifyAsync_NoRow_IsNotFound()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway { DigitalIdRow = null };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-999999");

            Assert.Equal(EKardValidity.NotFound, result.Validity);
        }

        [Fact]
        public async Task VerifyAsync_OfflineWithCache_ServesCacheWithLastSyncedAt()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null)
            };
            var service = CreateService(localDb, gateway, dbName);

            // First scan online — populates crs_status_cache.
            var live = await VerifyAsync(service, "BEN-000123");
            Assert.Equal(EKardValidity.Valid, live.Validity);

            // Second scan offline.
            gateway.ThrowOnStatus = true;
            var offline = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Valid, offline.Validity);
            Assert.Equal(EKardSource.LocalCache, offline.Source);
            Assert.NotNull(offline.LastSyncedAt);
        }

        [Fact]
        public async Task VerifyAsync_OfflineUncached_IsUnknown()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway { ThrowOnStatus = true };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Unknown, result.Validity);
            Assert.Equal(EKardSource.LocalCache, result.Source);
            Assert.Null(result.LastSyncedAt);
        }

        [Fact]
        public async Task VerifyAsync_PhotoFreshInCache_DoesNotRePullBlob()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var updatedAt = new DateTime(2026, 7, 1, 10, 0, 0);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null),
                DemographicCharacteristicId = 55,
                PhotoUpdatedAt = updatedAt,
                PhotoRow = new CrsPhotoRow(new byte[] { 1, 2, 3 }, updatedAt)
            };
            var service = CreateService(localDb, gateway, dbName);

            var first = await VerifyAsync(service, "BEN-000123");
            Assert.Equal(new byte[] { 1, 2, 3 }, first.Photo);
            Assert.Equal(1, gateway.PhotoBlobCallCount);

            // Same updated_at — freshness probe should serve the cache, no blob pull.
            var second = await VerifyAsync(service, "BEN-000123");
            Assert.Equal(new byte[] { 1, 2, 3 }, second.Photo);
            Assert.Equal(1, gateway.PhotoBlobCallCount);
        }

        [Fact]
        public async Task VerifyAsync_PhotoStale_RePullsBlobAndUpdatesCache()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null),
                DemographicCharacteristicId = 55,
                PhotoUpdatedAt = new DateTime(2026, 7, 1, 10, 0, 0),
                PhotoRow = new CrsPhotoRow(new byte[] { 1 }, new DateTime(2026, 7, 1, 10, 0, 0))
            };
            var service = CreateService(localDb, gateway, dbName);
            await VerifyAsync(service, "BEN-000123");
            Assert.Equal(1, gateway.PhotoBlobCallCount);

            // Remote photo changed — updated_at moved forward.
            gateway.PhotoUpdatedAt = new DateTime(2026, 7, 10, 9, 0, 0);
            gateway.PhotoRow = new CrsPhotoRow(new byte[] { 9, 9 }, gateway.PhotoUpdatedAt);

            var refreshed = await VerifyAsync(service, "BEN-000123");
            Assert.Equal(new byte[] { 9, 9 }, refreshed.Photo);
            Assert.Equal(2, gateway.PhotoBlobCallCount);
        }

        [Fact]
        public async Task VerifyAsync_NullPhotoBytesWithRow_IsConfirmedAbsent()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null),
                DemographicCharacteristicId = 55,
                PhotoUpdatedAt = DateTime.Now,
                PhotoRow = new CrsPhotoRow(null, DateTime.Now)
            };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Null(result.Photo);
            Assert.True(result.PhotoConfirmedAbsent);
        }

        [Fact]
        public async Task VerifyAsync_WritesContractCompliantAuditRow()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null)
            };
            var service = CreateService(localDb, gateway, dbName);

            await VerifyAsync(service, "BEN-000123");

            Assert.Equal(1, gateway.AuditInsertCount);
            var entry = gateway.LastAuditEntry!;
            Assert.Equal("DIGITAL_ID_VERIFICATION", entry.RecordType);
            Assert.Equal("BEN-000123", entry.ReferenceNo);
            Assert.StartsWith("VERIFY — e-Kard check by eKalinga+", entry.ActionTaken);
            Assert.Contains("Active", entry.ActionTaken);
            // Live CRS record_access_logs.action_taken is varchar(50) — the status
            // must survive within that budget or MySQL truncates it mid-word.
            Assert.True(entry.ActionTaken.Length <= 50, $"action_taken too long ({entry.ActionTaken.Length}): {entry.ActionTaken}");
            Assert.True(Guid.TryParse(entry.SyncId, out _));
            Assert.Equal(7, entry.UserId);
            Assert.Equal("tester", entry.UserName);
        }

        [Fact]
        public async Task VerifyAsync_AuditInsertFails_QueuesPendingLogAndDoesNotThrow()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null),
                ThrowOnAuditInsert = true
            };
            var service = CreateService(localDb, gateway, dbName);

            var result = await VerifyAsync(service, "BEN-000123");

            Assert.Equal(EKardValidity.Valid, result.Validity);
            using var checkDb = TestDbContextFactory.CreateContext(dbName);
            Assert.Equal(1, checkDb.CrsPendingAccessLogs.Count());
        }

        [Fact]
        public async Task FlushPendingCrsAccessLogsAsync_RetriesQueuedRows_RemovesOnSuccess()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway
            {
                DigitalIdRow = new CrsDigitalIdRow("EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null),
                ThrowOnAuditInsert = true
            };
            var service = CreateService(localDb, gateway, dbName);
            await VerifyAsync(service, "BEN-000123");
            Assert.Equal(1, localDb.CrsPendingAccessLogs.Count());

            // Back online — flush must insert then remove the queued row.
            gateway.ThrowOnAuditInsert = false;
            var flushed = await BackgroundMaintenanceService.FlushPendingCrsAccessLogsAsync(localDb, gateway);

            Assert.Equal(1, flushed);
            Assert.Equal(1, gateway.AuditInsertCount);
            Assert.Empty(localDb.CrsPendingAccessLogs.ToList());
        }

        [Fact]
        public async Task VerifyAsync_InvalidPayload_ReturnsUnknownWithoutQueryingCrs()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using var localDb = TestDbContextFactory.CreateContext(dbName);
            var gateway = new FakeCrsGateway();
            var service = CreateService(localDb, gateway, dbName);

            var result = await service.VerifyAsync(
                new EKardVerificationRequest { BeneficiaryId = "   " },
                CancellationToken.None);

            Assert.Equal(EKardValidity.Unknown, result.Validity);
            Assert.Equal(0, gateway.StatusCallCount);
        }
    }
}

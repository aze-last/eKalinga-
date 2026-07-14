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
    public class EKardVerificationServiceTests
    {
        private class FakeConnectionProvider : ICrsConnectionProvider
        {
            public string GetConnectionString() => "server=127.0.0.1;database=test;";
        }

        private class FakeResiliencyPolicy : ICrsResiliencyPolicy
        {
            public Func<CancellationToken, Task<object>>? OnExecute { get; set; }

            public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
            {
                if (OnExecute != null)
                {
                    var res = await OnExecute(cancellationToken);
                    return (T)res;
                }
                return await action(cancellationToken);
            }
        }

        private class FakeHealthService : ICrsHealthService
        {
            public bool Connected { get; set; } = true;
            public long LatencyMs => 10;
            public string RemoteVersion => "8.0.0";
            public DateTime? LastSuccessfulConnection { get; set; } = DateTime.UtcNow;
            public DateTime? LastFailure { get; set; }
            public string FailureReason => "";
            public int ConsecutiveFailures => 0;
            public bool IsCompatible { get; set; } = true;

            public Task CheckHealthAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private class FakeCrsDbContextFactory : ICrsDbContextFactory
        {
            private readonly DbContextOptions<CrsDbContext> _options;
            public int CreateCount;
            public int DelayMs;

            public FakeCrsDbContextFactory(string dbName, int delayMs = 0)
            {
                _options = new DbContextOptionsBuilder<CrsDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options;
                DelayMs = delayMs;
            }

            public CrsDbContext CreateDbContext()
            {
                Interlocked.Increment(ref CreateCount);
                if (DelayMs > 0)
                {
                    Thread.Sleep(DelayMs);
                }
                return new CrsDbContext(_options);
            }
        }

        [Fact]
        public async Task VerifyDigitalIdAsync_WhenFreshCacheExists_ReturnsCachedValueWithoutQueryingCrs()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            
            var payload = "ASMBID123456ABCDEF123456";
            string payloadHash;
            
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                payloadHash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
            }

            var statusCache = new DigitalIdStatusCache
            {
                BeneficiaryIdHash = payloadHash,
                EncryptedBeneficiaryId = ConnectionSecretProtector.Protect("BEN-999"),
                EncryptedStatus = ConnectionSecretProtector.Protect("Active"),
                EncryptedExpiryDate = ConnectionSecretProtector.Protect(""),
                EncryptedCardNumber = ConnectionSecretProtector.Protect("BID-999999"),
                EncryptedQrPayload = ConnectionSecretProtector.Protect(payload),
                UpdatedAt = DateTime.UtcNow
            };
            localDb.DigitalIdStatusCaches.Add(statusCache);
            await localDb.SaveChangesAsync();

            var connProvider = new FakeConnectionProvider();
            var resiliency = new FakeResiliencyPolicy();
            var health = new FakeHealthService();
            var crsDbFactory = new FakeCrsDbContextFactory(Guid.NewGuid().ToString("N"));

            resiliency.OnExecute = (_) => throw new Exception("Should not hit remote DB!");

            var service = new EKardVerificationService(localDb, connProvider, crsDbFactory, resiliency, health);

            var result = await service.VerifyDigitalIdAsync(
                new DigitalIdVerificationRequest { QrPayload = payload },
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal("Active", result.Status);
            Assert.Equal(VerificationSource.LocalCache, result.Source);
            Assert.Equal("BID-999999", result.IdNumber);
        }

        [Fact]
        public async Task VerifyDigitalIdAsync_WhenCacheIsExpired_QueriesRemoteAndUpdatesCache()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            var payload = "ASMBID123456ABCDEF123456";
            string payloadHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                payloadHash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
            }

            var statusCache = new DigitalIdStatusCache
            {
                BeneficiaryIdHash = payloadHash,
                EncryptedBeneficiaryId = ConnectionSecretProtector.Protect("BEN-999"),
                EncryptedStatus = ConnectionSecretProtector.Protect("Active"),
                EncryptedExpiryDate = ConnectionSecretProtector.Protect(""),
                EncryptedCardNumber = ConnectionSecretProtector.Protect("BID-999999"),
                EncryptedQrPayload = ConnectionSecretProtector.Protect(payload),
                UpdatedAt = DateTime.UtcNow.AddDays(-15)
            };
            localDb.DigitalIdStatusCaches.Add(statusCache);
            await localDb.SaveChangesAsync();

            var connProvider = new FakeConnectionProvider();
            var resiliency = new FakeResiliencyPolicy();
            var health = new FakeHealthService();
            
            var remoteDbName = Guid.NewGuid().ToString("N");
            var crsDbFactory = new FakeCrsDbContextFactory(remoteDbName);

            using (var remoteDb = crsDbFactory.CreateDbContext())
            {
                remoteDb.ValBeneficiaries.Add(new CrsValBeneficiary
                {
                    Id = 1,
                    BeneficiaryId = "BEN-999",
                    FullName = "Test Remote User"
                });
                remoteDb.BeneficiaryStagings.Add(new CrsBeneficiaryStaging
                {
                    StagingId = 10,
                    BeneficiaryId = "BEN-999"
                });
                remoteDb.BeneficiaryDigitalIds.Add(new CrsBeneficiaryDigitalId
                {
                    Id = 20,
                    BeneficiaryStagingId = 10,
                    CardNumber = "BID-999999",
                    QrPayload = payload,
                    IsActive = true
                });
                await remoteDb.SaveChangesAsync();
            }

            var service = new EKardVerificationService(localDb, connProvider, crsDbFactory, resiliency, health);

            var result = await service.VerifyDigitalIdAsync(
                new DigitalIdVerificationRequest { QrPayload = payload },
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal("Active", result.Status);
            Assert.Equal(VerificationSource.RemoteCRS, result.Source);
            Assert.Equal("BID-999999", result.IdNumber);
            Assert.NotNull(result.BeneficiaryDetails);
            Assert.Equal("Test Remote User", result.BeneficiaryDetails!.FullName);

            var updatedCache = await localDb.DigitalIdStatusCaches.FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash);
            Assert.NotNull(updatedCache);
            Assert.True(updatedCache!.UpdatedAt > DateTime.UtcNow.AddSeconds(-5));
        }

        [Fact]
        public async Task VerifyDigitalIdAsync_WhenRemoteThrowsAndExpiredCacheIsOfflineValid_ReturnsOfflineCacheResult()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            var payload = "ASMBID123456ABCDEF123456";
            string payloadHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                payloadHash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
            }

            var statusCache = new DigitalIdStatusCache
            {
                BeneficiaryIdHash = payloadHash,
                EncryptedBeneficiaryId = ConnectionSecretProtector.Protect("BEN-999"),
                EncryptedStatus = ConnectionSecretProtector.Protect("Active"),
                EncryptedExpiryDate = ConnectionSecretProtector.Protect(""),
                EncryptedCardNumber = ConnectionSecretProtector.Protect("BID-999999"),
                EncryptedQrPayload = ConnectionSecretProtector.Protect(payload),
                UpdatedAt = DateTime.UtcNow.AddHours(-10)
            };
            localDb.DigitalIdStatusCaches.Add(statusCache);
            await localDb.SaveChangesAsync();

            var connProvider = new FakeConnectionProvider();
            var resiliency = new FakeResiliencyPolicy();
            var health = new FakeHealthService { Connected = false };
            var crsDbFactory = new FakeCrsDbContextFactory(Guid.NewGuid().ToString("N"));

            var options = new DigitalIdCacheOptions
            {
                StatusRetentionDays = 0,
                OfflineValidityHours = 24
            };

            var optionsMock = Microsoft.Extensions.Options.Options.Create(options);

            var service = new EKardVerificationService(localDb, connProvider, crsDbFactory, resiliency, health, cacheOptions: optionsMock);

            var result = await service.VerifyDigitalIdAsync(
                new DigitalIdVerificationRequest { QrPayload = payload },
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal("Active", result.Status);
            Assert.Equal(VerificationSource.OfflineCache, result.Source);
            Assert.Equal("BID-999999", result.IdNumber);
        }

        [Fact]
        public async Task VerifyDigitalIdAsync_WithConcurrentLookups_PreventsCacheStampede()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            var payload = "ASMBID123456ABCDEF123456";
            
            // Set delay to 200ms so concurrent lookups stack up on the lock
            var crsDbFactory = new FakeCrsDbContextFactory(Guid.NewGuid().ToString("N"), delayMs: 200);
            using (var remoteDb = crsDbFactory.CreateDbContext())
            {
                remoteDb.ValBeneficiaries.Add(new CrsValBeneficiary { Id = 1, BeneficiaryId = "BEN-999", FullName = "Concurrent User" });
                remoteDb.BeneficiaryStagings.Add(new CrsBeneficiaryStaging { StagingId = 10, BeneficiaryId = "BEN-999" });
                remoteDb.BeneficiaryDigitalIds.Add(new CrsBeneficiaryDigitalId
                {
                    Id = 20,
                    BeneficiaryStagingId = 10,
                    CardNumber = "BID-999999",
                    QrPayload = payload,
                    IsActive = true
                });
                await remoteDb.SaveChangesAsync();
            }

            // Reset creation counter after setup
            crsDbFactory.CreateCount = 0;

            var connProvider = new FakeConnectionProvider();
            var resiliency = new FakeResiliencyPolicy();
            var health = new FakeHealthService();
            var service = new EKardVerificationService(localDb, connProvider, crsDbFactory, resiliency, health);

            var tasks = Enumerable.Range(0, 5).Select(_ => 
                service.VerifyDigitalIdAsync(new DigitalIdVerificationRequest { QrPayload = payload }, CancellationToken.None)
            ).ToList();

            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (!r.IsValid)
                {
                    throw new Exception($"Result at index {i} is invalid! Status: {r.Status}, Source: {r.Source}, IdNumber: {r.IdNumber}");
                }
                Assert.Equal("BID-999999", r.IdNumber);
            }

            // Verify that the DbContext was created exactly once (excluding setup)
            Assert.Equal(1, crsDbFactory.CreateCount);
        }

        [Fact]
        public async Task VerifyDigitalIdAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            var payload = "ASMBID123456ABCDEF123456";

            var connProvider = new FakeConnectionProvider();
            var resiliency = new FakeResiliencyPolicy();
            var health = new FakeHealthService();
            var crsDbFactory = new FakeCrsDbContextFactory(Guid.NewGuid().ToString("N"));

            var service = new EKardVerificationService(localDb, connProvider, crsDbFactory, resiliency, health);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.VerifyDigitalIdAsync(
                    new DigitalIdVerificationRequest { QrPayload = payload },
                    cts.Token);
            });
        }

        [Fact]
        public async Task VerifyDigitalIdAsync_WhenRemoteIsRevoked_ReturnsRevokedStatus()
        {
            using var localDb = TestDbContextFactory.CreateContext();
            var payload = "ASMBID123456ABCDEF123456";
            
            var crsDbFactory = new FakeCrsDbContextFactory(Guid.NewGuid().ToString("N"));
            using (var remoteDb = crsDbFactory.CreateDbContext())
            {
                remoteDb.ValBeneficiaries.Add(new CrsValBeneficiary { Id = 1, BeneficiaryId = "BEN-999", FullName = "Revoked User" });
                remoteDb.BeneficiaryStagings.Add(new CrsBeneficiaryStaging { StagingId = 10, BeneficiaryId = "BEN-999" });
                remoteDb.BeneficiaryDigitalIds.Add(new CrsBeneficiaryDigitalId
                {
                    Id = 20,
                    BeneficiaryStagingId = 10,
                    CardNumber = "BID-999999",
                    QrPayload = payload,
                    IsActive = false
                });
                await remoteDb.SaveChangesAsync();
            }

            var connProvider = new FakeConnectionProvider();
            var resiliency = new FakeResiliencyPolicy();
            var health = new FakeHealthService();
            var service = new EKardVerificationService(localDb, connProvider, crsDbFactory, resiliency, health);

            var result = await service.VerifyDigitalIdAsync(
                new DigitalIdVerificationRequest { QrPayload = payload },
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Equal("Revoked", result.Status);
            Assert.Equal(VerificationSource.RemoteCRS, result.Source);
        }
    }
}

using System;
using System.Collections.Generic;
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
    public class CrsMasterlistMirrorServiceTests
    {
        private class FakeBulkCrsGateway : ICrsGateway
        {
            public IReadOnlyList<CrsValBeneficiaryRow> Beneficiaries { get; set; } = Array.Empty<CrsValBeneficiaryRow>();
            public IReadOnlyList<CrsDigitalIdListRow> DigitalIds { get; set; } = Array.Empty<CrsDigitalIdListRow>();
            public bool ThrowOnBulkRead { get; set; }

            public Task<IReadOnlyList<CrsValBeneficiaryRow>> GetAllValidatedBeneficiariesAsync(CancellationToken cancellationToken)
            {
                if (ThrowOnBulkRead) throw new InvalidOperationException("CRS unreachable");
                return Task.FromResult(Beneficiaries);
            }

            public Task<IReadOnlyList<CrsDigitalIdListRow>> GetAllLatestDigitalIdRowsAsync(CancellationToken cancellationToken)
            {
                if (ThrowOnBulkRead) throw new InvalidOperationException("CRS unreachable");
                return Task.FromResult(DigitalIds);
            }

            public Task<CrsDigitalIdRow?> GetLatestDigitalIdRowAsync(string beneficiaryId, CancellationToken cancellationToken)
                => Task.FromResult<CrsDigitalIdRow?>(null);
            public Task<long?> GetDemographicCharacteristicIdAsync(string beneficiaryId, CancellationToken cancellationToken)
                => Task.FromResult<long?>(null);
            public Task<DateTime?> GetPhotoUpdatedAtAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
                => Task.FromResult<DateTime?>(null);
            public Task<CrsPhotoRow?> GetPhotoAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
                => Task.FromResult<CrsPhotoRow?>(null);
            public Task InsertAccessLogAsync(CrsAccessLogEntry entry, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public Task<CrsSchemaProbeResult> ProbeSchemaAsync(CancellationToken cancellationToken)
                => Task.FromResult(new CrsSchemaProbeResult(true, null));
        }

        private static CrsValBeneficiaryRow MakeRow(
            long residentsId,
            string beneficiaryId,
            string firstName,
            string lastName,
            string dateOfBirth = "1990-01-01")
        {
            return new CrsValBeneficiaryRow(
                residentsId, beneficiaryId, $"CR-{residentsId}", lastName, firstName, null,
                $"{firstName} {lastName}", "Male", dateOfBirth, "35", "Single", "Purok 1",
                false, null, null, null, false, null);
        }

        [Fact]
        public async Task Mirror_AddsMissingCrsRows_AsApproved()
        {
            var dbName = Guid.NewGuid().ToString("N");
            var gateway = new FakeBulkCrsGateway
            {
                Beneficiaries = new[]
                {
                    MakeRow(1001, "BEN-2026-0001", "Juan", "Reyes"),
                    MakeRow(1002, "BEN-2026-0002", "Maria", "Santos")
                }
            };
            var service = new CrsMasterlistMirrorService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            var result = await service.MirrorValidatedBeneficiariesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.AddedCount);

            using var context = TestDbContextFactory.CreateContext(dbName);
            var rows = await context.BeneficiaryStaging.ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(VerificationStatus.Approved, row.VerificationStatus));
            Assert.Contains(rows, row => row.BeneficiaryId == "BEN-2026-0001" && row.ResidentsId == 1001);
        }

        [Fact]
        public async Task Mirror_SkipsRowsAlreadyPresent_AndNeverModifiesThem()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using (var seedContext = TestDbContextFactory.CreateContext(dbName))
            {
                seedContext.BeneficiaryStaging.Add(new BeneficiaryStaging
                {
                    ResidentsId = 1001,
                    BeneficiaryId = "BEN-2026-0001",
                    FirstName = "Juan",
                    LastName = "Reyes",
                    FullName = "Juan Reyes",
                    DateOfBirth = "1990-01-01",
                    VerificationStatus = VerificationStatus.Approved,
                    ReviewNotes = "seeded locally"
                });
                await seedContext.SaveChangesAsync();
            }

            var gateway = new FakeBulkCrsGateway
            {
                Beneficiaries = new[]
                {
                    MakeRow(1001, "BEN-2026-0001", "Juan", "Reyes"),
                    MakeRow(1003, "BEN-2026-0003", "Pedro", "Cruz")
                }
            };
            var service = new CrsMasterlistMirrorService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            var result = await service.MirrorValidatedBeneficiariesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(1, result.SkippedCount);

            using var context = TestDbContextFactory.CreateContext(dbName);
            var existing = await context.BeneficiaryStaging.FirstAsync(r => r.ResidentsId == 1001);
            Assert.Equal("seeded locally", existing.ReviewNotes);
            Assert.Equal(2, await context.BeneficiaryStaging.CountAsync());
        }

        [Fact]
        public async Task Mirror_SkipsSeededProfiles_ByNameFingerprint_EvenWithoutCrsIds()
        {
            // Seeded test profiles (docs/mock-beneficiaries-added.txt) have no CRS ids;
            // a CRS row for the same person+DOB must not duplicate them.
            var dbName = Guid.NewGuid().ToString("N");
            using (var seedContext = TestDbContextFactory.CreateContext(dbName))
            {
                seedContext.BeneficiaryStaging.Add(new BeneficiaryStaging
                {
                    FirstName = "Aaron Luis",
                    LastName = "Mockson",
                    FullName = "Aaron Luis Mockson",
                    DateOfBirth = "1985-05-05",
                    VerificationStatus = VerificationStatus.Approved
                });
                await seedContext.SaveChangesAsync();
            }

            var gateway = new FakeBulkCrsGateway
            {
                Beneficiaries = new[] { MakeRow(2001, "BEN-2026-9001", "Aaron Luis", "Mockson", "1985-05-05") }
            };
            var service = new CrsMasterlistMirrorService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            var result = await service.MirrorValidatedBeneficiariesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(1, result.SkippedCount);
        }

        [Fact]
        public async Task Mirror_WhenCrsUnreachable_FailsSoft_WithoutTouchingLocalData()
        {
            var dbName = Guid.NewGuid().ToString("N");
            using (var seedContext = TestDbContextFactory.CreateContext(dbName))
            {
                seedContext.BeneficiaryStaging.Add(new BeneficiaryStaging
                {
                    FirstName = "Juan",
                    LastName = "Reyes",
                    VerificationStatus = VerificationStatus.Approved
                });
                await seedContext.SaveChangesAsync();
            }

            var gateway = new FakeBulkCrsGateway { ThrowOnBulkRead = true };
            var service = new CrsMasterlistMirrorService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            var result = await service.MirrorValidatedBeneficiariesAsync();

            Assert.False(result.IsSuccess);
            Assert.Contains("local masterlist", result.Message, StringComparison.OrdinalIgnoreCase);

            using var context = TestDbContextFactory.CreateContext(dbName);
            Assert.Equal(1, await context.BeneficiaryStaging.CountAsync());
        }

        [Fact]
        public async Task Mirror_RecordsSyncMetadataTimestamp()
        {
            var dbName = Guid.NewGuid().ToString("N");
            var gateway = new FakeBulkCrsGateway
            {
                Beneficiaries = new[] { MakeRow(1001, "BEN-2026-0001", "Juan", "Reyes") }
            };
            var service = new CrsMasterlistMirrorService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            await service.MirrorValidatedBeneficiariesAsync();

            using var context = TestDbContextFactory.CreateContext(dbName);
            var metadata = await context.SyncMetadata
                .FirstOrDefaultAsync(m => m.TableName == CrsMasterlistMirrorService.SyncMetadataKey);
            Assert.NotNull(metadata);
            Assert.True(metadata!.LastSyncAt > DateTime.MinValue);
        }
    }

    public class CrsDigitalIdCacheSyncServiceTests
    {
        private class FakeBulkCrsGateway : ICrsGateway
        {
            public IReadOnlyList<CrsDigitalIdListRow> DigitalIds { get; set; } = Array.Empty<CrsDigitalIdListRow>();
            public bool ThrowOnBulkRead { get; set; }

            public Task<IReadOnlyList<CrsDigitalIdListRow>> GetAllLatestDigitalIdRowsAsync(CancellationToken cancellationToken)
            {
                if (ThrowOnBulkRead) throw new InvalidOperationException("CRS unreachable");
                return Task.FromResult(DigitalIds);
            }

            public Task<IReadOnlyList<CrsValBeneficiaryRow>> GetAllValidatedBeneficiariesAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<CrsValBeneficiaryRow>>(Array.Empty<CrsValBeneficiaryRow>());
            public Task<CrsDigitalIdRow?> GetLatestDigitalIdRowAsync(string beneficiaryId, CancellationToken cancellationToken)
                => Task.FromResult<CrsDigitalIdRow?>(null);
            public Task<long?> GetDemographicCharacteristicIdAsync(string beneficiaryId, CancellationToken cancellationToken)
                => Task.FromResult<long?>(null);
            public Task<DateTime?> GetPhotoUpdatedAtAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
                => Task.FromResult<DateTime?>(null);
            public Task<CrsPhotoRow?> GetPhotoAsync(long demographicCharacteristicId, CancellationToken cancellationToken)
                => Task.FromResult<CrsPhotoRow?>(null);
            public Task InsertAccessLogAsync(CrsAccessLogEntry entry, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public Task<CrsSchemaProbeResult> ProbeSchemaAsync(CancellationToken cancellationToken)
                => Task.FromResult(new CrsSchemaProbeResult(true, null));
        }

        [Fact]
        public async Task Sync_UpsertsEncryptedStatusRows_ForAllCardholders()
        {
            var dbName = Guid.NewGuid().ToString("N");
            var gateway = new FakeBulkCrsGateway
            {
                DigitalIds = new[]
                {
                    new CrsDigitalIdListRow("BEN-2026-0001", "EK-001", "Active", DateTime.Today.AddYears(-1), DateTime.Today.AddYears(1), null, null),
                    new CrsDigitalIdListRow("BEN-2026-0002", "EK-002", "Revoked", DateTime.Today.AddYears(-2), null, DateTime.Today.AddDays(-3), "Lost card")
                }
            };
            var service = new CrsDigitalIdCacheSyncService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            var result = await service.SyncAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.UpsertedCount);

            using var context = TestDbContextFactory.CreateContext(dbName);
            var cached = await context.CrsStatusCaches.ToListAsync();
            Assert.Equal(2, cached.Count);
            var first = cached.First(c => c.BeneficiaryId == "BEN-2026-0001");
            Assert.Equal("EK-001", ConnectionSecretProtector.Unprotect(first.EncryptedIdNumber));
            Assert.Equal("Active", ConnectionSecretProtector.Unprotect(first.EncryptedStatus));
        }

        [Fact]
        public async Task Sync_UpdatesExistingCacheRow_InsteadOfDuplicating()
        {
            var dbName = Guid.NewGuid().ToString("N");
            var gateway = new FakeBulkCrsGateway
            {
                DigitalIds = new[]
                {
                    new CrsDigitalIdListRow("BEN-2026-0001", "EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null)
                }
            };
            var service = new CrsDigitalIdCacheSyncService(gateway, () => TestDbContextFactory.CreateContext(dbName));
            await service.SyncAsync();

            gateway.DigitalIds = new[]
            {
                new CrsDigitalIdListRow("BEN-2026-0001", "EK-001", "Revoked", DateTime.Today.AddYears(-1), null, DateTime.Today, "Reported stolen")
            };
            await service.SyncAsync();

            using var context = TestDbContextFactory.CreateContext(dbName);
            var cached = await context.CrsStatusCaches.ToListAsync();
            Assert.Single(cached);
            Assert.Equal("Revoked", ConnectionSecretProtector.Unprotect(cached[0].EncryptedStatus));
        }

        [Fact]
        public async Task Sync_WhenCrsUnreachable_FailsSoft_AndKeepsExistingCache()
        {
            var dbName = Guid.NewGuid().ToString("N");
            var gateway = new FakeBulkCrsGateway
            {
                DigitalIds = new[]
                {
                    new CrsDigitalIdListRow("BEN-2026-0001", "EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null)
                }
            };
            var service = new CrsDigitalIdCacheSyncService(gateway, () => TestDbContextFactory.CreateContext(dbName));
            await service.SyncAsync();

            gateway.ThrowOnBulkRead = true;
            var result = await service.SyncAsync();

            Assert.False(result.IsSuccess);

            using var context = TestDbContextFactory.CreateContext(dbName);
            Assert.Equal(1, await context.CrsStatusCaches.CountAsync());
        }

        [Fact]
        public async Task Sync_RecordsSyncMetadataTimestamp()
        {
            var dbName = Guid.NewGuid().ToString("N");
            var gateway = new FakeBulkCrsGateway
            {
                DigitalIds = new[]
                {
                    new CrsDigitalIdListRow("BEN-2026-0001", "EK-001", "Active", DateTime.Today.AddYears(-1), null, null, null)
                }
            };
            var service = new CrsDigitalIdCacheSyncService(gateway, () => TestDbContextFactory.CreateContext(dbName));

            await service.SyncAsync();

            using var context = TestDbContextFactory.CreateContext(dbName);
            var metadata = await context.SyncMetadata
                .FirstOrDefaultAsync(m => m.TableName == CrsDigitalIdCacheSyncService.SyncMetadataKey);
            Assert.NotNull(metadata);
        }
    }

    /// <summary>
    /// Text-scan tests asserting the bulk CRS reads stay read-only and follow the
    /// contract's most-recent-row rule, and that the mirror is wired into the
    /// enrollment and masterlist flows.
    /// </summary>
    public sealed class CrsBulkSyncSourceTests
    {
        private static string ReadSource(params string[] relativeParts)
        {
            var parts = new List<string> { AppContext.BaseDirectory, "..", "..", "..", ".." };
            parts.AddRange(relativeParts);
            return File.ReadAllText(Path.GetFullPath(Path.Combine(parts.ToArray())));
        }

        [Fact]
        public void CrsGateway_BulkReads_AreReadOnly_AndNeverFilterByActiveStatus()
        {
            var source = ReadSource("Services", "CRS", "CrsGateway.cs");

            Assert.Contains("FROM val_beneficiaries", source, StringComparison.Ordinal);
            Assert.Contains("MAX(issued_date)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("INSERT INTO val_beneficiaries", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE val_beneficiaries", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE FROM val_beneficiaries", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("status = 'Active'", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MirrorService_AutoApproves_AndNeverDeletes()
        {
            var source = ReadSource("Services", "CRS", "CrsMasterlistMirrorService.cs");

            Assert.Contains("VerificationStatus.Approved", source, StringComparison.Ordinal);
            Assert.Contains("BeneficiaryImportDeduplication.Evaluate", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".Remove(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("RemoveRange", source, StringComparison.Ordinal);
        }

        [Fact]
        public void EnrollmentPanel_MirrorsCrsBeforeLoadingUnifiedList()
        {
            var source = ReadSource("ViewModels", "BudgetViewModel.cs");

            Assert.Contains("CrsMasterlistMirrorService", source, StringComparison.Ordinal);
            Assert.Contains("MirrorValidatedBeneficiariesAsync", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MasterListRefresh_MirrorsCrsBeforeReloadingPages()
        {
            var source = ReadSource("ViewModels", "MasterListViewModel.cs");

            Assert.Contains("CrsMasterlistMirrorService", source, StringComparison.Ordinal);
        }

        [Fact]
        public void BackgroundMaintenance_RunsBothCrsSyncsOnStartup()
        {
            var source = ReadSource("Services", "DigitalId", "BackgroundMaintenanceService.cs");

            Assert.Contains("CrsMasterlistMirrorService", source, StringComparison.Ordinal);
            Assert.Contains("CrsDigitalIdCacheSyncService", source, StringComparison.Ordinal);
        }
    }
}

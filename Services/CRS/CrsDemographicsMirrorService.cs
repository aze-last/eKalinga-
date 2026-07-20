using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public sealed class CrsDemographicsMirrorResult
    {
        public bool IsSuccess { get; init; }
        public int AddedCount { get; init; }
        public int UpdatedCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Mirrors CRS demographic_characteristics (marital status, ethnicity, tribe)
    /// into the local crs_demographics_cache, keyed by the CRS row id. CRS stays
    /// strictly READ only. Unlike the masterlist mirror, existing cache rows ARE
    /// updated when the CRS updated_at moves — the cache is a snapshot, not
    /// operator-owned data. Rows are never deleted locally.
    /// </summary>
    public class CrsDemographicsMirrorService
    {
        public const string SyncMetadataKey = "CrsDemographicsMirror";

        private readonly ICrsGateway _gateway;
        private readonly Func<LocalDbContext> _localDbFactory;

        public CrsDemographicsMirrorService(
            ICrsGateway? gateway = null,
            Func<LocalDbContext>? localDbFactory = null)
        {
            _gateway = gateway ?? new CrsGateway();
            _localDbFactory = localDbFactory ?? (() => new LocalDbContext());
        }

        public async Task<CrsDemographicsMirrorResult> MirrorDemographicsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CrsDemographicRow> sourceRows;
            try
            {
                sourceRows = await _gateway.GetAllDemographicCharacteristicsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Offline / CRS unreachable — the local cache keeps serving.
                return new CrsDemographicsMirrorResult
                {
                    IsSuccess = false,
                    Message = $"CRS unreachable — using local demographics cache. ({ex.Message})"
                };
            }

            await using var context = _localDbFactory();

            var existingRows = await context.CrsDemographicsCaches
                .ToDictionaryAsync(row => row.DemographicCharacteristicId, cancellationToken);

            var addedCount = 0;
            var updatedCount = 0;

            foreach (var row in sourceRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceUpdatedAt = row.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss");

                if (existingRows.TryGetValue(row.DemographicCharacteristicId, out var cached))
                {
                    // Skip untouched rows so a no-change sync writes nothing.
                    if (cached.SourceUpdatedAt == sourceUpdatedAt &&
                        string.Equals(cached.BeneficiaryId, row.BeneficiaryId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    cached.BeneficiaryId = row.BeneficiaryId;
                    cached.MaritalStatus = row.MaritalStatus;
                    cached.Ethnicity = row.Ethnicity;
                    cached.Tribe = row.Tribe;
                    cached.SourceUpdatedAt = sourceUpdatedAt;
                    cached.SyncedAt = DateTime.Now;
                    updatedCount++;
                    continue;
                }

                context.CrsDemographicsCaches.Add(new CrsDemographicsCache
                {
                    DemographicCharacteristicId = row.DemographicCharacteristicId,
                    BeneficiaryId = row.BeneficiaryId,
                    MaritalStatus = row.MaritalStatus,
                    Ethnicity = row.Ethnicity,
                    Tribe = row.Tribe,
                    SourceUpdatedAt = sourceUpdatedAt,
                    SyncedAt = DateTime.Now
                });
                addedCount++;
            }

            if (addedCount > 0 || updatedCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            await TouchSyncMetadataAsync(context, cancellationToken);

            return new CrsDemographicsMirrorResult
            {
                IsSuccess = true,
                AddedCount = addedCount,
                UpdatedCount = updatedCount,
                Message = addedCount > 0 || updatedCount > 0
                    ? $"Demographics cache refreshed — {addedCount} added, {updatedCount} updated."
                    : "Demographics cache is up to date."
            };
        }

        private static async Task TouchSyncMetadataAsync(LocalDbContext context, CancellationToken cancellationToken)
        {
            try
            {
                var metadata = await context.SyncMetadata
                    .FirstOrDefaultAsync(m => m.TableName == SyncMetadataKey, cancellationToken);
                if (metadata == null)
                {
                    context.SyncMetadata.Add(new SyncMetadata { TableName = SyncMetadataKey, LastSyncAt = DateTime.Now });
                }
                else
                {
                    metadata.LastSyncAt = DateTime.Now;
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Timestamp bookkeeping must never fail the mirror.
            }
        }
    }
}

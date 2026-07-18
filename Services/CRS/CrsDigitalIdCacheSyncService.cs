using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public sealed class CrsDigitalIdCacheSyncResult
    {
        public bool IsSuccess { get; init; }
        public int UpsertedCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Bulk-pulls the most-recent digital_ids row per beneficiary from CRS into the
    /// existing crs_status_cache (DPAPI-encrypted) so e-Kard verification keeps
    /// working offline for every cardholder, not just previously scanned ones.
    /// CRS stays strictly READ only. Photo blobs are NOT bulk-pulled — they remain
    /// per-scan cached by CrsDigitalIdVerificationService (freshness-probed).
    /// </summary>
    public class CrsDigitalIdCacheSyncService
    {
        public const string SyncMetadataKey = "CrsDigitalIdCache";
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly ICrsGateway _gateway;
        private readonly Func<LocalDbContext> _localDbFactory;

        public CrsDigitalIdCacheSyncService(
            ICrsGateway? gateway = null,
            Func<LocalDbContext>? localDbFactory = null)
        {
            _gateway = gateway ?? new CrsGateway();
            _localDbFactory = localDbFactory ?? (() => new LocalDbContext());
        }

        public async Task<CrsDigitalIdCacheSyncResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CrsDigitalIdListRow> sourceRows;
            try
            {
                sourceRows = await _gateway.GetAllLatestDigitalIdRowsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Offline — existing cache keeps serving verifications.
                return new CrsDigitalIdCacheSyncResult
                {
                    IsSuccess = false,
                    Message = $"CRS unreachable — digital ID cache unchanged. ({ex.Message})"
                };
            }

            await using var context = _localDbFactory();

            var cachedById = await context.CrsStatusCaches
                .ToDictionaryAsync(c => c.BeneficiaryId, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var upserted = 0;
            foreach (var row in sourceRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!cachedById.TryGetValue(row.BeneficiaryId, out var cached))
                {
                    cached = new CrsStatusCache { BeneficiaryId = row.BeneficiaryId };
                    context.CrsStatusCaches.Add(cached);
                    cachedById[row.BeneficiaryId] = cached;
                }

                cached.EncryptedIdNumber = ConnectionSecretProtector.Protect(row.IdNumber);
                cached.EncryptedStatus = ConnectionSecretProtector.Protect(row.Status);
                cached.EncryptedIssuedDate = ConnectionSecretProtector.Protect(FormatDate(row.IssuedDate));
                cached.EncryptedExpiryDate = ConnectionSecretProtector.Protect(FormatDate(row.ExpiryDate));
                cached.EncryptedRevokedAt = ConnectionSecretProtector.Protect(FormatDate(row.RevokedAt));
                cached.EncryptedRevocationReason = ConnectionSecretProtector.Protect(row.RevocationReason ?? string.Empty);
                cached.SyncedAt = DateTime.Now;
                upserted++;
            }

            if (upserted > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            await TouchSyncMetadataAsync(context, cancellationToken);

            return new CrsDigitalIdCacheSyncResult
            {
                IsSuccess = true,
                UpsertedCount = upserted,
                Message = $"Digital ID cache refreshed — {upserted} card(s) cached."
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
                // Timestamp bookkeeping must never fail the sync.
            }
        }

        private static string FormatDate(DateTime? value)
        {
            return value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}

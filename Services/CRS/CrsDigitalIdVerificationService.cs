using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Implements the e-Kard Digital ID Verification Contract against the CRS
    /// database via <see cref="ICrsGateway"/>:
    /// - most-recent-row status (never WHERE status='Active'),
    /// - validity = Active AND unexpired (this DB never auto-expires cards),
    /// - two-hop photo with cache invalidated by demographic_characteristics.updated_at,
    /// - append-only record_access_logs audit that never blocks the verification
    ///   (failures queue into crs_pending_access_logs for retry).
    /// </summary>
    public class CrsDigitalIdVerificationService : ICrsDigitalIdVerificationService
    {
        public const string SystemName = "eKalinga+";
        public const string VerificationRecordType = "DIGITAL_ID_VERIFICATION";
        private const string ActiveStatus = "Active";
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> StampedeLocks = new();

        private readonly LocalDbContext _localDb;
        private readonly ICrsGateway _gateway;
        private readonly ICrsResiliencyPolicy _resiliencyPolicy;
        private readonly Func<LocalDbContext> _localDbFactory;

        /// <summary>
        /// The most recent fire-and-forget audit write. Exposed so tests (and the
        /// background maintenance flush) can await audit completion deterministically.
        /// </summary>
        public Task? LastAuditTask { get; private set; }

        public CrsDigitalIdVerificationService(
            LocalDbContext localDb,
            ICrsGateway? gateway = null,
            ICrsResiliencyPolicy? resiliencyPolicy = null,
            Func<LocalDbContext>? localDbFactory = null)
        {
            _localDb = localDb;
            _gateway = gateway ?? new CrsGateway();
            _resiliencyPolicy = resiliencyPolicy ?? new CrsResiliencyPolicy();
            _localDbFactory = localDbFactory ?? (() => new LocalDbContext());
        }

        public async Task<EKardVerificationResult> VerifyAsync(EKardVerificationRequest request, CancellationToken cancellationToken)
        {
            var beneficiaryId = EKardPayloadRouter.ExtractBeneficiaryId(request.BeneficiaryId);
            if (string.IsNullOrWhiteSpace(beneficiaryId))
            {
                return BuildResult(EKardValidity.Unknown, beneficiaryId, null, "InvalidPayload", null, null, null, null, null, false, EKardSource.LocalCache, null);
            }

            var semaphore = StampedeLocks.GetOrAdd(beneficiaryId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                EKardVerificationResult result;
                try
                {
                    result = await VerifyLiveAsync(beneficiaryId, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Offline / CRS unreachable — serve the pull-sync cache.
                    result = await VerifyFromCacheAsync(beneficiaryId, cancellationToken);
                }

                // Contract Part 3: record the check. Never block the verification on
                // the audit write — fire and forget; the audit path opens its own
                // connections/contexts and failures queue into crs_pending_access_logs.
                LastAuditTask = Task.Run(() => TryWriteAuditAsync(request, beneficiaryId, result.Status));

                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<CrsPhotoResult> GetPhotoAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            beneficiaryId = EKardPayloadRouter.ExtractBeneficiaryId(beneficiaryId);
            if (string.IsNullOrWhiteSpace(beneficiaryId))
            {
                return new CrsPhotoResult(null, false, false);
            }

            try
            {
                return await GetPhotoLiveAsync(beneficiaryId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Offline — serve whatever is cached, stale or not (contract Part 2 step 4).
                var cached = await _localDb.CrsPhotoCaches
                    .FirstOrDefaultAsync(c => c.BeneficiaryId == beneficiaryId, cancellationToken);
                if (cached == null)
                {
                    return new CrsPhotoResult(null, false, false);
                }

                return new CrsPhotoResult(DecryptPhoto(cached), cached.PhotoConfirmedAbsent, true);
            }
        }

        private async Task<EKardVerificationResult> VerifyLiveAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            var row = await _resiliencyPolicy.ExecuteAsync(
                token => _gateway.GetLatestDigitalIdRowAsync(beneficiaryId, token),
                cancellationToken);

            if (row == null)
            {
                // Never issued an e-Kard ID.
                return BuildResult(EKardValidity.NotFound, beneficiaryId, null, "NotFound", null, null, null, null, null, false, EKardSource.LiveCrs, DateTime.Now);
            }

            var validity = ResolveValidity(row);
            var photo = await GetPhotoLiveSafeAsync(beneficiaryId, cancellationToken);

            await UpsertStatusCacheAsync(beneficiaryId, row, cancellationToken);

            return BuildResult(
                validity,
                beneficiaryId,
                row.IdNumber,
                row.Status,
                row.IssuedDate,
                row.ExpiryDate,
                row.RevokedAt,
                row.RevocationReason,
                photo.PhotoBytes,
                photo.ConfirmedAbsent,
                EKardSource.LiveCrs,
                DateTime.Now);
        }

        private async Task<EKardVerificationResult> VerifyFromCacheAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            var cached = await _localDb.CrsStatusCaches
                .FirstOrDefaultAsync(c => c.BeneficiaryId == beneficiaryId, cancellationToken);

            if (cached == null)
            {
                return BuildResult(EKardValidity.Unknown, beneficiaryId, null, "OfflineUnavailable", null, null, null, null, null, false, EKardSource.LocalCache, null);
            }

            var row = new CrsDigitalIdRow(
                ConnectionSecretProtector.Unprotect(cached.EncryptedIdNumber),
                ConnectionSecretProtector.Unprotect(cached.EncryptedStatus),
                ParseDate(ConnectionSecretProtector.Unprotect(cached.EncryptedIssuedDate)),
                ParseDate(ConnectionSecretProtector.Unprotect(cached.EncryptedExpiryDate)),
                ParseDate(ConnectionSecretProtector.Unprotect(cached.EncryptedRevokedAt)),
                EmptyToNull(ConnectionSecretProtector.Unprotect(cached.EncryptedRevocationReason)));

            var photoCache = await _localDb.CrsPhotoCaches
                .FirstOrDefaultAsync(c => c.BeneficiaryId == beneficiaryId, cancellationToken);

            return BuildResult(
                ResolveValidity(row),
                beneficiaryId,
                row.IdNumber,
                row.Status,
                row.IssuedDate,
                row.ExpiryDate,
                row.RevokedAt,
                row.RevocationReason,
                photoCache == null ? null : DecryptPhoto(photoCache),
                photoCache?.PhotoConfirmedAbsent ?? false,
                EKardSource.LocalCache,
                cached.SyncedAt);
        }

        /// <summary>
        /// Contract validity rule: status='Active' AND (expiry_date IS NULL OR
        /// expiry_date >= today). e-Kard never auto-transitions status when
        /// expiry_date passes, so checking status alone would accept expired cards.
        /// Date-only comparison — an expiry of today is still valid.
        /// </summary>
        private static EKardValidity ResolveValidity(CrsDigitalIdRow row)
        {
            if (!string.Equals(row.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase))
            {
                return EKardValidity.Revoked;
            }

            if (row.ExpiryDate.HasValue && row.ExpiryDate.Value.Date < DateTime.Today)
            {
                return EKardValidity.Expired;
            }

            return EKardValidity.Valid;
        }

        private async Task<CrsPhotoResult> GetPhotoLiveSafeAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            try
            {
                return await GetPhotoLiveAsync(beneficiaryId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A photo failure must not fail the status verification.
                var cached = await _localDb.CrsPhotoCaches
                    .FirstOrDefaultAsync(c => c.BeneficiaryId == beneficiaryId, cancellationToken);
                return cached == null
                    ? new CrsPhotoResult(null, false, false)
                    : new CrsPhotoResult(DecryptPhoto(cached), cached.PhotoConfirmedAbsent, true);
            }
        }

        private async Task<CrsPhotoResult> GetPhotoLiveAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            var dcId = await _resiliencyPolicy.ExecuteAsync(
                token => _gateway.GetDemographicCharacteristicIdAsync(beneficiaryId, token),
                cancellationToken);

            if (dcId == null)
            {
                return new CrsPhotoResult(null, false, false);
            }

            var cached = await _localDb.CrsPhotoCaches
                .FirstOrDefaultAsync(c => c.DemographicCharacteristicId == dcId.Value, cancellationToken);

            // Cheap freshness probe first — no blob transferred (contract Part 2).
            var remoteUpdatedAt = await _resiliencyPolicy.ExecuteAsync(
                token => _gateway.GetPhotoUpdatedAtAsync(dcId.Value, token),
                cancellationToken);
            var remoteStamp = FormatDate(remoteUpdatedAt);

            if (cached != null && remoteStamp != null && remoteStamp == cached.SourceUpdatedAt)
            {
                return new CrsPhotoResult(DecryptPhoto(cached), cached.PhotoConfirmedAbsent, true);
            }

            // Stale or never cached — pull the real blob and refresh the cache.
            var photoRow = await _resiliencyPolicy.ExecuteAsync(
                token => _gateway.GetPhotoAsync(dcId.Value, token),
                cancellationToken);

            var bytes = photoRow?.ProfilePicture;
            var confirmedAbsent = photoRow != null && bytes == null;

            if (cached == null)
            {
                cached = new CrsPhotoCache { DemographicCharacteristicId = dcId.Value };
                _localDb.CrsPhotoCaches.Add(cached);
            }

            cached.BeneficiaryId = beneficiaryId;
            cached.EncryptedPhotoBytes = bytes == null
                ? string.Empty
                : ConnectionSecretProtector.Protect(Convert.ToBase64String(bytes));
            cached.PhotoConfirmedAbsent = confirmedAbsent;
            cached.SourceUpdatedAt = FormatDate(photoRow?.UpdatedAt) ?? remoteStamp;
            cached.SyncedAt = DateTime.Now;
            await _localDb.SaveChangesAsync(cancellationToken);

            return new CrsPhotoResult(bytes, confirmedAbsent, false);
        }

        private async Task UpsertStatusCacheAsync(string beneficiaryId, CrsDigitalIdRow row, CancellationToken cancellationToken)
        {
            var cached = await _localDb.CrsStatusCaches
                .FirstOrDefaultAsync(c => c.BeneficiaryId == beneficiaryId, cancellationToken);
            if (cached == null)
            {
                cached = new CrsStatusCache { BeneficiaryId = beneficiaryId };
                _localDb.CrsStatusCaches.Add(cached);
            }

            cached.EncryptedIdNumber = ConnectionSecretProtector.Protect(row.IdNumber);
            cached.EncryptedStatus = ConnectionSecretProtector.Protect(row.Status);
            cached.EncryptedIssuedDate = ConnectionSecretProtector.Protect(FormatDate(row.IssuedDate) ?? string.Empty);
            cached.EncryptedExpiryDate = ConnectionSecretProtector.Protect(FormatDate(row.ExpiryDate) ?? string.Empty);
            cached.EncryptedRevokedAt = ConnectionSecretProtector.Protect(FormatDate(row.RevokedAt) ?? string.Empty);
            cached.EncryptedRevocationReason = ConnectionSecretProtector.Protect(row.RevocationReason ?? string.Empty);
            cached.SyncedAt = DateTime.Now;

            await _localDb.SaveChangesAsync(cancellationToken);
        }

        private async Task TryWriteAuditAsync(EKardVerificationRequest request, string beneficiaryId, string status)
        {
            var entry = new CrsAccessLogEntry(
                request.UserId,
                string.IsNullOrWhiteSpace(request.UserName) ? SystemName : request.UserName,
                VerificationRecordType,
                beneficiaryId,
                // record_access_logs.action_taken is varchar(50) on live CRS — the
                // contract's long sentence loses the status to truncation, so keep
                // the action compact enough that the status always survives.
                $"VERIFY — e-Kard check by {SystemName}: {status}",
                DateTime.Now,
                Guid.NewGuid().ToString());

            try
            {
                await _gateway.InsertAccessLogAsync(entry, CancellationToken.None);
            }
            catch
            {
                await TryEnqueuePendingAuditAsync(entry);
            }

            try
            {
                // Fresh context — this runs on a background task and must not share
                // the caller's LocalDbContext across threads.
                await using var auditDb = _localDbFactory();
                await new AuditService(auditDb).LogActivityAsync(
                    request.UserId,
                    "VERIFY_EKARD",
                    "digital_ids",
                    null,
                    $"e-Kard {beneficiaryId} verified against CRS. Status: {status}.");
            }
            catch
            {
                // Local audit must never surface into the verification flow either.
            }
        }

        private async Task TryEnqueuePendingAuditAsync(CrsAccessLogEntry entry)
        {
            try
            {
                await using var queueDb = _localDbFactory();
                queueDb.CrsPendingAccessLogs.Add(new CrsPendingAccessLog
                {
                    PayloadJson = JsonSerializer.Serialize(entry),
                    CreatedAt = DateTime.Now
                });
                await queueDb.SaveChangesAsync();
            }
            catch
            {
                // Queueing is best-effort; verification already completed.
            }
        }

        private static EKardVerificationResult BuildResult(
            EKardValidity validity, string beneficiaryId, string? idNumber, string status,
            DateTime? issued, DateTime? expiry, DateTime? revokedAt, string? revocationReason,
            byte[]? photo, bool photoConfirmedAbsent, EKardSource source, DateTime? lastSyncedAt)
        {
            return new EKardVerificationResult(
                validity, beneficiaryId, idNumber, status, issued, expiry, revokedAt,
                revocationReason, photo, photoConfirmedAbsent, source, lastSyncedAt);
        }

        private static byte[]? DecryptPhoto(CrsPhotoCache cache)
        {
            if (string.IsNullOrEmpty(cache.EncryptedPhotoBytes))
            {
                return null;
            }

            var base64 = ConnectionSecretProtector.Unprotect(cache.EncryptedPhotoBytes);
            if (string.IsNullOrEmpty(base64))
            {
                return null;
            }

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch
            {
                return null;
            }
        }

        private static string? FormatDate(DateTime? value)
        {
            return value?.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        private static string? EmptyToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

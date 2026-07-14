using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendanceShiftingManagement.Services
{
    public class EKardVerificationService : IEKardVerificationService
    {
        private readonly LocalDbContext _localDb;
        private readonly ICrsConnectionProvider _connectionProvider;
        private readonly ICrsDbContextFactory _crsDbFactory;
        private readonly ICrsResiliencyPolicy _resiliencyPolicy;
        private readonly ICrsHealthService _healthService;
        private readonly ILogger<EKardVerificationService>? _logger;
        private readonly DigitalIdCacheOptions _cacheOptions;
        private readonly AuditService _auditService;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> StampedeLocks = new();

        public EKardVerificationService(
            LocalDbContext localDb,
            ICrsConnectionProvider? connectionProvider = null,
            ICrsDbContextFactory? crsDbFactory = null,
            ICrsResiliencyPolicy? resiliencyPolicy = null,
            ICrsHealthService? healthService = null,
            ILogger<EKardVerificationService>? logger = null,
            IOptions<DigitalIdCacheOptions>? cacheOptions = null,
            AuditService? auditService = null)
        {
            _localDb = localDb;
            _connectionProvider = connectionProvider ?? new CrsConnectionProvider();
            _crsDbFactory = crsDbFactory ?? new CrsDbContextFactory(_connectionProvider);
            _resiliencyPolicy = resiliencyPolicy ?? new CrsResiliencyPolicy();
            _healthService = healthService ?? new CrsHealthService(_connectionProvider);
            _logger = logger;
            _cacheOptions = cacheOptions?.Value ?? new DigitalIdCacheOptions();
            _auditService = auditService ?? new AuditService(localDb);
        }

        public async Task<VerificationResult> VerifyDigitalIdAsync(DigitalIdVerificationRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.QrPayload))
            {
                return new VerificationResult(false, "InvalidPayload", null, null, VerificationSource.OfflineCache, string.Empty);
            }

            var payloadHash = HashString(request.QrPayload);
            _logger?.LogInformation("Starting digital ID verification for payload hash {PayloadHash}.", payloadHash);

            var semaphore = StampedeLocks.GetOrAdd(payloadHash, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // 1. Try Read-Through Cache if not forcing refresh
                if (!request.ForceRefresh)
                {
                    var cachedStatus = await _localDb.DigitalIdStatusCaches
                        .FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash, cancellationToken);

                    if (cachedStatus != null)
                    {
                        var status = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedStatus);
                        var expiryStr = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedExpiryDate);
                        var cardNo = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedCardNumber);
                        var qr = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedQrPayload);
                        var beneficiaryId = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedBeneficiaryId);

                        DateTime? expiry = string.IsNullOrEmpty(expiryStr) ? null : DateTime.Parse(expiryStr);
                        
                        bool isStatusFresh = cachedStatus.UpdatedAt.AddDays(_cacheOptions.StatusRetentionDays) > DateTime.UtcNow;

                        if (isStatusFresh)
                        {
                            _logger?.LogInformation("Found fresh cached status for payload hash {PayloadHash}.", payloadHash);
                            
                            byte[]? photoBytes = null;
                            var cachedPhoto = await _localDb.DigitalIdPhotoCaches
                                .FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash, cancellationToken);

                            if (cachedPhoto != null)
                            {
                                bool isPhotoFresh = cachedPhoto.UpdatedAt.AddDays(_cacheOptions.PhotoRetentionDays) > DateTime.UtcNow;
                                if (isPhotoFresh)
                                {
                                    var base64Photo = ConnectionSecretProtector.Unprotect(cachedPhoto.EncryptedPhotoBytes);
                                    if (!string.IsNullOrEmpty(base64Photo))
                                    {
                                        photoBytes = Convert.FromBase64String(base64Photo);
                                    }
                                }
                            }

                            return new VerificationResult(
                                status == "Active",
                                status,
                                expiry,
                                photoBytes,
                                VerificationSource.LocalCache,
                                cardNo
                            );
                        }
                    }
                }

                // 2. Perform Remote Fetch
                try
                {
                    await _healthService.CheckHealthAsync(cancellationToken);
                    if (!_healthService.Connected || !_healthService.IsCompatible)
                    {
                        throw new InvalidOperationException("Remote CRS database is unreachable or incompatible.");
                    }

                    _logger?.LogInformation("Executing remote database fetch for payload hash {PayloadHash}.", payloadHash);

                    var remoteResult = await _resiliencyPolicy.ExecuteAsync(async (token) =>
                    {
                        await using var remoteDb = _crsDbFactory.CreateDbContext();

                        var queryResult = await (
                            from bid in remoteDb.BeneficiaryDigitalIds
                            join bs in remoteDb.BeneficiaryStagings on bid.BeneficiaryStagingId equals bs.StagingId
                            join val in remoteDb.ValBeneficiaries on bs.BeneficiaryId equals val.BeneficiaryId
                            where bid.QrPayload == request.QrPayload
                            select new
                            {
                                DigitalId = bid,
                                Staging = bs,
                                Beneficiary = val
                            }
                        ).FirstOrDefaultAsync(token);

                        return queryResult;
                    }, cancellationToken);

                    if (remoteResult == null)
                    {
                        _logger?.LogWarning("Payload hash {PayloadHash} not found in remote database.", payloadHash);
                        return new VerificationResult(false, "NotFound", null, null, VerificationSource.RemoteCRS, string.Empty);
                    }

                    var statusVal = remoteResult.DigitalId.IsActive ? "Active" : "Revoked";
                    var exp = remoteResult.DigitalId.RevokedAt;
                    var card = remoteResult.DigitalId.CardNumber;
                    var plainBenId = remoteResult.Beneficiary.BeneficiaryId;

                    byte[]? photo = null;
                    if (!string.IsNullOrWhiteSpace(remoteResult.DigitalId.PhotoPath) && File.Exists(remoteResult.DigitalId.PhotoPath))
                    {
                        try
                        {
                            photo = await File.ReadAllBytesAsync(remoteResult.DigitalId.PhotoPath, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to read remote digital ID photo file path.");
                        }
                    }

                    // Update Local Cache
                    await UpdateLocalCacheAsync(payloadHash, plainBenId, statusVal, exp, card, request.QrPayload, photo, cancellationToken);

                    await _auditService.LogActivityAsync(
                        null,
                        "VERIFY_CRS_ID",
                        "val_beneficiaries",
                        null,
                        $"Verified digital ID {card} from remote CRS. Status: {statusVal}."
                    );

                    return new VerificationResult(
                        remoteResult.DigitalId.IsActive,
                        statusVal,
                        exp,
                        photo,
                        VerificationSource.RemoteCRS,
                        card,
                        remoteResult.Beneficiary
                    );
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Remote database verification failed for payload hash {PayloadHash}. Falling back to offline cache.", payloadHash);

                    // 3. Fallback to Offline Cache
                    var cachedStatus = await _localDb.DigitalIdStatusCaches
                        .FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash, cancellationToken);

                    if (cachedStatus != null)
                    {
                        bool isOfflineValid = cachedStatus.UpdatedAt.AddHours(_cacheOptions.OfflineValidityHours) > DateTime.UtcNow;
                        if (isOfflineValid)
                        {
                            var status = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedStatus);
                            var expiryStr = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedExpiryDate);
                            var cardNo = ConnectionSecretProtector.Unprotect(cachedStatus.EncryptedCardNumber);
                            DateTime? expiry = string.IsNullOrEmpty(expiryStr) ? null : DateTime.Parse(expiryStr);

                            byte[]? photoBytes = null;
                            var cachedPhoto = await _localDb.DigitalIdPhotoCaches
                                .FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash, cancellationToken);

                            if (cachedPhoto != null)
                            {
                                var base64Photo = ConnectionSecretProtector.Unprotect(cachedPhoto.EncryptedPhotoBytes);
                                if (!string.IsNullOrEmpty(base64Photo))
                                {
                                    photoBytes = Convert.FromBase64String(base64Photo);
                                }
                            }

                            _logger?.LogInformation("Offline cache fallback successful for payload hash {PayloadHash}.", payloadHash);
                            return new VerificationResult(
                                status == "Active",
                                status,
                                expiry,
                                photoBytes,
                                VerificationSource.OfflineCache,
                                cardNo
                            );
                        }
                    }

                    return new VerificationResult(
                        false,
                        "OfflineUnavailable",
                        null,
                        null,
                        VerificationSource.OfflineCache,
                        string.Empty
                    );
                }
            }
            finally
            {
                semaphore.Release();
                StampedeLocks.TryRemove(payloadHash, out _);
            }
        }

        private async Task UpdateLocalCacheAsync(
            string payloadHash,
            string beneficiaryId,
            string status,
            DateTime? expiry,
            string cardNumber,
            string qrPayload,
            byte[]? photoBytes,
            CancellationToken cancellationToken)
        {
            try
            {
                var cachedStatus = await _localDb.DigitalIdStatusCaches
                    .FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash, cancellationToken);

                if (cachedStatus == null)
                {
                    cachedStatus = new DigitalIdStatusCache { BeneficiaryIdHash = payloadHash };
                    _localDb.DigitalIdStatusCaches.Add(cachedStatus);
                }

                cachedStatus.EncryptedBeneficiaryId = ConnectionSecretProtector.Protect(beneficiaryId);
                cachedStatus.EncryptedStatus = ConnectionSecretProtector.Protect(status);
                cachedStatus.EncryptedExpiryDate = ConnectionSecretProtector.Protect(expiry?.ToString() ?? string.Empty);
                cachedStatus.EncryptedCardNumber = ConnectionSecretProtector.Protect(cardNumber);
                cachedStatus.EncryptedQrPayload = ConnectionSecretProtector.Protect(qrPayload);
                cachedStatus.UpdatedAt = DateTime.UtcNow;

                if (photoBytes != null)
                {
                    var cachedPhoto = await _localDb.DigitalIdPhotoCaches
                        .FirstOrDefaultAsync(c => c.BeneficiaryIdHash == payloadHash, cancellationToken);

                    if (cachedPhoto == null)
                    {
                        cachedPhoto = new DigitalIdPhotoCache { BeneficiaryIdHash = payloadHash };
                        _localDb.DigitalIdPhotoCaches.Add(cachedPhoto);
                    }

                    var base64Photo = Convert.ToBase64String(photoBytes);
                    cachedPhoto.EncryptedBeneficiaryId = ConnectionSecretProtector.Protect(beneficiaryId);
                    cachedPhoto.EncryptedPhotoBytes = ConnectionSecretProtector.Protect(base64Photo);
                    cachedPhoto.PhotoHash = HashString(base64Photo);
                    cachedPhoto.UpdatedAt = DateTime.UtcNow;
                }

                await _localDb.SaveChangesAsync(cancellationToken);
                _logger?.LogInformation("Successfully updated local status/photo cache for payload hash {PayloadHash}.", payloadHash);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update local cache for payload hash {PayloadHash}.", payloadHash);
            }
        }

        private string HashString(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>Source of a beneficiary lookup so QR scans and manual key-in share one pipeline.</summary>
    public enum BeneficiaryLookupSource
    {
        QrPayload,
        BeneficiaryId
    }

    /// <summary>A single resolution request; both sources produce the same <see cref="BeneficiaryDigitalIdLookupResult"/>.</summary>
    public sealed record BeneficiaryLookupRequest(BeneficiaryLookupSource Source, string Value);

    public sealed record BeneficiaryDigitalIdLookupResult(
        int BeneficiaryStagingId,
        long? ResidentsId,
        int? HouseholdId,
        int? HouseholdMemberId,
        string FullName,
        string? BeneficiaryId,
        string? CivilRegistryId,
        string CardNumber,
        string? PhotoPath,
        IReadOnlyList<BeneficiaryAssistanceLedgerEntry> ReleaseHistory,
        string? Address,
        string? Age,
        string? Sex,
        bool IsOfflineError = false,
        string? ErrorMessage = null);

    public sealed class BeneficiaryDigitalIdService
    {
        private readonly LocalDbContext _context;
        private readonly AuditService _auditService;
        private readonly BeneficiaryAssistanceLedgerService _ledgerService;
        private readonly IEKardVerificationService _verificationService;

        public BeneficiaryDigitalIdService(
            LocalDbContext context,
            AuditService? auditService = null,
            BeneficiaryAssistanceLedgerService? ledgerService = null,
            IEKardVerificationService? verificationService = null)
        {
            _context = context;
            _auditService = auditService ?? new AuditService(context);
            _ledgerService = ledgerService ?? new BeneficiaryAssistanceLedgerService(context, _auditService);
            _verificationService = verificationService ?? new EKardVerificationService(context, auditService: _auditService);
        }

        public async Task<BeneficiaryDigitalId> EnsureIssuedAsync(int stagingId, int issuedByUserId)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .FirstOrDefaultAsync(row => row.StagingID == stagingId);

            if (stagingRow == null)
            {
                throw new InvalidOperationException("The selected beneficiary could not be found.");
            }

            if (stagingRow.VerificationStatus != VerificationStatus.Approved)
            {
                throw new InvalidOperationException("Only approved beneficiaries can receive digital IDs.");
            }

            var existingId = await _context.BeneficiaryDigitalIds
                .FirstOrDefaultAsync(item => item.BeneficiaryStagingId == stagingId);

            if (existingId != null)
            {
                existingId.HouseholdId = stagingRow.LinkedHouseholdId;
                existingId.HouseholdMemberId = stagingRow.LinkedHouseholdMemberId;
                existingId.IsActive = true;
                existingId.RevokedAt = null;
                await _context.SaveChangesAsync();
                return existingId;
            }

            var digitalId = new BeneficiaryDigitalId
            {
                BeneficiaryStagingId = stagingRow.StagingID,
                HouseholdId = stagingRow.LinkedHouseholdId,
                HouseholdMemberId = stagingRow.LinkedHouseholdMemberId,
                CardNumber = $"BID-{stagingRow.StagingID:D6}",
                QrPayload = BuildQrPayload(stagingRow.StagingID),
                PhotoPath = stagingRow.PhotoPath,
                IssuedByUserId = issuedByUserId,
                IssuedAt = DateTime.Now,
                IsActive = true
            };

            _context.BeneficiaryDigitalIds.Add(digitalId);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                issuedByUserId,
                "BeneficiaryDigitalIdIssued",
                nameof(BeneficiaryDigitalId),
                digitalId.Id,
                $"Issued digital ID '{digitalId.CardNumber}' for staged beneficiary #{stagingRow.StagingID}.");

            return digitalId;
        }

        public async Task<BeneficiaryDigitalId?> GetByStagingIdAsync(int stagingId)
        {
            return await _context.BeneficiaryDigitalIds
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.BeneficiaryStagingId == stagingId);
        }

        public async Task<bool> UpdatePhotoAsync(int stagingId, string? photoPath, int actedByUserId)
        {
            var digitalId = await _context.BeneficiaryDigitalIds
                .FirstOrDefaultAsync(item => item.BeneficiaryStagingId == stagingId);

            if (digitalId == null)
            {
                digitalId = await EnsureIssuedAsync(stagingId, actedByUserId);
            }

            digitalId.PhotoPath = NormalizeNullable(photoPath);
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryDigitalIdPhotoUpdated",
                nameof(BeneficiaryDigitalId),
                digitalId.Id,
                $"Updated the stored digital ID photo for staged beneficiary #{stagingId}.");

            return true;
        }

        public async Task<bool> MarkPrintedAsync(int stagingId, int actedByUserId)
        {
            var digitalId = await _context.BeneficiaryDigitalIds
                .FirstOrDefaultAsync(item => item.BeneficiaryStagingId == stagingId);

            if (digitalId == null)
            {
                return false;
            }

            digitalId.LastPrintedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditService.LogActivityAsync(
                actedByUserId,
                "BeneficiaryDigitalIdPrinted",
                nameof(BeneficiaryDigitalId),
                digitalId.Id,
                $"Printed digital ID '{digitalId.CardNumber}' for staged beneficiary #{stagingId}.");

            return true;
        }

        public async Task<BeneficiaryDigitalIdLookupResult?> LookupByQrPayloadAsync(string qrPayload, int? ayudaProgramId = null, CancellationToken cancellationToken = default)
        {
            var normalizedPayload = NormalizeNullable(qrPayload);
            if (normalizedPayload == null)
            {
                return null;
            }

            ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"START | RawPayload='{qrPayload}' | ProgramId={ayudaProgramId}");

            // 1. Try exact match first (works for new format and exact old format)
            var digitalId = await _context.BeneficiaryDigitalIds
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.IsActive && item.QrPayload == normalizedPayload, cancellationToken);

            if (digitalId != null)
            {
                ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"MATCH_STRATEGY=ExactMatch | DigitalId={digitalId.Id} | StagingId={digitalId.BeneficiaryStagingId}");
            }

            // 2. Try replacing '?' with '|' (in case database has not run bootstrap repairs yet)
            if (digitalId == null && normalizedPayload.Contains('?'))
            {
                var fallbackPayload = normalizedPayload.Replace('?', '|');
                digitalId = await _context.BeneficiaryDigitalIds
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.IsActive && item.QrPayload == fallbackPayload, cancellationToken);

                if (digitalId != null)
                {
                    ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"MATCH_STRATEGY=QuestionMarkFallback | DigitalId={digitalId.Id} | StagingId={digitalId.BeneficiaryStagingId}");
                }
            }

            // 3. Fallback: Robust Regex-based numeric StagingId extraction (handles ASMBID000123, ASM-BID|123, BID-123, etc. with variable digit lengths)
            if (digitalId == null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    normalizedPayload,
                    @"(?i)(?:ASMBID|ASM[?|\-]?BID|BID)[?|\-]?(\d+)",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                if (match.Success && int.TryParse(match.Groups[1].Value, out var stagingId))
                {
                    var potentialId = await _context.BeneficiaryDigitalIds
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => item.IsActive && item.BeneficiaryStagingId == stagingId, cancellationToken);

                    if (potentialId != null)
                    {
                        digitalId = potentialId;
                        ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"MATCH_STRATEGY=RegexStagingIdMatch | ExtractedStagingId={stagingId} | DigitalId={digitalId.Id}");
                    }
                }
            }

            if (digitalId == null)
            {
                ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, "LOCAL_MATCH=NONE | Attempting remote verification...");

                VerificationResult? remoteResult = null;
                try
                {
                    remoteResult = await _verificationService.VerifyDigitalIdAsync(
                        new DigitalIdVerificationRequest { QrPayload = normalizedPayload },
                        cancellationToken);
                }
                catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TimeoutException || ex is System.Net.Sockets.SocketException || ex is InvalidOperationException)
                {
                    ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"REMOTE_VERIFY_EXCEPTION | Error={ex.Message}");
                    return new BeneficiaryDigitalIdLookupResult(
                        0, null, null, null,
                        "OFFLINE — CANNOT VERIFY",
                        null, null, "OFFLINE", null,
                        Array.Empty<BeneficiaryAssistanceLedgerEntry>(),
                        null, null, null,
                        IsOfflineError: true,
                        ErrorMessage: "OFFLINE — CANNOT VERIFY");
                }

                if (remoteResult != null && remoteResult.IsValid && remoteResult.BeneficiaryDetails != null)
                {
                    var details = remoteResult.BeneficiaryDetails;
                    var searchBenId = NormalizeNullable(details.BeneficiaryId);
                    var searchCivilId = NormalizeNullable(details.CivilRegistryId);

                    ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"REMOTE_VERIFY_SUCCESS | BeneficiaryId={searchBenId} | CivilRegistryId={searchCivilId}");

                    // BUG 1 FIX: Case-insensitive, trimmed comparison on BOTH BeneficiaryId AND CivilRegistryId
                    BeneficiaryStaging? importedStaging = null;
                    if (searchBenId != null || searchCivilId != null)
                    {
                        var benUpper = searchBenId?.ToUpperInvariant();
                        var civilUpper = searchCivilId?.ToUpperInvariant();

                        var candidates = await _context.BeneficiaryStaging
                            .Where(row =>
                                (!string.IsNullOrEmpty(benUpper) && row.BeneficiaryId != null && row.BeneficiaryId.Trim().ToUpper() == benUpper) ||
                                (!string.IsNullOrEmpty(civilUpper) && row.CivilRegistryId != null && row.CivilRegistryId.Trim().ToUpper() == civilUpper))
                            .ToListAsync(cancellationToken);

                        importedStaging = candidates.FirstOrDefault();
                    }

                    // BUG 2 FIX: Gate auto-import behind explicit project enrollment check if beneficiary does NOT exist locally
                    if (importedStaging == null)
                    {
                        bool isEnrolled = false;
                        if (ayudaProgramId.HasValue)
                        {
                            var benUpper = searchBenId?.ToUpperInvariant();
                            var civilUpper = searchCivilId?.ToUpperInvariant();

                            if (!string.IsNullOrEmpty(benUpper) || !string.IsNullOrEmpty(civilUpper))
                            {
                                isEnrolled = await _context.AyudaProjectBeneficiaries
                                    .AnyAsync(apb => apb.AyudaProgramId == ayudaProgramId.Value &&
                                        ((!string.IsNullOrEmpty(benUpper) && apb.BeneficiaryId != null && apb.BeneficiaryId.Trim().ToUpper() == benUpper) ||
                                         (!string.IsNullOrEmpty(civilUpper) && apb.CivilRegistryId != null && apb.CivilRegistryId.Trim().ToUpper() == civilUpper)),
                                        cancellationToken);
                            }
                        }

                        if (ayudaProgramId.HasValue && !isEnrolled)
                        {
                            ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"REMOTE_UNENROLLED | Beneficiary '{details.FullName}' not enrolled in Program #{ayudaProgramId}. Returning read-only result without saving to DB.");

                            return new BeneficiaryDigitalIdLookupResult(
                                0,
                                details.ResidentsId,
                                null,
                                null,
                                details.FullName ?? BuildDisplayName(details.FirstName, details.MiddleName, details.LastName),
                                searchBenId,
                                searchCivilId,
                                remoteResult.IdNumber ?? "REMOTE-UNPERSISTED",
                                null,
                                Array.Empty<BeneficiaryAssistanceLedgerEntry>(),
                                NormalizeNullable(details.Address),
                                NormalizeNullable(details.Age),
                                NormalizeNullable(details.Sex));
                        }

                        importedStaging = new BeneficiaryStaging
                        {
                            ResidentsId = details.ResidentsId,
                            BeneficiaryId = searchBenId,
                            CivilRegistryId = searchCivilId,
                            LastName = details.LastName,
                            FirstName = details.FirstName,
                            MiddleName = details.MiddleName,
                            FullName = details.FullName,
                            Sex = details.Sex,
                            DateOfBirth = details.DateOfBirth,
                            Age = details.Age,
                            MaritalStatus = details.MaritalStatus,
                            Address = details.Address,
                            IsPwd = details.IsPwd,
                            PwdIdNo = details.PwdIdNo,
                            DisabilityType = details.DisabilityType,
                            CauseOfDisability = details.CauseOfDisability,
                            IsSenior = details.IsSenior,
                            SeniorIdNo = details.SeniorIdNo,
                            VerificationStatus = VerificationStatus.Approved,
                            ImportedAt = DateTime.Now
                        };
                        _context.BeneficiaryStaging.Add(importedStaging);
                        await _context.SaveChangesAsync(cancellationToken);
                        ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"STAGING_IMPORTED | StagingId={importedStaging.StagingID}");
                    }

                    var importedDigitalId = await _context.BeneficiaryDigitalIds
                        .FirstOrDefaultAsync(item => item.BeneficiaryStagingId == importedStaging.StagingID, cancellationToken);

                    if (importedDigitalId == null)
                    {
                        string? localPhotoPath = null;
                        if (remoteResult.Photo != null)
                        {
                            try
                            {
                                var photosDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Photos");
                                Directory.CreateDirectory(photosDir);
                                localPhotoPath = Path.Combine(photosDir, $"external_beneficiary_{importedStaging.StagingID}.jpg");
                                await File.WriteAllBytesAsync(localPhotoPath, remoteResult.Photo, cancellationToken);
                            }
                            catch
                            {
                                // Fail-safe
                            }
                        }

                        importedDigitalId = new BeneficiaryDigitalId
                        {
                            BeneficiaryStagingId = importedStaging.StagingID,
                            CardNumber = remoteResult.IdNumber,
                            QrPayload = normalizedPayload,
                            PhotoPath = localPhotoPath,
                            IssuedByUserId = 1,
                            IssuedAt = DateTime.Now,
                            IsActive = true
                        };
                        _context.BeneficiaryDigitalIds.Add(importedDigitalId);
                        await _context.SaveChangesAsync(cancellationToken);
                        ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"DIGITAL_ID_IMPORTED | DigitalId={importedDigitalId.Id}");
                    }

                    digitalId = importedDigitalId;
                }
                else
                {
                    var status = remoteResult?.Status;
                    if (status == "OfflineUnavailable" || status == "NetworkError")
                    {
                        ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"REMOTE_VERIFY_OFFLINE | Status={status}");
                        return new BeneficiaryDigitalIdLookupResult(
                            0, null, null, null,
                            "OFFLINE — CANNOT VERIFY",
                            null, null, "OFFLINE", null,
                            Array.Empty<BeneficiaryAssistanceLedgerEntry>(),
                            null, null, null,
                            IsOfflineError: true,
                            ErrorMessage: "OFFLINE — CANNOT VERIFY");
                    }

                    ScanDiagnosticLogger.Log("LookupByQrPayloadAsync", _context, $"REMOTE_VERIFY_FAILED | Status={status ?? "NULL"}");
                    return null;
                }
            }

            var stagingRow = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.StagingID == digitalId.BeneficiaryStagingId, cancellationToken);

            if (stagingRow == null)
            {
                return null;
            }

            var releaseHistory = await _ledgerService.GetEntriesAsync(stagingRow.CivilRegistryId, stagingRow.BeneficiaryId);
            return new BeneficiaryDigitalIdLookupResult(
                stagingRow.StagingID,
                stagingRow.ResidentsId,
                digitalId.HouseholdId,
                digitalId.HouseholdMemberId,
                BuildDisplayName(stagingRow),
                NormalizeNullable(stagingRow.BeneficiaryId),
                NormalizeNullable(stagingRow.CivilRegistryId),
                digitalId.CardNumber,
                NormalizeNullable(digitalId.PhotoPath),
                releaseHistory,
                NormalizeNullable(stagingRow.Address),
                NormalizeNullable(stagingRow.Age),
                NormalizeNullable(stagingRow.Sex));
        }

        /// <summary>
        /// Single resolution pipeline for both QR scans and manual Beneficiary ID key-in.
        /// Both sources return the identical <see cref="BeneficiaryDigitalIdLookupResult"/>.
        /// </summary>
        public async Task<BeneficiaryDigitalIdLookupResult?> ResolveLookupAsync(BeneficiaryLookupRequest request, int? ayudaProgramId = null, CancellationToken cancellationToken = default)
        {
            var value = NormalizeNullable(request.Value);
            if (value == null)
            {
                return null;
            }

            return request.Source switch
            {
                BeneficiaryLookupSource.QrPayload => await LookupByQrPayloadAsync(value, ayudaProgramId, cancellationToken),
                BeneficiaryLookupSource.BeneficiaryId => await LookupByBeneficiaryIdAsync(value, cancellationToken),
                _ => null
            };
        }

        /// <summary>
        /// Resolves a beneficiary by their human-readable Beneficiary ID (manual key-in fallback).
        /// Returns the same result shape as the QR path so the ViewModel state is identical.
        /// </summary>
        private async Task<BeneficiaryDigitalIdLookupResult?> LookupByBeneficiaryIdAsync(string beneficiaryId, CancellationToken cancellationToken)
        {
            var stagingRow = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.BeneficiaryId == beneficiaryId, cancellationToken);

            if (stagingRow == null)
            {
                return null;
            }

            var digitalId = await _context.BeneficiaryDigitalIds
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.IsActive && item.BeneficiaryStagingId == stagingRow.StagingID, cancellationToken);

            var releaseHistory = await _ledgerService.GetEntriesAsync(stagingRow.CivilRegistryId, stagingRow.BeneficiaryId);
            return new BeneficiaryDigitalIdLookupResult(
                stagingRow.StagingID,
                stagingRow.ResidentsId,
                digitalId?.HouseholdId ?? stagingRow.LinkedHouseholdId,
                digitalId?.HouseholdMemberId ?? stagingRow.LinkedHouseholdMemberId,
                BuildDisplayName(stagingRow),
                NormalizeNullable(stagingRow.BeneficiaryId),
                NormalizeNullable(stagingRow.CivilRegistryId),
                digitalId?.CardNumber ?? string.Empty,
                NormalizeNullable(digitalId?.PhotoPath) ?? NormalizeNullable(stagingRow.PhotoPath),
                releaseHistory,
                NormalizeNullable(stagingRow.Address),
                NormalizeNullable(stagingRow.Age),
                NormalizeNullable(stagingRow.Sex));
        }

        private static string BuildQrPayload(int stagingId)
        {
            var randomSuffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
            return $"ASMBID{stagingId:D6}{randomSuffix}";
        }

        private static string BuildDisplayName(BeneficiaryStaging row)
        {
            if (!string.IsNullOrWhiteSpace(row.FullName))
            {
                return row.FullName.Trim();
            }

            return BuildDisplayName(row.FirstName, row.MiddleName, row.LastName);
        }

        private static string BuildDisplayName(string? firstName, string? middleName, string? lastName)
        {
            return string.Join(" ", new[] { firstName, middleName, lastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

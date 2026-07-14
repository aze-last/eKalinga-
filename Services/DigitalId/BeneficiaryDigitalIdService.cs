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
        IReadOnlyList<BeneficiaryAssistanceLedgerEntry> ReleaseHistory);

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

        public async Task<BeneficiaryDigitalIdLookupResult?> LookupByQrPayloadAsync(string qrPayload, CancellationToken cancellationToken = default)
        {
            var normalizedPayload = NormalizeNullable(qrPayload);
            if (normalizedPayload == null)
            {
                return null;
            }

            // 1. Try exact match first (works for new format and exact old format)
            var digitalId = await _context.BeneficiaryDigitalIds
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.IsActive && item.QrPayload == normalizedPayload, cancellationToken);

            // 2. Try replacing '?' with '|' (in case database has not run bootstrap repairs yet)
            if (digitalId == null && normalizedPayload.Contains('?'))
            {
                var fallbackPayload = normalizedPayload.Replace('?', '|');
                digitalId = await _context.BeneficiaryDigitalIds
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.IsActive && item.QrPayload == fallbackPayload, cancellationToken);
            }

            // 3. Fallback: Normalize by stripping delimiters ('|', '?', '-', spaces) to match database payload
            if (digitalId == null)
            {
                var cleanPayload = normalizedPayload.Replace("|", "").Replace("?", "").Replace("-", "").Replace(" ", "");

                // If it looks like our local format, extract staging ID to do an indexed query
                if (cleanPayload.StartsWith("ASMBID", StringComparison.OrdinalIgnoreCase) && cleanPayload.Length >= 12)
                {
                    var stagingIdStr = cleanPayload.Substring(6, 6);
                    if (int.TryParse(stagingIdStr, out var stagingId))
                    {
                        var potentialId = await _context.BeneficiaryDigitalIds
                            .AsNoTracking()
                            .FirstOrDefaultAsync(item => item.IsActive && item.BeneficiaryStagingId == stagingId, cancellationToken);

                        if (potentialId != null)
                        {
                            var dbCleanPayload = potentialId.QrPayload.Replace("|", "").Replace("?", "").Replace("-", "").Replace(" ", "");
                            if (string.Equals(dbCleanPayload, cleanPayload, StringComparison.OrdinalIgnoreCase))
                            {
                                digitalId = potentialId;
                            }
                        }
                    }
                }
            }

            if (digitalId == null)
            {
                var remoteResult = await _verificationService.VerifyDigitalIdAsync(
                    new DigitalIdVerificationRequest { QrPayload = normalizedPayload },
                    cancellationToken);

                if (remoteResult != null && remoteResult.IsValid && remoteResult.BeneficiaryDetails != null)
                {
                    var details = remoteResult.BeneficiaryDetails;
                    
                    var importedStaging = await _context.BeneficiaryStaging
                        .FirstOrDefaultAsync(row => row.BeneficiaryId == details.BeneficiaryId, cancellationToken);

                    if (importedStaging == null)
                    {
                        importedStaging = new BeneficiaryStaging
                        {
                            ResidentsId = details.ResidentsId,
                            BeneficiaryId = details.BeneficiaryId,
                            CivilRegistryId = details.CivilRegistryId,
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
                    }

                    digitalId = importedDigitalId;
                }
                else
                {
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
                releaseHistory);
        }

        /// <summary>
        /// Single resolution pipeline for both QR scans and manual Beneficiary ID key-in.
        /// Both sources return the identical <see cref="BeneficiaryDigitalIdLookupResult"/>.
        /// </summary>
        public async Task<BeneficiaryDigitalIdLookupResult?> ResolveLookupAsync(BeneficiaryLookupRequest request, CancellationToken cancellationToken = default)
        {
            var value = NormalizeNullable(request.Value);
            if (value == null)
            {
                return null;
            }

            return request.Source switch
            {
                BeneficiaryLookupSource.QrPayload => await LookupByQrPayloadAsync(value, cancellationToken),
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
                releaseHistory);
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

            return string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

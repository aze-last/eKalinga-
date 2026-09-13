using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public sealed class CrsMasterlistMirrorResult
    {
        public bool IsSuccess { get; init; }
        public int AddedCount { get; init; }
        public int SkippedCount { get; init; }
        public int BackfilledCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Mirrors CRS val_beneficiaries into the local masterlist (BeneficiaryStaging).
    /// CRS is the source of truth: rows it holds that are missing locally are
    /// auto-created as Approved (the masterlist no longer has a pending stage for
    /// CRS-validated residents). CRS stays strictly READ only; existing local rows
    /// (including seeded test profiles) are never modified or deleted — except for
    /// backfilling a NULL/empty Address from the matched CRS row, which heals
    /// registries whose staging rows were created before addresses were mirrored
    /// (barangay-scoped enrollment filters match on the address text).
    /// </summary>
    public class CrsMasterlistMirrorService
    {
        public const string SyncMetadataKey = "CrsMasterlistMirror";

        private readonly ICrsGateway _gateway;
        private readonly Func<LocalDbContext> _localDbFactory;

        public CrsMasterlistMirrorService(
            ICrsGateway? gateway = null,
            Func<LocalDbContext>? localDbFactory = null)
        {
            _gateway = gateway ?? new CrsGateway();
            _localDbFactory = localDbFactory ?? (() => new LocalDbContext());
        }

        public async Task<CrsMasterlistMirrorResult> MirrorValidatedBeneficiariesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CrsValBeneficiaryRow> sourceRows;
            try
            {
                sourceRows = await _gateway.GetAllValidatedBeneficiariesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Offline / CRS unreachable — the local masterlist keeps serving.
                return new CrsMasterlistMirrorResult
                {
                    IsSuccess = false,
                    Message = $"CRS unreachable — using local masterlist. ({ex.Message})"
                };
            }

            await using var context = _localDbFactory();

            var existingCivilRegistryIds = new HashSet<string>(
                await context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(row => row.CivilRegistryId != null && row.CivilRegistryId != string.Empty)
                    .Select(row => row.CivilRegistryId!)
                    .ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);
            var existingBeneficiaryIds = new HashSet<string>(
                await context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(row => row.BeneficiaryId != null && row.BeneficiaryId != string.Empty)
                    .Select(row => row.BeneficiaryId!)
                    .ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);
            var existingResidentsIds = new HashSet<long>(
                await context.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(row => row.ResidentsId.HasValue)
                    .Select(row => row.ResidentsId!.Value)
                    .ToListAsync(cancellationToken));
            var existingFingerprints = new HashSet<string>(
                (await context.BeneficiaryStaging
                    .AsNoTracking()
                    .Select(row => new { row.FullName, row.FirstName, row.MiddleName, row.LastName, row.DateOfBirth })
                    .ToListAsync(cancellationToken))
                .Select(row => BeneficiaryImportDeduplication.BuildFingerprint(
                    ResolveDisplayName(row.FullName, row.FirstName, row.MiddleName, row.LastName),
                    row.DateOfBirth))
                .Where(fingerprint => !string.IsNullOrWhiteSpace(fingerprint)),
                StringComparer.OrdinalIgnoreCase);

            var addedCount = 0;
            var skippedCount = 0;
            var backfilledCount = 0;
            var backfilledEntities = new HashSet<BeneficiaryStaging>();

            // Tracked lookup of staging rows missing an address, keyed by the same
            // identity keys the dedup snapshot uses. Only these rows are eligible
            // for an address backfill; everything else stays untouched.
            var addressMissingByResidentsId = new Dictionary<long, BeneficiaryStaging>();
            var addressMissingByBeneficiaryId = new Dictionary<string, BeneficiaryStaging>(StringComparer.OrdinalIgnoreCase);
            var addressMissingByCivilRegistryId = new Dictionary<string, BeneficiaryStaging>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in await context.BeneficiaryStaging
                .Where(row => row.Address == null || row.Address == string.Empty)
                .ToListAsync(cancellationToken))
            {
                if (existing.ResidentsId.HasValue)
                {
                    addressMissingByResidentsId.TryAdd(existing.ResidentsId.Value, existing);
                }

                if (!string.IsNullOrWhiteSpace(existing.BeneficiaryId))
                {
                    addressMissingByBeneficiaryId.TryAdd(existing.BeneficiaryId.Trim(), existing);
                }

                if (!string.IsNullOrWhiteSpace(existing.CivilRegistryId))
                {
                    addressMissingByCivilRegistryId.TryAdd(existing.CivilRegistryId.Trim(), existing);
                }
            }

            foreach (var row in sourceRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var beneficiaryId = row.BeneficiaryId?.Trim();
                var civilRegistryId = row.CivilRegistryId?.Trim();
                var displayName = ResolveDisplayName(row.FullName, row.FirstName, row.MiddleName, row.LastName);

                var decision = BeneficiaryImportDeduplication.Evaluate(
                    row.ResidentsId,
                    beneficiaryId,
                    civilRegistryId,
                    displayName,
                    row.DateOfBirth,
                    new BeneficiaryImportDeduplicationSnapshot(
                        existingCivilRegistryIds,
                        existingBeneficiaryIds,
                        existingResidentsIds,
                        existingFingerprints));

                if (decision.ShouldSkip)
                {
                    skippedCount++;
                    if (!string.IsNullOrWhiteSpace(row.Address))
                    {
                        var match = FindAddressMissingMatch(
                            addressMissingByResidentsId,
                            addressMissingByBeneficiaryId,
                            addressMissingByCivilRegistryId,
                            row.ResidentsId,
                            beneficiaryId,
                            civilRegistryId);
                        if (match != null && backfilledEntities.Add(match))
                        {
                            match.Address = row.Address.Trim();
                            match.UpdatedAt = DateTime.Now;
                            backfilledCount++;
                        }
                    }

                    continue;
                }

                context.BeneficiaryStaging.Add(new BeneficiaryStaging
                {
                    ResidentsId = row.ResidentsId,
                    BeneficiaryId = beneficiaryId,
                    CivilRegistryId = civilRegistryId,
                    LastName = row.LastName,
                    FirstName = row.FirstName,
                    MiddleName = row.MiddleName,
                    FullName = row.FullName,
                    Sex = row.Sex,
                    DateOfBirth = row.DateOfBirth,
                    Age = row.Age,
                    MaritalStatus = row.MaritalStatus,
                    Address = row.Address,
                    IsPwd = row.IsPwd,
                    PwdIdNo = row.PwdIdNo,
                    DisabilityType = row.DisabilityType,
                    CauseOfDisability = row.CauseOfDisability,
                    IsSenior = row.IsSenior,
                    SeniorIdNo = row.SeniorIdNo,
                    // CRS already validated this resident — no local pending stage.
                    VerificationStatus = VerificationStatus.Approved,
                    ReviewNotes = "Auto-approved from CRS validated masterlist.",
                    ReviewedAt = DateTime.Now,
                    ImportedAt = DateTime.Now
                });

                if (!string.IsNullOrWhiteSpace(civilRegistryId))
                {
                    existingCivilRegistryIds.Add(civilRegistryId);
                }

                if (!string.IsNullOrWhiteSpace(beneficiaryId))
                {
                    existingBeneficiaryIds.Add(beneficiaryId);
                }

                if (row.ResidentsId.HasValue)
                {
                    existingResidentsIds.Add(row.ResidentsId.Value);
                }

                var fingerprint = BeneficiaryImportDeduplication.BuildFingerprint(displayName, row.DateOfBirth);
                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    existingFingerprints.Add(fingerprint);
                }

                addedCount++;
            }

            if (addedCount > 0 || backfilledCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            await TouchSyncMetadataAsync(context, cancellationToken);

            return new CrsMasterlistMirrorResult
            {
                IsSuccess = true,
                AddedCount = addedCount,
                SkippedCount = skippedCount,
                BackfilledCount = backfilledCount,
                Message = addedCount > 0
                    ? $"Masterlist refreshed — {addedCount} new beneficiary record(s) added."
                    : backfilledCount > 0
                        ? $"Masterlist refreshed — {backfilledCount} address(es) restored from CRS."
                        : "Masterlist is up to date."
            };
        }

        private static BeneficiaryStaging? FindAddressMissingMatch(
            Dictionary<long, BeneficiaryStaging> byResidentsId,
            Dictionary<string, BeneficiaryStaging> byBeneficiaryId,
            Dictionary<string, BeneficiaryStaging> byCivilRegistryId,
            long? residentsId,
            string? beneficiaryId,
            string? civilRegistryId)
        {
            if (residentsId.HasValue && byResidentsId.TryGetValue(residentsId.Value, out var byResidents))
            {
                return byResidents;
            }

            if (!string.IsNullOrWhiteSpace(beneficiaryId) && byBeneficiaryId.TryGetValue(beneficiaryId.Trim(), out var byBen))
            {
                return byBen;
            }

            if (!string.IsNullOrWhiteSpace(civilRegistryId) && byCivilRegistryId.TryGetValue(civilRegistryId.Trim(), out var byCivil))
            {
                return byCivil;
            }

            return null;
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

        private static string ResolveDisplayName(string? fullName, string? firstName, string? middleName, string? lastName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Trim();
            }

            return string.Join(" ", new[] { firstName, middleName, lastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }
    }
}

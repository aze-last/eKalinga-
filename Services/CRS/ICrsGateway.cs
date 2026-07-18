using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Most-recent digital_ids row for a beneficiary (contract Part 1).
    /// </summary>
    public sealed record CrsDigitalIdRow(
        string IdNumber,
        string Status,
        DateTime? IssuedDate,
        DateTime? ExpiryDate,
        DateTime? RevokedAt,
        string? RevocationReason);

    /// <summary>
    /// Photo row from demographic_characteristics (contract Part 2).
    /// Null ProfilePicture with a returned row = confirmed "no photo on file".
    /// </summary>
    public sealed record CrsPhotoRow(byte[]? ProfilePicture, DateTime? UpdatedAt);

    /// <summary>
    /// A record_access_logs row to append (contract Part 3). INSERT only.
    /// </summary>
    public sealed record CrsAccessLogEntry(
        int? UserId,
        string UserName,
        string RecordType,
        string ReferenceNo,
        string ActionTaken,
        DateTime AccessedAt,
        string SyncId);

    public sealed record CrsSchemaProbeResult(bool IsCompatible, string? Reason);

    /// <summary>
    /// A validated-beneficiary row from val_beneficiaries (read-only bulk mirror).
    /// Column set mirrors CrsBeneficiaryImportService's per-row read.
    /// </summary>
    public sealed record CrsValBeneficiaryRow(
        long? ResidentsId,
        string? BeneficiaryId,
        string? CivilRegistryId,
        string? LastName,
        string? FirstName,
        string? MiddleName,
        string? FullName,
        string? Sex,
        string? DateOfBirth,
        string? Age,
        string? MaritalStatus,
        string? Address,
        bool IsPwd,
        string? PwdIdNo,
        string? DisabilityType,
        string? CauseOfDisability,
        bool IsSenior,
        string? SeniorIdNo);

    /// <summary>
    /// A digital_ids row tagged with its beneficiary id, for bulk cache pulls.
    /// Same most-recent-row semantics as <see cref="CrsDigitalIdRow"/> — never
    /// filtered by status.
    /// </summary>
    public sealed record CrsDigitalIdListRow(
        string BeneficiaryId,
        string IdNumber,
        string Status,
        DateTime? IssuedDate,
        DateTime? ExpiryDate,
        DateTime? RevokedAt,
        string? RevocationReason);

    /// <summary>
    /// The ONLY code path that talks SQL to the e-Kard CRS database.
    /// Contract hard rules: digital_ids / val_beneficiaries / demographic_characteristics
    /// are READ only; record_access_logs is INSERT only; the photo lookup is raw SQL,
    /// never mapped through an ORM.
    /// </summary>
    public interface ICrsGateway
    {
        Task<CrsDigitalIdRow?> GetLatestDigitalIdRowAsync(string beneficiaryId, CancellationToken cancellationToken);
        Task<long?> GetDemographicCharacteristicIdAsync(string beneficiaryId, CancellationToken cancellationToken);
        Task<DateTime?> GetPhotoUpdatedAtAsync(long demographicCharacteristicId, CancellationToken cancellationToken);
        Task<CrsPhotoRow?> GetPhotoAsync(long demographicCharacteristicId, CancellationToken cancellationToken);
        Task InsertAccessLogAsync(CrsAccessLogEntry entry, CancellationToken cancellationToken);
        Task<CrsSchemaProbeResult> ProbeSchemaAsync(CancellationToken cancellationToken);

        /// <summary>All non-deleted val_beneficiaries rows (READ only, bulk masterlist sync).</summary>
        Task<IReadOnlyList<CrsValBeneficiaryRow>> GetAllValidatedBeneficiariesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Most-recent digital_ids row per beneficiary (READ only, bulk cache sync).
        /// Never filtered by status — the contract's most-recent-row rule applies.
        /// </summary>
        Task<IReadOnlyList<CrsDigitalIdListRow>> GetAllLatestDigitalIdRowsAsync(CancellationToken cancellationToken);
    }
}

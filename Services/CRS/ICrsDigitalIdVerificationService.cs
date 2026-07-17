using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public enum EKardValidity
    {
        Valid,
        Revoked,
        Expired,
        NotFound,
        Unknown // offline and never cached
    }

    public enum EKardSource
    {
        LiveCrs,
        LocalCache
    }

    public class EKardVerificationRequest
    {
        public string BeneficiaryId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool ForceRefresh { get; set; }
    }

    public sealed record EKardVerificationResult(
        EKardValidity Validity,
        string BeneficiaryId,
        string? IdNumber,
        string Status,
        DateTime? IssuedDate,
        DateTime? ExpiryDate,
        DateTime? RevokedAt,
        string? RevocationReason,
        byte[]? Photo,
        bool PhotoConfirmedAbsent,
        EKardSource Source,
        DateTime? LastSyncedAt);

    public sealed record CrsPhotoResult(byte[]? PhotoBytes, bool ConfirmedAbsent, bool FromCache);

    /// <summary>
    /// e-Kard CRS verification contract orchestrator: answers "does this person
    /// currently hold a valid, non-revoked e-Kard Digital ID?" with a cached photo
    /// for visual match, and records the check in record_access_logs.
    /// </summary>
    public interface ICrsDigitalIdVerificationService
    {
        Task<EKardVerificationResult> VerifyAsync(EKardVerificationRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// The single shared photo-fetch path (contract hard rule) — used by both
        /// verification and any general beneficiary-selection display.
        /// </summary>
        Task<CrsPhotoResult> GetPhotoAsync(string beneficiaryId, CancellationToken cancellationToken);
    }
}

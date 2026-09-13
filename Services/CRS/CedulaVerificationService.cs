using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Best-effort current-year Cedula check (Cedula Verification Contract, Sept 2026).
    /// Informational only: any failure, timeout, or empty result yields
    /// <see cref="CedulaVerificationResult.HasValidCedula"/> = false and the
    /// calling transaction proceeds exactly as if the check never existed.
    /// </summary>
    public sealed record CedulaVerificationResult(
        bool HasValidCedula,
        string? ReferenceNumber,
        DateTime? PaymentDate,
        int? YearCovered);

    public class CedulaVerificationService
    {
        private readonly ICrsGateway _gateway;

        public CedulaVerificationService(ICrsGateway? gateway = null)
        {
            _gateway = gateway ?? new CrsGateway();
        }

        public async Task<CedulaVerificationResult> CheckCurrentYearAsync(string? beneficiaryId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(beneficiaryId))
            {
                return new CedulaVerificationResult(false, null, null, null);
            }

            try
            {
                var year = DateTime.Now.Year;
                var row = await _gateway.GetValidCedulaAsync(beneficiaryId.Trim(), year, cancellationToken);
                if (row == null)
                {
                    // Neutral: not paid this year, or a walk-in recorded without a
                    // CRS beneficiary_id — never a red flag, never a blocker.
                    return new CedulaVerificationResult(false, null, null, null);
                }

                return new CedulaVerificationResult(true, row.ReferenceNumber, row.PaymentDate, row.YearCovered ?? year);
            }
            catch
            {
                // Fail-soft by contract: a slow or failed check must never become
                // a slow or failed transaction for the resident being served.
                // (Cancellation included — the badge simply stays hidden.)
                return new CedulaVerificationResult(false, null, null, null);
            }
        }
    }
}

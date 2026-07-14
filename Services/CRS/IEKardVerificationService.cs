using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public interface IEKardVerificationService
    {
        Task<VerificationResult> VerifyDigitalIdAsync(DigitalIdVerificationRequest request, CancellationToken cancellationToken);
    }
}

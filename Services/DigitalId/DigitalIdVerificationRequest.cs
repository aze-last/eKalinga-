using System;
using AttendanceShiftingManagement.Data;

namespace AttendanceShiftingManagement.Services
{
    public enum VerificationSource
    {
        LocalMasterlist,
        LocalCache,
        RemoteCRS,
        OfflineCache
    }

    public class DigitalIdVerificationRequest
    {
        public string QrPayload { get; set; } = string.Empty;
        public bool ForceRefresh { get; set; }
    }

    public record VerificationResult(
        bool IsValid,
        string Status,
        DateTime? Expiry,
        byte[]? Photo,
        VerificationSource Source,
        string IdNumber,
        CrsValBeneficiary? BeneficiaryDetails = null
    );
}

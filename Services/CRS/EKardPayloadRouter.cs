namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Routes scanned payloads between the app's own digital ID flow
    /// (ASMBID... QR payloads) and the e-Kard CRS verification contract flow
    /// (BEN-... beneficiary ids printed on municipal e-Kard cards).
    /// Single, tested seam for the payload-format assumption.
    /// </summary>
    public static class EKardPayloadRouter
    {
        private const string EKardPrefix = "BEN-";

        public static bool IsEKardPayload(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            return Normalize(payload).StartsWith(EKardPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the bare beneficiary id from a scanned payload — strips
        /// whitespace and wrapper delimiters some scanner guns add (| and ?),
        /// preserving the BEN-... core (same normalization the ASMBID path uses).
        /// </summary>
        public static string ExtractBeneficiaryId(string? payload)
        {
            return Normalize(payload);
        }

        private static string Normalize(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            return payload.Trim().Trim('|', '?').Trim();
        }
    }
}

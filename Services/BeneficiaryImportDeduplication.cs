namespace AttendanceShiftingManagement.Services
{
    internal sealed record BeneficiaryImportDeduplicationSnapshot(
        IReadOnlyCollection<string> CivilRegistryIds,
        IReadOnlyCollection<string> BeneficiaryIds,
        IReadOnlyCollection<long> ResidentsIds,
        IReadOnlyCollection<string> PersonFingerprints);

    internal sealed record BeneficiaryImportDeduplicationDecision(bool ShouldSkip, string Reason);

    internal static class BeneficiaryImportDeduplication
    {
        public static BeneficiaryImportDeduplicationDecision Evaluate(
            long? residentsId,
            string? beneficiaryId,
            string? civilRegistryId,
            string? fullName,
            string? dateOfBirth,
            BeneficiaryImportDeduplicationSnapshot snapshot)
        {
            var normalizedCivilRegistryId = NormalizeTextKey(civilRegistryId);
            if (!string.IsNullOrWhiteSpace(normalizedCivilRegistryId) &&
                snapshot.CivilRegistryIds.Contains(normalizedCivilRegistryId, StringComparer.OrdinalIgnoreCase))
            {
                return new BeneficiaryImportDeduplicationDecision(true, "Existing CivilRegistryId match.");
            }

            var normalizedBeneficiaryId = NormalizeTextKey(beneficiaryId);
            if (!string.IsNullOrWhiteSpace(normalizedBeneficiaryId) &&
                snapshot.BeneficiaryIds.Contains(normalizedBeneficiaryId, StringComparer.OrdinalIgnoreCase))
            {
                return new BeneficiaryImportDeduplicationDecision(true, "Existing BeneficiaryId match.");
            }

            if (residentsId.HasValue && snapshot.ResidentsIds.Contains(residentsId.Value))
            {
                return new BeneficiaryImportDeduplicationDecision(true, "Existing ResidentsId match.");
            }

            var fingerprint = BuildFingerprint(fullName, dateOfBirth);
            if (!string.IsNullOrWhiteSpace(fingerprint) &&
                snapshot.PersonFingerprints.Contains(fingerprint, StringComparer.OrdinalIgnoreCase))
            {
                return new BeneficiaryImportDeduplicationDecision(true, "Existing name/date-of-birth fingerprint match.");
            }

            return new BeneficiaryImportDeduplicationDecision(false, string.Empty);
        }

        public static string BuildFingerprint(string? fullName, string? dateOfBirth)
        {
            var normalizedName = NormalizeName(fullName);
            var normalizedBirthDate = NormalizeTextKey(dateOfBirth);

            if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(normalizedBirthDate))
            {
                return string.Empty;
            }

            return $"{normalizedName}|{normalizedBirthDate}";
        }

        private static string NormalizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var buffer = new List<char>(value.Length);
            foreach (var character in value.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
                {
                    buffer.Add(character);
                }
            }

            return string.Join(
                " ",
                new string(buffer.ToArray())
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string NormalizeTextKey(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}

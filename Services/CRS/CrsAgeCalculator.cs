using System.Globalization;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Age derivation per the CRS schema-drift notice: val_beneficiaries.age is being
    /// removed, so systems must compute age from date_of_birth in their own code —
    /// never select the column. Unparseable/missing birth dates yield null, never 0
    /// (0 would silently misclassify people as newborns in age-based checks).
    /// </summary>
    public static class CrsAgeCalculator
    {
        public static int? CalculateAge(DateTime? dateOfBirth)
        {
            if (dateOfBirth is null) return null;
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Value.Year;
            if (dateOfBirth.Value.Date > today.AddYears(-age)) age--;
            return age < 0 ? null : age;
        }

        /// <summary>
        /// Safe-parses a raw CRS date string ("yyyy-MM-dd", but possibly malformed: "0000-00-00",
        /// empty, or free text after the merge) and derives the age, as a string for the
        /// staging table's Age column. Null when the date cannot be parsed.
        /// </summary>
        public static string? CalculateAgeText(string? dateOfBirthRaw)
        {
            if (string.IsNullOrWhiteSpace(dateOfBirthRaw)) return null;
            if (!DateTime.TryParse(dateOfBirthRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOfBirth))
            {
                return null;
            }

            return CalculateAge(dateOfBirth)?.ToString(CultureInfo.InvariantCulture);
        }
    }
}

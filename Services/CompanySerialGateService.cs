using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Services
{
    public sealed record CompanySerialValidationResult(
        bool IsSuccess,
        bool WasBoundToDatabase,
        string Message,
        string CompanySerialNumber,
        string? RegisteredCompanyName = null);

    public static class CompanySerialGateService
    {
        public static CompanySerialValidationResult ValidateOrBind(AppDbContext context, SystemProfileSettingsModel localSettings)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(localSettings);

            var localCompanySerial = NormalizeSerial(localSettings.InstallSerial);
            if (string.IsNullOrWhiteSpace(localCompanySerial))
            {
                return new CompanySerialValidationResult(
                    false,
                    false,
                    "This installation does not have a company serial number yet.",
                    string.Empty);
            }

            var localCompanyName = BuildCompanyName(localSettings);
            var registrations = context.SystemRegistrations
                .OrderBy(item => item.Id)
                .ToList();

            if (registrations.Count > 1)
            {
                return new CompanySerialValidationResult(
                    false,
                    false,
                    "This database has multiple company serial registrations. Access is blocked until the registration record is fixed.",
                    localCompanySerial);
            }

            var registration = registrations.FirstOrDefault();
            if (registration == null)
            {
                registration = new SystemRegistration
                {
                    CompanySerialNumber = localCompanySerial,
                    CompanyName = localCompanyName,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    LastValidatedAt = DateTime.Now
                };

                context.SystemRegistrations.Add(registration);
                context.SaveChanges();

                return new CompanySerialValidationResult(
                    true,
                    true,
                    $"Company serial {localCompanySerial} is now registered to this database.",
                    localCompanySerial,
                    registration.CompanyName);
            }

            if (!string.Equals(registration.CompanySerialNumber, localCompanySerial, StringComparison.OrdinalIgnoreCase))
            {
                var registeredName = string.IsNullOrWhiteSpace(registration.CompanyName)
                    ? "another company"
                    : registration.CompanyName;

                return new CompanySerialValidationResult(
                    false,
                    false,
                    $"This database is already assigned to company serial {registration.CompanySerialNumber} ({registeredName}). The current installation serial {localCompanySerial} is not allowed to use it.",
                    localCompanySerial,
                    registration.CompanyName);
            }

            var normalizedCompanyName = localCompanyName;
            var shouldSave = false;
            if (!string.Equals(registration.CompanySerialNumber, localCompanySerial, StringComparison.Ordinal))
            {
                registration.CompanySerialNumber = localCompanySerial;
                shouldSave = true;
            }

            if (!string.Equals(registration.CompanyName, normalizedCompanyName, StringComparison.Ordinal))
            {
                registration.CompanyName = normalizedCompanyName;
                shouldSave = true;
            }

            registration.LastValidatedAt = DateTime.Now;
            registration.UpdatedAt = DateTime.Now;
            shouldSave = true;

            if (shouldSave)
            {
                context.SaveChanges();
            }

            return new CompanySerialValidationResult(
                true,
                false,
                $"Company serial {localCompanySerial} matched the active database registration.",
                localCompanySerial,
                registration.CompanyName);
        }

        private static string NormalizeSerial(string? serial)
        {
            return string.IsNullOrWhiteSpace(serial)
                ? string.Empty
                : serial.Trim().ToUpperInvariant();
        }

        private static string? BuildCompanyName(SystemProfileSettingsModel localSettings)
        {
            if (!string.IsNullOrWhiteSpace(localSettings.Owner))
            {
                return localSettings.Owner.Trim();
            }

            if (!string.IsNullOrWhiteSpace(localSettings.SystemName))
            {
                return localSettings.SystemName.Trim();
            }

            return null;
        }
    }
}

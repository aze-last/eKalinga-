using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class SystemProfileSettingsModel
    {
        public string SystemName { get; set; } = "Barangay Ayuda System";
        public string Owner { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;
        public string InstallSerial { get; set; } = string.Empty;
    }

    public sealed class SystemLoginBrandingSnapshot
    {
        public string Title { get; init; } = "Bagong Pilipinas";
        public string Subtitle { get; init; } = "Barangay Ayuda System";
        public string Address { get; init; } = string.Empty;
        public string InstallSerial { get; init; } = string.Empty;
        public string LogoPath { get; init; } = string.Empty;
        public bool HasCustomLogo => !string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath);
    }

    public static class SystemProfileSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static SystemProfileSettingsModel Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        public static SystemProfileSettingsModel Load(string runtimePath)
        {
            if (!File.Exists(runtimePath))
            {
                var defaults = EnsureInstallSerial(new SystemProfileSettingsModel());
                Save(defaults, runtimePath);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(runtimePath);
                var settings = JsonSerializer.Deserialize<SystemProfileSettingsModel>(json, JsonOptions) ?? new SystemProfileSettingsModel();
                var originalInstallSerial = settings.InstallSerial;
                var normalizedSettings = EnsureInstallSerial(settings);

                if (!string.Equals(normalizedSettings.InstallSerial, originalInstallSerial, StringComparison.Ordinal))
                {
                    Save(normalizedSettings, runtimePath);
                }

                return normalizedSettings;
            }
            catch
            {
                var defaults = EnsureInstallSerial(new SystemProfileSettingsModel());
                Save(defaults, runtimePath);
                return defaults;
            }
        }

        public static void Save(SystemProfileSettingsModel settings)
        {
            Save(settings, GetRuntimeSettingsPath());
        }

        public static void Save(SystemProfileSettingsModel settings, string runtimePath)
        {
            settings = EnsureInstallSerial(settings);

            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(settings, JsonOptions));
        }

        public static SystemLoginBrandingSnapshot BuildLoginBranding(SystemProfileSettingsModel settings)
        {
            settings = EnsureInstallSerial(settings);

            return new SystemLoginBrandingSnapshot
            {
                Title = string.IsNullOrWhiteSpace(settings.Owner) ? "Bagong Pilipinas" : settings.Owner.Trim(),
                Subtitle = string.IsNullOrWhiteSpace(settings.SystemName) ? "Barangay Ayuda System" : settings.SystemName.Trim(),
                Address = settings.CompanyAddress?.Trim() ?? string.Empty,
                InstallSerial = settings.InstallSerial,
                LogoPath = settings.LogoPath?.Trim() ?? string.Empty
            };
        }

        public static string CopyLogoToBrandingFolder(string sourcePath, string? existingLogoPath = null)
        {
            return CopyLogoToBrandingFolder(sourcePath, GetBrandingDirectory(), existingLogoPath);
        }

        public static string CopyLogoToBrandingFolder(string sourcePath, string brandingDirectory, string? existingLogoPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("A source logo path is required.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("The selected logo file could not be found.", sourcePath);
            }

            Directory.CreateDirectory(brandingDirectory);
            var extension = Path.GetExtension(sourcePath);
            var destinationPath = Path.Combine(brandingDirectory, $"system-logo-{Guid.NewGuid():N}{extension}");

            File.Copy(sourcePath, destinationPath, overwrite: true);

            if (!string.IsNullOrWhiteSpace(existingLogoPath)
                && !string.Equals(existingLogoPath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                RemoveStoredLogo(existingLogoPath);
            }

            return destinationPath;
        }

        public static void RemoveStoredLogo(string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath))
            {
                return;
            }

            try
            {
                if (File.Exists(logoPath))
                {
                    File.Delete(logoPath);
                }
            }
            catch
            {
                // Ignore cleanup failures; the profile can still fall back to the default logo.
            }
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "systemprofile.json");
        }

        private static string GetBrandingDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "branding");
        }

        private static SystemProfileSettingsModel EnsureInstallSerial(SystemProfileSettingsModel settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.InstallSerial))
            {
                return settings;
            }

            settings.InstallSerial = GenerateInstallSerial(DateTime.Now);
            return settings;
        }

        private static string GenerateInstallSerial(DateTime timestamp)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Span<char> suffix = stackalloc char[4];

            for (var index = 0; index < suffix.Length; index++)
            {
                suffix[index] = alphabet[Random.Shared.Next(alphabet.Length)];
            }

            return $"BAS-{timestamp:yyyyMMdd}-{new string(suffix)}";
        }
    }
}

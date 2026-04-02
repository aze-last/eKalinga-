using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class SystemProfileSettingsModel
    {
        public const string DefaultLogoUri = "pack://application:,,,/Images/96f88319-f1f4-46df-b780-691795d4e49e.png";
        public const string DefaultLoginBackgroundUri = "pack://application:,,,/Images/Gemini_Generated_Image_n4mhn0n4mhn0n4mh.png";

        public string SystemName { get; set; } = "eKalinga+";
        public string Owner { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string LogoPath { get; set; } = DefaultLogoUri;
        public string LoginBackgroundPath { get; set; } = DefaultLoginBackgroundUri;
        public string InstallSerial { get; set; } = string.Empty;
    }

    public sealed class SystemLoginBrandingSnapshot
    {
        public string Title { get; init; } = "Local Government Unit";
        public string Subtitle { get; init; } = "eKalinga+";
        public string Address { get; init; } = string.Empty;
        public string InstallSerial { get; init; } = string.Empty;
        public string LogoPath { get; init; } = string.Empty;
        public string LoginBackgroundPath { get; init; } = string.Empty;
        public bool HasCustomLogo => IsAvailableAsset(LogoPath);
        public bool HasCustomBackground => IsAvailableAsset(LoginBackgroundPath);

        private static bool IsAvailableAsset(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) || File.Exists(path));
        }
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
                Title = string.IsNullOrWhiteSpace(settings.Owner) ? "Local Government Unit" : settings.Owner.Trim(),
                Subtitle = string.IsNullOrWhiteSpace(settings.SystemName) ? "eKalinga+" : settings.SystemName.Trim(),
                Address = settings.CompanyAddress?.Trim() ?? string.Empty,
                InstallSerial = settings.InstallSerial,
                LogoPath = settings.LogoPath?.Trim() ?? string.Empty,
                LoginBackgroundPath = settings.LoginBackgroundPath?.Trim() ?? string.Empty
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

        public static string CopyBackgroundToBrandingFolder(string sourcePath, string? existingBackgroundPath = null)
        {
            return CopyBackgroundToBrandingFolder(sourcePath, GetBrandingDirectory(), existingBackgroundPath);
        }

        public static string CopyBackgroundToBrandingFolder(string sourcePath, string brandingDirectory, string? existingBackgroundPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("A source background path is required.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("The selected background file could not be found.", sourcePath);
            }

            Directory.CreateDirectory(brandingDirectory);
            var extension = Path.GetExtension(sourcePath);
            var destinationPath = Path.Combine(brandingDirectory, $"system-bg-{Guid.NewGuid():N}{extension}");

            File.Copy(sourcePath, destinationPath, overwrite: true);

            if (!string.IsNullOrWhiteSpace(existingBackgroundPath)
                && !string.Equals(existingBackgroundPath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                RemoveStoredLogo(existingBackgroundPath);
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
            settings.LogoPath = NormalizeAssetPath(settings.LogoPath, "96f88319-f1f4-46df-b780-691795d4e49e.png", SystemProfileSettingsModel.DefaultLogoUri);
            settings.LoginBackgroundPath = NormalizeAssetPath(settings.LoginBackgroundPath, "Gemini_Generated_Image_n4mhn0n4mhn0n4mh.png", SystemProfileSettingsModel.DefaultLoginBackgroundUri);

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

        private static string NormalizeAssetPath(string? currentPath, string defaultFileName, string defaultUri)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return defaultUri;
            }

            if (LooksLikePackUri(currentPath))
            {
                return currentPath.Trim();
            }

            if (File.Exists(currentPath))
            {
                return currentPath.Trim();
            }

            var fileName = Path.GetFileName(currentPath.Trim());
            return string.Equals(fileName, defaultFileName, StringComparison.OrdinalIgnoreCase)
                ? defaultUri
                : currentPath.Trim();
        }

        private static bool LooksLikePackUri(string path)
        {
            return path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
        }
    }
}

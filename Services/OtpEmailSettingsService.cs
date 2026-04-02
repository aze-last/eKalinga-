using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class OtpEmailSettingsModel
    {
        public string SmtpHost { get; set; } = "smtp-relay.brevo.com";
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderDisplayName { get; set; } = "eKalinga+ OTP";
    }

    public static class OtpEmailSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static OtpEmailSettingsModel Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        internal static OtpEmailSettingsModel Load(string runtimePath)
        {
            var defaults = LoadDefaultsFromAppSettings();
            if (!File.Exists(runtimePath))
            {
                return defaults;
            }

            try
            {
                var runtimeJson = File.ReadAllText(runtimePath);
                var runtimeSettings = JsonSerializer.Deserialize<OtpEmailSettingsModel>(runtimeJson, JsonOptions);
                if (runtimeSettings == null)
                {
                    return defaults;
                }

                runtimeSettings.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.Password);
                return Normalize(runtimeSettings);
            }
            catch
            {
                return defaults;
            }
        }

        public static void Save(OtpEmailSettingsModel settings)
        {
            Save(settings, GetRuntimeSettingsPath());
        }

        internal static void Save(OtpEmailSettingsModel settings, string runtimePath)
        {
            var normalized = Normalize(settings);
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            var payload = new OtpEmailSettingsModel
            {
                SmtpHost = normalized.SmtpHost,
                Port = normalized.Port,
                UseSsl = normalized.UseSsl,
                Username = normalized.Username,
                Password = ConnectionSecretProtector.Protect(normalized.Password),
                SenderEmail = normalized.SenderEmail,
                SenderDisplayName = normalized.SenderDisplayName
            };

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        public static bool IsConfigured(OtpEmailSettingsModel settings)
        {
            settings = Normalize(settings);

            return !string.IsNullOrWhiteSpace(settings.SmtpHost)
                && settings.Port > 0
                && !string.IsNullOrWhiteSpace(settings.Username)
                && !string.IsNullOrWhiteSpace(settings.Password)
                && !string.IsNullOrWhiteSpace(settings.SenderEmail);
        }

        private static OtpEmailSettingsModel LoadDefaultsFromAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var defaults = configuration.GetSection("OtpEmail").Get<OtpEmailSettingsModel>() ?? new OtpEmailSettingsModel();
            defaults.Password = ConnectionSecretProtector.Unprotect(defaults.Password);
            return Normalize(defaults);
        }

        private static OtpEmailSettingsModel Normalize(OtpEmailSettingsModel? settings)
        {
            settings ??= new OtpEmailSettingsModel();
            settings.SmtpHost = string.IsNullOrWhiteSpace(settings.SmtpHost)
                ? "smtp-relay.brevo.com"
                : settings.SmtpHost.Trim();
            settings.Port = settings.Port <= 0 ? 587 : settings.Port;
            settings.Username = settings.Username?.Trim() ?? string.Empty;
            settings.Password = settings.Password ?? string.Empty;
            settings.SenderEmail = settings.SenderEmail?.Trim() ?? string.Empty;
            settings.SenderDisplayName = string.IsNullOrWhiteSpace(settings.SenderDisplayName)
                ? "eKalinga+ OTP"
                : settings.SenderDisplayName.Trim();
            return settings;
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "otpemailsettings.json");
        }

        private static string ResolveConfigurationBasePath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(baseDirectory, "appsettings.json")))
            {
                return baseDirectory;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}

using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Connection options for the e-Kard CRS verification contract database
    /// (municipal CRS MySQL — digital_ids / val_beneficiaries / demographic_characteristics
    /// / record_access_logs). Mirrors the BudgetRuntimeOptions pattern: defaults from the
    /// top-level "CrsContractConnection" appsettings.json section, optional DPAPI-protected
    /// runtime override in %LocalAppData%.
    /// </summary>
    public sealed class CrsContractRuntimeOptions
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public DatabaseConnectionPreset CrsContractConnection { get; set; } = new()
        {
            DisplayName = "e-Kard CRS Verification Source",
            Port = 3306
        };

        public static CrsContractRuntimeOptions Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        internal static CrsContractRuntimeOptions Load(string runtimePath)
        {
            var defaults = LoadDefaultsFromAppSettings();
            if (!File.Exists(runtimePath))
            {
                return defaults;
            }

            try
            {
                var runtimeJson = File.ReadAllText(runtimePath);
                var runtimeSettings = JsonSerializer.Deserialize<CrsContractRuntimeOptions>(runtimeJson, JsonOptions);
                if (runtimeSettings == null)
                {
                    return defaults;
                }

                runtimeSettings.CrsContractConnection ??= new DatabaseConnectionPreset();
                runtimeSettings.CrsContractConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.CrsContractConnection.Password);
                return Normalize(runtimeSettings);
            }
            catch
            {
                return defaults;
            }
        }

        public static void Save(CrsContractRuntimeOptions settings)
        {
            Save(settings, GetRuntimeSettingsPath());
        }

        internal static void Save(CrsContractRuntimeOptions settings, string runtimePath)
        {
            var normalized = Normalize(settings);
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            var payload = new CrsContractRuntimeOptions
            {
                CrsContractConnection = new DatabaseConnectionPreset
                {
                    DisplayName = normalized.CrsContractConnection.DisplayName,
                    Server = normalized.CrsContractConnection.Server,
                    Port = normalized.CrsContractConnection.Port,
                    Database = normalized.CrsContractConnection.Database,
                    Username = normalized.CrsContractConnection.Username,
                    Password = ConnectionSecretProtector.Protect(normalized.CrsContractConnection.Password)
                }
            };

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        private static CrsContractRuntimeOptions LoadDefaultsFromAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var defaults = new CrsContractRuntimeOptions
            {
                CrsContractConnection = configuration.GetSection("CrsContractConnection").Get<DatabaseConnectionPreset>()
                    ?? new DatabaseConnectionPreset()
            };
            defaults.CrsContractConnection.Password = ConnectionSecretProtector.Unprotect(defaults.CrsContractConnection.Password);
            return Normalize(defaults);
        }

        private static CrsContractRuntimeOptions Normalize(CrsContractRuntimeOptions? settings)
        {
            settings ??= new CrsContractRuntimeOptions();
            settings.CrsContractConnection ??= new DatabaseConnectionPreset();
            settings.CrsContractConnection.DisplayName = string.IsNullOrWhiteSpace(settings.CrsContractConnection.DisplayName)
                ? "e-Kard CRS Verification Source"
                : settings.CrsContractConnection.DisplayName.Trim();
            settings.CrsContractConnection.Server = settings.CrsContractConnection.Server?.Trim() ?? string.Empty;
            settings.CrsContractConnection.Database = settings.CrsContractConnection.Database?.Trim() ?? string.Empty;
            settings.CrsContractConnection.Username = settings.CrsContractConnection.Username?.Trim() ?? string.Empty;
            settings.CrsContractConnection.Password = settings.CrsContractConnection.Password ?? string.Empty;
            if (settings.CrsContractConnection.Port <= 0)
            {
                settings.CrsContractConnection.Port = 3306;
            }

            return settings;
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "crscontractsettings.json");
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

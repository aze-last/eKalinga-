using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.IO;

namespace AttendanceShiftingManagement.Services
{
    public static class MunicipalityImportConnectionSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static DatabaseConnectionPreset Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        internal static DatabaseConnectionPreset Load(string runtimePath)
        {
            if (!File.Exists(runtimePath))
            {
                return LoadDefault();
            }

            try
            {
                var json = File.ReadAllText(runtimePath);
                var preset = JsonSerializer.Deserialize<DatabaseConnectionPreset>(json, JsonOptions);
                if (preset == null)
                {
                    return LoadDefault();
                }

                preset.Password = ConnectionSecretProtector.Unprotect(preset.Password);
                EnsureDisplayName(preset);
                return preset;
            }
            catch
            {
                return LoadDefault();
            }
        }

        public static void Save(DatabaseConnectionPreset preset)
        {
            Save(preset, GetRuntimeSettingsPath());
        }

        internal static void Save(DatabaseConnectionPreset preset, string runtimePath)
        {
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            var payload = new DatabaseConnectionPreset
            {
                DisplayName = "Municipality Import Source",
                Server = preset.Server,
                Port = preset.Port,
                Database = preset.Database,
                Username = preset.Username,
                Password = ConnectionSecretProtector.Protect(preset.Password)
            };

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        private static DatabaseConnectionPreset LoadDefault()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var configuredPreset = configuration.GetSection("MunicipalityImportConnection").Get<DatabaseConnectionPreset>();
            if (configuredPreset != null && !string.IsNullOrWhiteSpace(configuredPreset.Server))
            {
                configuredPreset.Password = ConnectionSecretProtector.Unprotect(configuredPreset.Password);
                EnsureDisplayName(configuredPreset);
                return configuredPreset;
            }

            return new DatabaseConnectionPreset
            {
                DisplayName = "Municipality Import Source",
                Server = string.Empty,
                Port = 3306,
                Database = string.Empty,
                Username = string.Empty,
                Password = string.Empty
            };
        }

        private static void EnsureDisplayName(DatabaseConnectionPreset preset)
        {
            if (string.IsNullOrWhiteSpace(preset.DisplayName))
            {
                preset.DisplayName = "Municipality Import Source";
            }
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "municipalityimportsettings.json");
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

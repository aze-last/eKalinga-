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
            var runtimePath = GetRuntimeSettingsPath();
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
            var runtimePath = GetRuntimeSettingsPath();
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
                Password = preset.Password
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
                EnsureDisplayName(configuredPreset);
                return configuredPreset;
            }

            var fallback = ConnectionSettingsService.Load().GetPreset("Remote");
            return new DatabaseConnectionPreset
            {
                DisplayName = "Municipality Import Source",
                Server = fallback.Server,
                Port = fallback.Port,
                Database = fallback.Database,
                Username = fallback.Username,
                Password = fallback.Password
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

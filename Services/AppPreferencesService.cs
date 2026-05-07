using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class AppPreferencesModel
    {
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public string UpdateManifestUrl { get; set; } = string.Empty;
    }

    public static class AppPreferencesService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static AppPreferencesModel Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        internal static AppPreferencesModel Load(string runtimePath)
        {
            if (!File.Exists(runtimePath))
            {
                var defaults = LoadDefaultsFromAppSettings();
                Save(defaults, runtimePath);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(runtimePath);
                return Normalize(JsonSerializer.Deserialize<AppPreferencesModel>(json, JsonOptions));
            }
            catch
            {
                var defaults = LoadDefaultsFromAppSettings();
                Save(defaults, runtimePath);
                return defaults;
            }
        }

        public static void Save(AppPreferencesModel settings)
        {
            Save(settings, GetRuntimeSettingsPath());
        }

        internal static void Save(AppPreferencesModel settings, string runtimePath)
        {
            var normalized = Normalize(settings);
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(normalized, JsonOptions));
        }

        private static AppPreferencesModel LoadDefaultsFromAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            return Normalize(new AppPreferencesModel
            {
                CheckForUpdatesOnStartup = configuration.GetValue("UpdateSettings:CheckOnStartup", true),
                UpdateManifestUrl = configuration["UpdateSettings:ManifestUrl"] ?? string.Empty
            });
        }

        private static AppPreferencesModel Normalize(AppPreferencesModel? settings)
        {
            settings ??= new AppPreferencesModel();
            settings.UpdateManifestUrl = settings.UpdateManifestUrl?.Trim() ?? string.Empty;
            return settings;
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "apppreferences.json");
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

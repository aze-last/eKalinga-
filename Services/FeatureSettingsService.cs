using System.Text.Json;
using System.IO;

namespace AttendanceShiftingManagement.Services
{
    public sealed class FeatureSettingsModel
    {
        public decimal LargeAssistanceWarningThreshold { get; set; } = 10000m;
    }

    public static class FeatureSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static FeatureSettingsModel Load()
        {
            var runtimePath = GetRuntimeSettingsPath();
            if (!File.Exists(runtimePath))
            {
                return new FeatureSettingsModel();
            }

            try
            {
                var json = File.ReadAllText(runtimePath);
                return JsonSerializer.Deserialize<FeatureSettingsModel>(json, JsonOptions) ?? new FeatureSettingsModel();
            }
            catch
            {
                return new FeatureSettingsModel();
            }
        }

        public static void Save(FeatureSettingsModel settings)
        {
            var runtimePath = GetRuntimeSettingsPath();
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(settings, JsonOptions));
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "featuresettings.json");
        }
    }
}

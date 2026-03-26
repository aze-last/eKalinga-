using AttendanceShiftingManagement.Models;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public static class BackupCatalogService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static BackupCatalogModel Load()
        {
            return Load(GetRuntimePath());
        }

        internal static BackupCatalogModel Load(string runtimePath)
        {
            if (!File.Exists(runtimePath))
            {
                return new BackupCatalogModel();
            }

            try
            {
                var json = File.ReadAllText(runtimePath);
                var catalog = JsonSerializer.Deserialize<BackupCatalogModel>(json, JsonOptions) ?? new BackupCatalogModel();
                var existingEntries = catalog.Entries
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.FilePath) && File.Exists(entry.FilePath))
                    .GroupBy(entry => entry.BackupId, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(entry => entry.CreatedAt).First())
                    .OrderBy(entry => entry.CreatedAt)
                    .ToList();

                catalog.Entries = existingEntries;
                return catalog;
            }
            catch
            {
                return new BackupCatalogModel();
            }
        }

        public static void Save(BackupCatalogModel catalog)
        {
            Save(catalog, GetRuntimePath());
        }

        internal static void Save(BackupCatalogModel catalog, string runtimePath)
        {
            var directory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(catalog, JsonOptions));
        }

        public static void Register(string filePath, BackupManifest manifest)
        {
            var catalog = Load();
            catalog.Entries.RemoveAll(entry => string.Equals(entry.BackupId, manifest.BackupId, StringComparison.OrdinalIgnoreCase));
            catalog.Entries.Add(new BackupCatalogEntry
            {
                BackupId = manifest.BackupId,
                BackupType = manifest.BackupType,
                ChainId = manifest.ChainId,
                BaseFullBackupId = manifest.BaseFullBackupId,
                PreviousBackupId = manifest.PreviousBackupId,
                SelectedPreset = manifest.SelectedPreset,
                PresetDisplayName = manifest.PresetDisplayName,
                Database = manifest.Database,
                CreatedAt = manifest.CreatedAt,
                FilePath = filePath
            });

            Save(catalog);
        }

        public static IReadOnlyList<BackupCatalogEntry> GetExistingEntriesForPreset(string presetKey)
        {
            return Load().Entries
                .Where(entry => string.Equals(entry.SelectedPreset, presetKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.CreatedAt)
                .ToList();
        }

        public static IReadOnlyList<BackupManifest> GetManifestsForPreset(string presetKey)
        {
            return GetExistingEntriesForPreset(presetKey)
                .Select(entry => new BackupManifest
                {
                    BackupId = entry.BackupId,
                    BackupType = entry.BackupType,
                    ChainId = entry.ChainId,
                    BaseFullBackupId = entry.BaseFullBackupId,
                    PreviousBackupId = entry.PreviousBackupId,
                    SelectedPreset = entry.SelectedPreset,
                    PresetDisplayName = entry.PresetDisplayName,
                    Database = entry.Database,
                    CreatedAt = entry.CreatedAt
                })
                .ToList();
        }

        public static BackupPresetStatus GetPresetStatus(string presetKey)
        {
            return BackupChainService.BuildPresetStatus(presetKey, GetManifestsForPreset(presetKey));
        }

        private static string GetRuntimePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "backupcatalog.json");
        }
    }
}

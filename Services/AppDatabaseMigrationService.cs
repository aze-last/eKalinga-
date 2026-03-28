using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed class AppDatabaseMigrationPresetResult
    {
        public string PresetKey { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public sealed class AppDatabaseMigrationBatchResult
    {
        public IReadOnlyList<AppDatabaseMigrationPresetResult> PresetResults { get; init; } = Array.Empty<AppDatabaseMigrationPresetResult>();
        public bool IsSuccess => PresetResults.All(result => result.IsSuccess);
    }

    public static class AppDatabaseMigrationService
    {
        private const string LocalPresetKey = "Local";
        private const string RemotePresetKey = "Remote";

        public static async Task<AppDatabaseMigrationBatchResult> MigrateLocalAndRemoteAsync()
        {
            var settings = ConnectionSettingsService.Load();
            var presetResults = new List<AppDatabaseMigrationPresetResult>
            {
                await MigratePresetAsync(settings, LocalPresetKey),
                await MigratePresetAsync(settings, RemotePresetKey)
            };

            return new AppDatabaseMigrationBatchResult
            {
                PresetResults = presetResults
            };
        }

        internal static async Task<AppDatabaseMigrationPresetResult> MigratePresetAsync(ConnectionSettingsModel settings, string presetKey)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var preset = settings.GetPreset(presetKey);
            var displayName = string.IsNullOrWhiteSpace(preset.DisplayName)
                ? presetKey
                : preset.DisplayName;

            if (!TryValidatePresetForMigration(preset, out var validationMessage))
            {
                return new AppDatabaseMigrationPresetResult
                {
                    PresetKey = presetKey,
                    DisplayName = displayName,
                    IsSuccess = false,
                    Message = $"{displayName}: {validationMessage}"
                };
            }

            try
            {
                await Task.Run(() => EnsurePresetMigrated(preset));

                return new AppDatabaseMigrationPresetResult
                {
                    PresetKey = presetKey,
                    DisplayName = displayName,
                    IsSuccess = true,
                    Message = $"{displayName}: migration completed for {preset.Server}:{preset.Port} / {preset.Database}."
                };
            }
            catch (Exception ex)
            {
                return new AppDatabaseMigrationPresetResult
                {
                    PresetKey = presetKey,
                    DisplayName = displayName,
                    IsSuccess = false,
                    Message = $"{displayName}: {ex.Message}"
                };
            }
        }

        internal static bool TryValidatePresetForMigration(DatabaseConnectionPreset preset, out string validationMessage)
        {
            ArgumentNullException.ThrowIfNull(preset);

            if (string.IsNullOrWhiteSpace(preset.Server))
            {
                validationMessage = "server or host name is missing.";
                return false;
            }

            if (preset.Port <= 0 || preset.Port > 65535)
            {
                validationMessage = "MySQL port is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(preset.Database))
            {
                validationMessage = "database name is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(preset.Username))
            {
                validationMessage = "database username is missing.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private static void EnsurePresetMigrated(DatabaseConnectionPreset preset)
        {
            var connectionString = ConnectionSettingsService.BuildConnectionString(preset);
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using var context = new AppDbContext(optionsBuilder.Options);
            StartupMigrationCoordinator.EnsureMigrated(context);
        }
    }
}

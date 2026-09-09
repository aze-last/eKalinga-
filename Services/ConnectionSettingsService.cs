using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class DatabaseConnectionPreset
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 3306;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public sealed class ConnectionSettingsModel
    {
        public string SelectedPreset { get; set; } = "Local";
        public Dictionary<string, DatabaseConnectionPreset> Presets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public DatabaseConnectionPreset GetPreset(string presetKey)
        {
            if (!Presets.TryGetValue(presetKey, out var preset))
            {
                preset = new DatabaseConnectionPreset { Key = presetKey };
                Presets[presetKey] = preset;
            }

            if (string.IsNullOrWhiteSpace(preset.Key))
            {
                preset.Key = presetKey;
            }

            return preset;
        }

        public IEnumerable<KeyValuePair<string, DatabaseConnectionPreset>> GetLanPresets()
        {
            return Presets.Where(p => ConnectionSettingsService.IsLanPresetKey(p.Key));
        }

        public IEnumerable<KeyValuePair<string, DatabaseConnectionPreset>> GetActiveLanPresets()
        {
            return Presets.Where(p => ConnectionSettingsService.IsLanPresetKey(p.Key) && p.Value.IsEnabled);
        }
    }

    public sealed class ConnectionTestResult
    {
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public static class ConnectionSettingsService
    {
        public const string LocalPresetKey = "Local";
        public const string LanPresetKey = "Lan";
        public const string RemotePresetKey = "Remote";
        private const string DefaultAppPresetKey = "Local";
        private static readonly HashSet<string> AllowedActiveAppPresetKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            LocalPresetKey,
            LanPresetKey,
            RemotePresetKey
        };

        private static readonly HashSet<string> RuntimeEditablePresetKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            LanPresetKey,
            RemotePresetKey
        };

        public static bool IsLanPresetKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return string.Equals(key, LanPresetKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("Lan_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRuntimeEditablePreset(string presetKey)
        {
            return RuntimeEditablePresetKeys.Contains(presetKey) || IsLanPresetKey(presetKey);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static string GetEffectiveConnectionString()
        {
            var settings = Load();
            return BuildConnectionString(settings.GetPreset(settings.SelectedPreset));
        }

        public static ConnectionSettingsModel Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        internal static ConnectionSettingsModel Load(string runtimePath)
        {
            var settings = LoadDefaultsFromAppSettings();

            if (!File.Exists(runtimePath))
            {
                EnsureRequiredPresets(settings);
                NormalizeActiveAppPreset(settings);
                return settings;
            }

            try
            {
                var runtimeJson = File.ReadAllText(runtimePath);
                var runtimeSettings = JsonSerializer.Deserialize<ConnectionSettingsModel>(runtimeJson, JsonOptions);
                if (runtimeSettings == null)
                {
                    EnsureRequiredPresets(settings);
                    NormalizeActiveAppPreset(settings);
                    return settings;
                }

                if (!string.IsNullOrWhiteSpace(runtimeSettings.SelectedPreset))
                {
                    settings.SelectedPreset = runtimeSettings.SelectedPreset;
                }

                foreach (var preset in runtimeSettings.Presets)
                {
                    if (!IsRuntimeEditablePreset(preset.Key))
                    {
                        continue;
                    }

                    var loadedPreset = ClonePresetForUse(preset.Value);
                    loadedPreset.Key = preset.Key;
                    settings.Presets[preset.Key] = loadedPreset;
                }
            }
            catch
            {
                EnsureRequiredPresets(settings);
                NormalizeActiveAppPreset(settings);
                return settings;
            }

            EnsureRequiredPresets(settings);
            NormalizeActiveAppPreset(settings);
            return settings;
        }

        public static void Save(ConnectionSettingsModel settings)
        {
            Save(settings, GetRuntimeSettingsPath());
        }

        internal static void Save(ConnectionSettingsModel settings, string runtimePath)
        {
            EnsureRequiredPresets(settings);
            NormalizeActiveAppPreset(settings);

            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            var payload = new ConnectionSettingsModel
            {
                SelectedPreset = settings.SelectedPreset,
                Presets = settings.Presets
                    .Where(pair => IsRuntimeEditablePreset(pair.Key))
                    .ToDictionary(
                        pair => pair.Key,
                        pair => ClonePresetForStorage(pair.Value),
                        StringComparer.OrdinalIgnoreCase)
            };

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        public static async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionPreset preset, int timeoutSeconds = 5)
        {
            try
            {
                var connStr = BuildConnectionString(preset, guidFormatNone: false, timeoutSeconds: timeoutSeconds);
                await using var connection = new MySqlConnection(connStr);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 1));
                await connection.OpenAsync(cts.Token);
                await using var command = new MySqlCommand("SELECT DATABASE();", connection);
                var databaseName = (string?)await command.ExecuteScalarAsync(cts.Token) ?? preset.Database;

                return new ConnectionTestResult
                {
                    IsSuccess = true,
                    Message = $"Connected successfully to {databaseName} on {preset.Server}:{preset.Port}."
                };
            }
            catch (OperationCanceledException)
            {
                return new ConnectionTestResult
                {
                    IsSuccess = false,
                    Message = $"Connection timed out after {timeoutSeconds}s connecting to {preset.Server}:{preset.Port}."
                };
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult
                {
                    IsSuccess = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        public static bool IsPresetConfigured(DatabaseConnectionPreset preset)
        {
            ArgumentNullException.ThrowIfNull(preset);

            return !string.IsNullOrWhiteSpace(preset.Server)
                && preset.Port > 0
                && !string.IsNullOrWhiteSpace(preset.Database)
                && !string.IsNullOrWhiteSpace(preset.Username);
        }

        public static string FormatPresetSummary(DatabaseConnectionPreset preset)
        {
            ArgumentNullException.ThrowIfNull(preset);

            var displayName = string.IsNullOrWhiteSpace(preset.DisplayName)
                ? "Database preset"
                : preset.DisplayName.Trim();

            return IsPresetConfigured(preset)
                ? $"Target: {displayName}"
                : $"{displayName}: not configured yet.";
        }

        public static string BuildConnectionString(DatabaseConnectionPreset preset)
        {
            return BuildConnectionString(preset, guidFormatNone: false, timeoutSeconds: 15);
        }

        public static string BuildConnectionString(DatabaseConnectionPreset preset, bool guidFormatNone)
        {
            return BuildConnectionString(preset, guidFormatNone, timeoutSeconds: 15);
        }

        public static string BuildConnectionString(DatabaseConnectionPreset preset, bool guidFormatNone, int timeoutSeconds)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = preset.Server,
                Port = (uint)Math.Max(1, preset.Port),
                Database = preset.Database,
                UserID = preset.Username,
                Password = preset.Password,
                CharacterSet = "utf8mb4",
                ConnectionTimeout = (uint)Math.Max(1, timeoutSeconds),
                DefaultCommandTimeout = 300,
                AllowLoadLocalInfile = true,
                Keepalive = 30,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                AllowPublicKeyRetrieval = true,
                SslMode = MySqlSslMode.Preferred
            };

            if (guidFormatNone)
            {
                // CRS contract requirement: SyncId columns are CHAR(36); without
                // GuidFormat=None MySqlConnector tries to auto-map them to System.Guid.
                builder.GuidFormat = MySqlGuidFormat.None;
            }

            return builder.ConnectionString;
        }

        private static ConnectionSettingsModel LoadDefaultsFromAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var settings = new ConnectionSettingsModel
            {
                SelectedPreset = configuration["ConnectionSettings:SelectedPreset"] ?? InferSelectedPreset(configuration.GetConnectionString("DefaultConnection"))
            };

            foreach (var presetSection in configuration.GetSection("ConnectionSettings:Presets").GetChildren())
            {
                var preset = presetSection.Get<DatabaseConnectionPreset>();
                if (preset == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(preset.DisplayName))
                {
                    preset.DisplayName = presetSection.Key;
                }

                settings.Presets[presetSection.Key] = ClonePresetForUse(preset);
            }

            EnsureRequiredPresets(settings);
            return settings;
        }

        private static void EnsureRequiredPresets(ConnectionSettingsModel settings)
        {
            if (!settings.Presets.ContainsKey(LocalPresetKey))
            {
                settings.Presets[LocalPresetKey] = new DatabaseConnectionPreset
                {
                    DisplayName = "Local",
                    Server = "127.0.0.1",
                    Port = 3306,
                    Database = "attendance_shifting_db",
                    Username = "root",
                    Password = "codenameHylux122818"
                };
            }

            if (!settings.Presets.ContainsKey(LanPresetKey))
            {
                settings.Presets[LanPresetKey] = new DatabaseConnectionPreset
                {
                    DisplayName = "Network (LAN)",
                    Server = string.Empty,
                    Port = 3306,
                    Database = "attendance_shifting_db",
                    Username = "root",
                    Password = string.Empty
                };
            }

            if (!settings.Presets.ContainsKey(RemotePresetKey))
            {
                settings.Presets[RemotePresetKey] = new DatabaseConnectionPreset
                {
                    DisplayName = "Remote",
                    Server = "194.59.164.58",
                    Port = 3306,
                    Database = "u621755393_ams",
                    Username = "u621755393_ams_user",
                    Password = "Ams@2026"
                };
            }

            if (string.IsNullOrWhiteSpace(settings.SelectedPreset) || !settings.Presets.ContainsKey(settings.SelectedPreset))
            {
                settings.SelectedPreset = DefaultAppPresetKey;
            }
        }

        private static void NormalizeActiveAppPreset(ConnectionSettingsModel settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SelectedPreset))
            {
                settings.SelectedPreset = DefaultAppPresetKey;
                return;
            }

            if (!AllowedActiveAppPresetKeys.Contains(settings.SelectedPreset)
                && !IsLanPresetKey(settings.SelectedPreset))
            {
                settings.SelectedPreset = DefaultAppPresetKey;
            }
        }

        private static DatabaseConnectionPreset ClonePresetForUse(DatabaseConnectionPreset preset)
        {
            return new DatabaseConnectionPreset
            {
                Key = preset.Key,
                DisplayName = preset.DisplayName,
                Server = preset.Server,
                Port = preset.Port,
                Database = preset.Database,
                Username = preset.Username,
                Password = ConnectionSecretProtector.Unprotect(preset.Password),
                IsEnabled = preset.IsEnabled
            };
        }

        private static DatabaseConnectionPreset ClonePresetForStorage(DatabaseConnectionPreset preset)
        {
            return new DatabaseConnectionPreset
            {
                Key = preset.Key,
                DisplayName = preset.DisplayName,
                Server = preset.Server,
                Port = preset.Port,
                Database = preset.Database,
                Username = preset.Username,
                Password = ConnectionSecretProtector.Protect(preset.Password),
                IsEnabled = preset.IsEnabled
            };
        }

        private static string InferSelectedPreset(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return "Local";
            }

            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (builder.Server is "127.0.0.1" or "localhost")
            {
                return "Local";
            }

            return "Remote";
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

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "connectionsettings.json");
        }
    }
}

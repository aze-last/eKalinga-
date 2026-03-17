using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class DatabaseConnectionPreset
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 3306;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class ConnectionSettingsModel
    {
        public string SelectedPreset { get; set; } = "Remote";
        public Dictionary<string, DatabaseConnectionPreset> Presets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public DatabaseConnectionPreset GetPreset(string presetKey)
        {
            if (!Presets.TryGetValue(presetKey, out var preset))
            {
                preset = new DatabaseConnectionPreset();
                Presets[presetKey] = preset;
            }

            return preset;
        }
    }

    public sealed class ConnectionTestResult
    {
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public static class ConnectionSettingsService
    {
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
            var settings = LoadDefaultsFromAppSettings();

            var runtimePath = GetRuntimeSettingsPath();
            if (!File.Exists(runtimePath))
            {
                EnsureRequiredPresets(settings);
                return settings;
            }

            try
            {
                var runtimeJson = File.ReadAllText(runtimePath);
                var runtimeSettings = JsonSerializer.Deserialize<ConnectionSettingsModel>(runtimeJson, JsonOptions);
                if (runtimeSettings == null)
                {
                    EnsureRequiredPresets(settings);
                    return settings;
                }

                if (!string.IsNullOrWhiteSpace(runtimeSettings.SelectedPreset))
                {
                    settings.SelectedPreset = runtimeSettings.SelectedPreset;
                }

                foreach (var preset in runtimeSettings.Presets)
                {
                    settings.Presets[preset.Key] = ClonePreset(preset.Value);
                }
            }
            catch
            {
                EnsureRequiredPresets(settings);
                return settings;
            }

            EnsureRequiredPresets(settings);
            return settings;
        }

        public static void Save(ConnectionSettingsModel settings)
        {
            EnsureRequiredPresets(settings);

            var runtimePath = GetRuntimeSettingsPath();
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            var payload = new ConnectionSettingsModel
            {
                SelectedPreset = settings.SelectedPreset,
                Presets = settings.Presets.ToDictionary(
                    pair => pair.Key,
                    pair => ClonePreset(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
            };

            File.WriteAllText(runtimePath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        public static async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionPreset preset)
        {
            try
            {
                await using var connection = new MySqlConnection(BuildConnectionString(preset));
                await connection.OpenAsync();
                await using var command = new MySqlCommand("SELECT DATABASE();", connection);
                var databaseName = (string?)await command.ExecuteScalarAsync() ?? preset.Database;

                return new ConnectionTestResult
                {
                    IsSuccess = true,
                    Message = $"Connected successfully to {databaseName} on {preset.Server}:{preset.Port}."
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

        public static string BuildConnectionString(DatabaseConnectionPreset preset)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = preset.Server,
                Port = (uint)Math.Max(1, preset.Port),
                Database = preset.Database,
                UserID = preset.Username,
                Password = preset.Password,
                CharacterSet = "utf8mb4",
                ConnectionTimeout = 5
            };

            return builder.ConnectionString;
        }

        private static ConnectionSettingsModel LoadDefaultsFromAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: false)
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

                settings.Presets[presetSection.Key] = preset;
            }

            EnsureRequiredPresets(settings);
            return settings;
        }

        private static void EnsureRequiredPresets(ConnectionSettingsModel settings)
        {
            if (!settings.Presets.ContainsKey("Local"))
            {
                settings.Presets["Local"] = new DatabaseConnectionPreset
                {
                    DisplayName = "Local",
                    Server = "127.0.0.1",
                    Port = 3306,
                    Database = "attendance_shifting_db",
                    Username = "root",
                    Password = "codenameHylux122818"
                };
            }

            if (!settings.Presets.ContainsKey("Lan"))
            {
                settings.Presets["Lan"] = new DatabaseConnectionPreset
                {
                    DisplayName = "Network (LAN)",
                    Server = "192.168.1.2",
                    Port = 3306,
                    Database = "attendance_shifting_db",
                    Username = "lan_client",
                    Password = "client123"
                };
            }

            if (!settings.Presets.ContainsKey("Remote"))
            {
                settings.Presets["Remote"] = new DatabaseConnectionPreset
                {
                    DisplayName = "Remote (Hostinger)",
                    Server = "194.59.164.58",
                    Port = 3306,
                    Database = "u621755393_ams",
                    Username = "u621755393_ams_user",
                    Password = "Ams@2026"
                };
            }

            if (string.IsNullOrWhiteSpace(settings.SelectedPreset) || !settings.Presets.ContainsKey(settings.SelectedPreset))
            {
                settings.SelectedPreset = "Remote";
            }
        }

        private static DatabaseConnectionPreset ClonePreset(DatabaseConnectionPreset preset)
        {
            return new DatabaseConnectionPreset
            {
                DisplayName = preset.DisplayName,
                Server = preset.Server,
                Port = preset.Port,
                Database = preset.Database,
                Username = preset.Username,
                Password = preset.Password
            };
        }

        private static string InferSelectedPreset(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return "Remote";
            }

            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (builder.Server is "127.0.0.1" or "localhost")
            {
                return "Local";
            }

            if (builder.Server.StartsWith("192.168.", StringComparison.OrdinalIgnoreCase))
            {
                return "Lan";
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

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

        public DatabaseConnectionPreset RemoteConnection { get; set; } = new()
        {
            DisplayName = "e-Kard CRS Verification Source (Remote)",
            Port = 3306
        };

        public DatabaseConnectionPreset LanConnection { get; set; } = new()
        {
            DisplayName = "e-Kard CRS Verification Source (LAN)",
            Port = 3306
        };

        private DatabaseConnectionPreset _crsContractConnection = new()
        {
            DisplayName = "e-Kard CRS Verification Source",
            Port = 3306
        };

        public DatabaseConnectionPreset CrsContractConnection
        {
            get => GetEffectiveConnection();
            set
            {
                _crsContractConnection = value ?? new DatabaseConnectionPreset();
                if (IsLocalOrLanHost(_crsContractConnection.Server))
                {
                    LanConnection = ClonePreset(_crsContractConnection);
                }
                else if (ConnectionSettingsService.IsPresetConfigured(_crsContractConnection))
                {
                    RemoteConnection = ClonePreset(_crsContractConnection);
                }
            }
        }

        public DatabaseConnectionPreset GetEffectiveConnection(string? activePresetKey = null)
        {
            var presetKey = activePresetKey;
            if (string.IsNullOrWhiteSpace(presetKey))
            {
                try
                {
                    presetKey = ConnectionSettingsService.Load().SelectedPreset;
                }
                catch
                {
                    presetKey = ConnectionSettingsService.RemotePresetKey;
                }
            }

            // CRS is an independent verification database connection.
            // Do NOT hijack or force CRS to local LAN when the app's database preset is LAN.
            if (ConnectionSettingsService.IsPresetConfigured(_crsContractConnection) && !IsPlaceholder(_crsContractConnection.Server))
            {
                return _crsContractConnection;
            }

            if (!string.IsNullOrWhiteSpace(RemoteConnection.Server) && !IsPlaceholder(RemoteConnection.Server))
            {
                return RemoteConnection;
            }

            if (!string.IsNullOrWhiteSpace(LanConnection.Server) && !IsPlaceholder(LanConnection.Server))
            {
                return LanConnection;
            }

            return _crsContractConnection;
        }

        public static bool IsLocalOrLanHost(string? server)
        {
            if (string.IsNullOrWhiteSpace(server)) return false;
            server = server.Trim();
            return server.StartsWith("192.168.", StringComparison.OrdinalIgnoreCase)
                || server.StartsWith("10.", StringComparison.OrdinalIgnoreCase)
                || server.StartsWith("172.16.", StringComparison.OrdinalIgnoreCase)
                || server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || server.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlaceholder(string? server)
        {
            if (string.IsNullOrWhiteSpace(server)) return true;
            return server.Contains("YOUR_REMOTE_SERVER", StringComparison.OrdinalIgnoreCase)
                || server.Contains("YOUR_CRS_DB", StringComparison.OrdinalIgnoreCase);
        }

        public static DatabaseConnectionPreset ClonePreset(DatabaseConnectionPreset source)
        {
            return new DatabaseConnectionPreset
            {
                DisplayName = source.DisplayName,
                Server = source.Server,
                Port = source.Port,
                Database = source.Database,
                Username = source.Username,
                Password = source.Password,
                IsEnabled = source.IsEnabled
            };
        }

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

                runtimeSettings.RemoteConnection ??= new DatabaseConnectionPreset();
                runtimeSettings.RemoteConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.RemoteConnection.Password);

                runtimeSettings.LanConnection ??= new DatabaseConnectionPreset();
                runtimeSettings.LanConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.LanConnection.Password);

                runtimeSettings._crsContractConnection ??= new DatabaseConnectionPreset();
                runtimeSettings._crsContractConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings._crsContractConnection.Password);

                if (ConnectionSettingsService.IsPresetConfigured(defaults._crsContractConnection) && !IsPlaceholder(defaults._crsContractConnection.Server))
                {
                    if (!ConnectionSettingsService.IsPresetConfigured(runtimeSettings._crsContractConnection) ||
                        IsPlaceholder(runtimeSettings._crsContractConnection.Server) ||
                        !string.Equals(runtimeSettings._crsContractConnection.Server, defaults._crsContractConnection.Server, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(runtimeSettings._crsContractConnection.Database, defaults._crsContractConnection.Database, StringComparison.OrdinalIgnoreCase))
                    {
                        runtimeSettings._crsContractConnection = ClonePreset(defaults._crsContractConnection);
                        runtimeSettings.RemoteConnection = ClonePreset(defaults._crsContractConnection);
                    }
                }

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

            var effective = normalized.GetEffectiveConnection();
            var payload = new CrsContractRuntimeOptions
            {
                RemoteConnection = new DatabaseConnectionPreset
                {
                    DisplayName = normalized.RemoteConnection.DisplayName,
                    Server = normalized.RemoteConnection.Server,
                    Port = normalized.RemoteConnection.Port,
                    Database = normalized.RemoteConnection.Database,
                    Username = normalized.RemoteConnection.Username,
                    Password = ConnectionSecretProtector.Protect(normalized.RemoteConnection.Password)
                },
                LanConnection = new DatabaseConnectionPreset
                {
                    DisplayName = normalized.LanConnection.DisplayName,
                    Server = normalized.LanConnection.Server,
                    Port = normalized.LanConnection.Port,
                    Database = normalized.LanConnection.Database,
                    Username = normalized.LanConnection.Username,
                    Password = ConnectionSecretProtector.Protect(normalized.LanConnection.Password)
                },
                _crsContractConnection = new DatabaseConnectionPreset
                {
                    DisplayName = effective.DisplayName,
                    Server = effective.Server,
                    Port = effective.Port,
                    Database = effective.Database,
                    Username = effective.Username,
                    Password = ConnectionSecretProtector.Protect(effective.Password)
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
                RemoteConnection = configuration.GetSection("CrsContractConnection").Get<DatabaseConnectionPreset>()
                    ?? new DatabaseConnectionPreset(),
                _crsContractConnection = configuration.GetSection("CrsContractConnection").Get<DatabaseConnectionPreset>()
                    ?? new DatabaseConnectionPreset()
            };
            defaults.RemoteConnection.Password = ConnectionSecretProtector.Unprotect(defaults.RemoteConnection.Password);
            defaults._crsContractConnection.Password = ConnectionSecretProtector.Unprotect(defaults._crsContractConnection.Password);
            return Normalize(defaults);
        }

        private static CrsContractRuntimeOptions Normalize(CrsContractRuntimeOptions? settings)
        {
            settings ??= new CrsContractRuntimeOptions();

            settings._crsContractConnection ??= new DatabaseConnectionPreset();
            settings._crsContractConnection.DisplayName = string.IsNullOrWhiteSpace(settings._crsContractConnection.DisplayName)
                ? "e-Kard CRS Verification Source"
                : settings._crsContractConnection.DisplayName.Trim();
            settings._crsContractConnection.Server = settings._crsContractConnection.Server?.Trim() ?? string.Empty;
            settings._crsContractConnection.Database = settings._crsContractConnection.Database?.Trim() ?? string.Empty;
            settings._crsContractConnection.Username = settings._crsContractConnection.Username?.Trim() ?? string.Empty;
            settings._crsContractConnection.Password = settings._crsContractConnection.Password ?? string.Empty;
            if (settings._crsContractConnection.Port <= 0) settings._crsContractConnection.Port = 3306;

            settings.RemoteConnection ??= new DatabaseConnectionPreset { DisplayName = "e-Kard CRS Verification Source (Remote)", Port = 3306 };
            settings.RemoteConnection.DisplayName = string.IsNullOrWhiteSpace(settings.RemoteConnection.DisplayName)
                ? "e-Kard CRS Verification Source (Remote)"
                : settings.RemoteConnection.DisplayName.Trim();
            settings.RemoteConnection.Server = settings.RemoteConnection.Server?.Trim() ?? string.Empty;
            settings.RemoteConnection.Database = settings.RemoteConnection.Database?.Trim() ?? string.Empty;
            settings.RemoteConnection.Username = settings.RemoteConnection.Username?.Trim() ?? string.Empty;
            settings.RemoteConnection.Password = settings.RemoteConnection.Password ?? string.Empty;
            if (settings.RemoteConnection.Port <= 0) settings.RemoteConnection.Port = 3306;

            settings.LanConnection ??= new DatabaseConnectionPreset { DisplayName = "e-Kard CRS Verification Source (LAN)", Port = 3306 };
            settings.LanConnection.DisplayName = string.IsNullOrWhiteSpace(settings.LanConnection.DisplayName)
                ? "e-Kard CRS Verification Source (LAN)"
                : settings.LanConnection.DisplayName.Trim();
            settings.LanConnection.Server = settings.LanConnection.Server?.Trim() ?? string.Empty;
            settings.LanConnection.Database = settings.LanConnection.Database?.Trim() ?? string.Empty;
            settings.LanConnection.Username = settings.LanConnection.Username?.Trim() ?? string.Empty;
            settings.LanConnection.Password = settings.LanConnection.Password ?? string.Empty;
            if (settings.LanConnection.Port <= 0) settings.LanConnection.Port = 3306;

            if (!ConnectionSettingsService.IsPresetConfigured(settings.RemoteConnection) || IsPlaceholder(settings.RemoteConnection.Server))
            {
                if (ConnectionSettingsService.IsPresetConfigured(settings._crsContractConnection) && !IsLocalOrLanHost(settings._crsContractConnection.Server) && !IsPlaceholder(settings._crsContractConnection.Server))
                {
                    settings.RemoteConnection = ClonePreset(settings._crsContractConnection);
                }
            }

            if (!ConnectionSettingsService.IsPresetConfigured(settings.LanConnection))
            {
                if (ConnectionSettingsService.IsPresetConfigured(settings._crsContractConnection) && IsLocalOrLanHost(settings._crsContractConnection.Server))
                {
                    settings.LanConnection = ClonePreset(settings._crsContractConnection);
                }
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

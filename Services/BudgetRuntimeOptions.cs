using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class BudgetRuntimeOptions
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static readonly DatabaseConnectionPreset DefaultRemoteConnection = new()
        {
            DisplayName = "GGMS Budget Source (Remote)",
            Server = "193.203.175.157",
            Port = 3306,
            Database = "u518908950_ggms",
            Username = "u518908950_ggms",
            Password = "Sulop@2025"
        };

        public string AyudaOfficeCode { get; set; } = string.Empty;
        public string GgmsOfficeTable { get; set; } = "tbl_offices";
        public string GgmsBudgetAllocationTable { get; set; } = "budget_allocations";
        public string GgmsAllocationTable { get; set; } = "officeallocations";
        public string GgmsConsolidatedTransactionTable { get; set; } = "consolidated_transactions";
        public string GgmsProjectDetailsTable { get; set; } = "project_details";

        public DatabaseConnectionPreset RemoteConnection { get; set; } = ClonePreset(DefaultRemoteConnection);

        public DatabaseConnectionPreset LanConnection { get; set; } = new()
        {
            DisplayName = "GGMS Budget Source (LAN)",
            Port = 3306
        };

        private DatabaseConnectionPreset _ggmsConnection = new()
        {
            DisplayName = "GGMS Budget Source",
            Port = 3306
        };

        public DatabaseConnectionPreset GgmsConnection
        {
            get => GetEffectiveConnection();
            set
            {
                _ggmsConnection = value ?? new DatabaseConnectionPreset();
                if (IsLocalOrLanHost(_ggmsConnection.Server))
                {
                    LanConnection = ClonePreset(_ggmsConnection);
                }
                else if (ConnectionSettingsService.IsPresetConfigured(_ggmsConnection) && !IsPlaceholder(_ggmsConnection.Server))
                {
                    RemoteConnection = ClonePreset(_ggmsConnection);
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

            // GGMS is an independent municipal database connection.
            // Do NOT hijack or force GGMS to local LAN when the app's database preset is LAN.
            if (ConnectionSettingsService.IsPresetConfigured(_ggmsConnection) && !IsPlaceholder(_ggmsConnection.Server))
            {
                return _ggmsConnection;
            }

            if (!string.IsNullOrWhiteSpace(RemoteConnection.Server) && !IsPlaceholder(RemoteConnection.Server))
            {
                return RemoteConnection;
            }

            if (!string.IsNullOrWhiteSpace(LanConnection.Server) && !IsPlaceholder(LanConnection.Server))
            {
                return LanConnection;
            }

            return ClonePreset(DefaultRemoteConnection);
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
                || server.Contains("YOUR_GGMS_DB", StringComparison.OrdinalIgnoreCase);
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

        public static BudgetRuntimeOptions Load()
        {
            return Load(GetRuntimeSettingsPath());
        }

        internal static BudgetRuntimeOptions Load(string runtimePath)
        {
            var defaults = LoadDefaultsFromAppSettings();
            if (!File.Exists(runtimePath))
            {
                return defaults;
            }

            try
            {
                var runtimeJson = File.ReadAllText(runtimePath);
                var runtimeSettings = JsonSerializer.Deserialize<BudgetRuntimeOptions>(runtimeJson, JsonOptions);
                if (runtimeSettings == null)
                {
                    return defaults;
                }

                runtimeSettings.RemoteConnection ??= new DatabaseConnectionPreset();
                runtimeSettings.RemoteConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.RemoteConnection.Password);

                runtimeSettings.LanConnection ??= new DatabaseConnectionPreset();
                runtimeSettings.LanConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.LanConnection.Password);

                runtimeSettings._ggmsConnection ??= new DatabaseConnectionPreset();
                runtimeSettings._ggmsConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings._ggmsConnection.Password);

                if (ConnectionSettingsService.IsPresetConfigured(defaults._ggmsConnection) && !IsPlaceholder(defaults._ggmsConnection.Server))
                {
                    if (!ConnectionSettingsService.IsPresetConfigured(runtimeSettings._ggmsConnection) ||
                        IsPlaceholder(runtimeSettings._ggmsConnection.Server) ||
                        !string.Equals(runtimeSettings._ggmsConnection.Server, defaults._ggmsConnection.Server, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(runtimeSettings._ggmsConnection.Database, defaults._ggmsConnection.Database, StringComparison.OrdinalIgnoreCase))
                    {
                        runtimeSettings._ggmsConnection = ClonePreset(defaults._ggmsConnection);
                        runtimeSettings.RemoteConnection = ClonePreset(defaults._ggmsConnection);
                    }
                }

                return Normalize(runtimeSettings);
            }
            catch
            {
                return defaults;
            }
        }

        public static void Save(BudgetRuntimeOptions settings)
        {
            Save(settings, GetRuntimeSettingsPath());
        }

        internal static void Save(BudgetRuntimeOptions settings, string runtimePath)
        {
            var normalized = Normalize(settings);
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                Directory.CreateDirectory(runtimeDirectory);
            }

            var effective = normalized.GetEffectiveConnection();
            var payload = new BudgetRuntimeOptions
            {
                AyudaOfficeCode = normalized.AyudaOfficeCode,
                GgmsOfficeTable = normalized.GgmsOfficeTable,
                GgmsBudgetAllocationTable = normalized.GgmsBudgetAllocationTable,
                GgmsAllocationTable = normalized.GgmsAllocationTable,
                GgmsConsolidatedTransactionTable = normalized.GgmsConsolidatedTransactionTable,
                GgmsProjectDetailsTable = normalized.GgmsProjectDetailsTable,
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
                _ggmsConnection = new DatabaseConnectionPreset
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

        private static BudgetRuntimeOptions LoadDefaultsFromAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(ResolveConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var defaults = configuration.GetSection("Budget").Get<BudgetRuntimeOptions>() ?? new BudgetRuntimeOptions();
            
            var ggmsConfig = configuration.GetSection("Budget:GgmsConnection").Get<DatabaseConnectionPreset>();
            if (ggmsConfig != null && ConnectionSettingsService.IsPresetConfigured(ggmsConfig) && !IsPlaceholder(ggmsConfig.Server))
            {
                defaults._ggmsConnection = ggmsConfig;
            }
            else
            {
                var muniConfig = configuration.GetSection("MunicipalityImportConnection").Get<DatabaseConnectionPreset>();
                if (muniConfig != null && ConnectionSettingsService.IsPresetConfigured(muniConfig) && !IsPlaceholder(muniConfig.Server))
                {
                    defaults._ggmsConnection = muniConfig;
                    defaults._ggmsConnection.DisplayName = "GGMS Budget Source";
                }
            }

            if (ConnectionSettingsService.IsPresetConfigured(defaults._ggmsConnection) && !IsPlaceholder(defaults._ggmsConnection.Server))
            {
                defaults.RemoteConnection = ClonePreset(defaults._ggmsConnection);
            }
            else
            {
                defaults.RemoteConnection ??= ClonePreset(DefaultRemoteConnection);
            }

            defaults.LanConnection ??= new DatabaseConnectionPreset { DisplayName = "GGMS Budget Source (LAN)", Port = 3306 };
            defaults._ggmsConnection ??= new DatabaseConnectionPreset();
            defaults._ggmsConnection.Password = ConnectionSecretProtector.Unprotect(defaults._ggmsConnection.Password);
            defaults.RemoteConnection.Password = ConnectionSecretProtector.Unprotect(defaults.RemoteConnection.Password);
            defaults.LanConnection.Password = ConnectionSecretProtector.Unprotect(defaults.LanConnection.Password);

            return Normalize(defaults);
        }

        private static BudgetRuntimeOptions Normalize(BudgetRuntimeOptions? settings)
        {
            settings ??= new BudgetRuntimeOptions();
            settings.AyudaOfficeCode = settings.AyudaOfficeCode?.Trim() ?? string.Empty;
            settings.GgmsOfficeTable = string.IsNullOrWhiteSpace(settings.GgmsOfficeTable)
                ? "tbl_offices"
                : settings.GgmsOfficeTable.Trim();
            settings.GgmsBudgetAllocationTable = string.IsNullOrWhiteSpace(settings.GgmsBudgetAllocationTable)
                ? "budget_allocations"
                : settings.GgmsBudgetAllocationTable.Trim();
            settings.GgmsAllocationTable = string.IsNullOrWhiteSpace(settings.GgmsAllocationTable)
                ? "officeallocations"
                : settings.GgmsAllocationTable.Trim();
            settings.GgmsConsolidatedTransactionTable = string.IsNullOrWhiteSpace(settings.GgmsConsolidatedTransactionTable)
                ? "consolidated_transactions"
                : settings.GgmsConsolidatedTransactionTable.Trim();
            settings.GgmsProjectDetailsTable = string.IsNullOrWhiteSpace(settings.GgmsProjectDetailsTable)
                ? "project_details"
                : settings.GgmsProjectDetailsTable.Trim();

            settings._ggmsConnection ??= new DatabaseConnectionPreset();
            settings._ggmsConnection.DisplayName = string.IsNullOrWhiteSpace(settings._ggmsConnection.DisplayName)
                ? "GGMS Budget Source"
                : settings._ggmsConnection.DisplayName.Trim();
            settings._ggmsConnection.Server = settings._ggmsConnection.Server?.Trim() ?? string.Empty;
            settings._ggmsConnection.Database = settings._ggmsConnection.Database?.Trim() ?? string.Empty;
            settings._ggmsConnection.Username = settings._ggmsConnection.Username?.Trim() ?? string.Empty;
            settings._ggmsConnection.Password = settings._ggmsConnection.Password ?? string.Empty;
            if (settings._ggmsConnection.Port <= 0) settings._ggmsConnection.Port = 3306;

            settings.RemoteConnection ??= ClonePreset(DefaultRemoteConnection);
            settings.RemoteConnection.DisplayName = string.IsNullOrWhiteSpace(settings.RemoteConnection.DisplayName)
                ? "GGMS Budget Source (Remote)"
                : settings.RemoteConnection.DisplayName.Trim();
            settings.RemoteConnection.Server = settings.RemoteConnection.Server?.Trim() ?? string.Empty;
            settings.RemoteConnection.Database = settings.RemoteConnection.Database?.Trim() ?? string.Empty;
            settings.RemoteConnection.Username = settings.RemoteConnection.Username?.Trim() ?? string.Empty;
            settings.RemoteConnection.Password = settings.RemoteConnection.Password ?? string.Empty;
            if (settings.RemoteConnection.Port <= 0) settings.RemoteConnection.Port = 3306;

            settings.LanConnection ??= new DatabaseConnectionPreset { DisplayName = "GGMS Budget Source (LAN)", Port = 3306 };
            settings.LanConnection.DisplayName = string.IsNullOrWhiteSpace(settings.LanConnection.DisplayName)
                ? "GGMS Budget Source (LAN)"
                : settings.LanConnection.DisplayName.Trim();
            settings.LanConnection.Server = settings.LanConnection.Server?.Trim() ?? string.Empty;
            settings.LanConnection.Database = settings.LanConnection.Database?.Trim() ?? string.Empty;
            settings.LanConnection.Username = settings.LanConnection.Username?.Trim() ?? string.Empty;
            settings.LanConnection.Password = settings.LanConnection.Password ?? string.Empty;
            if (settings.LanConnection.Port <= 0) settings.LanConnection.Port = 3306;

            // If an older configuration had only _ggmsConnection set:
            if (!ConnectionSettingsService.IsPresetConfigured(settings.RemoteConnection) || IsPlaceholder(settings.RemoteConnection.Server))
            {
                if (ConnectionSettingsService.IsPresetConfigured(settings._ggmsConnection) && !IsLocalOrLanHost(settings._ggmsConnection.Server) && !IsPlaceholder(settings._ggmsConnection.Server))
                {
                    settings.RemoteConnection = ClonePreset(settings._ggmsConnection);
                }
                else
                {
                    settings.RemoteConnection = ClonePreset(DefaultRemoteConnection);
                }
            }

            if (!ConnectionSettingsService.IsPresetConfigured(settings.LanConnection))
            {
                if (ConnectionSettingsService.IsPresetConfigured(settings._ggmsConnection) && IsLocalOrLanHost(settings._ggmsConnection.Server))
                {
                    settings.LanConnection = ClonePreset(settings._ggmsConnection);
                }
            }

            return settings;
        }

        private static string GetRuntimeSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AttendanceShiftingManagement",
                "ggmssettings.json");
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

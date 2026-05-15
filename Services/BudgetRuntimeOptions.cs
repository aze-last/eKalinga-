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

        public string AyudaOfficeCode { get; set; } = string.Empty;
        public string GgmsOfficeTable { get; set; } = "tbl_offices";
        public string GgmsAllocationTable { get; set; } = "officeallocations";
        public string GgmsConsolidatedTransactionTable { get; set; } = "consolidated_transactions";
        public DatabaseConnectionPreset GgmsConnection { get; set; } = new()
        {
            DisplayName = "GGMS Budget Source",
            Port = 3306
        };

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

                runtimeSettings.GgmsConnection ??= new DatabaseConnectionPreset();
                runtimeSettings.GgmsConnection.Password = ConnectionSecretProtector.Unprotect(runtimeSettings.GgmsConnection.Password);
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

            var payload = new BudgetRuntimeOptions
            {
                AyudaOfficeCode = normalized.AyudaOfficeCode,
                GgmsOfficeTable = normalized.GgmsOfficeTable,
                GgmsAllocationTable = normalized.GgmsAllocationTable,
                GgmsConsolidatedTransactionTable = normalized.GgmsConsolidatedTransactionTable,
                GgmsConnection = new DatabaseConnectionPreset
                {
                    DisplayName = normalized.GgmsConnection.DisplayName,
                    Server = normalized.GgmsConnection.Server,
                    Port = normalized.GgmsConnection.Port,
                    Database = normalized.GgmsConnection.Database,
                    Username = normalized.GgmsConnection.Username,
                    Password = ConnectionSecretProtector.Protect(normalized.GgmsConnection.Password)
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
            defaults.GgmsConnection ??= new DatabaseConnectionPreset();
            defaults.GgmsConnection.Password = ConnectionSecretProtector.Unprotect(defaults.GgmsConnection.Password);
            return Normalize(defaults);
        }

        private static BudgetRuntimeOptions Normalize(BudgetRuntimeOptions? settings)
        {
            settings ??= new BudgetRuntimeOptions();
            settings.AyudaOfficeCode = settings.AyudaOfficeCode?.Trim() ?? string.Empty;
            settings.GgmsOfficeTable = string.IsNullOrWhiteSpace(settings.GgmsOfficeTable)
                ? "tbl_offices"
                : settings.GgmsOfficeTable.Trim();
            settings.GgmsAllocationTable = string.IsNullOrWhiteSpace(settings.GgmsAllocationTable)
                ? "officeallocations"
                : settings.GgmsAllocationTable.Trim();
            settings.GgmsConsolidatedTransactionTable = string.IsNullOrWhiteSpace(settings.GgmsConsolidatedTransactionTable)
                ? "consolidated_transactions"
                : settings.GgmsConsolidatedTransactionTable.Trim();

            settings.GgmsConnection ??= new DatabaseConnectionPreset();
            settings.GgmsConnection.DisplayName = string.IsNullOrWhiteSpace(settings.GgmsConnection.DisplayName)
                ? "GGMS Budget Source"
                : settings.GgmsConnection.DisplayName.Trim();
            settings.GgmsConnection.Server = settings.GgmsConnection.Server?.Trim() ?? string.Empty;
            settings.GgmsConnection.Database = settings.GgmsConnection.Database?.Trim() ?? string.Empty;
            settings.GgmsConnection.Username = settings.GgmsConnection.Username?.Trim() ?? string.Empty;
            settings.GgmsConnection.Password = settings.GgmsConnection.Password ?? string.Empty;
            if (settings.GgmsConnection.Port <= 0)
            {
                settings.GgmsConnection.Port = 3306;
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

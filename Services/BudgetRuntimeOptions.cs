using Microsoft.Extensions.Configuration;

namespace AttendanceShiftingManagement.Services
{
    public sealed class BudgetRuntimeOptions
    {
        public string AyudaOfficeCode { get; set; } = "OFF-2026-0006";
        public string GgmsOfficeTable { get; set; } = "tbl_offices";
        public string GgmsAllocationTable { get; set; } = "officeallocations";
        public DatabaseConnectionPreset GgmsConnection { get; set; } = new()
        {
            DisplayName = "GGMS Budget Source",
            Port = 3306
        };

        public static BudgetRuntimeOptions Load()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = configuration
                .GetSection("Budget")
                .Get<BudgetRuntimeOptions>() ?? new BudgetRuntimeOptions();

            options.GgmsConnection ??= new DatabaseConnectionPreset();
            if (string.IsNullOrWhiteSpace(options.GgmsConnection.DisplayName))
            {
                options.GgmsConnection.DisplayName = "GGMS Budget Source";
            }

            if (options.GgmsConnection.Port <= 0)
            {
                options.GgmsConnection.Port = 3306;
            }

            return options;
        }
    }
}

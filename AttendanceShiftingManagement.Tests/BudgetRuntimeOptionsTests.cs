using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BudgetRuntimeOptionsTests
{
    [Fact]
    public void SaveAndLoad_WithRuntimeFile_EncryptsPasswordAtRest()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "ggmssettings.json");

        try
        {
            var expected = new BudgetRuntimeOptions
            {
                AyudaOfficeCode = "OFF-2026-0006",
                GgmsOfficeTable = "tbl_offices",
                GgmsAllocationTable = "officeallocations",
                GgmsConnection = new DatabaseConnectionPreset
                {
                    DisplayName = "GGMS Budget Source",
                    Server = "10.0.0.5",
                    Port = 3307,
                    Database = "ggms_db",
                    Username = "ggms-user",
                    Password = "plain-text-password"
                }
            };

            BudgetRuntimeOptions.Save(expected, runtimePath);

            var persistedJson = File.ReadAllText(runtimePath);
            Assert.DoesNotContain("plain-text-password", persistedJson, StringComparison.Ordinal);

            var loaded = BudgetRuntimeOptions.Load(runtimePath);
            Assert.Equal(expected.AyudaOfficeCode, loaded.AyudaOfficeCode);
            Assert.Equal(expected.GgmsOfficeTable, loaded.GgmsOfficeTable);
            Assert.Equal(expected.GgmsAllocationTable, loaded.GgmsAllocationTable);
            Assert.Equal(expected.GgmsConnection.Server, loaded.GgmsConnection.Server);
            Assert.Equal(expected.GgmsConnection.Port, loaded.GgmsConnection.Port);
            Assert.Equal(expected.GgmsConnection.Database, loaded.GgmsConnection.Database);
            Assert.Equal(expected.GgmsConnection.Username, loaded.GgmsConnection.Username);
            Assert.Equal(expected.GgmsConnection.Password, loaded.GgmsConnection.Password);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}

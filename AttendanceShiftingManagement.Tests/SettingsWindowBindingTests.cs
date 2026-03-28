namespace AttendanceShiftingManagement.Tests;

public sealed class SettingsWindowBindingTests
{
    [Fact]
    public void SettingsWindow_AppDatabaseTabExposesMigrationButton()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("MIGRATE LOCAL + HOSTINGER NOW", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding MigrateLocalAndRemoteCommand}\"", xaml, StringComparison.Ordinal);
    }
}

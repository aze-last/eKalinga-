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

        Assert.Contains("MIGRATE LOCAL + REMOTE NOW", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding MigrateLocalAndRemoteCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"GGMS Budget Source\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Updates\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CheckForUpdatesCommand}\"", xaml, StringComparison.Ordinal);
    }
}

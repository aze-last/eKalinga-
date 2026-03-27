namespace AttendanceShiftingManagement.Tests;

public sealed class SettingsWindowBindingTests
{
    [Fact]
    public void SystemInstallSerialTextBox_UsesOneWayBinding()
    {
        var settingsWindowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(settingsWindowPath);

        Assert.Contains("Text=\"{Binding SystemInstallSerial, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Company Serial Number", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemProfileTab_UsesScrollViewer()
    {
        var settingsWindowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(settingsWindowPath);

        Assert.Contains("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"System Profile\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseBackupTab_ShowsPerTypeBackupDates()
    {
        var settingsWindowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(settingsWindowPath);

        Assert.Contains("Last Full Backup", xaml, StringComparison.Ordinal);
        Assert.Contains("Last Incremental Backup", xaml, StringComparison.Ordinal);
        Assert.Contains("Last Differential Backup", xaml, StringComparison.Ordinal);
    }
}

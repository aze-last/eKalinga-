namespace AttendanceShiftingManagement.Tests;

public sealed class BarangayDashboardSyncSourceTests
{
    [Fact]
    public void BarangayDashboardPage_ExposesManualSyncButtonBetweenSettingsAndLogout()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

        var xaml = File.ReadAllText(Path.Combine(root, "Views", "BarangayDashboardPage.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "Views", "BarangayDashboardPage.xaml.cs"));
        var mainWindowCode = File.ReadAllText(Path.Combine(root, "Views", "MainWindow.xaml.cs"));

        Assert.Contains("Text=\"SYNC REMOTE AND LOCAL\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"SyncRemoteAndLocalButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("window.SyncRemoteAndLocalFromDashboardAsync()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SyncRemoteAndLocalFromDashboardAsync", mainWindowCode, StringComparison.Ordinal);
    }
}

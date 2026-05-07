namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionLivePreviewWindowSourceTests
{
    [Fact]
    public void ProjectDistributionLivePreviewWindow_DefinesSecondScreenMonitorLayout()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionLivePreviewWindow.xaml"));

        Assert.True(File.Exists(windowPath));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("WindowStyle=\"None\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WindowState=\"Maximized\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ResizeMode=\"NoResize\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LivePreviewPrimaryLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LivePreviewSecondaryLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LivePreviewProgramName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LivePreviewQueueStatusText}\"", xaml, StringComparison.Ordinal);
    }
}

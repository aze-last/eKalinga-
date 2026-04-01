namespace AttendanceShiftingManagement.Tests;

public sealed class DigitalIdPrintPreviewSourceTests
{
    [Fact]
    public void DigitalIdPrintService_UsesPreviewWindowBeforePrinting()
    {
        var servicePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "DigitalIdPrintService.cs"));

        var source = File.ReadAllText(servicePath);

        Assert.Contains("DigitalIdPrintPreviewWindow", source, StringComparison.Ordinal);
        Assert.Contains("previewWindow.ShowDialog()", source, StringComparison.Ordinal);
        Assert.Contains("dialog.PrintVisual(printCard", source, StringComparison.Ordinal);
        Assert.Contains("Stretch = Stretch.Uniform", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Stretch = Stretch.UniformToFill", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DigitalIdPrintPreviewWindow_RendersPreviewAndPrintActions()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "DigitalIdPrintPreviewWindow.xaml"));

        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Text=\"Digital ID Preview\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Review the beneficiary card layout before sending it to the printer.", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"PRINT\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"CLOSE\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewHost", xaml, StringComparison.Ordinal);
    }
}

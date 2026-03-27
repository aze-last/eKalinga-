namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkPageBindingTests
{
    [Fact]
    public void CashForWorkPage_BindsBudgetReleaseControls()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "CashForWorkOcrPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("ItemsSource=\"{Binding AyudaPrograms}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReleaseAmountText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ReleaseBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateAttendanceScannerSessionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionUrl, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionPin}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"{Binding AttendanceScannerQrImage}\"", xaml, StringComparison.Ordinal);
    }
}

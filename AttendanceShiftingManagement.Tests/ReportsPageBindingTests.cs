namespace AttendanceShiftingManagement.Tests;

public sealed class ReportsPageBindingTests
{
    [Fact]
    public void ReportsPage_BindsReportFiltersPreviewAndExportActions()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ReportsPage.xaml"));

        var xaml = File.ReadAllText(pagePath);



        Assert.Contains("SelectedItem=\"{Binding SelectedReportType}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ProgramFilters}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedProgramFilter}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding DateFrom}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding DateTo}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RefreshReportCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ExportCsvCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SavePdfCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrintReportCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LayoutSummary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource ThemeCardBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource ThemeCardRaisedBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource ThemeAccentSoftBrush}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"White\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#F8FBFF\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Metrics}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PreviewRows}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"SAVE PDF\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"PRINT PREVIEW\"", xaml, StringComparison.Ordinal);
    }
}

namespace AttendanceShiftingManagement.Tests;

public sealed class MasterListPageBindingTests
{
    [Fact]
    public void MasterListPage_UsesValidatedBeneficiariesLabelsAndPaginationBindings()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "MasterListPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"Validated Beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Master List\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Beneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PageSummary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedPageSize}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Delay=250}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationLabels_UseValidatedBeneficiariesTerminology()
    {
        var dashboardPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BarangayDashboardPage.xaml"));

        var dashboardXaml = File.ReadAllText(dashboardPath);

        Assert.Contains("Text=\"MASTERLIST\"", dashboardXaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowMasterListCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", dashboardXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"OPEN VALIDATED BENEFICIARIES\"", dashboardXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"BROWSE HOUSEHOLDS\"", dashboardXaml, StringComparison.Ordinal);
    }
}

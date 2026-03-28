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
    }

    [Fact]
    public void NavigationLabels_UseValidatedBeneficiariesTerminology()
    {
        var mainWindowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "MainWindow.xaml"));

        var dashboardPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BarangayDashboardPage.xaml"));

        var mainWindowXaml = File.ReadAllText(mainWindowPath);
        var dashboardXaml = File.ReadAllText(dashboardPath);

        Assert.Contains("Text=\"Validated Beneficiaries\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Beneficiary Review\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Grievance / Corrections\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Beneficiaries\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Masterlist\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Household registry\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"OPEN VALIDATED BENEFICIARIES\"", dashboardXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"BROWSE HOUSEHOLDS\"", dashboardXaml, StringComparison.Ordinal);
    }
}

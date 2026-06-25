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

        Assert.Contains("Text=\"MASTERLIST &amp; REGISTRY\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Validated Beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PendingBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ApprovedBeneficiaries}\"", xaml, StringComparison.Ordinal);
        
        // Independent pagination commands
        Assert.Contains("Command=\"{Binding PreviousPendingPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPendingPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousApprovedPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextApprovedPageCommand}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"{Binding PendingPageSummary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ApprovedPageSummary}\"", xaml, StringComparison.Ordinal);
        
        Assert.Contains("Background=\"{DynamicResource ThemeCardSubtleBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"#CC0F172A\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Binding=\"{Binding FullName}\"", xaml, StringComparison.Ordinal);
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

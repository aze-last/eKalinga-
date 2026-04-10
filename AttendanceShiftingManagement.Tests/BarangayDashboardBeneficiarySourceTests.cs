namespace AttendanceShiftingManagement.Tests;

public sealed class BarangayDashboardBeneficiarySourceTests
{
    [Fact]
    public void BarangayDashboardService_UsesApprovedBeneficiaries_ForCashForWorkAvailability()
    {
        var servicePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "BarangayDashboardService.cs"));

        var source = File.ReadAllText(servicePath);

        Assert.Contains("CashForWorkBeneficiaryCount", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EligibleWorkers", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCashForWorkEligible", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BarangayDashboardService_AndViewModel_DoNotExposeHouseholdRegistryMetrics()
    {
        var servicePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "BarangayDashboardService.cs"));

        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "BarangayDashboardViewModel.cs"));

        var serviceSource = File.ReadAllText(servicePath);
        var viewModelSource = File.ReadAllText(viewModelPath);

        Assert.DoesNotContain("ActiveHouseholds", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalMembers", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveHouseholdCount", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisteredMemberCount", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ApprovedBeneficiaryCount", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("RejectedBeneficiaryCount", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BarangayDashboardPage_UsesRefinedDashboardModuleAndSummaryLabels()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BarangayDashboardPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"AID REQUESTS\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"PENDING\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"BUDGET ALERTS\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"DISTRIBUTIONS\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Validated Beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cash-for-Work\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Reports\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowReportsCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("No reports view wired yet", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Active households\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Registered members\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Households currently available in the registry.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Total members stored in the active database.", xaml, StringComparison.Ordinal);
    }
}

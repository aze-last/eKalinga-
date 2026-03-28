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
    public void BarangayDashboardPage_UsesBeneficiaryFirstMetricCardLabels()
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

        Assert.Contains("Text=\"{Binding CashForWorkBeneficiaryCount, StringFormat=N0}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cash-for-work beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Approved beneficiaries currently available for cash-for-work events.\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Approved beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Rejected beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding EligibleWorkerCount, StringFormat=N0}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Eligible workers\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Members tagged for cash-for-work participation.\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Active households\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Registered members\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Households currently available in the registry.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Total members stored in the active database.", xaml, StringComparison.Ordinal);
    }
}

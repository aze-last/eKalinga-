using System.Threading;
using System.Windows.Markup;

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
    public void BarangayDashboardPage_UsesHealthCenterStyleGridWithOnlyProjectModules()
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

        Assert.Contains("Text=\"{Binding DataContext.OfficeName, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DataContext.SoftwareSubtitle, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DataContext.OfficeName, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Validated Beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Aid Request\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Budget\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Distribution\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cash-for-Work\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Reports\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Pending Review\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Open Events\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Refresh Dashboard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Settings\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Check for Update\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Logout\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowReportsCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RefreshCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Patients\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Consultations\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Appointments\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Laboratory\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Billing\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Dispensing\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Health Programs\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Forecasting\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Recent Activity\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding RecentActivities}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding TodaySummaries}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BarangayDashboardPage_XamlParsesWithoutStyleErrors()
    {
        Exception? parseException = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var pagePath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "Views",
                    "BarangayDashboardPage.xaml"));

                var xaml = File.ReadAllText(pagePath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.BarangayDashboardPage\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"SettingsButton_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"CheckForUpdateButton_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"LogoutButton_Click\"", string.Empty, StringComparison.Ordinal);

                _ = XamlReader.Parse(xaml);
            }
            catch (Exception ex)
            {
                parseException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(parseException is null, parseException?.ToString());
    }
}

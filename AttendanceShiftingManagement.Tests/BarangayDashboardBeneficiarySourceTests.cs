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
        Assert.DoesNotContain("IsAnnouncementVisible", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DismissAnnouncementCommand", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BarangayDashboardPage_UsesApprovedDashboardModulesAndShellCommands()
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
        Assert.Contains("Text=\"{Binding DataContext.OfficeProfileLabel, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DataContext.OfficeInitials, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DataContext.SoftwareTitle, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DataContext.SoftwareSubtitle, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"AID REQUEST\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"MASTERLIST\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"Cash-for-Work\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Reports\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Software Trademark\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"/Images/default icon.png\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"eKalinga+\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"DATABASE STATUS\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ActiveDatabaseLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding StatusMessage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LastRefreshLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowAssistanceCasesCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowMasterListCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowBudgetCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowDistributionCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowCashForWorkCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowReportsCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"SettingsButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"CheckForUpdateButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"LogoutButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Barangay Management System\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Attendance &amp; Aid Distribution Portal\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"SYSTEM STATUS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Secure Sync Active\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Pending Review\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Open Events\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Refresh Dashboard\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding RefreshCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Feature Announcement\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"What's New in eKalinga+\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAnnouncementVisible", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DismissAnnouncementCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"AID REQUESTS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"PENDING\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"BUDGET ALERTS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"DISTRIBUTIONS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding AidRequestCount, StringFormat={}{0:N0}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding PendingReviewCount, StringFormat={}{0:N0}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding BudgetAlertCount, StringFormat={}{0:N0}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding DistributionCount, StringFormat={}{0:N0}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Recent Activity\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding RecentActivities}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Today\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding TodayLabel}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding TimeLabel}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding TodaySummaries}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Beneficiaries\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Patients\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Consultations\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Appointments\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Laboratory\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Billing\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Dispensing\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Health Programs\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Forecasting\"", xaml, StringComparison.Ordinal);
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
                    .Replace(" Click=\"LogoutButton_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Button_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"SyncRemoteAndLocalButton_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace("xmlns:helpers=\"clr-namespace:AttendanceShiftingManagement.Helpers\"", "xmlns:helpers=\"clr-namespace:AttendanceShiftingManagement.Helpers;assembly=AttendanceShiftingManagement\"", StringComparison.Ordinal);

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

using System.Threading;
using System.Windows;
using System.Windows.Markup;

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
        Assert.Contains("Text=\"{Binding AttendanceSearchText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AttendanceSourceFilters}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ExportAttendanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Events}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedEvent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionUrl, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionPin}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"{Binding AttendanceScannerQrImage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding EligibleBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedEligibleBeneficiary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DisplayMemberPath=\"DisplayLabel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"CreateEvent_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"NEW EVENT\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Beneficiary ID\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Civil Registry ID\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Select a household member to add.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Household\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Purok\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CashForWorkPage_XamlParsesWithoutStyleErrors()
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
                    "CashForWorkOcrPage.xaml"));

                var xaml = File.ReadAllText(pagePath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.CashForWorkOcrPage\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"CreateEvent_Click\"", string.Empty, StringComparison.Ordinal);

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

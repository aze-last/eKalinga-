using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class AssistanceCaseManagementPageBindingTests
{
    [Fact]
    public void AssistanceCaseManagementPage_BindsBeneficiaryHistoryControls()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "AssistanceCaseManagementPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("CITIZEN REQUESTS", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Command=\"{Binding OpenIntakeModalCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PagedRequests}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding TotalCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PendingTriageCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding InFlightCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ResolvedCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.InspectRequestCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SubmitIntakeCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseIntakeModalCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseInspectModalCommand}\"", xaml, StringComparison.Ordinal);

        // Verification that public portal is NOT present per user requirement
        Assert.DoesNotContain("Public Portal", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenPublicPortalCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistanceCaseManagementPage_XamlParsesWithoutStyleErrors()
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
                    "AssistanceCaseManagementPage.xaml"));

                var xaml = File.ReadAllText(pagePath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.AssistanceCaseManagementPage\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Browse_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace("QrCodeScanned=\"Scanner_QrCodeScanned\"", string.Empty, StringComparison.Ordinal)
                    .Replace("Closed=\"Scanner_Closed\"", string.Empty, StringComparison.Ordinal)
                    .Replace("xmlns:helpers=\"clr-namespace:AttendanceShiftingManagement.Helpers\"", "xmlns:helpers=\"clr-namespace:AttendanceShiftingManagement.Helpers;assembly=AttendanceShiftingManagement\"", StringComparison.Ordinal)
                    .Replace("xmlns:local=\"clr-namespace:AttendanceShiftingManagement.Views\"", "xmlns:local=\"clr-namespace:AttendanceShiftingManagement.Views;assembly=AttendanceShiftingManagement\"", StringComparison.Ordinal);

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

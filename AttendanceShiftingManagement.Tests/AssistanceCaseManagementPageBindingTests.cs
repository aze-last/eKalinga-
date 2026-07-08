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

        Assert.Contains("Text=\"BENEFICIARY\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Assistance History\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SearchCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding SearchResults}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedBeneficiary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LoadSearchNextPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LoadSearchPreviousPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LoadNextPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LoadPreviousPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenRecordAssistanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveRecordAssistanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"RECORD ASSISTANCE\"", xaml, StringComparison.Ordinal);

        Assert.DoesNotContain("ItemsSource=\"{Binding CasesView}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItem=\"{Binding SelectedCase}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding ExportCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding ShowPendingCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding OpenCasePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibility=\"{Binding IsCasePanelOpen", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding CloseCasePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Aid request command center\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Request Queue\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding AssistanceCaseBudgets}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItem=\"{Binding SelectedAssistanceCaseBudget}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip=\"{Binding AssistanceCaseBudgetErrorMessage}\"", xaml, StringComparison.Ordinal);
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

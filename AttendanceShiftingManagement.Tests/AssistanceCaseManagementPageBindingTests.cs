using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class AssistanceCaseManagementPageBindingTests
{
    [Fact]
    public void AssistanceCaseManagementPage_BindsAidRequestDashboardControls()
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

        Assert.Contains("Text=\"Aid Request\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"REFRESH\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Aid Request Registry\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CasesView}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedCase}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutoGenerateColumns=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ExportCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowAllCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowPendingCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenCasePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding IsCasePanelOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseCasePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UnderReviewCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ValidatedBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedValidatedBeneficiary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CurrentPage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding TotalPages}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding Households}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding AvailableHouseholdMembers}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint=\"Household\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint=\"Optional household member\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("household/member from the registry", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Aid request command center\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Request Queue\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AssistanceCaseBudgets}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedAssistanceCaseBudget}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding AssistanceCaseBudgetErrorMessage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasAssistanceCaseBudgetError, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReleaseKindOptions}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedReleaseKind}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DeleteCommand}\"", xaml, StringComparison.Ordinal);
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
                    .Replace("<helpers:NullToHiddenConverter x:Key=\"NullToHiddenConverter\" />", string.Empty, StringComparison.Ordinal)
                    .Replace("Converter={StaticResource NullToHiddenConverter}", string.Empty, StringComparison.Ordinal);

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

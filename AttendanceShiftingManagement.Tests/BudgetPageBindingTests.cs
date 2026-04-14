namespace AttendanceShiftingManagement.Tests;

public sealed class BudgetPageBindingTests
{
    [Fact]
    public void BudgetPage_UsesBudgetBindings()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BudgetPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"Budget Control\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SyncGovernmentBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Quick Actions\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Programs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Private Donations\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LedgerSearchText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LedgerSourceFilters}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ExportLedgerCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RecordDonationCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateProgramCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenDonationPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseDonationPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenProgramPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseProgramPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding IsAnySetupPanelOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramAssistanceType, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramUnitAmountText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramItemDescription, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramBudgetCapText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding ProgramStartDate}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding ProgramEndDate}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LedgerEntriesView}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedLedgerEntry, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding IsLedgerHistoryCardOpen, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseLedgerHistoryCardCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SelectedLedgerEntry.ProgramName, TargetNullValue=--}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Release\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Binding=\"{Binding ReleaseKind}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenSeminarPanelCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseSeminarPanelCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateSeminarCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Seminar Setup Panel", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardPage_ContainsBudgetModuleTile()
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

        Assert.Contains("Text=\"Budget\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowBudgetCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
    }
}

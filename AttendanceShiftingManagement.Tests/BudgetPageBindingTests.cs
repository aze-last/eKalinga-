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
        Assert.Contains("Command=\"{Binding RecordDonationCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateProgramCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Project / Distribution Setup\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramAssistanceType, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramUnitAmountText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramItemDescription, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramBudgetCapText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding ProgramStartDate}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDate=\"{Binding ProgramEndDate}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Release\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Binding=\"{Binding ReleaseKind}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ContainsBudgetSidebarEntry()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "MainWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("Text=\"Budget\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowBudgetCommand}\"", xaml, StringComparison.Ordinal);
    }
}

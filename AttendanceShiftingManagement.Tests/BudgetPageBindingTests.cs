namespace AttendanceShiftingManagement.Tests;

public sealed class BudgetPageBindingTests
{
    private static string GetProjectFilePath(params string[] relativeSegments)
    {
        var directory = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "AttendanceShiftingManagement.csproj")))
            {
                return Path.Combine(new[] { directory }.Concat(relativeSegments).ToArray());
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate the AttendanceShiftingManagement project root.");
    }

    [Fact]
    public void BudgetPage_UsesSidebarPanelWorkflowBindings()
    {
        var pagePath = GetProjectFilePath("Views", "BudgetPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Content=\"Dashboard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowDashboardCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Budget Ledger\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenLedgerPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Private Donations\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenDonationPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Programs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenProgramPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Ayuda Projects\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Aid Request Budgets\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Cash-for-Work Budgets\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Seminars\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Refresh Workspace\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"{Binding CurrentPanelTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CurrentPanelSubtitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding DonationPanelVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding ProgramPanelVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding SeminarPanelVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HistoryDetailVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ClosePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseHistoryDetailCommand}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("ItemsSource=\"{Binding LedgerEntriesView}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedLedgerEntry, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LedgerSearchText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LedgerSourceFilters}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ExportLedgerCommand}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Command=\"{Binding RecordDonationCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BrowseProofCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Donations}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Command=\"{Binding CreateProgramCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Programs}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramCode, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgramName, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateAssistanceCaseBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AssistanceCaseBudgets}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateCashForWorkBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CashForWorkBudgets}\"", xaml, StringComparison.Ordinal);

    }

    [Fact]
    public void DashboardPage_ContainsBudgetModuleTile()
    {
        var pagePath = GetProjectFilePath("Views", "BarangayDashboardPage.xaml");

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"BUDGET\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.ShowBudgetCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
    }
}

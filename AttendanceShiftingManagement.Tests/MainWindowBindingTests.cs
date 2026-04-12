using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public void MainWindow_UsesDashboardShellLayout()
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

        Assert.Contains("Text=\"{Binding OfficeName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OfficeProfileLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SoftwareTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SoftwareSubtitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UserDisplayName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ConnectionSummary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Workspaces\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Settings\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Check for Update\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Logout\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CurrentSectionTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CurrentSectionSubtitle}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"OVERVIEW\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"OPERATIONS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"MANAGEMENT\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Back to Dashboard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowDashboardCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowAssistanceCasesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowMasterListCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowDistributionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowCashForWorkCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowReportsCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding CurrentView}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource ThemeWindowShellBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{StaticResource SidebarShellBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ThemeCardRaisedBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ClipToBounds=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_XamlParsesWithoutStyleErrors()
    {
        Exception? parseException = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var windowPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "Views",
                    "MainWindow.xaml"));

                var xaml = File.ReadAllText(windowPath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.MainWindow\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Icon=\"/Images/municipal-house.ico\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Minimize_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"MaximizeRestore_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Close_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"CheckForUpdate_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Settings_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Logout_Click\"", string.Empty, StringComparison.Ordinal);

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

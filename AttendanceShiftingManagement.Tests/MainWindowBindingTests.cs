using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Text.RegularExpressions;
using System.Linq;

namespace AttendanceShiftingManagement.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public void MainWindow_UsesMinimalContentHostLayout()
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

        Assert.Contains("Content=\"{Binding CurrentView}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource ThemeWindowShellBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ClipToBounds=\"True\"", xaml, StringComparison.Ordinal);

        Assert.Single(Regex.Matches(xaml, "Click=\"Minimize_Click\"", RegexOptions.None).Cast<Match>());
        Assert.Single(Regex.Matches(xaml, "Click=\"MaximizeRestore_Click\"", RegexOptions.None).Cast<Match>());
        Assert.Single(Regex.Matches(xaml, "Click=\"Close_Click\"", RegexOptions.None).Cast<Match>());

        Assert.DoesNotContain("ShowDashboardCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowAssistanceCasesCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowMasterListCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowDistributionCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowBudgetCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowCashForWorkCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowReportsCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentSectionTitle", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentSectionSubtitle", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SidebarShellBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspaces", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Active Connection", xaml, StringComparison.Ordinal);
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

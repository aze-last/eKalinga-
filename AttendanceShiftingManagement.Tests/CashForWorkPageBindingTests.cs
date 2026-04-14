using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkPageBindingTests
{
    [Fact]
    public void CashForWorkPage_UsesLeftRailAndWorkflowPanelBindings()
    {
        var pagePath = ResolveCashForWorkPagePath();

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Command=\"{Binding DataContext.ShowDashboardCommand, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenCreateEventPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenSeminarPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenAddBeneficiariesPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenScanAttendancePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenPayoutPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RefreshWorkspaceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Events}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedEvent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding SavedAttendanceRows}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding EventEditorVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding BeneficiariesVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding ScanAttendanceVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding PayoutVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveEventCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding AddParticipantCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveManualAttendanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateAttendanceScannerSessionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ReleaseBudgetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Participants}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AyudaPrograms}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedAyudaProgram}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReleaseAmountText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionUrl, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionPin}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"{Binding AttendanceScannerQrImage}\"", xaml, StringComparison.Ordinal);

        Assert.DoesNotContain("Click=\"CreateEvent_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"EditEvent_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"DeleteEvent_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"EditAttendance_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"DeleteAttendance_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Cash-for-Work Workspace", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding HistoryPreviewRows}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding PrintAttendanceSheetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding OpenAnnouncementsPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibility=\"{Binding HasOpenAnnouncements, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibility=\"{Binding DrawerVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibility=\"{Binding AnnouncementsVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"OpenSettings_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"NEW EVENT\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"EDIT EVENT\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"DELETE EVENT\"", xaml, StringComparison.Ordinal);
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

                var pagePath = ResolveCashForWorkPagePath();

                var xaml = File.ReadAllText(pagePath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.CashForWorkOcrPage\"", string.Empty, StringComparison.Ordinal);

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

    private static string ResolveCashForWorkPagePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "Views", "CashForWorkOcrPage.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Views\\CashForWorkOcrPage.xaml from the test output directory.");
    }
}

using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class SeminarAttendancePageBindingTests
{
    [Fact]
    public void SeminarAttendancePage_UsesLeftRailAndWorkflowPanelBindings()
    {
        var pagePath = ResolveSeminarAttendancePagePath();

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Command=\"{Binding OpenCreateSeminarPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenEditSeminarPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenScanAttendancePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenPcScannerCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenAnnouncementsPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RefreshWorkspaceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DeleteSeminarCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveSeminarCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateAttendanceScannerSessionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding EditAttendanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DeleteAttendanceCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveAttendanceSheetPdfCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrintAttendanceSheetCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousAttendancePageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextAttendancePageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ClosePanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.SelectAnnouncementEventCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding SavedAttendanceRows}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedAttendanceRow}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding OpenAnnouncements}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding SeminarEditorVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding ScanAttendanceVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding AnnouncementsVisibility}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding SeminarEditorSubmitLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionUrl, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AttendanceScannerSessionPin}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"{Binding AttendanceScannerQrImage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"Browse_Click\"", xaml, StringComparison.Ordinal);

        Assert.Contains("ItemsSource=\"{Binding BenefitTypes}\"", xaml, StringComparison.Ordinal);

        // Seminars are attendance-only: no payout, manual-attendance, enrollment, or event-kind UI.
        Assert.DoesNotContain("ReleaseBudgetCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveManualAttendanceCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenPayoutPanelCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PayoutRailVisibility", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ManualAttendanceVisibility", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("EventEditorKind", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SeminarAttendancePage_XamlParsesWithoutStyleErrors()
    {
        Exception? parseException = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var pagePath = ResolveSeminarAttendancePagePath();

                var xaml = File.ReadAllText(pagePath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.SeminarAttendancePage\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Browse_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace("QrCodeScanned=\"Scanner_QrCodeScanned\"", string.Empty, StringComparison.Ordinal)
                    .Replace("Closed=\"Scanner_Closed\"", string.Empty, StringComparison.Ordinal)
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

    private static string ResolveSeminarAttendancePagePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "Views", "SeminarAttendancePage.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Views\\SeminarAttendancePage.xaml from the test output directory.");
    }
}

using System.Threading;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class SessionAnnouncementWindowSourceTests
{
    [Fact]
    public void SessionAnnouncementWindow_UsesPopupStructureAndParsesXaml()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SessionAnnouncementWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("Text=\"{Binding Title}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Recent Feed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Continue\"", xaml, StringComparison.Ordinal);

        Exception? parseException = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var parseableXaml = xaml
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.SessionAnnouncementWindow\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Continue_Click\"", string.Empty, StringComparison.Ordinal)
                    .Replace("xmlns:helpers=\"clr-namespace:AttendanceShiftingManagement.Helpers\"", "xmlns:helpers=\"clr-namespace:AttendanceShiftingManagement.Helpers;assembly=AttendanceShiftingManagement\"", StringComparison.Ordinal);

                _ = XamlReader.Parse(parseableXaml);
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

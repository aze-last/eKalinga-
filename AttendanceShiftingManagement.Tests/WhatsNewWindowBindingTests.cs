using System.Threading;
using System.Windows.Markup;

namespace AttendanceShiftingManagement.Tests;

public sealed class WhatsNewWindowBindingTests
{
    [Fact]
    public void WhatsNewWindow_BindsVersionTitleAndReleaseNotes()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "WhatsNewWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("Title=\"What's New\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding VersionTitle, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Subtitle, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Entries, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Notes, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"Close_Click\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsNewWindow_XamlParsesWithoutStyleErrors()
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
                    "WhatsNewWindow.xaml"));

                var xaml = File.ReadAllText(windowPath)
                    .Replace("x:Class=\"AttendanceShiftingManagement.Views.WhatsNewWindow\"", string.Empty, StringComparison.Ordinal)
                    .Replace(" Click=\"Close_Click\"", string.Empty, StringComparison.Ordinal);

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

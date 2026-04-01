namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsWindowBindingTests
{
    [Fact]
    public void ConnectionSettingsWindow_UsesGeneralCredentialEditorBinding()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ConnectionSettingsWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("Visibility=\"{Binding ShowCredentialEditor, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Selection only\"", xaml, StringComparison.Ordinal);
    }
}

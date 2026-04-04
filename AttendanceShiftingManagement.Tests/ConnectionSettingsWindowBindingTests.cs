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

    [Fact]
    public void ConnectionSettingsWindow_ExposesLazySaveOtpBindings()
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

        Assert.Contains("Visibility=\"{Binding ShowSaveOtpPanel, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SendSaveOtpCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding VerifySaveOtpCommand}\"", xaml, StringComparison.Ordinal);
    }
}

namespace AttendanceShiftingManagement.Tests;

public sealed class SettingsWindowBindingTests
{
    [Fact]
    public void SettingsWindow_AppDatabaseTabExposesMigrationButton()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("MIGRATE LOCAL + REMOTE NOW", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding MigrateLocalAndRemoteCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"GGMS Budget Source\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Updates\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CheckForUpdatesCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DownloadUpdateCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding InstallPendingUpdateCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RemindMeLaterCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_UsesOneWayBindings_ForReadOnlyUpdateDisplayFields()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("Text=\"{Binding CurrentAppVersion, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LatestAvailableVersion, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UpdatePublishedAt, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UpdateReleaseNotes, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DownloadedInstallerLabel, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding UpdateDownloadProgressPercent, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_UsesPasswordUnlockOverlay_AndLazyOtpSaveBindings()
    {
        var windowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml"));

        var xaml = File.ReadAllText(windowPath);

        Assert.Contains("Style x:Key=\"ProtectedSettingsContentGridStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Binding=\"{Binding IsSensitiveSettingsLocked}\" Value=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PasswordChanged=\"ProtectedSettingsUnlockPasswordBox_PasswordChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding UnlockSensitiveSettingsCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding ShowSensitiveSettingsOtpPanel, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SendSensitiveSettingsOtpCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding VerifySensitiveSettingsOtpCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SendPasswordChangeOtpCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding VerifyPasswordChangeOtpCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding ShowPasswordChangeOtpPanel, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
    }
}

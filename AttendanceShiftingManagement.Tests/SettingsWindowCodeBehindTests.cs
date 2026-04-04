namespace AttendanceShiftingManagement.Tests;

public sealed class SettingsWindowCodeBehindTests
{
    [Fact]
    public void SettingsWindow_OpensAppDatabaseWindowWithoutPreUnlockGate()
    {
        var codeBehindPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "SettingsWindow.xaml.cs"));

        var codeBehind = File.ReadAllText(codeBehindPath);

        Assert.DoesNotContain("if (!ViewModel.IsSensitiveSettingsUnlocked)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("new ConnectionSettingsWindow(selectionOnly: false, requireOtpOnSave: true)", codeBehind, StringComparison.Ordinal);
    }
}

namespace AttendanceShiftingManagement.Tests;

public sealed class LoginViewModelSourceTests
{
    [Fact]
    public void LoginViewModel_KeepsCompanySerialAsDisplayOnly()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "LoginViewModel.cs"));

        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Company Serial: {BrandInstallSerial}", source, StringComparison.Ordinal);
        Assert.Contains("Company serial is shown for reference only.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CompanySerialGateService.ValidateOrBind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Security:EnforceCompanySerialGate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginViewModel_DoesNotAutoSyncRemoteReviewData_OnStartup()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "LoginViewModel.cs"));

        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("RemotePhaseOneSyncService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshRemoteReviewSyncStatusAsync", source, StringComparison.Ordinal);
    }
}

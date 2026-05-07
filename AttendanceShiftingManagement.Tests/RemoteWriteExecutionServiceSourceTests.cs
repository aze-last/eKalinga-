namespace AttendanceShiftingManagement.Tests;

public sealed class RemoteWriteExecutionServiceSourceTests
{
    [Fact]
    public void ReleaseServices_RouteRemoteWritesThroughSharedHelper()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

        var assistanceSource = File.ReadAllText(Path.Combine(root, "Services", "AssistanceCaseManagementService.cs"));
        var cashForWorkSource = File.ReadAllText(Path.Combine(root, "Services", "CashForWorkService.cs"));
        var distributionSource = File.ReadAllText(Path.Combine(root, "Services", "ProjectDistributionService.cs"));

        Assert.Contains("RemoteWriteExecutionService.ShouldRouteToRemote", assistanceSource, StringComparison.Ordinal);
        Assert.Contains("RemoteWriteExecutionService.ExecuteRemoteWriteAsync", assistanceSource, StringComparison.Ordinal);

        Assert.Contains("RemoteWriteExecutionService.ShouldRouteToRemote", cashForWorkSource, StringComparison.Ordinal);
        Assert.Contains("RemoteWriteExecutionService.ExecuteRemoteWriteAsync", cashForWorkSource, StringComparison.Ordinal);

        Assert.Contains("RemoteWriteExecutionService.ShouldRouteToRemote", distributionSource, StringComparison.Ordinal);
        Assert.Contains("RemoteWriteExecutionService.ExecuteRemoteWriteAsync", distributionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteWriteExecutionService_UsesExplicitReentryGuard()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

        var source = File.ReadAllText(Path.Combine(root, "Services", "RemoteWriteExecutionService.cs"));

        Assert.Contains("AsyncLocal<int>", source, StringComparison.Ordinal);
        Assert.Contains("IsRemoteWriteInProgress()", source, StringComparison.Ordinal);
        Assert.Contains("EnterRemoteWriteScope()", source, StringComparison.Ordinal);
        Assert.Contains("ExitRemoteWriteScope()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteWriteExecutionService_DoesNotAutoSyncBackToLocal()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

        var source = File.ReadAllText(Path.Combine(root, "Services", "RemoteWriteExecutionService.cs"));

        Assert.DoesNotContain("SyncFromRemoteToLocalAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RemotePhaseOneSyncResult", source, StringComparison.Ordinal);
    }
}

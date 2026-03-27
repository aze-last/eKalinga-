namespace AttendanceShiftingManagement.Tests;

public sealed class LocalScannerGatewayDistributionSourceTests
{
    [Fact]
    public void LocalScannerGatewayService_ContainsDistributionClaimEndpoint_AndProjectAwareScannerUi()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "LocalScannerGatewayService.cs"));

        var source = File.ReadAllText(sourcePath);

        Assert.Contains("/api/distribution/claim", source, StringComparison.Ordinal);
        Assert.Contains("ScannerSessionMode.Distribution", source, StringComparison.Ordinal);
        Assert.Contains("AyudaProgramId", source, StringComparison.Ordinal);
        Assert.Contains("Mark as Received", source, StringComparison.Ordinal);
    }
}

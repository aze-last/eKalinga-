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
        Assert.Contains("Mark as Released", source, StringComparison.Ordinal);
        Assert.Contains("claimSummary", source, StringComparison.Ordinal);
        Assert.Contains("claimHistory", source, StringComparison.Ordinal);
        Assert.Contains("Beneficiary Claims", source, StringComparison.Ordinal);
        Assert.Contains("canMarkAttendance", source, StringComparison.Ordinal);
        Assert.Contains("Attendance can only be recorded for an open event scheduled today.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalScannerGatewayService_WiresCameraPhotoButton_ToLaunchPicker_AndAutoUploadSelection()
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

        Assert.Contains("capture=\"environment\"", source, StringComparison.Ordinal);
        Assert.Contains("lookupImageButton.addEventListener(\"click\", openImageCapture);", source, StringComparison.Ordinal);
        Assert.Contains("imageInput.addEventListener(\"change\", lookupSelectedImage);", source, StringComparison.Ordinal);
        Assert.Contains("imageInput.click();", source, StringComparison.Ordinal);
        Assert.Contains("Uploading QR image...", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalScannerGatewayService_IncludesLiveCameraScannerSupport_AndHelpfulUploadFallback()
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

        Assert.Contains("Start Live Camera Scan", source, StringComparison.Ordinal);
        Assert.Contains("BarcodeDetector", source, StringComparison.Ordinal);
        Assert.Contains("getUserMedia", source, StringComparison.Ordinal);
        Assert.Contains("cameraPreview", source, StringComparison.Ordinal);
        Assert.Contains("Move closer, improve lighting, or try live camera scan.", source, StringComparison.Ordinal);
        Assert.Contains("Use a JPG or PNG photo, or try live camera scan.", source, StringComparison.Ordinal);
    }
}

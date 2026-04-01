using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class QrCodeToolkitServiceTests
{
    [Fact]
    public void QrCodeToolkitService_RoundTripsGeneratedQrPayload()
    {
        const string payload = "ASM-BID|000321|TESTPAYLOAD";
        var bytes = QrCodeToolkitService.GenerateQrPngBytes(payload, 14);

        using var stream = new MemoryStream(bytes);
        var decoded = QrCodeToolkitService.TryDecodePayload(stream);

        Assert.Equal(payload, decoded);
    }
}

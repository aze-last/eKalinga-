using AttendanceShiftingManagement.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

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

    [Fact]
    public void QrCodeToolkitService_DecodesSmallQrFromPhotoLikeImage()
    {
        const string payload = "ASM-BID|000654|PHOTOLOOKUP";
        var qrBytes = QrCodeToolkitService.GenerateQrPngBytes(payload, 14);

        using var qrStream = new MemoryStream(qrBytes);
        using var qrBitmap = new Bitmap(qrStream);
        using var canvas = new Bitmap(1600, 1000);
        using var graphics = Graphics.FromImage(canvas);

        graphics.Clear(Color.FromArgb(245, 247, 250));
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var cardBrush = new SolidBrush(Color.White);
        graphics.FillRectangle(cardBrush, 430, 210, 720, 440);
        graphics.DrawRectangle(Pens.LightGray, 430, 210, 720, 440);

        graphics.TranslateTransform(780, 540);
        graphics.RotateTransform(-8f);
        graphics.DrawImage(qrBitmap, new Rectangle(-44, -44, 88, 88));
        graphics.ResetTransform();

        using var buffer = new MemoryStream();
        canvas.Save(buffer, ImageFormat.Png);
        buffer.Position = 0;

        var decoded = QrCodeToolkitService.TryDecodePayload(buffer);

        Assert.Equal(payload, decoded);
    }
}

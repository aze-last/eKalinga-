using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace AttendanceShiftingManagement.Services
{
    public static class QrCodeToolkitService
    {
        public static BitmapImage GenerateQrImage(string payload, int pixelsPerModule = 8)
        {
            var bytes = GenerateQrPngBytes(payload, pixelsPerModule);
            return LoadBitmap(bytes);
        }

        public static byte[] GenerateQrPngBytes(string payload, int pixelsPerModule = 8)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            return qrCode.GetGraphic(Math.Max(4, pixelsPerModule));
        }

        public static string? TryDecodePayload(string imagePath)
        {
            using var stream = File.OpenRead(imagePath);
            return TryDecodePayload(stream);
        }

        public static string? TryDecodePayload(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;

            using var bitmap = new Bitmap(buffer);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,
                    PossibleFormats = [BarcodeFormat.QR_CODE]
                }
            };

            var result = reader.Decode(bitmap);
            return result?.Text?.Trim();
        }

        private static BitmapImage LoadBitmap(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}

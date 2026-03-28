using QRCoder;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using ZXing;

namespace AttendanceShiftingManagement.Services
{
    public static class QrCodeToolkitService
    {
        public static BitmapImage GenerateQrImage(string payload, int pixelsPerModule = 8)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(Math.Max(4, pixelsPerModule));
            return LoadBitmap(bytes);
        }

        public static string? TryDecodePayload(string imagePath)
        {
            using var stream = File.OpenRead(imagePath);
            return TryDecodePayload(stream);
        }

        public static string? TryDecodePayload(Stream stream)
        {
            using var bitmap = new Bitmap(stream);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options =
                {
                    TryHarder = true
                }
            };

            var result = reader.Decode(bitmap);
            return result?.Text;
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

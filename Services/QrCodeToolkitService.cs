using QRCoder;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace AttendanceShiftingManagement.Services
{
    public static class QrCodeToolkitService
    {
        public static BitmapImage GenerateBarcodeImage(string payload, int width, int height)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 0,
                    PureBarcode = true
                }
            };

            using var bitmap = writer.Write(payload);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

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

            using var bitmap = LoadWorkingBitmap(buffer);
            NormalizeOrientation(bitmap);

            foreach (var maxDimension in DecodePassDimensions)
            {
                using var candidate = ResizeForDecode(bitmap, maxDimension);
                var decoded = TryDecodeBitmap(candidate);
                if (!string.IsNullOrWhiteSpace(decoded))
                {
                    return decoded;
                }
            }

            return null;
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

        private static readonly int[] DecodePassDimensions = [0, 2200, 1600, 1200, 900, 700, 500];

        private static Bitmap LoadWorkingBitmap(Stream stream)
        {
            using var source = new Bitmap(stream);
            var working = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(working);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);

            return working;
        }

        private static string? TryDecodeBitmap(Bitmap bitmap)
        {
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
            return string.IsNullOrWhiteSpace(result?.Text) ? null : result.Text.Trim();
        }

        private static Bitmap ResizeForDecode(Bitmap source, int maxDimension)
        {
            var sourceMaxDimension = Math.Max(source.Width, source.Height);
            if (maxDimension <= 0 || sourceMaxDimension <= maxDimension)
            {
                return (Bitmap)source.Clone();
            }

            var scale = maxDimension / (double)sourceMaxDimension;
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(resized);
            graphics.Clear(Color.White);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, width, height);

            return resized;
        }

        private static void NormalizeOrientation(Bitmap bitmap)
        {
            const int orientationPropertyId = 0x0112;

            if (Array.IndexOf(bitmap.PropertyIdList, orientationPropertyId) < 0)
            {
                return;
            }

            PropertyItem? orientationProperty;

            try
            {
                orientationProperty = bitmap.GetPropertyItem(orientationPropertyId);
            }
            catch (ArgumentException)
            {
                return;
            }

            var orientationBytes = orientationProperty?.Value;
            if (orientationBytes == null || orientationBytes.Length == 0)
            {
                return;
            }

            var orientation = orientationBytes[0];
            var rotateFlipType = orientation switch
            {
                2 => RotateFlipType.RotateNoneFlipX,
                3 => RotateFlipType.Rotate180FlipNone,
                4 => RotateFlipType.Rotate180FlipX,
                5 => RotateFlipType.Rotate90FlipX,
                6 => RotateFlipType.Rotate90FlipNone,
                7 => RotateFlipType.Rotate270FlipX,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };

            if (rotateFlipType != RotateFlipType.RotateNoneFlipNone)
            {
                bitmap.RotateFlip(rotateFlipType);
            }

            try
            {
                bitmap.RemovePropertyItem(orientationPropertyId);
            }
            catch (ArgumentException)
            {
            }
        }
    }
}

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

class Program
{
    static void Main()
    {
        GenerateBarcodeImage("B-123|A1B2C3D4", 600, 100, "old.png");
        GenerateBarcodeImage("ASMBID000123ABCDEF", 600, 100, "new.png");
    }

    public static void GenerateBarcodeImage(string payload, int width, int height, string filename)
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
        bitmap.Save(filename, ImageFormat.Png);
        Console.WriteLine($"Generated {filename} for payload: {payload}");
    }
}

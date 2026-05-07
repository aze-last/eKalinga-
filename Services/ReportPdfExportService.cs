using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AttendanceShiftingManagement.Helpers;

namespace AttendanceShiftingManagement.Services
{
    public sealed class ReportPdfExportOptions
    {
        public string PreparedBy { get; init; } = string.Empty;
        public DateTime? GeneratedAt { get; init; }
    }

    public sealed class ReportPdfExportService
    {
        private const double A4PortraitWidth = 595;
        private const double A4PortraitHeight = 842;
        private const double Margin = 36;
        private const double LineHeight = 13;

        public void Save(ReportsSnapshot snapshot, string filePath, ReportPdfExportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A PDF file path is required.", nameof(filePath));
            }

            options ??= new ReportPdfExportOptions();
            var isLandscape = string.Equals(snapshot.SuggestedOrientation, "Landscape", StringComparison.OrdinalIgnoreCase);
            var pageWidth = isLandscape ? A4PortraitHeight : A4PortraitWidth;
            var pageHeight = isLandscape ? A4PortraitWidth : A4PortraitHeight;
            var builder = new PdfDocumentBuilder(pageWidth, pageHeight);
            var branding = SystemProfileSettingsService.BuildLoginBranding(SystemProfileSettingsService.Load());
            var systemLogo = PdfImageAsset.TryLoad(branding.LogoPath);
            var appLogo = PdfImageAsset.TryLoad(ResolveDefaultLogoPath());
            if (systemLogo != null)
            {
                builder.RegisterImage("SystemLogo", systemLogo);
            }

            if (appLogo != null)
            {
                builder.RegisterImage("AppLogo", appLogo);
            }

            var generatedAt = options.GeneratedAt ?? DateTime.Now;
            var page = builder.AddPage();
            var y = pageHeight - Margin;

            y = DrawHeader(page, snapshot, generatedAt, y, systemLogo != null, appLogo != null);
            y = DrawMetadata(page, snapshot, options.PreparedBy, y);
            y = DrawMetrics(page, snapshot, y);
            y = DrawHighlights(page, snapshot, y);
            DrawTable(builder, page, snapshot.Table, ref y);
            builder.Write(filePath);
        }

        private static double DrawHeader(PdfPageContent page, ReportsSnapshot snapshot, DateTime generatedAt, double y, bool hasSystemLogo, bool hasAppLogo)
        {
            const double systemLogoWidth = 72;
            const double systemLogoHeight = 53;
            const double appLogoWidth = 78;
            const double appLogoHeight = 58;
            const double logoTopOffset = 10;

            if (hasSystemLogo)
            {
                page.DrawImage("SystemLogo", Margin, y - systemLogoHeight + logoTopOffset, systemLogoWidth, systemLogoHeight);
            }

            if (hasAppLogo)
            {
                page.DrawImage("AppLogo", page.Width - Margin - appLogoWidth, y - appLogoHeight + logoTopOffset, appLogoWidth, appLogoHeight);
            }

            page.DrawCenteredText(Sanitize(snapshot.Title), page.Width / 2, y - 2, 18, bold: true, "#0F172A");
            y -= 16;
            page.DrawCenteredText(Sanitize(snapshot.Subtitle), page.Width / 2, y, 9, bold: false, "#475569");
            y -= 13;
            page.DrawCenteredText($"Generated: {generatedAt:MMM dd, yyyy HH:mm}", page.Width / 2, y, 8, bold: false, "#64748B");
            y -= 16;
            page.DrawLine(Margin, y, page.Width - Margin, y, "#1D8CFF", 1.2);
            return y - 18;
        }

        private static double DrawMetadata(PdfPageContent page, ReportsSnapshot snapshot, string preparedBy, double y)
        {
            page.DrawText($"Report Range: {Sanitize(snapshot.RangeLabel)}", Margin, y, 10, bold: true, "#334155");
            y -= LineHeight;
            page.DrawText($"Program Scope: {Sanitize(snapshot.ProgramLabel)}", Margin, y, 10, bold: true, "#334155");
            y -= LineHeight;
            page.DrawText($"Prepared By: {Sanitize(string.IsNullOrWhiteSpace(preparedBy) ? "--" : preparedBy)}", Margin, y, 10, bold: true, "#334155");
            return y - 18;
        }

        private static double DrawMetrics(PdfPageContent page, ReportsSnapshot snapshot, double y)
        {
            if (snapshot.Metrics.Count == 0)
            {
                return y;
            }

            const double gap = 8;
            var columns = Math.Min(snapshot.Metrics.Count, 4);
            var cardWidth = (page.Width - (Margin * 2) - (gap * (columns - 1))) / columns;
            const double cardHeight = 54;
            var x = Margin;

            foreach (var metric in snapshot.Metrics.Take(columns))
            {
                page.FillRect(x, y - cardHeight + 8, cardWidth, cardHeight, "#F8FBFF");
                page.StrokeRect(x, y - cardHeight + 8, cardWidth, cardHeight, "#D9E6FA", 0.8);
                page.DrawText(Sanitize(metric.Label).ToUpperInvariant(), x + 8, y - 6, 7.5, bold: true, "#64748B");
                page.DrawText(Sanitize(metric.Value), x + 8, y - 24, 13, bold: true, "#0D2B6E");
                page.DrawText(Sanitize(metric.Note), x + 8, y - 39, 7.5, bold: false, "#64748B");
                x += cardWidth + gap;
            }

            return y - cardHeight - 8;
        }

        private static double DrawHighlights(PdfPageContent page, ReportsSnapshot snapshot, double y)
        {
            if (snapshot.Highlights.Count == 0)
            {
                return y;
            }

            page.DrawText("Executive Summary", Margin, y, 12, bold: true, "#0F172A");
            y -= 16;

            foreach (var highlight in snapshot.Highlights.Take(6))
            {
                var lines = PdfPageContent.WrapText(Sanitize(highlight), page.Width - (Margin * 2) - 12, 9);
                foreach (var line in lines.Take(3))
                {
                    page.DrawText($"- {line}", Margin + 8, y, 9, bold: false, "#334155");
                    y -= 11;
                }
            }

            return y - 8;
        }

        private static void DrawTable(PdfDocumentBuilder builder, PdfPageContent page, DataTable table, ref double y)
        {
            page.DrawText("Detailed Table", Margin, y, 12, bold: true, "#0F172A");
            y -= 18;

            if (table.Columns.Count == 0)
            {
                page.DrawText("No table columns available.", Margin, y, 10, bold: false, "#64748B");
                return;
            }

            var maxColumns = Math.Min(table.Columns.Count, 8);
            var tableWidth = page.Width - (Margin * 2);
            var columnWidth = tableWidth / maxColumns;

            DrawTableHeader(page, table, maxColumns, columnWidth, ref y);

            foreach (DataRow dataRow in table.Rows)
            {
                var cells = new List<IReadOnlyList<string>>();
                var rowHeight = 20.0;

                for (var columnIndex = 0; columnIndex < maxColumns; columnIndex++)
                {
                    var raw = Convert.ToString(dataRow[columnIndex], CultureInfo.CurrentCulture) ?? string.Empty;
                    var wrapped = PdfPageContent.WrapText(Sanitize(raw), columnWidth - 8, 8).Take(4).ToArray();
                    cells.Add(wrapped.Length == 0 ? new[] { string.Empty } : wrapped);
                    rowHeight = Math.Max(rowHeight, 8 + (cells[^1].Count * 10));
                }

                if (y - rowHeight < Margin + 24)
                {
                    page = builder.AddPage();
                    y = page.Height - Margin;
                    DrawTableHeader(page, table, maxColumns, columnWidth, ref y);
                }

                var x = Margin;
                page.FillRect(x, y - rowHeight, tableWidth, rowHeight, "#FFFFFF");
                for (var columnIndex = 0; columnIndex < maxColumns; columnIndex++)
                {
                    page.StrokeRect(x, y - rowHeight, columnWidth, rowHeight, "#E2E8F0", 0.5);
                    var textY = y - 10;
                    foreach (var line in cells[columnIndex])
                    {
                        page.DrawText(line, x + 4, textY, 8, bold: false, "#334155");
                        textY -= 10;
                    }

                    x += columnWidth;
                }

                y -= rowHeight;
            }
        }

        private static void DrawTableHeader(PdfPageContent page, DataTable table, int maxColumns, double columnWidth, ref double y)
        {
            const double headerHeight = 24;
            var x = Margin;
            for (var columnIndex = 0; columnIndex < maxColumns; columnIndex++)
            {
                page.FillRect(x, y - headerHeight, columnWidth, headerHeight, "#EAF3FF");
                page.StrokeRect(x, y - headerHeight, columnWidth, headerHeight, "#CFE2FF", 0.7);
                page.DrawText(Sanitize(table.Columns[columnIndex].ColumnName), x + 4, y - 15, 8, bold: true, "#0D2B6E");
                x += columnWidth;
            }

            y -= headerHeight;
        }

        private static string Sanitize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "--";
            }

            return value
                .Replace("₱", "PHP ", StringComparison.Ordinal)
                .Replace("→", "to", StringComparison.Ordinal)
                .Trim();
        }

        private static string? ResolveDefaultLogoPath()
        {
            foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var current = new DirectoryInfo(root);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, "Images", "default icon.png");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    current = current.Parent;
                }
            }

            return null;
        }

        private sealed class PdfDocumentBuilder
        {
            private readonly List<PdfPageContent> _pages = new();
            private readonly Dictionary<string, PdfImageAsset> _images = new(StringComparer.Ordinal);

            public PdfDocumentBuilder(double pageWidth, double pageHeight)
            {
                PageWidth = pageWidth;
                PageHeight = pageHeight;
            }

            public double PageWidth { get; }
            public double PageHeight { get; }

            public void RegisterImage(string name, PdfImageAsset image)
            {
                _images[name] = image;
            }

            public PdfPageContent AddPage()
            {
                var page = new PdfPageContent(PageWidth, PageHeight);
                _pages.Add(page);
                return page;
            }

            public void Write(string filePath)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

                var firstImageObjectNumber = 5;
                var imageRefs = _images
                    .Select((item, index) => new PdfImageObjectRef(item.Key, item.Value, firstImageObjectNumber + index))
                    .ToList();
                var firstPageObjectNumber = firstImageObjectNumber + imageRefs.Count;

                var objects = new List<string>
                {
                    "<< /Type /Catalog /Pages 2 0 R >>",
                    BuildPagesObject(firstPageObjectNumber),
                    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"
                };

                foreach (var imageRef in imageRefs)
                {
                    objects.Add(BuildImageObject(imageRef.Image));
                }

                for (var index = 0; index < _pages.Count; index++)
                {
                    var pageObjectNumber = firstPageObjectNumber + (index * 2);
                    var contentObjectNumber = pageObjectNumber + 1;
                    objects.Add(BuildPageObject(contentObjectNumber, imageRefs));
                    objects.Add(BuildStreamObject(_pages[index].Build(index + 1, _pages.Count)));
                }

                var output = new StringBuilder();
                var offsets = new List<int> { 0 };
                output.Append("%PDF-1.4\n");

                for (var index = 0; index < objects.Count; index++)
                {
                    offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
                    output.AppendFormat(CultureInfo.InvariantCulture, "{0} 0 obj\n", index + 1);
                    output.Append(objects[index]);
                    output.Append("\nendobj\n");
                }

                var xrefOffset = Encoding.ASCII.GetByteCount(output.ToString());
                output.AppendFormat(CultureInfo.InvariantCulture, "xref\n0 {0}\n", objects.Count + 1);
                output.Append("0000000000 65535 f \n");

                foreach (var offset in offsets.Skip(1))
                {
                    output.AppendFormat(CultureInfo.InvariantCulture, "{0:0000000000} 00000 n \n", offset);
                }

                output.AppendFormat(CultureInfo.InvariantCulture, "trailer\n<< /Size {0} /Root 1 0 R >>\nstartxref\n{1}\n%%EOF", objects.Count + 1, xrefOffset);
                File.WriteAllBytes(filePath, Encoding.ASCII.GetBytes(output.ToString()));
            }

            private string BuildPagesObject(int firstPageObjectNumber)
            {
                var kids = string.Join(" ", Enumerable.Range(0, _pages.Count).Select(index => $"{firstPageObjectNumber + (index * 2)} 0 R"));
                return $"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>";
            }

            private string BuildPageObject(int contentObjectNumber, IReadOnlyList<PdfImageObjectRef> imageRefs)
            {
                var xObjects = imageRefs.Count == 0
                    ? string.Empty
                    : $" /XObject << {string.Join(" ", imageRefs.Select(item => $"/{item.Name} {item.ObjectNumber} 0 R"))} >>";

                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0.##} {PageHeight:0.##}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >>{xObjects} >> /Contents {contentObjectNumber} 0 R >>");
            }

            private static string BuildStreamObject(string content)
            {
                var byteLength = Encoding.ASCII.GetByteCount(content);
                return string.Create(CultureInfo.InvariantCulture, $"<< /Length {byteLength} >>\nstream\n{content}\nendstream");
            }

            private static string BuildImageObject(PdfImageAsset image)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length {image.HexRgbData.Length + 1} >>\nstream\n{image.HexRgbData}>\nendstream");
            }
        }

        private sealed class PdfPageContent
        {
            private readonly StringBuilder _content = new();

            public PdfPageContent(double width, double height)
            {
                Width = width;
                Height = height;
            }

            public double Width { get; }
            public double Height { get; }

            public void DrawText(string value, double x, double y, double size, bool bold, string color)
            {
                var (r, g, b) = ParseColor(color);
                _content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} rg\n", r, g, b);
                _content.Append("BT ");
                _content.Append(bold ? "/F2 " : "/F1 ");
                _content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##} Tf {1:0.##} {2:0.##} Td ", size, x, y);
                _content.Append('(');
                _content.Append(EscapePdfText(value));
                _content.Append(") Tj ET\n");
            }

            public void DrawCenteredText(string value, double centerX, double y, double size, bool bold, string color)
            {
                var estimatedWidth = EstimateTextWidth(value, size, bold);
                DrawText(value, centerX - (estimatedWidth / 2), y, size, bold, color);
            }

            public void DrawImage(string name, double x, double y, double width, double height)
            {
                _content.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "q {0:0.##} 0 0 {1:0.##} {2:0.##} {3:0.##} cm /{4} Do Q\n",
                    width,
                    height,
                    x,
                    y,
                    name);
            }

            public void DrawLine(double x1, double y1, double x2, double y2, string color, double width)
            {
                var (r, g, b) = ParseColor(color);
                _content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} RG {3:0.##} w {4:0.##} {5:0.##} m {6:0.##} {7:0.##} l S\n", r, g, b, width, x1, y1, x2, y2);
            }

            public void FillRect(double x, double y, double width, double height, string color)
            {
                var (r, g, b) = ParseColor(color);
                _content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} {5:0.##} {6:0.##} re f\n", r, g, b, x, y, width, height);
            }

            public void StrokeRect(double x, double y, double width, double height, string color, double strokeWidth)
            {
                var (r, g, b) = ParseColor(color);
                _content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} RG {3:0.##} w {4:0.##} {5:0.##} {6:0.##} {7:0.##} re S\n", r, g, b, strokeWidth, x, y, width, height);
            }

            public string Build(int pageNumber, int pageCount)
            {
                DrawText($"Page {pageNumber} of {pageCount}", Width - Margin - 62, Margin - 12, 8, bold: false, "#64748B");
                return _content.ToString();
            }

            public static IReadOnlyList<string> WrapText(string value, double maxWidth, double fontSize)
            {
                var clean = Sanitize(value);
                var maxChars = Math.Max(8, (int)Math.Floor(maxWidth / Math.Max(1, fontSize * 0.52)));
                var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var lines = new List<string>();
                var current = new StringBuilder();

                foreach (var word in words)
                {
                    if (word.Length > maxChars)
                    {
                        if (current.Length > 0)
                        {
                            lines.Add(current.ToString());
                            current.Clear();
                        }

                        lines.Add(word[..Math.Min(word.Length, maxChars)]);
                        continue;
                    }

                    var nextLength = current.Length == 0 ? word.Length : current.Length + 1 + word.Length;
                    if (nextLength > maxChars && current.Length > 0)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                    }

                    if (current.Length > 0)
                    {
                        current.Append(' ');
                    }

                    current.Append(word);
                }

                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                }

                return lines.Count == 0 ? new[] { "--" } : lines;
            }

            private static string EscapePdfText(string value)
            {
                var sanitized = new StringBuilder();
                foreach (var character in value)
                {
                    sanitized.Append(character is >= ' ' and <= '~' ? character : ' ');
                }

                return sanitized
                    .Replace("\\", "\\\\")
                    .Replace("(", "\\(")
                    .Replace(")", "\\)")
                    .ToString();
            }

            private static double EstimateTextWidth(string value, double size, bool bold)
            {
                var factor = bold ? 0.56 : 0.52;
                return value.Length * size * factor;
            }

            private static (double Red, double Green, double Blue) ParseColor(string color)
            {
                var hex = color.TrimStart('#');
                var red = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
                var green = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
                var blue = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
                return (red, green, blue);
            }
        }

        private sealed record PdfImageObjectRef(string Name, PdfImageAsset Image, int ObjectNumber);

        private sealed class PdfImageAsset
        {
            private PdfImageAsset(int width, int height, string hexRgbData)
            {
                Width = width;
                Height = height;
                HexRgbData = hexRgbData;
            }

            public int Width { get; }
            public int Height { get; }
            public string HexRgbData { get; }

            public static PdfImageAsset? TryLoad(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                try
                {
                    if (LocalImageLoader.Load(path) is not BitmapSource bitmapSource)
                    {
                        return null;
                    }

                    var converted = new FormatConvertedBitmap(bitmapSource, PixelFormats.Pbgra32, null, 0);
                    converted.Freeze();

                    var width = converted.PixelWidth;
                    var height = converted.PixelHeight;
                    var stride = width * 4;
                    var pixels = new byte[stride * height];
                    converted.CopyPixels(pixels, stride, 0);

                    var hex = new StringBuilder(width * height * 6);
                    for (var index = 0; index < pixels.Length; index += 4)
                    {
                        var blue = pixels[index];
                        var green = pixels[index + 1];
                        var red = pixels[index + 2];
                        var alpha = pixels[index + 3];

                        // Flatten transparent PNG pixels onto white for a predictable PDF header.
                        var flattenedRed = Math.Min(255, red + (255 - alpha));
                        var flattenedGreen = Math.Min(255, green + (255 - alpha));
                        var flattenedBlue = Math.Min(255, blue + (255 - alpha));

                        hex.Append(flattenedRed.ToString("X2", CultureInfo.InvariantCulture));
                        hex.Append(flattenedGreen.ToString("X2", CultureInfo.InvariantCulture));
                        hex.Append(flattenedBlue.ToString("X2", CultureInfo.InvariantCulture));
                    }

                    return new PdfImageAsset(width, height, hex.ToString());
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}

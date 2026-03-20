using System.Diagnostics;
using System.IO;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    public sealed class TesseractCliOcrService : IOcrService
    {
        private readonly string _tesseractExePath;
        private readonly string _language;
        private readonly string _pageSegmentationMode;
        private readonly double _minimumLineConfidence;

        public TesseractCliOcrService()
            : this(OcrRuntimeOptions.Load())
        {
        }

        public TesseractCliOcrService(OcrRuntimeOptions options)
        {
            _tesseractExePath = options.TesseractExePath ?? @"C:\Program Files\Tesseract-OCR\tesseract.exe";
            _language = options.TesseractLanguage ?? "eng";
            _pageSegmentationMode = options.TesseractPageSegmentationMode ?? "6";
            _minimumLineConfidence = double.TryParse(options.TesseractMinimumLineConfidence, out var parsedConfidence)
                ? parsedConfidence
                : 35;
        }

        public async Task<OcrHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_tesseractExePath))
            {
                return new OcrHealthResult(false, $"tesseract.exe not found at {_tesseractExePath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _tesseractExePath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new OcrHealthResult(false, "Failed to start Tesseract process.");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0
                ? new OcrHealthResult(true, output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Tesseract ready")
                : new OcrHealthResult(false, "Tesseract returned a non-zero exit code.");
        }

        public async Task<IReadOnlyList<string>> ExtractNamesAsync(
            string imagePath,
            IReadOnlyList<string>? hintNames = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file was not found.", imagePath);
            }

            if (!File.Exists(_tesseractExePath))
            {
                throw new FileNotFoundException("Tesseract executable was not found.", _tesseractExePath);
            }

            var tempOutputBase = Path.Combine(Path.GetTempPath(), $"cfw-ocr-{Guid.NewGuid():N}");
            var userWordsPath = hintNames is { Count: > 0 }
                ? BuildUserWordsFile(hintNames)
                : null;
            var startInfo = new ProcessStartInfo
            {
                FileName = _tesseractExePath,
                Arguments =
                    BuildArguments(imagePath, tempOutputBase, userWordsPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Tesseract process.");
                }

                var stdError = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Tesseract OCR failed: {stdError}");
                }

                var outputPath = $"{tempOutputBase}.tsv";
                if (!File.Exists(outputPath))
                {
                    return Array.Empty<string>();
                }

                var text = await File.ReadAllTextAsync(outputPath, cancellationToken);
                return ParseNamesFromTsv(text, _minimumLineConfidence);
            }
            finally
            {
                var outputPath = $"{tempOutputBase}.tsv";
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                if (!string.IsNullOrWhiteSpace(userWordsPath) && File.Exists(userWordsPath))
                {
                    File.Delete(userWordsPath);
                }
            }
        }

        private string BuildArguments(string imagePath, string tempOutputBase, string? userWordsPath)
        {
            var userWordsArg = string.IsNullOrWhiteSpace(userWordsPath)
                ? string.Empty
                : $" --user-words \"{userWordsPath}\"";

            return
                $"\"{imagePath}\" \"{tempOutputBase}\" -l {_language} --psm {_pageSegmentationMode}{userWordsArg} tsv";
        }

        private static string BuildUserWordsFile(IReadOnlyList<string> hintNames)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"cfw-user-words-{Guid.NewGuid():N}.txt");
            var words = hintNames
                .SelectMany(name => name
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Concat(new[] { name }))
                .Select(word => word.Trim())
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            File.WriteAllLines(tempPath, words);
            return tempPath;
        }

        private static IReadOnlyList<string> ParseNamesFromTsv(string tsvText, double minimumLineConfidence)
        {
            var rows = tsvText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(ParseTsvRow)
                .Where(row => row != null)
                .Cast<TesseractWordRow>()
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .ToList();

            var groupedLines = rows
                .GroupBy(row => $"{row.BlockNum}:{row.ParNum}:{row.LineNum}")
                .Select(group =>
                {
                    var words = group
                        .OrderBy(row => row.WordNum)
                        .Select(row => RemoveNoise(row.Text))
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .ToList();

                    var averageConfidence = group
                        .Where(row => row.Confidence >= 0)
                        .DefaultIfEmpty()
                        .Average(row => row?.Confidence ?? 0);

                    return new
                    {
                        Text = string.Join(" ", words),
                        Confidence = averageConfidence
                    };
                })
                .Where(line => line.Confidence >= minimumLineConfidence)
                .Select(line => line.Text.Trim())
                .Where(IsLikelyPersonName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return groupedLines;
        }

        private static TesseractWordRow? ParseTsvRow(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 12)
            {
                return null;
            }

            if (!int.TryParse(parts[2], out var blockNum) ||
                !int.TryParse(parts[3], out var parNum) ||
                !int.TryParse(parts[4], out var lineNum) ||
                !int.TryParse(parts[5], out var wordNum) ||
                !double.TryParse(parts[10], out var confidence))
            {
                return null;
            }

            return new TesseractWordRow(
                blockNum,
                parNum,
                lineNum,
                wordNum,
                confidence,
                parts[11]);
        }

        private static string RemoveNoise(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (char.IsLetter(character) || char.IsWhiteSpace(character) || character is '.' or '-')
                {
                    builder.Append(character);
                }
            }

            return string.Join(" ", builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool IsLikelyPersonName(string value)
        {
            var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || tokens.Length > 4)
            {
                return false;
            }

            var shortTokens = tokens.Count(token => token.Length == 1);
            if (shortTokens > 1)
            {
                return false;
            }

            return tokens.All(token =>
                token.Any(char.IsLetter) &&
                token.Count(char.IsLetter) >= Math.Max(1, token.Length / 2));
        }

        private sealed record TesseractWordRow(
            int BlockNum,
            int ParNum,
            int LineNum,
            int WordNum,
            double Confidence,
            string Text);
    }
}

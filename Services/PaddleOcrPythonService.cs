using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public sealed class PaddleOcrPythonService : IOcrService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly HashSet<string> HeaderNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "ATTENDANCE",
            "BARANGAY",
            "BENEFICIARY",
            "CASH",
            "DATE",
            "EVENT",
            "FOR",
            "HOUSEHOLD",
            "IN",
            "LOCATION",
            "LOGBOOK",
            "NAME",
            "NAMES",
            "OUT",
            "PRESENT",
            "REMARKS",
            "SIGNATURE",
            "STATUS",
            "TIME",
            "WORK"
        };

        private readonly string _pythonExePath;
        private readonly string _scriptPath;
        private readonly string _textDetectionModel;
        private readonly string _textRecognitionModel;
        private readonly double _minimumLineConfidence;
        private readonly bool _disableMkldnn;
        private readonly int _timeoutSeconds;

        public PaddleOcrPythonService()
            : this(OcrRuntimeOptions.Load())
        {
        }

        public PaddleOcrPythonService(OcrRuntimeOptions options)
        {
            _pythonExePath = string.IsNullOrWhiteSpace(options.PaddlePythonExePath)
                ? "python"
                : options.PaddlePythonExePath;

            var configuredScriptPath = string.IsNullOrWhiteSpace(options.PaddleScriptPath)
                ? Path.Combine("ocr", "paddle_ocr_engine.py")
                : options.PaddleScriptPath;

            _scriptPath = Path.IsPathRooted(configuredScriptPath)
                ? configuredScriptPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredScriptPath);

            _textDetectionModel = string.IsNullOrWhiteSpace(options.PaddleTextDetectionModel)
                ? "PP-OCRv5_mobile_det"
                : options.PaddleTextDetectionModel;
            _textRecognitionModel = string.IsNullOrWhiteSpace(options.PaddleTextRecognitionModel)
                ? "en_PP-OCRv5_mobile_rec"
                : options.PaddleTextRecognitionModel;
            _minimumLineConfidence = double.TryParse(options.PaddleMinimumLineConfidence, out var parsedConfidence)
                ? parsedConfidence
                : 0.55;
            _disableMkldnn = options.PaddleDisableMkldnn;
            _timeoutSeconds = options.PaddleTimeoutSeconds > 0
                ? options.PaddleTimeoutSeconds
                : 300;
        }

        public async Task<OcrHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_scriptPath))
            {
                return new OcrHealthResult(false, $"OCR script not found at {_scriptPath}");
            }

            try
            {
                var responseText = await RunPythonAsync("--health", cancellationToken);
                var response = JsonSerializer.Deserialize<PaddleHealthResponse>(responseText, JsonOptions);

                if (response?.Success == true)
                {
                    return new OcrHealthResult(true, response.Detail ?? "PaddleOCR is ready.");
                }

                return new OcrHealthResult(false, response?.Error ?? "PaddleOCR health check failed.");
            }
            catch (Exception ex)
            {
                return new OcrHealthResult(false, ex.Message);
            }
        }

        public async Task<IReadOnlyList<string>> ExtractNamesAsync(
            string imagePath,
            IReadOnlyList<string>? hintNames = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file was not found.", imagePath);
            }

            if (!File.Exists(_scriptPath))
            {
                throw new FileNotFoundException("PaddleOCR script was not found.", _scriptPath);
            }

            var responseText = await RunPythonAsync(
                $"\"{imagePath}\" --det-model \"{_textDetectionModel}\" --rec-model \"{_textRecognitionModel}\" --disable-mkldnn {_disableMkldnn.ToString().ToLowerInvariant()}",
                cancellationToken);
            var response = JsonSerializer.Deserialize<PaddleProcessResponse>(responseText, JsonOptions)
                ?? throw new InvalidOperationException("PaddleOCR returned an unreadable response.");

            if (!response.Success)
            {
                throw new InvalidOperationException(response.Error ?? "PaddleOCR processing failed.");
            }

            return ParseCandidateNames(response.Lines ?? new List<PaddleOcrLine>(), hintNames, _minimumLineConfidence);
        }

        private async Task<string> RunPythonAsync(string arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonExePath,
                Arguments = $"\"{_scriptPath}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start the PaddleOCR Python process.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to start Python OCR process via '{_pythonExePath}': {ex.Message}", ex);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? $"PaddleOCR process exited with code {process.ExitCode}."
                    : $"PaddleOCR process exited with code {process.ExitCode}: {error}");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? "PaddleOCR returned no output."
                    : error);
            }

            return output;
        }

        private static IReadOnlyList<string> ParseCandidateNames(
            IReadOnlyList<PaddleOcrLine> lines,
            IReadOnlyList<string>? hintNames,
            double minimumLineConfidence)
        {
            var filteredLines = lines
                .Where(line => line.Confidence >= minimumLineConfidence)
                .OrderBy(line => line.Position?.Top ?? double.MaxValue)
                .ThenBy(line => line.Position?.Left ?? double.MaxValue)
                .ToList();

            var normalizedHints = (hintNames ?? Array.Empty<string>())
                .Select(name => new HintCandidate(name, NormalizeName(name)))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Normalized))
                .DistinctBy(candidate => candidate.Normalized, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedHints.Count > 0 && filteredLines.Count > 0)
            {
                var rowCandidates = BuildRowCandidates(filteredLines);
                var matchedHints = rowCandidates
                    .Select(candidate => FindBestHintMatch(candidate, normalizedHints))
                    .Where(match => match != null && match.Score >= 0.45)
                    .Select(match => match!.Original)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (matchedHints.Count > 0)
                {
                    return matchedHints;
                }
            }

            return filteredLines
                .Select(line => CleanText(line.Text))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Where(text => !ContainsHeaderNoise(text))
                .Where(text => IsLikelyPersonName(text) || MatchesKnownName(text, normalizedHints.Select(h => h.Normalized).ToList()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<string> BuildRowCandidates(IReadOnlyList<PaddleOcrLine> lines)
        {
            var lineHeights = lines
                .Select(line => line.Position == null ? 0 : line.Position.Bottom - line.Position.Top)
                .Where(height => height > 1)
                .OrderBy(height => height)
                .ToList();

            var medianHeight = lineHeights.Count == 0
                ? 60
                : lineHeights[lineHeights.Count / 2];
            var rowTolerance = Math.Clamp(medianHeight * 0.85, 40, 140);

            var rows = new List<List<PaddleOcrLine>>();
            foreach (var line in lines)
            {
                if (rows.Count == 0)
                {
                    rows.Add(new List<PaddleOcrLine> { line });
                    continue;
                }

                var currentRow = rows[^1];
                var rowAnchor = currentRow.Average(item => item.Position?.Top ?? 0);
                var top = line.Position?.Top ?? double.MaxValue;

                if (Math.Abs(top - rowAnchor) <= rowTolerance)
                {
                    currentRow.Add(line);
                }
                else
                {
                    rows.Add(new List<PaddleOcrLine> { line });
                }
            }

            return rows
                .Select(row => string.Join(" ", row
                    .OrderBy(item => item.Position?.Left ?? double.MaxValue)
                    .Select(item => CleanText(item.Text))
                    .Where(text => !string.IsNullOrWhiteSpace(text))))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .ToList();
        }

        private static HintCandidate? FindBestHintMatch(string candidate, IReadOnlyList<HintCandidate> hints)
        {
            var normalizedCandidate = NormalizeName(candidate);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                return null;
            }

            var compactCandidate = normalizedCandidate.Replace(" ", string.Empty, StringComparison.Ordinal);
            var shortCandidate = ToShortName(normalizedCandidate);
            HintCandidate? bestHint = null;
            var bestScore = 0d;

            foreach (var hint in hints)
            {
                var compactHint = hint.Normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
                var score = new[]
                {
                    CalculateSimilarity(normalizedCandidate, hint.Normalized),
                    CalculateSimilarity(compactCandidate, compactHint),
                    CalculateSimilarity(shortCandidate, ToShortName(hint.Normalized)),
                    CalculateTokenEvidence(normalizedCandidate, hint.Normalized)
                }.Max();

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHint = hint with { Score = score };
                }
            }

            return bestHint;
        }

        private static double CalculateTokenEvidence(string candidate, string hint)
        {
            var candidateTokens = candidate
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var hintTokens = hint
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (candidateTokens.Length == 0 || hintTokens.Length == 0)
            {
                return 0;
            }

            return candidateTokens
                .Select(candidateToken => hintTokens
                    .Select(hintToken => CalculateSimilarity(candidateToken, hintToken))
                    .DefaultIfEmpty(0)
                    .Max())
                .Average();
        }

        private static string CleanText(string raw)
        {
            var builder = new StringBuilder(raw.Length);
            foreach (var character in raw)
            {
                if (char.IsLetter(character) || char.IsWhiteSpace(character) || character is '.' or '-' or '\'')
                {
                    builder.Append(character);
                }
            }

            var cleaned = string.Join(" ", builder.ToString()
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

            return cleaned.Replace("0", "O", StringComparison.Ordinal);
        }

        private static bool ContainsHeaderNoise(string value)
        {
            var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return tokens.Any(token => HeaderNoiseTokens.Contains(token));
        }

        private static bool MatchesKnownName(string value, IReadOnlyList<string> normalizedHints)
        {
            if (normalizedHints.Count == 0)
            {
                return false;
            }

            var normalizedValue = NormalizeName(value);
            var bestScore = normalizedHints
                .Select(hint => CalculateSimilarity(normalizedValue, hint))
                .DefaultIfEmpty(0)
                .Max();

            return bestScore >= 0.55;
        }

        private static bool IsLikelyPersonName(string value)
        {
            var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || tokens.Length > 5)
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

        private static string NormalizeName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var character in name.ToUpperInvariant())
            {
                if (char.IsLetter(character) || char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            return string.Join(" ", builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static double CalculateSimilarity(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return 0;
            }

            if (left == right)
            {
                return 1;
            }

            var fullScore = 1.0 - (double)LevenshteinDistance(left, right) / Math.Max(left.Length, right.Length);
            var shortScore = 1.0 - (double)LevenshteinDistance(ToShortName(left), ToShortName(right)) / Math.Max(ToShortName(left).Length, ToShortName(right).Length);
            return Math.Max(fullScore, shortScore);
        }

        private static string ToShortName(string name)
        {
            var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length <= 2
                ? name
                : $"{tokens[0]} {tokens[^1]}";
        }

        private static int LevenshteinDistance(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return right.Length;
            }

            if (string.IsNullOrEmpty(right))
            {
                return left.Length;
            }

            var costs = new int[right.Length + 1];
            for (var index = 0; index <= right.Length; index++)
            {
                costs[index] = index;
            }

            for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
            {
                var previousDiagonal = costs[0];
                costs[0] = leftIndex;

                for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
                {
                    var temp = costs[rightIndex];
                    var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                    costs[rightIndex] = Math.Min(
                        Math.Min(costs[rightIndex] + 1, costs[rightIndex - 1] + 1),
                        previousDiagonal + substitutionCost);
                    previousDiagonal = temp;
                }
            }

            return costs[right.Length];
        }

        private sealed class PaddleHealthResponse
        {
            public bool Success { get; set; }
            public string? Detail { get; set; }
            public string? Error { get; set; }
        }

        private sealed class PaddleProcessResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public List<PaddleOcrLine>? Lines { get; set; }
        }

        private sealed class PaddleOcrLine
        {
            public string Text { get; set; } = string.Empty;
            public double Confidence { get; set; }
            public PaddlePosition? Position { get; set; }
        }

        private sealed class PaddlePosition
        {
            public double Top { get; set; }
            public double Left { get; set; }
            public double Right { get; set; }
            public double Bottom { get; set; }
        }

        private sealed record HintCandidate(string Original, string Normalized)
        {
            public double Score { get; init; }
        }
    }
}

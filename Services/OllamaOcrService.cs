using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO;

namespace AttendanceShiftingManagement.Services
{
    public sealed class OllamaOcrService : IOcrService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;

        public OllamaOcrService()
            : this(OcrRuntimeOptions.Load(), null)
        {
        }

        public OllamaOcrService(OcrRuntimeOptions options, string? modelOverride = null)
        {
            var baseUrl = options.BaseUrl ?? "http://localhost:11434";
            _model = string.IsNullOrWhiteSpace(modelOverride)
                ? options.Model ?? "richardyoung/olmocr2:7b-q8"
                : modelOverride;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
            };
        }

        public async Task<OcrHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync("api/version", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new OcrHealthResult(false, $"HTTP {(int)response.StatusCode}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var version = document.RootElement.TryGetProperty("version", out var versionElement)
                    ? versionElement.GetString() ?? "unknown"
                    : "unknown";

                return new OcrHealthResult(true, version);
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

            var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            var imageBase64 = Convert.ToBase64String(imageBytes);
            var hintText = hintNames is { Count: > 0 }
                ? $"Possible participant names include: {string.Join(", ", hintNames.Take(30))}. "
                : string.Empty;

            var payload = new
            {
                model = _model,
                stream = false,
                format = "json",
                options = new
                {
                    temperature = 0
                },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content =
                            hintText +
                            "You are reading a handwritten attendance logbook image. " +
                            "Extract only person names from the page. " +
                            "Return strict JSON with this shape: {\"names\":[\"Full Name\"]}. " +
                            "Do not include dates, signatures, check marks, titles, counts, or any explanation. " +
                            "If a name is unreadable, omit it instead of guessing.",
                        images = new[] { imageBase64 }
                    }
                }
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync("api/chat", request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Ollama OCR request failed: {response.StatusCode} {responseBody}");
            }

            using var envelope = JsonDocument.Parse(responseBody);
            var content = envelope.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return ParseNamesFromResponse(content);
        }

        private static IReadOnlyList<string> ParseNamesFromResponse(string content)
        {
            var cleaned = ExtractJsonPayload(content);

            try
            {
                using var document = JsonDocument.Parse(cleaned);
                if (document.RootElement.TryGetProperty("names", out var namesElement) &&
                    namesElement.ValueKind == JsonValueKind.Array)
                {
                    return namesElement
                        .EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()?.Trim() ?? string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch
            {
                // Fall back to line parsing below.
            }

            return content
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().TrimStart('-', '*', '•'))
                .Select(RemoveLeadingNumbering)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ExtractJsonPayload(string content)
        {
            var trimmed = content.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewLine = trimmed.IndexOf('\n');
                var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                {
                    trimmed = trimmed.Substring(firstNewLine + 1, lastFence - firstNewLine - 1).Trim();
                }
            }

            return trimmed;
        }

        private static string RemoveLeadingNumbering(string value)
        {
            var trimmed = value.Trim();
            var index = 0;
            while (index < trimmed.Length && (char.IsDigit(trimmed[index]) || trimmed[index] is '.' or ')' or '-'))
            {
                index++;
            }

            return trimmed[index..].Trim();
        }
    }

}

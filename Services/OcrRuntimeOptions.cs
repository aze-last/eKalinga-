using Microsoft.Extensions.Configuration;

namespace AttendanceShiftingManagement.Services
{
    public sealed class OcrRuntimeOptions
    {
        public string Provider { get; set; } = "Tesseract";
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "richardyoung/olmocr2:7b-q8";
        public string TesseractExePath { get; set; } = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
        public string TesseractLanguage { get; set; } = "eng";
        public string TesseractPageSegmentationMode { get; set; } = "6";
        public string TesseractMinimumLineConfidence { get; set; } = "35";
        public List<OcrProfile> Profiles { get; set; } = new();

        public static OcrRuntimeOptions Load()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = configuration
                .GetSection("Ocr")
                .Get<OcrRuntimeOptions>() ?? new OcrRuntimeOptions();

            if (options.Profiles.Count == 0)
            {
                options.Profiles.Add(new OcrProfile
                {
                    Name = "Tesseract (CPU)",
                    Provider = "Tesseract"
                });
                options.Profiles.Add(new OcrProfile
                {
                    Name = $"Ollama - {options.Model}",
                    Provider = "Ollama",
                    Model = options.Model
                });
                options.Profiles.Add(new OcrProfile
                {
                    Name = "Ollama - qwen3-vl:8b",
                    Provider = "Ollama",
                    Model = "qwen3-vl:8b"
                });
            }

            return options;
        }
    }

    public sealed class OcrProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Provider { get; set; } = "Tesseract";
        public string? Model { get; set; }
    }

    public static class OcrServiceFactory
    {
        public static IOcrService Create(OcrRuntimeOptions options, OcrProfile? profile)
        {
            var selectedProvider = profile?.Provider ?? options.Provider;
            return selectedProvider.Equals("Tesseract", StringComparison.OrdinalIgnoreCase)
                ? new TesseractCliOcrService(options)
                : new OllamaOcrService(options, profile?.Model);
        }
    }
}

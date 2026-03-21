using Microsoft.Extensions.Configuration;
using System.IO;

namespace AttendanceShiftingManagement.Services
{
    public sealed class OcrRuntimeOptions
    {
        public string Provider { get; set; } = "PaddleOCR";
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "richardyoung/olmocr2:7b-q8";
        public string TesseractExePath { get; set; } = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
        public string TesseractLanguage { get; set; } = "eng";
        public string TesseractPageSegmentationMode { get; set; } = "6";
        public string TesseractMinimumLineConfidence { get; set; } = "35";
        public string PaddlePythonExePath { get; set; } = "python";
        public string PaddleScriptPath { get; set; } = Path.Combine("ocr", "paddle_ocr_engine.py");
        public string PaddleTextDetectionModel { get; set; } = "PP-OCRv5_mobile_det";
        public string PaddleTextRecognitionModel { get; set; } = "en_PP-OCRv5_mobile_rec";
        public string PaddleMinimumLineConfidence { get; set; } = "0.55";
        public bool PaddleDisableMkldnn { get; set; } = true;
        public int PaddleTimeoutSeconds { get; set; } = 300;
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
                    Name = "PaddleOCR (Local Python)",
                    Provider = "PaddleOCR"
                });
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
                options.Profiles.Add(new OcrProfile
                {
                    Name = "Ollama - aha2025/llama-joycaption-beta-one-hf-llava:Q:8",
                    Provider = "Ollama",
                    Model = "aha2025/llama-joycaption-beta-one-hf-llava:Q:8"
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

            if (selectedProvider.Equals("PaddleOCR", StringComparison.OrdinalIgnoreCase) ||
                selectedProvider.Equals("Paddle", StringComparison.OrdinalIgnoreCase))
            {
                return new PaddleOcrPythonService(options);
            }

            if (selectedProvider.Equals("Tesseract", StringComparison.OrdinalIgnoreCase))
            {
                return new TesseractCliOcrService(options);
            }

            return new OllamaOcrService(options, profile?.Model);
        }
    }
}

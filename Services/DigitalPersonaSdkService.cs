using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.IO;

namespace AttendanceShiftingManagement.Services
{
    public class DigitalPersonaSdkService
    {
        private const int DefaultEnrollmentSampleCount = 4;

        private readonly string? _nativePath;
        private readonly int _defaultCaptureTimeoutMs;
        private readonly int _defaultMatchThreshold;

        public DigitalPersonaSdkService()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            _nativePath = config["Biometrics:DigitalPersonaNativePath"];
            _defaultCaptureTimeoutMs = config.GetValue("Biometrics:CaptureTimeoutMs", 10000);
            _defaultMatchThreshold = config.GetValue("Biometrics:MatchThreshold", 21474);
        }

        public bool IsSdkAvailable(out string message)
        {
            message = BuildUnavailableMessage();
            return false;
        }

        public FingerprintDeviceStatus GetDeviceStatus()
        {
            return new FingerprintDeviceStatus
            {
                IsConnected = false,
                Message = BuildUnavailableMessage()
            };
        }

        public FingerprintEnrollmentSampleResult CaptureEnrollmentSample(int? timeoutMs = null)
        {
            _ = timeoutMs.GetValueOrDefault(_defaultCaptureTimeoutMs);
            throw new InvalidOperationException(BuildUnavailableMessage("capture enrollment samples"));
        }

        public FingerprintTemplateBuildResult BuildTemplate(IReadOnlyCollection<byte[]> samplePayloads)
        {
            if (samplePayloads == null || samplePayloads.Count == 0)
            {
                throw new ArgumentException("At least one fingerprint sample is required to build a template.", nameof(samplePayloads));
            }

            var acceptedSamples = samplePayloads.Count(payload => payload != null && payload.Length > 0);
            if (acceptedSamples == 0)
            {
                throw new InvalidOperationException("Fingerprint sample payloads are empty.");
            }

            if (acceptedSamples < DefaultEnrollmentSampleCount)
            {
                return new FingerprintTemplateBuildResult
                {
                    IsReady = false,
                    SamplesAccepted = acceptedSamples,
                    SamplesRemaining = DefaultEnrollmentSampleCount - acceptedSamples,
                    StatusMessage = $"{DefaultEnrollmentSampleCount - acceptedSamples} more capture(s) needed to build the template."
                };
            }

            var normalizedPayloads = samplePayloads
                .Where(payload => payload != null && payload.Length > 0)
                .Take(DefaultEnrollmentSampleCount)
                .OrderBy(payload => Convert.ToHexString(SHA256.HashData(payload)))
                .ToList();

            using var memory = new MemoryStream();
            foreach (var payload in normalizedPayloads)
            {
                var lengthBytes = BitConverter.GetBytes(payload.Length);
                memory.Write(lengthBytes, 0, lengthBytes.Length);
                memory.Write(payload, 0, payload.Length);
            }

            var templateBytes = SHA256.HashData(memory.ToArray());

            return new FingerprintTemplateBuildResult
            {
                IsReady = true,
                TemplateData = templateBytes,
                SamplesAccepted = DefaultEnrollmentSampleCount,
                SamplesRemaining = 0,
                StatusMessage = "Template ready. Click Save Ready Template to store it."
            };
        }

        public int GetEnrollmentRequiredSampleCount()
        {
            return DefaultEnrollmentSampleCount;
        }

        public FingerprintVerificationProbe CaptureVerificationProbe(int? timeoutMs = null)
        {
            _ = timeoutMs.GetValueOrDefault(_defaultCaptureTimeoutMs);
            throw new InvalidOperationException(BuildUnavailableMessage("capture verification probes"));
        }

        public FingerprintVerifyResult VerifyProbeAgainstTemplate(byte[] probeData, byte[] enrolledTemplate, int? matchThreshold = null)
        {
            if (probeData == null || probeData.Length == 0)
            {
                throw new ArgumentException("Fingerprint probe data is required.", nameof(probeData));
            }

            if (enrolledTemplate == null || enrolledTemplate.Length == 0)
            {
                throw new ArgumentException("Fingerprint template data is required.", nameof(enrolledTemplate));
            }

            var threshold = matchThreshold.GetValueOrDefault(_defaultMatchThreshold);
            var probeHash = SHA256.HashData(probeData);
            var templateHash = SHA256.HashData(enrolledTemplate);
            var score = CalculateDistance(probeHash, templateHash);

            return new FingerprintVerifyResult
            {
                IsMatch = score <= Math.Min(threshold, 32),
                Score = score,
                Threshold = threshold
            };
        }

        private string BuildUnavailableMessage(string action = "use the fingerprint scanner")
        {
            var configuredPath = string.IsNullOrWhiteSpace(_nativePath)
                ? string.Empty
                : $" Configured SDK path: {_nativePath}.";

            return $"Fingerprint integration is unavailable in this build because the DigitalPersona SDK is not installed. Unable to {action}.{configuredPath}";
        }

        private static int CalculateDistance(IReadOnlyList<byte> left, IReadOnlyList<byte> right)
        {
            var length = Math.Min(left.Count, right.Count);
            var distance = 0;

            for (var index = 0; index < length; index++)
            {
                if (left[index] != right[index])
                {
                    distance++;
                }
            }

            distance += Math.Abs(left.Count - right.Count);
            return distance;
        }
    }

    public class FingerprintEnrollmentSampleResult
    {
        public byte[] SampleData { get; set; } = Array.Empty<byte>();
        public int? QualityScore { get; set; }
        public string ReaderDescription { get; set; } = string.Empty;
        public bool IsUsableForEnrollment { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }

    public class FingerprintTemplateBuildResult
    {
        public bool IsReady { get; set; }
        public byte[] TemplateData { get; set; } = Array.Empty<byte>();
        public int SamplesAccepted { get; set; }
        public int SamplesRemaining { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    public class FingerprintVerificationProbe
    {
        public byte[] FeatureSetData { get; set; } = Array.Empty<byte>();
        public int? QualityScore { get; set; }
        public string ReaderDescription { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
    }

    public class FingerprintVerifyResult
    {
        public bool IsMatch { get; set; }
        public int Score { get; set; }
        public int Threshold { get; set; }
    }

    public class FingerprintDeviceStatus
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace AttendanceShiftingManagement.Services
{
    public class DigitalPersonaSdkService
    {
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

            PrepareRuntime();
        }

        public bool IsSdkAvailable(out string message)
        {
            try
            {
                var readers = GetReadersCollection();
                readers.Refresh();
                message = "DigitalPersona One Touch SDK is available.";
                return true;
            }
            catch (Exception ex)
            {
                message = BuildDetailedExceptionMessage(ex);
                return false;
            }
        }

        public FingerprintDeviceStatus GetDeviceStatus()
        {
            try
            {
                var readers = GetReadersCollection();
                readers.Refresh();

                if (readers.Count == 0)
                {
                    return new FingerprintDeviceStatus
                    {
                        IsConnected = false,
                        Message = "DigitalPersona SDK loaded but no fingerprint scanner was detected."
                    };
                }

                var reader = readers.Values[0];
                return new FingerprintDeviceStatus
                {
                    IsConnected = true,
                    Message = $"Scanner detected: {BuildReaderDescription(reader)}"
                };
            }
            catch (Exception ex)
            {
                return new FingerprintDeviceStatus
                {
                    IsConnected = false,
                    Message = $"SDK loaded but scanner is not ready: {BuildDetailedExceptionMessage(ex)}"
                };
            }
        }

        public FingerprintEnrollmentSampleResult CaptureEnrollmentSample(int? timeoutMs = null)
        {
            var capture = CaptureSingleSample(timeoutMs);
            var extraction = ExtractFeatureSet(capture.Sample, DataPurpose.Enrollment);
            var feedback = ResolveFeedback(extraction, capture);
            var hasFeatureSet = extraction.FeatureSet != null;

            return new FingerprintEnrollmentSampleResult
            {
                SampleData = hasFeatureSet ? SerializeSample(capture.Sample) : Array.Empty<byte>(),
                QualityScore = ToQualityScore(feedback),
                ReaderDescription = capture.ReaderDescription,
                IsUsableForEnrollment = hasFeatureSet && feedback == CaptureFeedback.Good,
                Feedback = BuildCaptureFeedbackMessage(feedback, extraction.ErrorMessage, DataPurpose.Enrollment, hasFeatureSet)
            };
        }

        public FingerprintTemplateBuildResult BuildTemplate(IReadOnlyCollection<byte[]> samplePayloads)
        {
            if (samplePayloads == null || samplePayloads.Count == 0)
            {
                throw new ArgumentException("At least one fingerprint sample is required to build a template.", nameof(samplePayloads));
            }

            var enrollment = new Enrollment();
            string? lastRejectedFeedback = null;
            int acceptedSamples = 0;

            foreach (var payload in samplePayloads)
            {
                var sample = DeserializeSample(payload);
                var extraction = ExtractFeatureSet(sample, DataPurpose.Enrollment);
                if (extraction.FeatureSet == null || extraction.Feedback != CaptureFeedback.Good)
                {
                    lastRejectedFeedback = BuildCaptureFeedbackMessage(
                        extraction.Feedback,
                        extraction.ErrorMessage,
                        DataPurpose.Enrollment,
                        hasFeatureSet: false);
                    continue;
                }

                enrollment.AddFeatures(extraction.FeatureSet);
                acceptedSamples++;

                if (enrollment.TemplateStatus == Enrollment.Status.Ready ||
                    enrollment.TemplateStatus == Enrollment.Status.Failed)
                {
                    break;
                }
            }

            if (enrollment.TemplateStatus == Enrollment.Status.Ready && enrollment.Template != null)
            {
                return new FingerprintTemplateBuildResult
                {
                    IsReady = true,
                    TemplateData = SerializeTemplate(enrollment.Template),
                    SamplesAccepted = acceptedSamples,
                    SamplesRemaining = 0,
                    StatusMessage = "Template ready. Click Save Ready Template to store it."
                };
            }

            if (enrollment.TemplateStatus == Enrollment.Status.Failed)
            {
                throw new InvalidOperationException("Fingerprint enrollment failed. Clear the current captures and try again.");
            }

            var remaining = (int)enrollment.FeaturesNeeded;
            var statusMessage = remaining > 0
                ? $"{remaining} more capture(s) needed to build the template."
                : "Capture another sample to continue enrollment.";

            if (!string.IsNullOrWhiteSpace(lastRejectedFeedback))
            {
                statusMessage = $"Last sample feedback: {lastRejectedFeedback}. {statusMessage}";
            }

            return new FingerprintTemplateBuildResult
            {
                IsReady = false,
                SamplesAccepted = acceptedSamples,
                SamplesRemaining = remaining,
                StatusMessage = statusMessage
            };
        }

        public int GetEnrollmentRequiredSampleCount()
        {
            return (int)new Enrollment().FeaturesNeeded;
        }

        public FingerprintVerificationProbe CaptureVerificationProbe(int? timeoutMs = null)
        {
            var capture = CaptureSingleSample(timeoutMs);
            var extraction = ExtractFeatureSet(capture.Sample, DataPurpose.Verification);
            var feedback = ResolveFeedback(extraction, capture);

            if (extraction.FeatureSet == null || feedback != CaptureFeedback.Good)
            {
                throw new InvalidOperationException(
                    BuildCaptureFeedbackMessage(feedback, extraction.ErrorMessage, DataPurpose.Verification, hasFeatureSet: false));
            }

            return new FingerprintVerificationProbe
            {
                FeatureSetData = SerializeFeatureSet(extraction.FeatureSet),
                QualityScore = ToQualityScore(feedback),
                ReaderDescription = capture.ReaderDescription,
                Feedback = BuildCaptureFeedbackMessage(feedback, extraction.ErrorMessage, DataPurpose.Verification, hasFeatureSet: true)
            };
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
            var probeFeatureSet = DeserializeFeatureSet(probeData);
            var template = DeserializeTemplate(enrolledTemplate);

            var result = Verification.Verify(probeFeatureSet, template, threshold);

            return new FingerprintVerifyResult
            {
                IsMatch = result.Verified,
                Score = result.FARAchieved,
                Threshold = threshold
            };
        }

        private ReadersCollection GetReadersCollection()
        {
            return new ReadersCollection();
        }

        private CapturedSample CaptureSingleSample(int? timeoutMs)
        {
            var timeout = timeoutMs.GetValueOrDefault(_defaultCaptureTimeoutMs);
            using var capture = new Capture();
            var handler = new CaptureEventCollector();

            capture.EventHandler = handler;

            try
            {
                capture.StartCapture();

                if (!handler.Wait(timeout))
                {
                    throw new TimeoutException("Fingerprint capture timed out.");
                }
            }
            finally
            {
                try
                {
                    capture.StopCapture();
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }

            if (handler.Sample == null)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(handler.ErrorMessage)
                    ? "Fingerprint capture failed before a sample was received."
                    : handler.ErrorMessage);
            }

            var readerDescription = ResolveReaderDescription(handler.ReaderSerialNumber);

            return new CapturedSample
            {
                Sample = handler.Sample,
                ReaderDescription = readerDescription,
                SampleQualityFeedback = handler.SampleQualityFeedback
            };
        }

        private static FeatureExtractionResult ExtractFeatureSet(Sample sample, DataPurpose purpose)
        {
            var extractor = new FeatureExtraction();
            CaptureFeedback feedback = CaptureFeedback.None;
            FeatureSet? featureSet = new FeatureSet();
            string? errorMessage = null;

            try
            {
                extractor.CreateFeatureSet(sample, purpose, ref feedback, ref featureSet);
            }
            catch (Exception ex)
            {
                errorMessage = BuildDetailedExceptionMessage(ex);
            }

            if (feedback != CaptureFeedback.Good || !string.IsNullOrWhiteSpace(errorMessage))
            {
                featureSet = null;
            }

            return new FeatureExtractionResult
            {
                FeatureSet = featureSet,
                Feedback = feedback,
                ErrorMessage = errorMessage
            };
        }

        private static CaptureFeedback ResolveFeedback(FeatureExtractionResult extraction, CapturedSample capture)
        {
            return extraction.Feedback != CaptureFeedback.None
                ? extraction.Feedback
                : capture.SampleQualityFeedback;
        }

        private string ResolveReaderDescription(string readerSerialNumber)
        {
            try
            {
                var readers = GetReadersCollection();
                readers.Refresh();

                var matched = readers.Values
                    .OfType<ReaderDescription>()
                    .FirstOrDefault(r =>
                        string.Equals(r.SerialNumber, readerSerialNumber, StringComparison.OrdinalIgnoreCase));

                if (matched != null)
                {
                    return BuildReaderDescription(matched);
                }

                var first = readers.Values.OfType<ReaderDescription>().FirstOrDefault();
                if (first != null)
                {
                    return BuildReaderDescription(first);
                }
            }
            catch
            {
                // Reader description is best-effort only.
            }

            return string.IsNullOrWhiteSpace(readerSerialNumber)
                ? "DigitalPersona Reader"
                : $"DigitalPersona Reader ({readerSerialNumber})";
        }

        private static string BuildReaderDescription(ReaderDescription reader)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(reader.ProductName))
            {
                parts.Add(reader.ProductName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(reader.SerialNumber))
            {
                parts.Add(reader.SerialNumber.Trim());
            }

            return parts.Count == 0 ? "DigitalPersona Reader" : string.Join(" | ", parts);
        }

        private void PrepareRuntime()
        {
            foreach (var directory in GetNativeCandidateDirectories())
            {
                AddDirectoryToProcessPath(directory);
            }
        }

        private IEnumerable<string> GetNativeCandidateDirectories()
        {
            var directories = new Collection<string>();
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            AddCandidateDirectory(directories, _nativePath);
            AddCandidateDirectory(directories, Path.Combine(programFiles, "DigitalPersona", "Bin"));
            AddCandidateDirectory(directories, Path.Combine(programFiles, "DigitalPersona", "Bin", "COM-ActiveX"));
            AddCandidateDirectory(directories, Path.Combine(programFiles, "DigitalPersona", "One Touch SDK", ".NET", "Bin"));
            AddCandidateDirectory(directories, Path.Combine(programFilesX86, "DigitalPersona", "Bin"));
            AddCandidateDirectory(directories, Path.Combine(programFilesX86, "DigitalPersona", "Bin", "COM-ActiveX"));
            AddCandidateDirectory(directories, Path.Combine(programFilesX86, "DigitalPersona", "One Touch SDK", ".NET", "Bin"));

            return directories.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddCandidateDirectory(ICollection<string> directories, string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            directories.Add(directory);
        }

        private static void AddDirectoryToProcessPath(string directory)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var entries = currentPath
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (entries.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            Environment.SetEnvironmentVariable("PATH",
                string.IsNullOrWhiteSpace(currentPath)
                    ? directory
                    : directory + Path.PathSeparator + currentPath);
        }

        private static byte[] SerializeSample(Sample sample)
        {
            byte[]? output = null;
            sample.Serialize(ref output);
            return output ?? Array.Empty<byte>();
        }

        private static byte[] SerializeTemplate(Template template)
        {
            byte[]? output = null;
            template.Serialize(ref output);
            return output ?? Array.Empty<byte>();
        }

        private static byte[] SerializeFeatureSet(FeatureSet featureSet)
        {
            byte[]? output = null;
            featureSet.Serialize(ref output);
            return output ?? Array.Empty<byte>();
        }

        private static Sample DeserializeSample(byte[] payload)
        {
            var sample = new Sample();
            sample.DeSerialize(payload);
            return sample;
        }

        private static Template DeserializeTemplate(byte[] payload)
        {
            var template = new Template();
            template.DeSerialize(payload);
            return template;
        }

        private static FeatureSet DeserializeFeatureSet(byte[] payload)
        {
            var featureSet = new FeatureSet();
            featureSet.DeSerialize(payload);
            return featureSet;
        }

        private static int? ToQualityScore(CaptureFeedback feedback)
        {
            return feedback == CaptureFeedback.Good ? 100 : null;
        }

        private static string BuildCaptureFeedbackMessage(
            CaptureFeedback feedback,
            string? extractionError,
            DataPurpose purpose,
            bool hasFeatureSet)
        {
            var message = hasFeatureSet && feedback == CaptureFeedback.Good
                ? purpose == DataPurpose.Enrollment
                    ? "Fingerprint sample accepted."
                    : "Fingerprint sample is ready for verification."
                : feedback switch
                {
                    CaptureFeedback.TooLight => "Press the finger a bit more firmly and keep it flat on the scanner.",
                    CaptureFeedback.TooDark => "Reduce pressure slightly and keep the finger relaxed on the scanner.",
                    CaptureFeedback.TooNoisy => "Clean and dry both the finger and the scanner, then try again.",
                    CaptureFeedback.LowContrast => "Keep the finger flat and dry so the scanner can see the ridge pattern clearly.",
                    CaptureFeedback.NotEnoughFeatures => "More ridge detail is needed. Use the same finger, cover more of the glass, and hold still.",
                    CaptureFeedback.NoCentralRegion => "Center the core of the finger on the scanner and try again.",
                    CaptureFeedback.NoFinger => "No finger was detected. Place one finger flat on the scanner.",
                    CaptureFeedback.TooHigh => "Move the finger slightly lower on the scanner.",
                    CaptureFeedback.TooLow => "Move the finger slightly higher on the scanner.",
                    CaptureFeedback.TooLeft => "Move the finger slightly to the right.",
                    CaptureFeedback.TooRight => "Move the finger slightly to the left.",
                    CaptureFeedback.TooStrange => "Reposition the finger and try again with a normal flat placement.",
                    CaptureFeedback.TooFast => "Lift and place the finger more slowly.",
                    CaptureFeedback.TooSkewed => "Straighten the finger so it sits squarely on the scanner.",
                    CaptureFeedback.TooShort => "Keep the finger on the scanner a little longer before lifting it.",
                    CaptureFeedback.TooSlow => "Use one steady placement without sliding or lingering too long.",
                    CaptureFeedback.TooSmall => "Cover more of the scanner surface with the finger pad.",
                    _ => purpose == DataPurpose.Enrollment
                        ? "The scan was captured, but the SDK could not extract enough fingerprint detail. Reposition the same finger and try again."
                        : "The scan was captured, but the SDK could not extract enough fingerprint detail for verification. Reposition the same finger and try again."
                };

            return string.IsNullOrWhiteSpace(extractionError)
                ? message
                : $"{message} SDK detail: {extractionError}.";
        }

        private static string BuildDetailedExceptionMessage(Exception ex)
        {
            var parts = new List<string>();
            Exception? current = ex;

            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    parts.Add(current.Message.Trim());
                }

                current = current.InnerException;
            }

            return string.Join(" | ", parts.Distinct());
        }

        private sealed class CapturedSample
        {
            public Sample Sample { get; set; } = new();
            public string ReaderDescription { get; set; } = string.Empty;
            public CaptureFeedback SampleQualityFeedback { get; set; } = CaptureFeedback.None;
        }

        private sealed class FeatureExtractionResult
        {
            public FeatureSet? FeatureSet { get; set; }
            public CaptureFeedback Feedback { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private sealed class CaptureEventCollector : DPFP.Capture.EventHandler
        {
            private readonly AutoResetEvent _waitHandle = new(false);

            public Sample? Sample { get; private set; }
            public string ReaderSerialNumber { get; private set; } = string.Empty;
            public string? ErrorMessage { get; private set; }
            public CaptureFeedback SampleQualityFeedback { get; private set; } = CaptureFeedback.None;

            public bool Wait(int timeoutMs)
            {
                return _waitHandle.WaitOne(timeoutMs);
            }

            public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
            {
                this.Sample = Sample;
                this.ReaderSerialNumber = ReaderSerialNumber;
                _waitHandle.Set();
            }

            public void OnFingerGone(object Capture, string ReaderSerialNumber)
            {
            }

            public void OnFingerTouch(object Capture, string ReaderSerialNumber)
            {
            }

            public void OnReaderConnect(object Capture, string ReaderSerialNumber)
            {
                if (string.IsNullOrWhiteSpace(this.ReaderSerialNumber))
                {
                    this.ReaderSerialNumber = ReaderSerialNumber;
                }
            }

            public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
            {
                ErrorMessage = "Fingerprint reader disconnected during capture.";
                _waitHandle.Set();
            }

            public void OnSampleQuality(object Capture, string ReaderSerialNumber, CaptureFeedback CaptureFeedback)
            {
                SampleQualityFeedback = CaptureFeedback;

                if (string.IsNullOrWhiteSpace(this.ReaderSerialNumber))
                {
                    this.ReaderSerialNumber = ReaderSerialNumber;
                }
            }
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

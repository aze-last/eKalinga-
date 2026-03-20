using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    public class FingerprintService
    {
        private static readonly byte[] TemplateEntropy = Encoding.UTF8.GetBytes("ASMS-FP-V1");

        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly DigitalPersonaSdkService _sdkService;

        public FingerprintService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
            _sdkService = new DigitalPersonaSdkService();
        }

        public FingerprintTemplate EnrollOrUpdateTemplate(
            int actingUserId,
            int targetUserId,
            int fingerIndex,
            byte[] rawTemplateData,
            int? qualityScore = null,
            string templateFormat = "DPFP.Template")
        {
            EnsureAdminAccess(actingUserId);
            ValidateFingerIndex(fingerIndex);

            if (rawTemplateData == null || rawTemplateData.Length == 0)
            {
                throw new ArgumentException("Fingerprint template data is required.", nameof(rawTemplateData));
            }

            var targetUser = _context.Users.FirstOrDefault(u => u.Id == targetUserId && u.IsActive);
            if (targetUser == null)
            {
                throw new InvalidOperationException("Target user was not found or is inactive.");
            }

            var now = DateTime.Now;
            var protectedTemplate = ProtectTemplate(rawTemplateData);

            var existing = _context.FingerprintTemplates
                .FirstOrDefault(ft => ft.UserId == targetUserId && ft.FingerIndex == fingerIndex);

            if (existing == null)
            {
                existing = new FingerprintTemplate
                {
                    UserId = targetUserId,
                    FingerIndex = fingerIndex,
                    TemplateData = protectedTemplate,
                    TemplateFormat = string.IsNullOrWhiteSpace(templateFormat) ? "DPFP.Template" : templateFormat.Trim(),
                    QualityScore = qualityScore,
                    IsActive = true,
                    EnrolledAt = now,
                    EnrolledByUserId = actingUserId
                };

                _context.FingerprintTemplates.Add(existing);
            }
            else
            {
                existing.TemplateData = protectedTemplate;
                existing.TemplateFormat = string.IsNullOrWhiteSpace(templateFormat) ? existing.TemplateFormat : templateFormat.Trim();
                existing.QualityScore = qualityScore;
                existing.IsActive = true;
                existing.EnrolledAt = now;
                existing.EnrolledByUserId = actingUserId;
            }

            _context.SaveChanges();

            _auditService.LogActivity(
                actingUserId,
                "FingerprintEnrolled",
                "FingerprintTemplate",
                existing.Id,
                $"Admin enrolled fingerprint for user {targetUserId}, finger index {fingerIndex}, quality={qualityScore?.ToString() ?? "n/a"}.");

            return existing;
        }

        public void DeactivateTemplate(int actingUserId, int targetUserId, int fingerIndex)
        {
            EnsureAdminAccess(actingUserId);
            ValidateFingerIndex(fingerIndex);

            var template = _context.FingerprintTemplates
                .FirstOrDefault(ft => ft.UserId == targetUserId && ft.FingerIndex == fingerIndex && ft.IsActive);

            if (template == null)
            {
                return;
            }

            template.IsActive = false;
            _context.SaveChanges();

            _auditService.LogActivity(
                actingUserId,
                "FingerprintDeactivated",
                "FingerprintTemplate",
                template.Id,
                $"Admin deactivated fingerprint for user {targetUserId}, finger index {fingerIndex}.");
        }

        public void DeleteTemplateById(int actingUserId, int templateId)
        {
            EnsureAdminAccess(actingUserId);

            var template = _context.FingerprintTemplates
                .FirstOrDefault(ft => ft.Id == templateId && ft.IsActive);

            if (template == null)
            {
                return;
            }

            template.IsActive = false;
            _context.SaveChanges();

            _auditService.LogActivity(
                actingUserId,
                "FingerprintDeactivated",
                "FingerprintTemplate",
                template.Id,
                $"Admin deactivated fingerprint template id {template.Id} (user {template.UserId}, finger index {template.FingerIndex}).");
        }

        public List<FingerprintTemplate> GetTemplatesForUser(int actingUserId, int targetUserId)
        {
            EnsureAdminAccess(actingUserId);

            return _context.FingerprintTemplates
                .AsNoTracking()
                .Where(ft => ft.UserId == targetUserId)
                .OrderBy(ft => ft.FingerIndex)
                .ThenByDescending(ft => ft.EnrolledAt)
                .ToList();
        }

        public FingerprintEnrollmentSampleResult CaptureEnrollmentSampleFromDevice(int actingUserId, int? timeoutMs = null)
        {
            EnsureAdminAccess(actingUserId);
            return _sdkService.CaptureEnrollmentSample(timeoutMs);
        }

        public FingerprintTemplateBuildResult BuildEnrollmentTemplate(int actingUserId, IReadOnlyCollection<byte[]> samplePayloads)
        {
            EnsureAdminAccess(actingUserId);
            return _sdkService.BuildTemplate(samplePayloads);
        }

        public FingerprintDeviceStatus GetDeviceStatus(int actingUserId)
        {
            EnsureAdminAccess(actingUserId);
            return _sdkService.GetDeviceStatus();
        }

        public int GetEnrollmentRequiredSampleCount(int actingUserId)
        {
            EnsureAdminAccess(actingUserId);
            return _sdkService.GetEnrollmentRequiredSampleCount();
        }

        public FingerprintVerifyAgainstTemplateResult VerifyAgainstTemplate(
            int actingUserId,
            int templateId,
            int? timeoutMs = null,
            int? matchThreshold = null)
        {
            EnsureAdminAccess(actingUserId);

            var template = _context.FingerprintTemplates
                .FirstOrDefault(ft => ft.Id == templateId && ft.IsActive);

            if (template == null)
            {
                throw new InvalidOperationException("Template not found or inactive.");
            }

            var probe = _sdkService.CaptureVerificationProbe(timeoutMs);
            var enrolledBytes = UnprotectTemplate(template.TemplateData);
            var verify = _sdkService.VerifyProbeAgainstTemplate(probe.FeatureSetData, enrolledBytes, matchThreshold);

            if (verify.IsMatch)
            {
                template.LastVerifiedAt = DateTime.Now;
                _context.SaveChanges();
            }

            _auditService.LogActivity(
                actingUserId,
                "FingerprintVerifyAttempt",
                "FingerprintTemplate",
                templateId,
                $"Admin verification attempt for template {templateId}. Match={verify.IsMatch}, Score={verify.Score}, Threshold={verify.Threshold}.");

            return new FingerprintVerifyAgainstTemplateResult
            {
                IsMatch = verify.IsMatch,
                Score = verify.Score,
                Threshold = verify.Threshold,
                CapturedQualityScore = probe.QualityScore,
                ReaderDescription = probe.ReaderDescription
            };
        }

        public FingerprintIdentifyResult IdentifyUserFromCapture(int? timeoutMs = null, int? matchThreshold = null)
        {
            var probe = _sdkService.CaptureVerificationProbe(timeoutMs);
            var activeTemplates = GetActiveTemplatesForMatching();

            FingerprintTemplateMatchPayload? bestMatch = null;
            int? bestScore = null;
            int threshold = matchThreshold ?? 21474;

            foreach (var candidate in activeTemplates)
            {
                var verify = _sdkService.VerifyProbeAgainstTemplate(probe.FeatureSetData, candidate.TemplateData, threshold);
                if (!verify.IsMatch)
                {
                    continue;
                }

                if (!bestScore.HasValue || verify.Score < bestScore.Value)
                {
                    bestScore = verify.Score;
                    bestMatch = candidate;
                }
            }

            if (bestMatch != null)
            {
                MarkTemplateVerified(bestMatch.TemplateId);
            }

            return new FingerprintIdentifyResult
            {
                IsMatched = bestMatch != null,
                MatchedUserId = bestMatch?.UserId,
                MatchedTemplateId = bestMatch?.TemplateId,
                Score = bestScore,
                Threshold = threshold,
                CapturedQualityScore = probe.QualityScore,
                ReaderDescription = probe.ReaderDescription
            };
        }

        public List<FingerprintTemplateMatchPayload> GetActiveTemplatesForMatching()
        {
            return _context.FingerprintTemplates
                .AsNoTracking()
                .Where(ft => ft.IsActive)
                .Select(ft => new FingerprintTemplateMatchPayload
                {
                    TemplateId = ft.Id,
                    UserId = ft.UserId,
                    FingerIndex = ft.FingerIndex,
                    TemplateData = UnprotectTemplate(ft.TemplateData)
                })
                .ToList();
        }

        public void MarkTemplateVerified(int templateId)
        {
            var template = _context.FingerprintTemplates.FirstOrDefault(ft => ft.Id == templateId);
            if (template == null)
            {
                return;
            }

            template.LastVerifiedAt = DateTime.Now;
            _context.SaveChanges();
        }

        private void EnsureAdminAccess(int actingUserId)
        {
            var actingUser = _context.Users.FirstOrDefault(u => u.Id == actingUserId && u.IsActive);
            if (actingUser == null || actingUser.Role != UserRole.Admin)
            {
                throw new UnauthorizedAccessException("Only Admin can configure fingerprint templates.");
            }
        }

        private static void ValidateFingerIndex(int fingerIndex)
        {
            if (fingerIndex < 0 || fingerIndex > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(fingerIndex), "Finger index must be between 0 and 9.");
            }
        }

        private static byte[] ProtectTemplate(byte[] rawTemplateData)
        {
            return ProtectedData.Protect(rawTemplateData, TemplateEntropy, DataProtectionScope.CurrentUser);
        }

        private static byte[] UnprotectTemplate(byte[] protectedTemplateData)
        {
            return ProtectedData.Unprotect(protectedTemplateData, TemplateEntropy, DataProtectionScope.CurrentUser);
        }
    }

    public class FingerprintTemplateMatchPayload
    {
        public int TemplateId { get; set; }
        public int UserId { get; set; }
        public int FingerIndex { get; set; }
        public byte[] TemplateData { get; set; } = Array.Empty<byte>();
    }

    public class FingerprintVerifyAgainstTemplateResult
    {
        public bool IsMatch { get; set; }
        public int Score { get; set; }
        public int Threshold { get; set; }
        public int? CapturedQualityScore { get; set; }
        public string ReaderDescription { get; set; } = string.Empty;
    }

    public class FingerprintIdentifyResult
    {
        public bool IsMatched { get; set; }
        public int? MatchedUserId { get; set; }
        public int? MatchedTemplateId { get; set; }
        public int? Score { get; set; }
        public int Threshold { get; set; }
        public int? CapturedQualityScore { get; set; }
        public string ReaderDescription { get; set; } = string.Empty;
    }
}

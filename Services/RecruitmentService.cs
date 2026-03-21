using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class RecruitmentService
    {
        private readonly AppDbContext _context;

        public RecruitmentService(AppDbContext context)
        {
            _context = context;
        }

        public List<RecruitmentCandidate> GetCandidates()
        {
            return _context.RecruitmentCandidates
                .AsNoTracking()
                .OrderByDescending(c => c.AppliedAt)
                .ToList();
        }

        public RecruitmentCandidate AddCandidate(
            string fullName,
            string email,
            RecruitmentSource source,
            DateTime appliedAt,
            decimal? expectedSalary,
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new Exception("Candidate full name is required.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new Exception("Candidate email is required.");
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            bool duplicate = _context.RecruitmentCandidates.Any(c => c.Email.ToLower() == normalizedEmail);
            if (duplicate)
            {
                throw new Exception("A candidate with this email already exists.");
            }

            var candidate = new RecruitmentCandidate
            {
                FullName = fullName.Trim(),
                Email = normalizedEmail,
                Source = source,
                Stage = RecruitmentStage.Applied,
                AppliedAt = appliedAt,
                ExpectedSalary = expectedSalary,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.RecruitmentCandidates.Add(candidate);
            _context.SaveChanges();

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Recruitment,
                action: "created",
                entityId: candidate.Id);

            return candidate;
        }

        public void AdvanceStage(int candidateId)
        {
            var candidate = _context.RecruitmentCandidates.FirstOrDefault(c => c.Id == candidateId);
            if (candidate == null)
            {
                throw new Exception("Candidate not found.");
            }

            candidate.Stage = NextStage(candidate.Stage);
            ApplyStageTimestamps(candidate);
            candidate.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Recruitment,
                action: "advanced",
                entityId: candidate.Id);
        }

        public void MarkHired(int candidateId)
        {
            var candidate = _context.RecruitmentCandidates.FirstOrDefault(c => c.Id == candidateId);
            if (candidate == null)
            {
                throw new Exception("Candidate not found.");
            }

            candidate.Stage = RecruitmentStage.Hired;
            candidate.HiredAt ??= DateTime.Now;
            candidate.OfferedAt ??= DateTime.Now.AddDays(-1);
            candidate.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Recruitment,
                action: "hired",
                entityId: candidate.Id);
        }

        public void Reject(int candidateId)
        {
            var candidate = _context.RecruitmentCandidates.FirstOrDefault(c => c.Id == candidateId);
            if (candidate == null)
            {
                throw new Exception("Candidate not found.");
            }

            candidate.Stage = RecruitmentStage.Rejected;
            candidate.IsActive = false;
            candidate.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Recruitment,
                action: "rejected",
                entityId: candidate.Id);
        }

        private static RecruitmentStage NextStage(RecruitmentStage stage)
        {
            return stage switch
            {
                RecruitmentStage.Applied => RecruitmentStage.Screening,
                RecruitmentStage.Screening => RecruitmentStage.Interview,
                RecruitmentStage.Interview => RecruitmentStage.OfferExtended,
                RecruitmentStage.OfferExtended => RecruitmentStage.Hired,
                _ => stage
            };
        }

        private static void ApplyStageTimestamps(RecruitmentCandidate candidate)
        {
            if (candidate.Stage == RecruitmentStage.Interview && !candidate.InterviewedAt.HasValue)
            {
                candidate.InterviewedAt = DateTime.Now;
            }

            if (candidate.Stage == RecruitmentStage.OfferExtended && !candidate.OfferedAt.HasValue)
            {
                candidate.OfferedAt = DateTime.Now;
            }

            if (candidate.Stage == RecruitmentStage.Hired && !candidate.HiredAt.HasValue)
            {
                candidate.HiredAt = DateTime.Now;
            }
        }
    }
}

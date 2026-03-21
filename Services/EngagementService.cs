using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class EngagementService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public EngagementService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
        }

        public List<EngagementSurvey> GetSurveys()
        {
            return _context.EngagementSurveys
                .AsNoTracking()
                .Include(s => s.Employee)
                .ThenInclude(e => e.Position)
                .OrderByDescending(s => s.SurveyDate)
                .ToList();
        }

        public EngagementSurvey SubmitSurvey(
            int employeeId,
            DateTime surveyDate,
            int enpsScore,
            decimal engagementScore,
            decimal wellbeingScore,
            BurnoutRiskLevel burnoutRisk,
            string? comments,
            int actorUserId)
        {
            if (enpsScore < -100 || enpsScore > 100)
            {
                throw new Exception("eNPS score must be between -100 and 100.");
            }

            if (engagementScore < 0m || engagementScore > 100m)
            {
                throw new Exception("Engagement score must be between 0 and 100.");
            }

            if (wellbeingScore < 0m || wellbeingScore > 100m)
            {
                throw new Exception("Wellbeing score must be between 0 and 100.");
            }

            var employee = _context.Employees.FirstOrDefault(e => e.Id == employeeId);
            if (employee == null)
            {
                throw new Exception("Employee not found.");
            }

            var survey = new EngagementSurvey
            {
                EmployeeId = employeeId,
                SurveyDate = surveyDate.Date,
                EnpsScore = enpsScore,
                EngagementScore = engagementScore,
                WellbeingScore = wellbeingScore,
                BurnoutRisk = burnoutRisk,
                Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.EngagementSurveys.Add(survey);
            _context.SaveChanges();

            _auditService.LogActivity(
                actorUserId,
                "SurveySubmitted",
                "EngagementSurvey",
                survey.Id,
                $"Engagement survey submitted for employeeId={employeeId}.");

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Engagement,
                action: "survey_submitted",
                entityId: survey.Id,
                actorUserId: actorUserId);

            return survey;
        }
    }
}

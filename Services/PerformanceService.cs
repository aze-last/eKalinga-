using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class PerformanceService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public PerformanceService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
        }

        public List<PerformanceGoal> GetGoals()
        {
            return _context.PerformanceGoals
                .AsNoTracking()
                .Include(g => g.Employee)
                .ThenInclude(e => e.Position)
                .OrderBy(g => g.DueDate)
                .ToList();
        }

        public List<TrainingRecord> GetTrainingRecords()
        {
            return _context.TrainingRecords
                .AsNoTracking()
                .Include(t => t.Employee)
                .ThenInclude(e => e.Position)
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        public void AdvanceGoalProgress(int goalId, decimal incrementPercent, int actorUserId)
        {
            var goal = _context.PerformanceGoals
                .Include(g => g.Employee)
                .FirstOrDefault(g => g.Id == goalId);
            if (goal == null)
            {
                throw new Exception("Performance goal not found.");
            }

            goal.CompletionPercent = Math.Min(100m, Math.Max(0m, goal.CompletionPercent + incrementPercent));
            goal.Status = ResolveGoalStatus(goal.CompletionPercent, goal.DueDate);
            goal.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            _auditService.LogActivity(
                actorUserId,
                "GoalProgressUpdated",
                "PerformanceGoal",
                goal.Id,
                $"Goal '{goal.GoalTitle}' for {goal.Employee.FullName} updated to {goal.CompletionPercent:N0}%.");

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Performance,
                action: "goal_progress_updated",
                entityId: goal.Id,
                actorUserId: actorUserId);
        }

        public void MarkGoalCompleted(int goalId, int actorUserId)
        {
            var goal = _context.PerformanceGoals
                .Include(g => g.Employee)
                .FirstOrDefault(g => g.Id == goalId);
            if (goal == null)
            {
                throw new Exception("Performance goal not found.");
            }

            goal.CompletionPercent = 100m;
            goal.Status = PerformanceGoalStatus.Completed;
            goal.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            _auditService.LogActivity(
                actorUserId,
                "GoalCompleted",
                "PerformanceGoal",
                goal.Id,
                $"Goal '{goal.GoalTitle}' marked completed for {goal.Employee.FullName}.");

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Performance,
                action: "goal_completed",
                entityId: goal.Id,
                actorUserId: actorUserId);
        }

        public void MarkTrainingCompleted(int trainingId, decimal effectivenessScore, int actorUserId)
        {
            var training = _context.TrainingRecords
                .Include(t => t.Employee)
                .FirstOrDefault(t => t.Id == trainingId);
            if (training == null)
            {
                throw new Exception("Training record not found.");
            }

            if (effectivenessScore < 1m || effectivenessScore > 5m)
            {
                throw new Exception("Effectiveness score must be between 1.0 and 5.0.");
            }

            training.Status = TrainingStatus.Completed;
            training.CompletedAt ??= DateTime.Now;
            training.EffectivenessScore = effectivenessScore;
            _context.SaveChanges();

            _auditService.LogActivity(
                actorUserId,
                "TrainingCompleted",
                "TrainingRecord",
                training.Id,
                $"Training '{training.TrainingName}' completed by {training.Employee.FullName}.");

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Performance,
                action: "training_completed",
                entityId: training.Id,
                actorUserId: actorUserId);
        }

        private static PerformanceGoalStatus ResolveGoalStatus(decimal completionPercent, DateTime dueDate)
        {
            if (completionPercent >= 100m)
            {
                return PerformanceGoalStatus.Completed;
            }

            if (dueDate.Date < DateTime.Today)
            {
                return PerformanceGoalStatus.Overdue;
            }

            return completionPercent <= 0m
                ? PerformanceGoalStatus.NotStarted
                : PerformanceGoalStatus.InProgress;
        }
    }
}

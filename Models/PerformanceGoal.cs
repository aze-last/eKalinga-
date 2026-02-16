using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("performance_goals")]
    public class PerformanceGoal
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("goal_title")]
        [Required]
        [MaxLength(200)]
        public string GoalTitle { get; set; } = string.Empty;

        [Column("completion_percent")]
        public decimal CompletionPercent { get; set; }

        [Column("review_score")]
        public decimal? ReviewScore { get; set; }

        [Column("manager_feedback_score")]
        public decimal? ManagerFeedbackScore { get; set; }

        [Column("due_date")]
        public DateTime DueDate { get; set; }

        [Column("status")]
        public PerformanceGoalStatus Status { get; set; } = PerformanceGoalStatus.NotStarted;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }

    public enum PerformanceGoalStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Overdue
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("engagement_surveys")]
    public class EngagementSurvey
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("survey_date")]
        public DateTime SurveyDate { get; set; } = DateTime.Today;

        [Column("enps_score")]
        public int EnpsScore { get; set; }

        [Column("engagement_score")]
        public decimal EngagementScore { get; set; }

        [Column("wellbeing_score")]
        public decimal WellbeingScore { get; set; }

        [Column("burnout_risk")]
        public BurnoutRiskLevel BurnoutRisk { get; set; } = BurnoutRiskLevel.Low;

        [Column("comments")]
        [MaxLength(500)]
        public string? Comments { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }

    public enum BurnoutRiskLevel
    {
        Low,
        Medium,
        High
    }
}

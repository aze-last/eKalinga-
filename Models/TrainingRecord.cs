using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("training_records")]
    public class TrainingRecord
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("training_name")]
        [Required]
        [MaxLength(200)]
        public string TrainingName { get; set; } = string.Empty;

        [Column("is_mandatory")]
        public bool IsMandatory { get; set; } = true;

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("effectiveness_score")]
        public decimal? EffectivenessScore { get; set; }

        [Column("status")]
        public TrainingStatus Status { get; set; } = TrainingStatus.Pending;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }

    public enum TrainingStatus
    {
        Pending,
        InProgress,
        Completed,
        Overdue
    }
}

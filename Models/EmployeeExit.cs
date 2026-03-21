using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("employee_exits")]
    public class EmployeeExit
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("exit_type")]
        [Required]
        public EmployeeExitType ExitType { get; set; } = EmployeeExitType.Resignation;

        [Column("is_voluntary")]
        public bool IsVoluntary { get; set; } = true;

        [Column("reason")]
        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Column("last_working_date")]
        [Required]
        public DateTime LastWorkingDate { get; set; } = DateTime.Today;

        [Column("recorded_by")]
        [Required]
        public int RecordedBy { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("recorded_at")]
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;

        [ForeignKey("RecordedBy")]
        public User RecordedByUser { get; set; } = null!;
    }

    public enum EmployeeExitType
    {
        Resignation,
        Termination,
        EndOfContract,
        AWOL,
        Retirement
    }
}

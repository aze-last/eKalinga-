using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("employees")]
    public class Employee
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("full_name")]
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Column("position_id")]
        [Required]
        public int PositionId { get; set; }

        [Column("hourly_rate")]
        [Required]
        public decimal HourlyRate { get; set; }

        [Column("date_hired")]
        [Required]
        public DateTime DateHired { get; set; }

        [Column("status")]
        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        // Navigation
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [ForeignKey("PositionId")]
        public Position Position { get; set; } = null!;

        public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
    }

    public enum EmployeeStatus
    {
        Active,
        Inactive
    }
}
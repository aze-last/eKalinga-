using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("attendance")]
    public class Attendance
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("shift_id")]
        [Required]
        public int ShiftId { get; set; }

        [Column("time_in")]
        public DateTime? TimeIn { get; set; }

        [Column("time_out")]
        public DateTime? TimeOut { get; set; }

        [Column("total_hours")]
        public decimal TotalHours { get; set; }

        [Column("overtime_hours")]
        public decimal OvertimeHours { get; set; }

        [Column("status")]
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Open;

        // Navigation
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;

        [ForeignKey("ShiftId")]
        public Shift Shift { get; set; } = null!;
    }

    public enum AttendanceStatus
    {
        Open,
        Closed
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("leave_requests")]
    public class LeaveRequest
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("leave_type")]
        [Required]
        public LeaveType Type { get; set; }

        [Column("start_date")]
        [Required]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        [Required]
        public DateTime EndDate { get; set; }

        [Column("reason")]
        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Column("status")]
        [Required]
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("rejection_reason")]
        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;

        [ForeignKey("ApprovedBy")]
        public User? ApprovedByUser { get; set; }

        // Calculated property
        [NotMapped]
        public int TotalDays => (EndDate.Date - StartDate.Date).Days + 1;
    }

    public enum LeaveType
    {
        Vacation,
        Sick,
        Emergency,
        Personal
    }

    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }
}

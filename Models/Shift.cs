using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("shifts")]
    public class Shift
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("shift_date")]
        [Required]
        public DateTime ShiftDate { get; set; }

        [Column("start_time")]
        [Required]
        public TimeSpan StartTime { get; set; }

        [Column("end_time")]
        [Required]
        public TimeSpan EndTime { get; set; }

        [Column("position_id")]
        [Required]
        public int PositionId { get; set; }

        [Column("created_by")]
        [Required]
        public int CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("PositionId")]
        public Position Position { get; set; } = null!;

        [ForeignKey("CreatedBy")]
        public User CreatedByUser { get; set; } = null!;

        public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
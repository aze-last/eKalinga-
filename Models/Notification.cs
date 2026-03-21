using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("type")]
        [Required]
        public NotificationType Type { get; set; }

        [Column("title")]
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Column("message")]
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("action_url")]
        [MaxLength(500)]
        public string? ActionUrl { get; set; }

        // Navigation
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    public enum NotificationType
    {
        ShiftAssigned,
        ShiftChanged,
        ShiftReminder,
        LeaveApproved,
        LeaveRejected,
        SchedulePublished,
        General
    }
}

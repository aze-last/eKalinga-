using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("user_preferences")]
    public class UserPreference
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("preferred_shift_block")]
        [MaxLength(20)]
        public string PreferredShiftBlock { get; set; } = "Any";

        [Column("preferred_days_off")]
        [MaxLength(60)]
        public string PreferredDaysOff { get; set; } = string.Empty;

        [Column("preferred_positions")]
        [MaxLength(255)]
        public string PreferredPositions { get; set; } = string.Empty;

        [Column("notification_types")]
        [MaxLength(80)]
        public string NotificationTypes { get; set; } = "Leave,Shift,Announcement";

        [Column("notification_channels")]
        [MaxLength(40)]
        public string NotificationChannels { get; set; } = "InApp";

        [Column("default_view")]
        [MaxLength(40)]
        public string DefaultView { get; set; } = "Dashboard";

        [Column("report_format")]
        [MaxLength(20)]
        public string ReportFormat { get; set; } = "CSV";

        [Column("approval_signature")]
        [MaxLength(120)]
        public string ApprovalSignature { get; set; } = string.Empty;

        [Column("auto_notify_on_approval")]
        public bool AutoNotifyOnApproval { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

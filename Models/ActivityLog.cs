using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("activity_logs")]
    public class ActivityLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("action")]
        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [Column("entity")]
        [Required]
        [MaxLength(100)]
        public string Entity { get; set; } = string.Empty;

        [Column("entity_id")]
        public int? EntityId { get; set; }

        [Column("details")]
        [MaxLength(1000)]
        public string Details { get; set; } = string.Empty;

        [Column("ip_address")]
        [MaxLength(50)]
        public string IpAddress { get; set; } = string.Empty;

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("scanner_sessions")]
    public class ScannerSession
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("mode")]
        public ScannerSessionMode Mode { get; set; } = ScannerSessionMode.Lookup;

        [Column("session_token")]
        [Required]
        [MaxLength(80)]
        public string SessionToken { get; set; } = string.Empty;

        [Column("pin_hash")]
        [Required]
        [MaxLength(128)]
        public string PinHash { get; set; } = string.Empty;

        [Column("cash_for_work_event_id")]
        public int? CashForWorkEventId { get; set; }

        [Column("ayuda_program_id")]
        public int? AyudaProgramId { get; set; }

        [Column("created_by_user_id")]
        [Required]
        public int CreatedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddMinutes(15);

        [Column("last_accessed_at")]
        public DateTime? LastAccessedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    public enum ScannerSessionMode
    {
        Lookup,
        Attendance,
        Distribution
    }
}

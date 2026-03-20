using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("cash_for_work_attendance")]
    public class CashForWorkAttendance
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("participant_id")]
        [Required]
        public int ParticipantId { get; set; }

        [Column("attendance_date")]
        [Required]
        public DateTime AttendanceDate { get; set; }

        [Column("status")]
        public CashForWorkAttendanceStatus Status { get; set; } = CashForWorkAttendanceStatus.Present;

        [Column("source")]
        public AttendanceCaptureSource Source { get; set; } = AttendanceCaptureSource.Manual;

        [Column("ocr_extracted_name")]
        [MaxLength(150)]
        public string? OcrExtractedName { get; set; }

        [Column("recorded_by_user_id")]
        [Required]
        public int RecordedByUserId { get; set; }

        [Column("recorded_at")]
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(ParticipantId))]
        public CashForWorkParticipant Participant { get; set; } = null!;

        [ForeignKey(nameof(RecordedByUserId))]
        public User RecordedByUser { get; set; } = null!;
    }

    public enum CashForWorkAttendanceStatus
    {
        Present,
        Absent
    }

    public enum AttendanceCaptureSource
    {
        Manual,
        OcrUpload
    }
}

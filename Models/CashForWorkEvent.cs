using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("cash_for_work_events")]
    public class CashForWorkEvent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Column("location")]
        [Required]
        [MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        [Column("event_date")]
        [Required]
        public DateTime EventDate { get; set; }

        [Column("start_time")]
        [Required]
        public TimeSpan StartTime { get; set; }

        [Column("end_time")]
        [Required]
        public TimeSpan EndTime { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("created_by_user_id")]
        [Required]
        public int CreatedByUserId { get; set; }

        [Column("status")]
        public CashForWorkEventStatus Status { get; set; } = CashForWorkEventStatus.Draft;

        [Column("ayuda_program_id")]
        public int? AyudaProgramId { get; set; }

        [Column("budget_ledger_entry_id")]
        public int? BudgetLedgerEntryId { get; set; }

        [Column("release_amount", TypeName = "decimal(18,2)")]
        public decimal? ReleaseAmount { get; set; }

        [Column("released_at")]
        public DateTime? ReleasedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(CreatedByUserId))]
        public User CreatedByUser { get; set; } = null!;

        [ForeignKey(nameof(AyudaProgramId))]
        public AyudaProgram? AyudaProgram { get; set; }

        [ForeignKey(nameof(BudgetLedgerEntryId))]
        public BudgetLedgerEntry? BudgetLedgerEntry { get; set; }

        public ICollection<CashForWorkParticipant> Participants { get; set; } = new List<CashForWorkParticipant>();
    }

    public enum CashForWorkEventStatus
    {
        Draft,
        Open,
        Completed,
        Cancelled
    }
}

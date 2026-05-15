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

        [Column("finish_date")]
        public DateTime? FinishDate { get; set; }

        [Column("benefit_type")]
        public CashForWorkBenefitType BenefitType { get; set; } = CashForWorkBenefitType.None;

        [Column("benefit_description")]
        [MaxLength(250)]
        public string? BenefitDescription { get; set; }

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

        [Column("event_kind")]
        public CashForWorkEventKind EventKind { get; set; } = CashForWorkEventKind.CashForWork;

        [Column("ayuda_program_id")]
        public int? AyudaProgramId { get; set; }

        [Column("cash_for_work_budget_id")]
        public int? CashForWorkBudgetId { get; set; }

        [Column("budget_ledger_entry_id")]
        public int? BudgetLedgerEntryId { get; set; }

        [Column("unit_amount", TypeName = "decimal(18,2)")]
        public decimal UnitAmount { get; set; }

        [Column("release_amount", TypeName = "decimal(18,2)")]
        public decimal? ReleaseAmount { get; set; }

        [Column("released_at")]
        public DateTime? ReleasedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [ForeignKey(nameof(CreatedByUserId))]
        public User CreatedByUser { get; set; } = null!;

        [ForeignKey(nameof(AyudaProgramId))]
        public AyudaProgram? AyudaProgram { get; set; }

        [ForeignKey(nameof(CashForWorkBudgetId))]
        public CashForWorkBudget? CashForWorkBudget { get; set; }

        [ForeignKey(nameof(BudgetLedgerEntryId))]
        public BudgetLedgerEntry? BudgetLedgerEntry { get; set; }

        public ICollection<CashForWorkParticipant> Participants { get; set; } = new List<CashForWorkParticipant>();

        [NotMapped]
        public string WorkspaceLabel =>
            $"{(EventKind == CashForWorkEventKind.Seminar ? "Seminar" : "Cash-for-Work")} | {Title} | {EventDate:MMM dd, yyyy}";

        [NotMapped]
        public string WorkspaceAnnouncementLabel =>
            $"{Title} - {Location} - {EventDate:MMM dd, yyyy}";
    }

    public enum CashForWorkEventKind
    {
        CashForWork,
        Seminar
    }

    public enum CashForWorkBenefitType
    {
        None,
        Cash,
        Goods
    }

    public enum CashForWorkEventStatus
    {
        Draft,
        Open,
        Completed,
        Cancelled
    }
}

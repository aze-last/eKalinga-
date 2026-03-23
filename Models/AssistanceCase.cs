using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("assistance_cases")]
    public class AssistanceCase
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("case_number")]
        [Required]
        [MaxLength(40)]
        public string CaseNumber { get; set; } = string.Empty;

        [Column("household_id")]
        [Required]
        public int HouseholdId { get; set; }

        [Column("household_member_id")]
        public int? HouseholdMemberId { get; set; }

        [Column("assistance_type")]
        [Required]
        [MaxLength(120)]
        public string AssistanceType { get; set; } = string.Empty;

        [Column("priority")]
        public AssistanceCasePriority Priority { get; set; } = AssistanceCasePriority.Medium;

        [Column("status")]
        public AssistanceCaseStatus Status { get; set; } = AssistanceCaseStatus.Pending;

        [Column("requested_amount", TypeName = "decimal(18,2)")]
        public decimal? RequestedAmount { get; set; }

        [Column("approved_amount", TypeName = "decimal(18,2)")]
        public decimal? ApprovedAmount { get; set; }

        [Column("requested_on")]
        public DateTime RequestedOn { get; set; } = DateTime.Today;

        [Column("scheduled_release_date")]
        public DateTime? ScheduledReleaseDate { get; set; }

        [Column("summary")]
        [MaxLength(250)]
        public string? Summary { get; set; }

        [Column("notes")]
        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Column("resolution_notes")]
        [MaxLength(1000)]
        public string? ResolutionNotes { get; set; }

        [Column("created_by_user_id")]
        [Required]
        public int CreatedByUserId { get; set; }

        [Column("reviewed_by_user_id")]
        public int? ReviewedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(HouseholdId))]
        public Household Household { get; set; } = null!;

        [ForeignKey(nameof(HouseholdMemberId))]
        public HouseholdMember? HouseholdMember { get; set; }
    }

    public enum AssistanceCaseStatus
    {
        Pending,
        UnderReview,
        Approved,
        Released,
        Closed,
        Rejected,
        Cancelled
    }

    public enum AssistanceCasePriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("recruitment_candidates")]
    public class RecruitmentCandidate
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("full_name")]
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Column("source")]
        [Required]
        public RecruitmentSource Source { get; set; } = RecruitmentSource.JobBoard;

        [Column("stage")]
        [Required]
        public RecruitmentStage Stage { get; set; } = RecruitmentStage.Applied;

        [Column("applied_at")]
        [Required]
        public DateTime AppliedAt { get; set; } = DateTime.Now;

        [Column("interviewed_at")]
        public DateTime? InterviewedAt { get; set; }

        [Column("offered_at")]
        public DateTime? OfferedAt { get; set; }

        [Column("hired_at")]
        public DateTime? HiredAt { get; set; }

        [Column("expected_salary")]
        public decimal? ExpectedSalary { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public enum RecruitmentSource
    {
        JobBoard,
        Referral,
        WalkIn,
        Campus,
        SocialMedia,
        Agency,
        Other
    }

    public enum RecruitmentStage
    {
        Applied,
        Screening,
        Interview,
        OfferExtended,
        Hired,
        Rejected,
        Withdrawn
    }
}

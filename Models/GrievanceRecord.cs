using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("grievance_records")]
    public class GrievanceRecord
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grievance_number")]
        [Required]
        [MaxLength(40)]
        public string GrievanceNumber { get; set; } = string.Empty;

        [Column("civil_registry_id")]
        [MaxLength(120)]
        public string? CivilRegistryId { get; set; }

        [Column("beneficiary_id")]
        [MaxLength(120)]
        public string? BeneficiaryId { get; set; }

        [Column("staging_id")]
        public int? StagingId { get; set; }

        [Column("assistance_case_id")]
        public int? AssistanceCaseId { get; set; }

        [Column("cash_for_work_event_id")]
        public int? CashForWorkEventId { get; set; }

        [Column("type")]
        public GrievanceType Type { get; set; } = GrievanceType.Duplicate;

        [Column("status")]
        public GrievanceStatus Status { get; set; } = GrievanceStatus.Open;

        [Column("title")]
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Column("filed_by_user_id")]
        public int FiledByUserId { get; set; }

        [Column("assigned_to_user_id")]
        public int? AssignedToUserId { get; set; }

        [Column("resolution_remarks")]
        [MaxLength(1000)]
        public string? ResolutionRemarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }
    }

    public enum GrievanceType
    {
        Duplicate,
        WrongIdentity,
        MissingBeneficiary,
        WrongRelease
    }

    public enum GrievanceStatus
    {
        Open,
        UnderReview,
        Resolved,
        Rejected
    }
}

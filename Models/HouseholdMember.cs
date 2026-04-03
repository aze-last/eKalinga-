using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("household_members")]
    public class HouseholdMember
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("household_id")]
        [Required]
        public int HouseholdId { get; set; }

        [Column("full_name")]
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Column("relationship_to_head")]
        [MaxLength(100)]
        public string RelationshipToHead { get; set; } = string.Empty;

        [Column("occupation")]
        [MaxLength(100)]
        public string Occupation { get; set; } = string.Empty;

        [Column("is_cash_for_work_eligible")]
        public bool IsCashForWorkEligible { get; set; } = true;

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(HouseholdId))]
        public Household Household { get; set; } = null!;

        public ICollection<CashForWorkParticipant> CashForWorkParticipants { get; set; } = new List<CashForWorkParticipant>();
    }
}

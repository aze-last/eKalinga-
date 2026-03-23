using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("beneficiary_assistance_ledger")]
    public class BeneficiaryAssistanceLedgerEntry
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("civil_registry_id")]
        [MaxLength(120)]
        public string? CivilRegistryId { get; set; }

        [Column("beneficiary_id")]
        [MaxLength(120)]
        public string? BeneficiaryId { get; set; }

        [Column("source_module")]
        public BeneficiaryAssistanceSourceModule SourceModule { get; set; } = BeneficiaryAssistanceSourceModule.ManualHistory;

        [Column("source_record_id")]
        [MaxLength(80)]
        public string? SourceRecordId { get; set; }

        [Column("release_date")]
        public DateTime ReleaseDate { get; set; } = DateTime.Today;

        [Column("amount", TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column("remarks")]
        [Required]
        [MaxLength(1000)]
        public string Remarks { get; set; } = string.Empty;

        [Column("recorded_by_user_id")]
        public int RecordedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum BeneficiaryAssistanceSourceModule
    {
        ManualHistory,
        AssistanceCase,
        CashForWork,
        Grievance
    }
}

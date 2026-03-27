using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("ayuda_programs")]
    public class AyudaProgram
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("program_code")]
        [Required]
        [MaxLength(40)]
        public string ProgramCode { get; set; } = string.Empty;

        [Column("program_name")]
        [Required]
        [MaxLength(150)]
        public string ProgramName { get; set; } = string.Empty;

        [Column("program_type")]
        public AyudaProgramType ProgramType { get; set; } = AyudaProgramType.GeneralPurpose;

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("assistance_type")]
        [MaxLength(120)]
        public string? AssistanceType { get; set; }

        [Column("unit_amount", TypeName = "decimal(18,2)")]
        public decimal? UnitAmount { get; set; }

        [Column("item_description")]
        [MaxLength(250)]
        public string? ItemDescription { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("budget_cap", TypeName = "decimal(18,2)")]
        public decimal? BudgetCap { get; set; }

        [Column("distribution_status")]
        public AyudaProgramDistributionStatus DistributionStatus { get; set; } = AyudaProgramDistributionStatus.Draft;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_by_user_id")]
        public int CreatedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    [Table("ayuda_project_beneficiaries")]
    public class AyudaProjectBeneficiary
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ayuda_program_id")]
        public int AyudaProgramId { get; set; }

        [Column("beneficiary_staging_id")]
        public int BeneficiaryStagingId { get; set; }

        [Column("household_id")]
        public int? HouseholdId { get; set; }

        [Column("household_member_id")]
        public int? HouseholdMemberId { get; set; }

        [Column("beneficiary_id")]
        [MaxLength(120)]
        public string? BeneficiaryId { get; set; }

        [Column("civil_registry_id")]
        [MaxLength(120)]
        public string? CivilRegistryId { get; set; }

        [Column("full_name")]
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Column("added_by_user_id")]
        public int AddedByUserId { get; set; }

        [Column("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(AyudaProgramId))]
        public AyudaProgram? AyudaProgram { get; set; }
    }

    [Table("ayuda_project_claims")]
    public class AyudaProjectClaim
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ayuda_program_id")]
        public int AyudaProgramId { get; set; }

        [Column("beneficiary_staging_id")]
        public int BeneficiaryStagingId { get; set; }

        [Column("project_beneficiary_id")]
        public int? ProjectBeneficiaryId { get; set; }

        [Column("household_id")]
        public int? HouseholdId { get; set; }

        [Column("household_member_id")]
        public int? HouseholdMemberId { get; set; }

        [Column("beneficiary_id")]
        [MaxLength(120)]
        public string? BeneficiaryId { get; set; }

        [Column("civil_registry_id")]
        [MaxLength(120)]
        public string? CivilRegistryId { get; set; }

        [Column("full_name")]
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Column("assistance_type_snapshot")]
        [MaxLength(120)]
        public string? AssistanceTypeSnapshot { get; set; }

        [Column("item_description_snapshot")]
        [MaxLength(250)]
        public string? ItemDescriptionSnapshot { get; set; }

        [Column("unit_amount_snapshot", TypeName = "decimal(18,2)")]
        public decimal? UnitAmountSnapshot { get; set; }

        [Column("qr_payload")]
        [MaxLength(200)]
        public string? QrPayload { get; set; }

        [Column("remarks")]
        [MaxLength(1000)]
        public string? Remarks { get; set; }

        [Column("claimed_by_user_id")]
        public int ClaimedByUserId { get; set; }

        [Column("claimed_at")]
        public DateTime ClaimedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(AyudaProgramId))]
        public AyudaProgram? AyudaProgram { get; set; }
    }

    [Table("government_budget_snapshots")]
    public class GovernmentBudgetSnapshot
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("office_code")]
        [Required]
        [MaxLength(40)]
        public string OfficeCode { get; set; } = string.Empty;

        [Column("office_name")]
        [Required]
        [MaxLength(120)]
        public string OfficeName { get; set; } = string.Empty;

        [Column("yearly_budget_id")]
        public int YearlyBudgetId { get; set; }

        [Column("allocated_amount", TypeName = "decimal(18,2)")]
        public decimal AllocatedAmount { get; set; }

        [Column("spent_amount", TypeName = "decimal(18,2)")]
        public decimal SpentAmount { get; set; }

        [Column("source_row_id")]
        [MaxLength(80)]
        public string? SourceRowId { get; set; }

        [Column("sync_status")]
        public GovernmentBudgetSyncStatus SyncStatus { get; set; } = GovernmentBudgetSyncStatus.Synced;

        [Column("synced_at")]
        public DateTime SyncedAt { get; set; } = DateTime.Now;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    [Table("private_donations")]
    public class PrivateDonation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("donor_type")]
        public PrivateDonationDonorType DonorType { get; set; } = PrivateDonationDonorType.Person;

        [Column("donor_name")]
        [Required]
        [MaxLength(180)]
        public string DonorName { get; set; } = string.Empty;

        [Column("amount", TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column("date_received")]
        public DateTime DateReceived { get; set; } = DateTime.Today;

        [Column("reference_number")]
        [MaxLength(80)]
        public string? ReferenceNumber { get; set; }

        [Column("remarks")]
        [MaxLength(1000)]
        public string? Remarks { get; set; }

        [Column("proof_type")]
        public DonationProofType ProofType { get; set; } = DonationProofType.Cash;

        [Column("proof_reference_number")]
        [MaxLength(80)]
        public string? ProofReferenceNumber { get; set; }

        [Column("proof_file_path")]
        [MaxLength(255)]
        public string? ProofFilePath { get; set; }

        [Column("received_by_user_id")]
        public int ReceivedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    [Table("budget_ledger_entries")]
    public class BudgetLedgerEntry
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("entry_type")]
        public BudgetLedgerEntryType EntryType { get; set; } = BudgetLedgerEntryType.Release;

        [Column("feature_source")]
        public BudgetLedgerFeatureSource FeatureSource { get; set; } = BudgetLedgerFeatureSource.BudgetModule;

        [Column("source_record_id")]
        [MaxLength(80)]
        public string SourceRecordId { get; set; } = string.Empty;

        [Column("program_id")]
        public int? ProgramId { get; set; }

        [Column("recipient_count")]
        public int RecipientCount { get; set; }

        [Column("total_amount", TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column("government_portion", TypeName = "decimal(18,2)")]
        public decimal GovernmentPortion { get; set; }

        [Column("private_portion", TypeName = "decimal(18,2)")]
        public decimal PrivatePortion { get; set; }

        [Column("entry_date")]
        public DateTime EntryDate { get; set; } = DateTime.Today;

        [Column("remarks")]
        [MaxLength(1000)]
        public string? Remarks { get; set; }

        [Column("release_kind", TypeName = "varchar(32)")]
        public AssistanceReleaseKind? ReleaseKind { get; set; }

        [Column("recorded_by_user_id")]
        public int RecordedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum AyudaProgramType
    {
        AssistanceCase,
        CashForWork,
        GeneralPurpose
    }

    public enum AyudaProgramDistributionStatus
    {
        Draft,
        Open,
        Closed
    }

    public enum GovernmentBudgetSyncStatus
    {
        Synced,
        MissingConfiguration,
        Failed
    }

    public enum PrivateDonationDonorType
    {
        Person,
        Organization
    }

    public enum DonationProofType
    {
        Cash,
        Check,
        Voucher,
        BankTransfer,
        Other
    }

    public enum BudgetLedgerEntryType
    {
        Donation,
        Release
    }

    public enum BudgetLedgerFeatureSource
    {
        BudgetModule,
        AssistanceCase,
        CashForWork,
        ProjectDistribution
    }
}

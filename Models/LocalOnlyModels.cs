using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    /// <summary>
    /// Local-only cache of the GGMS officeallocations row for the Ayuda office.
    /// Refreshed when online. Never synced to Hostinger.
    /// </summary>
    [Table("ggms_allocation_cache")]
    public class GgmsAllocationCache
    {
        [Key]
        public int GgmsAllocationCacheId { get; set; }

        [MaxLength(40)]
        public string OfficeCode { get; set; } = string.Empty;

        [MaxLength(120)]
        public string OfficeName { get; set; } = string.Empty;

        public int YearlyBudgetId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AllocatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SpentAmount { get; set; }

        [MaxLength(80)]
        public string? SourceRowId { get; set; }

        public DateTime CachedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only cache of GGMS consolidated transaction rows.
    /// Refreshed when online. Never synced to Hostinger.
    /// </summary>
    [Table("ggms_transaction_cache")]
    public class GgmsTransactionCache
    {
        [Key]
        public int GgmsTransactionCacheId { get; set; }

        [MaxLength(80)]
        public string? TransactionId { get; set; }

        [MaxLength(40)]
        public string? OfficeCode { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime? TransactionDate { get; set; }

        [MaxLength(80)]
        public string? ReferenceNumber { get; set; }

        public DateTime CachedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only mirror of GGMS project_details rows for the Ayuda office.
    /// GGMS admins split the office allocation into per-project sub-budgets here;
    /// refreshed by the Sync GGMS action (read-only from GGMS). Never synced to Hostinger.
    /// Rows are never deleted — projects missing from GGMS are soft-archived via Status.
    /// </summary>
    [Table("ggms_project_cache")]
    public class GgmsProjectCache
    {
        [Key]
        public int GgmsProjectCacheId { get; set; }

        /// <summary>GGMS project_details.project_details_id (e.g. OPP-2026-0006). Unique upsert key.</summary>
        [MaxLength(45)]
        public string ProjectDetailsId { get; set; } = string.Empty;

        public int YearlyBudgetId { get; set; }

        [MaxLength(45)]
        public string OfficeCode { get; set; } = string.Empty;

        [MaxLength(120)]
        public string ProjectName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? SystemName { get; set; }

        /// <summary>Sub-allocation carved out of the office-level GGMS budget for this project.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalBudget { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "active";

        [MaxLength(10)]
        public string? VoucherCode { get; set; }

        public DateTime? SourceCreatedAt { get; set; }

        public DateTime? SourceUpdatedAt { get; set; }

        public DateTime CachedAt { get; set; } = DateTime.Now;

        /// <summary>True once a local AyudaProgram has been created from this GGMS project.</summary>
        public bool IsLinked { get; set; }
    }

    /// <summary>
    /// Tracks the last sync timestamp per table. Local-only.
    /// </summary>
    [Table("sync_metadata")]
    public class SyncMetadata
    {
        [Key]
        [MaxLength(80)]
        public string TableName { get; set; } = string.Empty;

        public DateTime LastSyncAt { get; set; } = DateTime.MinValue;
    }

    /// <summary>
    /// Local queue for GGMS transactions that failed to sync because GGMS was offline.
    /// </summary>
    [Table("ggms_pending_transaction_cache")]
    public class GgmsPendingTransactionCache
    {
        [Key]
        public int Id { get; set; }

        public string PayloadJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only cache of digital ID photos, encrypted via DPAPI.
    /// </summary>
    [Table("digital_id_photo_cache")]
    public class DigitalIdPhotoCache
    {
        [Key]
        [MaxLength(100)]
        public string BeneficiaryIdHash { get; set; } = string.Empty;

        public string EncryptedBeneficiaryId { get; set; } = string.Empty;

        public string EncryptedPhotoBytes { get; set; } = string.Empty;

        [MaxLength(64)]
        public string PhotoHash { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only cache of digital ID status, encrypted via DPAPI.
    /// </summary>
    [Table("digital_id_status_cache")]
    public class DigitalIdStatusCache
    {
        [Key]
        [MaxLength(100)]
        public string BeneficiaryIdHash { get; set; } = string.Empty;

        public string EncryptedBeneficiaryId { get; set; } = string.Empty;

        public string EncryptedStatus { get; set; } = string.Empty;

        public string EncryptedExpiryDate { get; set; } = string.Empty;

        public string EncryptedCardNumber { get; set; } = string.Empty;

        public string EncryptedQrPayload { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only cache of e-Kard CRS digital ID status rows (contract: digital_ids
    /// most-recent-row per beneficiary_id), encrypted via DPAPI. Pull-sync style —
    /// served when offline with a "last synced" timestamp.
    /// </summary>
    [Table("crs_status_cache")]
    public class CrsStatusCache
    {
        [Key]
        [MaxLength(60)]
        public string BeneficiaryId { get; set; } = string.Empty;

        public string EncryptedIdNumber { get; set; } = string.Empty;

        public string EncryptedStatus { get; set; } = string.Empty;

        public string EncryptedIssuedDate { get; set; } = string.Empty;

        public string EncryptedExpiryDate { get; set; } = string.Empty;

        public string EncryptedRevokedAt { get; set; } = string.Empty;

        public string EncryptedRevocationReason { get; set; } = string.Empty;

        public DateTime SyncedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only cache of e-Kard CRS resident photos, keyed by
    /// demographic_characteristics.id and invalidated by comparing the remote
    /// updated_at (contract Part 2), encrypted via DPAPI. A row with
    /// PhotoConfirmedAbsent = true is a confirmed "no photo on file" state,
    /// distinct from "never checked" (no row).
    /// </summary>
    [Table("crs_photo_cache")]
    public class CrsPhotoCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long DemographicCharacteristicId { get; set; }

        [MaxLength(60)]
        public string BeneficiaryId { get; set; } = string.Empty;

        public string EncryptedPhotoBytes { get; set; } = string.Empty;

        public bool PhotoConfirmedAbsent { get; set; }

        [MaxLength(60)]
        public string? SourceUpdatedAt { get; set; }

        public DateTime SyncedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local-only cache of e-Kard CRS demographic_characteristics rows (marital
    /// status, ethnicity, tribe), keyed by demographic_characteristics.id and
    /// refreshed by the masterlist mirror. READ only from CRS; the profile_picture
    /// blob is excluded — photos live in crs_photo_cache.
    /// </summary>
    [Table("crs_demographics_cache")]
    public class CrsDemographicsCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long DemographicCharacteristicId { get; set; }

        [MaxLength(60)]
        public string BeneficiaryId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? MaritalStatus { get; set; }

        [MaxLength(120)]
        public string? Ethnicity { get; set; }

        [MaxLength(120)]
        public string? Tribe { get; set; }

        [MaxLength(60)]
        public string? SourceUpdatedAt { get; set; }

        public DateTime SyncedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Local queue for CRS record_access_logs verification-audit rows that failed
    /// to write because the CRS database was offline. Contract rule: never block a
    /// verification on the audit write — queue and retry instead.
    /// </summary>
    [Table("crs_pending_access_logs")]
    public class CrsPendingAccessLog
    {
        [Key]
        public int Id { get; set; }

        public string PayloadJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

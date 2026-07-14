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
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("beneficiary_digital_ids")]
    public class BeneficiaryDigitalId
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("beneficiary_staging_id")]
        [Required]
        public int BeneficiaryStagingId { get; set; }

        [Column("household_id")]
        [Required]
        public int HouseholdId { get; set; }

        [Column("household_member_id")]
        public int? HouseholdMemberId { get; set; }

        [Column("card_number")]
        [Required]
        [MaxLength(40)]
        public string CardNumber { get; set; } = string.Empty;

        [Column("qr_payload")]
        [Required]
        [MaxLength(200)]
        public string QrPayload { get; set; } = string.Empty;

        [Column("photo_path")]
        [MaxLength(255)]
        public string? PhotoPath { get; set; }

        [Column("issued_by_user_id")]
        [Required]
        public int IssuedByUserId { get; set; }

        [Column("issued_at")]
        public DateTime IssuedAt { get; set; } = DateTime.Now;

        [Column("last_printed_at")]
        public DateTime? LastPrintedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("fingerprint_templates")]
    public class FingerprintTemplate
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("finger_index")]
        [Required]
        public int FingerIndex { get; set; }

        [Column("template_data")]
        [Required]
        public byte[] TemplateData { get; set; } = Array.Empty<byte>();

        [Column("template_format")]
        [Required]
        [MaxLength(50)]
        public string TemplateFormat { get; set; } = "DPFP.Template";

        [Column("quality_score")]
        public int? QualityScore { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("enrolled_at")]
        public DateTime EnrolledAt { get; set; } = DateTime.Now;

        [Column("enrolled_by_user_id")]
        public int? EnrolledByUserId { get; set; }

        [Column("last_verified_at")]
        public DateTime? LastVerifiedAt { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [ForeignKey("EnrolledByUserId")]
        public User? EnrolledByUser { get; set; }
    }
}

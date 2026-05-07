using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    public enum AssetStatus
    {
        Available,
        Borrowed,
        Maintenance,
        Lost,
        Damaged
    }

    [Table("barangay_assets")]
    public class BarangayAsset
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("asset_tag")]
        [Required]
        [MaxLength(50)]
        public string AssetTag { get; set; } = string.Empty;

        [Column("category")]
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("status", TypeName = "varchar(32)")]
        public AssetStatus Status { get; set; } = AssetStatus.Available;

        [Column("added_on")]
        public DateTime AddedOn { get; set; } = DateTime.Now;
    }
}

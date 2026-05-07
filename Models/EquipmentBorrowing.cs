using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("equipment_borrowings")]
    public class EquipmentBorrowing
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("asset_id")]
        [Required]
        public int AssetId { get; set; }

        [Column("beneficiary_id")]
        [MaxLength(120)]
        public string? BeneficiaryId { get; set; }

        [Column("beneficiary_name")]
        [MaxLength(150)]
        public string? BeneficiaryName { get; set; }

        [Column("borrow_date")]
        public DateTime BorrowDate { get; set; } = DateTime.Now;

        [Column("due_date")]
        public DateTime DueDate { get; set; }

        [Column("return_date")]
        public DateTime? ReturnDate { get; set; }

        [Column("condition_out")]
        [MaxLength(200)]
        public string? ConditionOut { get; set; }

        [Column("condition_in")]
        [MaxLength(200)]
        public string? ConditionIn { get; set; }

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [ForeignKey(nameof(AssetId))]
        public BarangayAsset Asset { get; set; } = null!;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("households")]
    public class Household
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("household_code")]
        [Required]
        [MaxLength(50)]
        public string HouseholdCode { get; set; } = string.Empty;

        [Column("head_name")]
        [Required]
        [MaxLength(150)]
        public string HeadName { get; set; } = string.Empty;

        [Column("address_line")]
        [Required]
        [MaxLength(250)]
        public string AddressLine { get; set; } = string.Empty;

        [Column("purok")]
        [MaxLength(100)]
        public string Purok { get; set; } = string.Empty;

        [Column("contact_number")]
        [MaxLength(50)]
        public string ContactNumber { get; set; } = string.Empty;

        [Column("status")]
        public HouseholdStatus Status { get; set; } = HouseholdStatus.Active;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
    }

    public enum HouseholdStatus
    {
        Active,
        Inactive
    }
}

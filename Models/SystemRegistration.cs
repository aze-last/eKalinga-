using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("system_registrations")]
    public sealed class SystemRegistration
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("company_serial_number")]
        [Required]
        [MaxLength(80)]
        public string CompanySerialNumber { get; set; } = string.Empty;

        [Column("company_name")]
        [MaxLength(180)]
        public string? CompanyName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Column("last_validated_at")]
        public DateTime? LastValidatedAt { get; set; }
    }
}

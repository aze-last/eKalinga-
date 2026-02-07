using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("user_profiles")]
    public class UserProfile
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("full_name")]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Column("nickname")]
        [MaxLength(80)]
        public string Nickname { get; set; } = string.Empty;

        [Column("phone")]
        [MaxLength(30)]
        public string Phone { get; set; } = string.Empty;

        [Column("address")]
        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        [Column("emergency_contact_name")]
        [MaxLength(120)]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Column("emergency_contact_phone")]
        [MaxLength(30)]
        public string EmergencyContactPhone { get; set; } = string.Empty;

        [Column("photo_path")]
        [MaxLength(255)]
        public string PhotoPath { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

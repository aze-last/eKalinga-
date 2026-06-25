using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("users")]
    public class User
    {
        public Guid SyncId { get; set; } = Guid.NewGuid();

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Column("email")]
        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Column("password_hash")]
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("role")]
        [Required]
        public UserRole Role { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public enum UserRole
    {
        SuperAdmin,
        Admin
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("shift_assignments")]
    public class ShiftAssignment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("shift_id")]
        [Required]
        public int ShiftId { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        // Navigation
        [ForeignKey("ShiftId")]
        public Shift Shift { get; set; } = null!;

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }
}
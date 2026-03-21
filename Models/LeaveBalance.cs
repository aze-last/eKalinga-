using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("leave_balances")]
    public class LeaveBalance
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("year")]
        [Required]
        public int Year { get; set; }

        [Column("vacation_days")]
        public decimal VacationDays { get; set; } = 15; // Default 15 days per year

        [Column("sick_days")]
        public decimal SickDays { get; set; } = 10; // Default 10 days per year

        [Column("used_vacation_days")]
        public decimal UsedVacationDays { get; set; } = 0;

        [Column("used_sick_days")]
        public decimal UsedSickDays { get; set; } = 0;

        // Navigation
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;

        // Calculated properties
        [NotMapped]
        public decimal RemainingVacationDays => VacationDays - UsedVacationDays;

        [NotMapped]
        public decimal RemainingSickDays => SickDays - UsedSickDays;
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("payroll")]
    public class Payroll
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        [Required]
        public int EmployeeId { get; set; }

        [Column("period_start")]
        [Required]
        public DateTime PeriodStart { get; set; }

        [Column("period_end")]
        [Required]
        public DateTime PeriodEnd { get; set; }

        [Column("regular_pay")]
        public decimal RegularPay { get; set; }

        [Column("overtime_pay")]
        public decimal OvertimePay { get; set; }

        [Column("holiday_pay")]
        public decimal HolidayPay { get; set; }

        [Column("total_pay")]
        public decimal TotalPay { get; set; }

        [Column("generated_at")]
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        [Column("generated_by")]
        [Required]
        public int GeneratedBy { get; set; }

        // Navigation
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;

        [ForeignKey("GeneratedBy")]
        public User GeneratedByUser { get; set; } = null!;
    }
}
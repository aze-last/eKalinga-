using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("holidays")]
    public class Holiday
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("holiday_date")]
        [Required]
        public DateTime HolidayDate { get; set; }

        [Column("name")]
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("is_double_pay")]
        public bool IsDoublePay { get; set; } = true;
    }
}
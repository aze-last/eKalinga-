using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("positions")]
    public class Position
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("area")]
        [Required]
        public PositionArea Area { get; set; }

        // Navigation
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    }

    public enum PositionArea
    {
        Kitchen,
        POS,
        DT,
        Lobby
    }
}
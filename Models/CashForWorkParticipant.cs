using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("cash_for_work_participants")]
    public class CashForWorkParticipant
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("event_id")]
        [Required]
        public int EventId { get; set; }

        [Column("household_member_id")]
        [Required]
        public int HouseholdMemberId { get; set; }

        [Column("added_by_user_id")]
        [Required]
        public int AddedByUserId { get; set; }

        [Column("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(EventId))]
        public CashForWorkEvent Event { get; set; } = null!;

        [ForeignKey(nameof(HouseholdMemberId))]
        public HouseholdMember HouseholdMember { get; set; } = null!;

        [ForeignKey(nameof(AddedByUserId))]
        public User AddedByUser { get; set; } = null!;

        public ICollection<CashForWorkAttendance> Attendances { get; set; } = new List<CashForWorkAttendance>();
    }
}

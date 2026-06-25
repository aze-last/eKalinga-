using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("user_permissions")]
    public class UserPermission
    {
        public Guid SyncId { get; set; } = Guid.NewGuid();

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        [Column("can_access_dashboard")]
        public bool CanAccessDashboard { get; set; } = true;

        [Column("can_access_master_list")]
        public bool CanAccessMasterList { get; set; } = true;

        [Column("can_access_assistance_cases")]
        public bool CanAccessAssistanceCases { get; set; } = true;

        [Column("can_access_budget")]
        public bool CanAccessBudget { get; set; } = true;

        [Column("can_access_distribution")]
        public bool CanAccessDistribution { get; set; } = true;

        [Column("can_access_cash_for_work")]
        public bool CanAccessCashForWork { get; set; } = true;

        [Column("can_access_borrowing")]
        public bool CanAccessBorrowing { get; set; } = true;

        [Column("can_access_reports")]
        public bool CanAccessReports { get; set; } = true;

        [Column("can_access_ggms_transactions")]
        public bool CanAccessGgmsTransactions { get; set; } = true;

        [Column("can_access_app_database")]
        public bool CanAccessAppDatabase { get; set; } = true;

        [Column("can_access_ggms_budget_source")]
        public bool CanAccessGgmsBudgetSource { get; set; } = true;

        [Column("can_access_scanning_portal")]
        public bool CanAccessScanningPortal { get; set; } = true;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}

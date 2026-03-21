using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<EmployeeExit> EmployeeExits { get; set; }
        public DbSet<EngagementSurvey> EngagementSurveys { get; set; }
        public DbSet<FingerprintTemplate> FingerprintTemplates { get; set; }
        public DbSet<PerformanceGoal> PerformanceGoals { get; set; }
        public DbSet<RecruitmentCandidate> RecruitmentCandidates { get; set; }
        public DbSet<TrainingRecord> TrainingRecords { get; set; }

        public DbSet<Household> Households { get; set; }
        public DbSet<HouseholdMember> HouseholdMembers { get; set; }
        public DbSet<BeneficiaryStaging> BeneficiaryStaging { get; set; }
        public DbSet<CashForWorkEvent> CashForWorkEvents { get; set; }
        public DbSet<CashForWorkParticipant> CashForWorkParticipants { get; set; }
        public DbSet<CashForWorkAttendance> CashForWorkAttendances { get; set; }

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = ConnectionSettingsService.GetEffectiveConnectionString();
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<Position>()
                .Property(p => p.Area)
                .HasConversion<string>();

            modelBuilder.Entity<Employee>()
                .Property(e => e.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Attendance>()
                .Property(a => a.Status)
                .HasConversion<string>();

            modelBuilder.Entity<LeaveRequest>()
                .Property(lr => lr.Type)
                .HasConversion<string>();

            modelBuilder.Entity<LeaveRequest>()
                .Property(lr => lr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Notification>()
                .Property(n => n.Type)
                .HasConversion<string>();

            modelBuilder.Entity<EmployeeExit>()
                .Property(exit => exit.ExitType)
                .HasConversion<string>();

            modelBuilder.Entity<PerformanceGoal>()
                .Property(goal => goal.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Household>()
                .Property(household => household.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkEvent>()
                .Property(cashForWorkEvent => cashForWorkEvent.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkAttendance>()
                .Property(attendance => attendance.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkAttendance>()
                .Property(attendance => attendance.Source)
                .HasConversion<string>();

            modelBuilder.Entity<UserProfile>()
                .HasIndex(profile => profile.UserId)
                .IsUnique();

            modelBuilder.Entity<UserPreference>()
                .HasIndex(preference => preference.UserId)
                .IsUnique();

            modelBuilder.Entity<Employee>()
                .HasIndex(employee => employee.UserId)
                .IsUnique();

            modelBuilder.Entity<ShiftAssignment>()
                .HasIndex(assignment => new { assignment.ShiftId, assignment.EmployeeId })
                .IsUnique();

            modelBuilder.Entity<FingerprintTemplate>()
                .HasIndex(template => new { template.UserId, template.FingerIndex })
                .IsUnique();

            modelBuilder.Entity<Household>()
                .HasIndex(household => household.HouseholdCode)
                .IsUnique();

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(row => row.CivilRegistryId);

            modelBuilder.Entity<CashForWorkParticipant>()
                .HasIndex(participant => new { participant.EventId, participant.HouseholdMemberId })
                .IsUnique();
        }
    }
}

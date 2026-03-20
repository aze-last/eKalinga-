using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using AttendanceShiftingManagement.Services;
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
        public DbSet<FingerprintTemplate> FingerprintTemplates { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<RecruitmentCandidate> RecruitmentCandidates { get; set; }
        public DbSet<EmployeeExit> EmployeeExits { get; set; }
        public DbSet<PerformanceGoal> PerformanceGoals { get; set; }
        public DbSet<TrainingRecord> TrainingRecords { get; set; }
        public DbSet<EngagementSurvey> EngagementSurveys { get; set; }
        public DbSet<Household> Households { get; set; }
        public DbSet<HouseholdMember> HouseholdMembers { get; set; }
        public DbSet<CashForWorkEvent> CashForWorkEvents { get; set; }
        public DbSet<CashForWorkParticipant> CashForWorkParticipants { get; set; }
        public DbSet<CashForWorkAttendance> CashForWorkAttendances { get; set; }
        public DbSet<BeneficiaryStaging> BeneficiaryStaging { get; set; }

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

            // Configure enum to string conversions
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

            modelBuilder.Entity<RecruitmentCandidate>()
                .Property(rc => rc.Source)
                .HasConversion<string>();

            modelBuilder.Entity<RecruitmentCandidate>()
                .Property(rc => rc.Stage)
                .HasConversion<string>();

            modelBuilder.Entity<EmployeeExit>()
                .Property(ee => ee.ExitType)
                .HasConversion<string>();

            modelBuilder.Entity<PerformanceGoal>()
                .Property(pg => pg.Status)
                .HasConversion<string>();

            modelBuilder.Entity<TrainingRecord>()
                .Property(tr => tr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<EngagementSurvey>()
                .Property(es => es.BurnoutRisk)
                .HasConversion<string>();

            modelBuilder.Entity<Household>()
                .Property(h => h.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkEvent>()
                .Property(e => e.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkAttendance>()
                .Property(a => a.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkAttendance>()
                .Property(a => a.Source)
                .HasConversion<string>();

            modelBuilder.Entity<UserProfile>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            modelBuilder.Entity<UserPreference>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // Unique constraint for shift assignments
            modelBuilder.Entity<ShiftAssignment>()
                .HasIndex(sa => new { sa.ShiftId, sa.EmployeeId })
                .IsUnique();

            // Unique constraint for user-employee relationship
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.UserId)
                .IsUnique();

            modelBuilder.Entity<FingerprintTemplate>()
                .HasIndex(ft => new { ft.UserId, ft.FingerIndex })
                .IsUnique();

            modelBuilder.Entity<FingerprintTemplate>()
                .HasIndex(ft => ft.IsActive);

            modelBuilder.Entity<FingerprintTemplate>()
                .HasOne(ft => ft.EnrolledByUser)
                .WithMany()
                .HasForeignKey(ft => ft.EnrolledByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<RecruitmentCandidate>()
                .HasIndex(rc => rc.Email);

            modelBuilder.Entity<EmployeeExit>()
                .HasIndex(ee => ee.EmployeeId);

            modelBuilder.Entity<PerformanceGoal>()
                .HasIndex(pg => pg.EmployeeId);

            modelBuilder.Entity<TrainingRecord>()
                .HasIndex(tr => tr.EmployeeId);

            modelBuilder.Entity<EngagementSurvey>()
                .HasIndex(es => es.EmployeeId);

            modelBuilder.Entity<EngagementSurvey>()
                .HasIndex(es => es.SurveyDate);

            modelBuilder.Entity<Household>()
                .HasIndex(h => h.HouseholdCode)
                .IsUnique();

            modelBuilder.Entity<HouseholdMember>()
                .HasIndex(hm => hm.HouseholdId);

            modelBuilder.Entity<CashForWorkEvent>()
                .HasIndex(e => new { e.EventDate, e.Status });

            modelBuilder.Entity<CashForWorkParticipant>()
                .HasIndex(p => new { p.EventId, p.HouseholdMemberId })
                .IsUnique();

            modelBuilder.Entity<CashForWorkAttendance>()
                .HasIndex(a => new { a.ParticipantId, a.AttendanceDate })
                .IsUnique();

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(b => b.VerificationStatus);

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(b => b.CivilRegistryId);
        }
    }
}

using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<Household> Households => Set<Household>();
        public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
        public DbSet<BeneficiaryStaging> BeneficiaryStaging => Set<BeneficiaryStaging>();
        public DbSet<CashForWorkEvent> CashForWorkEvents => Set<CashForWorkEvent>();
        public DbSet<CashForWorkParticipant> CashForWorkParticipants => Set<CashForWorkParticipant>();
        public DbSet<CashForWorkAttendance> CashForWorkAttendances => Set<CashForWorkAttendance>();

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

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            modelBuilder.Entity<UserProfile>()
                .HasIndex(profile => profile.UserId)
                .IsUnique();

            modelBuilder.Entity<Household>()
                .HasIndex(household => household.HouseholdCode)
                .IsUnique();

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(row => row.CivilRegistryId);

            modelBuilder.Entity<CashForWorkParticipant>()
                .HasIndex(participant => new { participant.EventId, participant.HouseholdMemberId })
                .IsUnique();

            modelBuilder.Entity<UserProfile>()
                .HasOne<User>()
                .WithOne()
                .HasForeignKey<UserProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActivityLog>()
                .HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

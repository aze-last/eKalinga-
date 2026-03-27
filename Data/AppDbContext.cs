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
        public DbSet<SystemRegistration> SystemRegistrations => Set<SystemRegistration>();
        public DbSet<Household> Households => Set<Household>();
        public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
        public DbSet<AssistanceCase> AssistanceCases => Set<AssistanceCase>();
        public DbSet<BeneficiaryStaging> BeneficiaryStaging => Set<BeneficiaryStaging>();
        public DbSet<BeneficiaryAssistanceLedgerEntry> BeneficiaryAssistanceLedgerEntries => Set<BeneficiaryAssistanceLedgerEntry>();
        public DbSet<BeneficiaryDigitalId> BeneficiaryDigitalIds => Set<BeneficiaryDigitalId>();
        public DbSet<AyudaProgram> AyudaPrograms => Set<AyudaProgram>();
        public DbSet<AyudaProjectBeneficiary> AyudaProjectBeneficiaries => Set<AyudaProjectBeneficiary>();
        public DbSet<AyudaProjectClaim> AyudaProjectClaims => Set<AyudaProjectClaim>();
        public DbSet<GovernmentBudgetSnapshot> GovernmentBudgetSnapshots => Set<GovernmentBudgetSnapshot>();
        public DbSet<PrivateDonation> PrivateDonations => Set<PrivateDonation>();
        public DbSet<BudgetLedgerEntry> BudgetLedgerEntries => Set<BudgetLedgerEntry>();
        public DbSet<CashForWorkEvent> CashForWorkEvents => Set<CashForWorkEvent>();
        public DbSet<CashForWorkParticipant> CashForWorkParticipants => Set<CashForWorkParticipant>();
        public DbSet<CashForWorkAttendance> CashForWorkAttendances => Set<CashForWorkAttendance>();
        public DbSet<ScannerSession> ScannerSessions => Set<ScannerSession>();

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
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
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

            modelBuilder.Entity<AssistanceCase>()
                .Property(assistanceCase => assistanceCase.Status)
                .HasConversion<string>();

            modelBuilder.Entity<AssistanceCase>()
                .Property(assistanceCase => assistanceCase.Priority)
                .HasConversion<string>();

            modelBuilder.Entity<AssistanceCase>()
                .Property(assistanceCase => assistanceCase.ReleaseKind)
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

            modelBuilder.Entity<ScannerSession>()
                .Property(session => session.Mode)
                .HasConversion<string>();

            modelBuilder.Entity<BeneficiaryAssistanceLedgerEntry>()
                .Property(entry => entry.SourceModule)
                .HasConversion<string>();

            modelBuilder.Entity<AyudaProgram>()
                .Property(program => program.ProgramType)
                .HasConversion<string>();

            modelBuilder.Entity<AyudaProgram>()
                .Property(program => program.DistributionStatus)
                .HasConversion<string>();

            modelBuilder.Entity<GovernmentBudgetSnapshot>()
                .Property(snapshot => snapshot.SyncStatus)
                .HasConversion<string>();

            modelBuilder.Entity<PrivateDonation>()
                .Property(donation => donation.DonorType)
                .HasConversion<string>();

            modelBuilder.Entity<PrivateDonation>()
                .Property(donation => donation.ProofType)
                .HasConversion<string>();

            modelBuilder.Entity<BudgetLedgerEntry>()
                .Property(entry => entry.EntryType)
                .HasConversion<string>();

            modelBuilder.Entity<BudgetLedgerEntry>()
                .Property(entry => entry.FeatureSource)
                .HasConversion<string>();

            modelBuilder.Entity<BudgetLedgerEntry>()
                .Property(entry => entry.ReleaseKind)
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

            modelBuilder.Entity<SystemRegistration>()
                .HasIndex(registration => registration.CompanySerialNumber)
                .IsUnique();

            modelBuilder.Entity<Household>()
                .HasIndex(household => household.HouseholdCode)
                .IsUnique();

            modelBuilder.Entity<AssistanceCase>()
                .HasIndex(assistanceCase => assistanceCase.CaseNumber)
                .IsUnique();

            modelBuilder.Entity<AyudaProgram>()
                .HasIndex(program => program.ProgramCode)
                .IsUnique();

            modelBuilder.Entity<AyudaProjectBeneficiary>()
                .HasIndex(item => new { item.AyudaProgramId, item.BeneficiaryStagingId })
                .IsUnique();

            modelBuilder.Entity<AyudaProjectClaim>()
                .HasIndex(item => new { item.AyudaProgramId, item.BeneficiaryStagingId })
                .IsUnique();

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(row => row.CivilRegistryId);

            modelBuilder.Entity<BeneficiaryDigitalId>()
                .HasIndex(item => item.BeneficiaryStagingId)
                .IsUnique();

            modelBuilder.Entity<BeneficiaryDigitalId>()
                .HasIndex(item => item.CardNumber)
                .IsUnique();

            modelBuilder.Entity<BeneficiaryDigitalId>()
                .HasIndex(item => item.QrPayload)
                .IsUnique();

            modelBuilder.Entity<BeneficiaryAssistanceLedgerEntry>()
                .HasIndex(entry => entry.CivilRegistryId);

            modelBuilder.Entity<BeneficiaryAssistanceLedgerEntry>()
                .HasIndex(entry => entry.BeneficiaryId);

            modelBuilder.Entity<BudgetLedgerEntry>()
                .HasIndex(entry => new { entry.FeatureSource, entry.SourceRecordId, entry.EntryType });

            modelBuilder.Entity<CashForWorkParticipant>()
                .HasIndex(participant => new { participant.EventId, participant.HouseholdMemberId })
                .IsUnique();

            modelBuilder.Entity<ScannerSession>()
                .HasIndex(session => session.SessionToken)
                .IsUnique();

            modelBuilder.Entity<AyudaProjectBeneficiary>()
                .HasOne(item => item.AyudaProgram)
                .WithMany()
                .HasForeignKey(item => item.AyudaProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AyudaProjectClaim>()
                .HasOne(item => item.AyudaProgram)
                .WithMany()
                .HasForeignKey(item => item.AyudaProgramId)
                .OnDelete(DeleteBehavior.Cascade);

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

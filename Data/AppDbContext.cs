using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<SystemRegistration> SystemRegistrations => Set<SystemRegistration>();
        public DbSet<Household> Households => Set<Household>();
        public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
        public DbSet<AssistanceCase> AssistanceCases => Set<AssistanceCase>();
        public DbSet<AssistanceCaseBudget> AssistanceCaseBudgets => Set<AssistanceCaseBudget>();
        public DbSet<CashForWorkBudget> CashForWorkBudgets => Set<CashForWorkBudget>();
        public DbSet<BeneficiaryStaging> BeneficiaryStaging => Set<BeneficiaryStaging>();
        public DbSet<BeneficiaryAssistanceLedgerEntry> BeneficiaryAssistanceLedgerEntries => Set<BeneficiaryAssistanceLedgerEntry>();
        public DbSet<BeneficiaryDigitalId> BeneficiaryDigitalIds => Set<BeneficiaryDigitalId>();
        public DbSet<AyudaProgram> AyudaPrograms => Set<AyudaProgram>();
        public DbSet<AyudaProjectBeneficiary> AyudaProjectBeneficiaries => Set<AyudaProjectBeneficiary>();
        public DbSet<AyudaProjectClaim> AyudaProjectClaims => Set< AyudaProjectClaim>();
        public DbSet<ProjectBudgetSource> ProjectBudgetSources => Set<ProjectBudgetSource>();
        public DbSet<GovernmentBudgetSnapshot> GovernmentBudgetSnapshots => Set<GovernmentBudgetSnapshot>();
        public DbSet<PrivateDonation> PrivateDonations => Set<PrivateDonation>();
        public DbSet<BudgetLedgerEntry> BudgetLedgerEntries => Set<BudgetLedgerEntry>();
        public DbSet<CashForWorkEvent> CashForWorkEvents => Set<CashForWorkEvent>();
        public DbSet<CashForWorkParticipant> CashForWorkParticipants => Set<CashForWorkParticipant>();
        public DbSet<CashForWorkAttendance> CashForWorkAttendances => Set<CashForWorkAttendance>();
        public DbSet<ScannerSession> ScannerSessions => Set<ScannerSession>();
        public DbSet<BarangayAsset> BarangayAssets => Set<BarangayAsset>();
        public DbSet<EquipmentBorrowing> EquipmentBorrowings => Set<EquipmentBorrowing>();

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
                // We use a fixed version instead of AutoDetect to prevent the app from crashing 
                // on startup if the MySQL server is unreachable.
                var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
                optionsBuilder.UseMySql(connectionString, serverVersion);
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

            modelBuilder.Entity<CashForWorkEvent>()
                .Property(cashForWorkEvent => cashForWorkEvent.EventKind)
                .HasConversion<string>();

            modelBuilder.Entity<CashForWorkEvent>()
                .Property(cashForWorkEvent => cashForWorkEvent.BenefitType)
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

            modelBuilder.Entity<BarangayAsset>()
                .Property(asset => asset.Status)
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

            modelBuilder.Entity<AyudaProgram>()
                .Property(program => program.ReleaseKind)
                .HasConversion<string>();

            modelBuilder.Entity<AyudaProjectBeneficiary>()
                .Property(item => item.Status)
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

            modelBuilder.Entity<PrivateDonation>()
                .HasCheckConstraint("CK_PrivateDonation_SingleTarget",
                    "(target_program_id IS NOT NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL) OR " +
                    "(target_program_id IS NULL AND target_assistance_case_budget_id IS NOT NULL AND target_cash_for_work_budget_id IS NULL) OR " +
                    "(target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NOT NULL) OR " +
                    "(target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL)");

            modelBuilder.Entity<GovernmentBudgetSnapshot>()
                .HasCheckConstraint("CK_GovernmentBudgetSnapshot_SingleTarget",
                    "(target_program_id IS NOT NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL) OR " +
                    "(target_program_id IS NULL AND target_assistance_case_budget_id IS NOT NULL AND target_cash_for_work_budget_id IS NULL) OR " +
                    "(target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NOT NULL) OR " +
                    "(target_program_id IS NULL AND target_assistance_case_budget_id IS NULL AND target_cash_for_work_budget_id IS NULL)");

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

            modelBuilder.Entity<BarangayAsset>()
                .HasIndex(asset => asset.AssetTag)
                .IsUnique();

            modelBuilder.Entity<AssistanceCaseBudget>()
                .HasIndex(item => item.BudgetCode)
                .IsUnique();

            modelBuilder.Entity<CashForWorkBudget>()
                .HasIndex(item => item.BudgetCode)
                .IsUnique();

            modelBuilder.Entity<AyudaProjectBeneficiary>()
                .HasIndex(item => new { item.AyudaProgramId, item.BeneficiaryStagingId })
                .IsUnique();

            modelBuilder.Entity<AyudaProjectClaim>()
                .HasIndex(item => new { item.AyudaProgramId, item.BeneficiaryStagingId })
                .IsUnique();

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(row => row.CivilRegistryId);

            modelBuilder.Entity<BeneficiaryStaging>()
                .HasIndex(row => row.ResidentsId);

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
                .HasIndex(participant => new { participant.EventId, participant.BeneficiaryStagingId })
                .IsUnique();

            modelBuilder.Entity<ScannerSession>()
                .HasIndex(session => session.SessionToken)
                .IsUnique();

            modelBuilder.Entity<EquipmentBorrowing>()
                .HasIndex(borrowing => borrowing.BeneficiaryId);

            modelBuilder.Entity<UserPermission>()
                .HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserPermission>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            modelBuilder.Entity<AyudaProjectBeneficiary>()
                .HasOne(item => item.AyudaProgram)
                .WithMany()
                .HasForeignKey(item => item.AyudaProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssistanceCase>()
                .HasOne(item => item.AssistanceCaseBudget)
                .WithMany()
                .HasForeignKey(item => item.AssistanceCaseBudgetId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CashForWorkEvent>()
                .HasOne(item => item.CashForWorkBudget)
                .WithMany()
                .HasForeignKey(item => item.CashForWorkBudgetId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AyudaProjectClaim>()
                .HasOne(item => item.AyudaProgram)
                .WithMany()
                .HasForeignKey(item => item.AyudaProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectBudgetSource>()
                .HasOne(item => item.AyudaProgram)
                .WithMany()
                .HasForeignKey(item => item.AyudaProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectBudgetSource>()
                .HasIndex(item => new { item.AyudaProgramId, item.Priority });

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

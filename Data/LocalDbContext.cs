using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public class LocalDbContext : DbContext
    {
        // ── Standard App Tables ────────────────────────────────────────────────
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
        public DbSet<AyudaProjectClaim> AyudaProjectClaims => Set<AyudaProjectClaim>();
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

        // ── Local-Only Offline Cache Tables ────────────────────────────────────
        public DbSet<GgmsAllocationCache> GgmsAllocationCache => Set<GgmsAllocationCache>();
        public DbSet<GgmsTransactionCache> GgmsTransactionCache => Set<GgmsTransactionCache>();
        public DbSet<GgmsPendingTransactionCache> GgmsPendingTransactionCache => Set<GgmsPendingTransactionCache>();
        public DbSet<GgmsProjectCache> GgmsProjectCache => Set<GgmsProjectCache>();
        public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();
        public DbSet<DigitalIdPhotoCache> DigitalIdPhotoCaches => Set<DigitalIdPhotoCache>();
        public DbSet<DigitalIdStatusCache> DigitalIdStatusCaches => Set<DigitalIdStatusCache>();
        public DbSet<CrsStatusCache> CrsStatusCaches => Set<CrsStatusCache>();
        public DbSet<CrsPhotoCache> CrsPhotoCaches => Set<CrsPhotoCache>();
        public DbSet<CrsPendingAccessLog> CrsPendingAccessLogs => Set<CrsPendingAccessLog>();

        public LocalDbContext()
        {
        }

        public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Always point to local SQLite file for offline usage
                optionsBuilder.UseSqlite("Data Source=ams.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            if (Database.ProviderName == "Pomelo.EntityFrameworkCore.MySql")
            {
                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    if (entityType.ClrType != null && entityType.ClrType.GetProperty("SyncId") != null)
                    {
                        modelBuilder.Entity(entityType.ClrType).Ignore("SyncId");
                    }
                }
            }

            // ── Enums as Strings (SQLite handles this fine) ─────────────────────
            modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>();
            modelBuilder.Entity<Household>().Property(h => h.Status).HasConversion<string>();
            modelBuilder.Entity<AssistanceCase>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<AssistanceCase>().Property(a => a.Priority).HasConversion<string>();
            modelBuilder.Entity<AssistanceCase>().Property(a => a.ReleaseKind).HasConversion<string>();
            modelBuilder.Entity<CashForWorkEvent>().Property(c => c.Status).HasConversion<string>();
            modelBuilder.Entity<CashForWorkEvent>().Property(c => c.EventKind).HasConversion<string>();
            modelBuilder.Entity<CashForWorkEvent>().Property(c => c.BenefitType).HasConversion<string>();
            modelBuilder.Entity<CashForWorkAttendance>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<CashForWorkAttendance>().Property(a => a.Source).HasConversion<string>();
            modelBuilder.Entity<ScannerSession>().Property(s => s.Mode).HasConversion<string>();
            modelBuilder.Entity<BarangayAsset>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<BeneficiaryAssistanceLedgerEntry>().Property(e => e.SourceModule).HasConversion<string>();
            modelBuilder.Entity<AyudaProgram>().Property(p => p.ProgramType).HasConversion<string>();
            modelBuilder.Entity<AyudaProgram>().Property(p => p.DistributionStatus).HasConversion<string>();
            modelBuilder.Entity<AyudaProgram>().Property(p => p.ReleaseKind).HasConversion<string>();
            modelBuilder.Entity<AyudaProjectBeneficiary>().Property(i => i.Status).HasConversion<string>();
            modelBuilder.Entity<GovernmentBudgetSnapshot>().Property(s => s.SyncStatus).HasConversion<string>();
            modelBuilder.Entity<PrivateDonation>().Property(d => d.DonorType).HasConversion<string>();
            modelBuilder.Entity<PrivateDonation>().Property(d => d.ProofType).HasConversion<string>();
            modelBuilder.Entity<PrivateDonation>().Property(d => d.DonationType).HasConversion<string>();
            modelBuilder.Entity<BudgetLedgerEntry>().Property(e => e.EntryType).HasConversion<string>();
            modelBuilder.Entity<BudgetLedgerEntry>().Property(e => e.FeatureSource).HasConversion<string>();
            modelBuilder.Entity<BudgetLedgerEntry>().Property(e => e.ReleaseKind).HasConversion<string>();

            // Note: HasCheckConstraint logic from AppDbContext is OMITTED here for SQLite compatibility.

            // ── Unique Indexes ──────────────────────────────────────────────────
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<UserProfile>().HasIndex(p => p.UserId).IsUnique();
            modelBuilder.Entity<SystemRegistration>().HasIndex(r => r.CompanySerialNumber).IsUnique();
            modelBuilder.Entity<Household>().HasIndex(h => h.HouseholdCode).IsUnique();
            modelBuilder.Entity<AssistanceCase>().HasIndex(a => a.CaseNumber).IsUnique();
            modelBuilder.Entity<AyudaProgram>().HasIndex(p => p.ProgramCode).IsUnique();
            modelBuilder.Entity<BarangayAsset>().HasIndex(a => a.AssetTag).IsUnique();
            modelBuilder.Entity<AssistanceCaseBudget>().HasIndex(i => i.BudgetCode).IsUnique();
            modelBuilder.Entity<CashForWorkBudget>().HasIndex(i => i.BudgetCode).IsUnique();
            modelBuilder.Entity<AyudaProjectBeneficiary>().HasIndex(i => new { i.AyudaProgramId, i.BeneficiaryStagingId }).IsUnique();
            modelBuilder.Entity<AyudaProjectClaim>().HasIndex(i => new { i.AyudaProgramId, i.BeneficiaryStagingId }).IsUnique();
            modelBuilder.Entity<BeneficiaryDigitalId>().HasIndex(i => i.BeneficiaryStagingId).IsUnique();
            modelBuilder.Entity<BeneficiaryDigitalId>().HasIndex(i => i.CardNumber).IsUnique();
            modelBuilder.Entity<BeneficiaryDigitalId>().HasIndex(i => i.QrPayload).IsUnique();
            modelBuilder.Entity<CashForWorkParticipant>().HasIndex(p => new { p.EventId, p.BeneficiaryStagingId }).IsUnique();
            modelBuilder.Entity<ScannerSession>().HasIndex(s => s.SessionToken).IsUnique();
            modelBuilder.Entity<GgmsProjectCache>().HasIndex(p => p.ProjectDetailsId).IsUnique();

            // ── Normal Indexes ──────────────────────────────────────────────────
            modelBuilder.Entity<BeneficiaryStaging>().HasIndex(r => r.CivilRegistryId);
            modelBuilder.Entity<BeneficiaryStaging>().HasIndex(r => r.ResidentsId);
            modelBuilder.Entity<BeneficiaryStaging>().HasIndex(r => r.BeneficiaryId);
            modelBuilder.Entity<BeneficiaryStaging>().HasIndex(r => r.LastName);
            modelBuilder.Entity<BeneficiaryStaging>().HasIndex(r => r.FirstName);
            modelBuilder.Entity<BeneficiaryAssistanceLedgerEntry>().HasIndex(e => e.CivilRegistryId);
            modelBuilder.Entity<BeneficiaryAssistanceLedgerEntry>().HasIndex(e => e.BeneficiaryId);
            modelBuilder.Entity<BudgetLedgerEntry>().HasIndex(e => new { e.FeatureSource, e.SourceRecordId, e.EntryType });
            modelBuilder.Entity<EquipmentBorrowing>().HasIndex(b => b.BeneficiaryId);
            modelBuilder.Entity<UserPermission>().HasIndex(p => p.UserId).IsUnique();
            modelBuilder.Entity<ProjectBudgetSource>().HasIndex(i => new { i.AyudaProgramId, i.Priority });

            // ── Relationships & Delete Behaviors ───────────────────────────────
            modelBuilder.Entity<UserPermission>()
                .HasOne(p => p.User).WithOne().HasForeignKey<UserPermission>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AyudaProjectBeneficiary>()
                .HasOne(i => i.AyudaProgram).WithMany().HasForeignKey(i => i.AyudaProgramId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssistanceCase>()
                .HasOne(i => i.AssistanceCaseBudget).WithMany().HasForeignKey(i => i.AssistanceCaseBudgetId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CashForWorkEvent>()
                .HasOne(i => i.CashForWorkBudget).WithMany().HasForeignKey(i => i.CashForWorkBudgetId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AyudaProjectClaim>()
                .HasOne(i => i.AyudaProgram).WithMany().HasForeignKey(i => i.AyudaProgramId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectBudgetSource>()
                .HasOne(i => i.AyudaProgram).WithMany().HasForeignKey(i => i.AyudaProgramId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProfile>()
                .HasOne<User>().WithOne().HasForeignKey<UserProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActivityLog>()
                .HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Data
{
    [Table("val_beneficiaries")]
    public class CrsValBeneficiary
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("residents_id")]
        public long? ResidentsId { get; set; }

        [Column("beneficiary_id")]
        public string BeneficiaryId { get; set; } = string.Empty;

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("civilregistry_id")]
        public string? CivilRegistryId { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("sex")]
        public string? Sex { get; set; }

        [Column("date_of_birth")]
        public string? DateOfBirth { get; set; }

        /// <summary>
        /// Per the CRS schema-drift notice the age column must not be read (removed/type-drifted
        /// post-migration); age is derived from the safe-parsed birth date instead.
        /// </summary>
        [NotMapped]
        public string? Age => CrsAgeCalculator.CalculateAgeText(DateOfBirth);

        [Column("marital_status")]
        public string? MaritalStatus { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("is_pwd")]
        public bool IsPwd { get; set; }

        [Column("pwd_id_no")]
        public string? PwdIdNo { get; set; }

        [Column("is_senior")]
        public bool IsSenior { get; set; }

        [Column("senior_id_no")]
        public string? SeniorIdNo { get; set; }

        [Column("disability_type")]
        public string? DisabilityType { get; set; }

        [Column("cause_of_disability")]
        public string? CauseOfDisability { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    [Table("BeneficiaryStaging")]
    public class CrsBeneficiaryStaging
    {
        [Key]
        [Column("StagingID")]
        public int StagingId { get; set; }

        [Column("BeneficiaryId")]
        public string? BeneficiaryId { get; set; }
    }

    [Table("beneficiary_digital_ids")]
    public class CrsBeneficiaryDigitalId
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("beneficiary_staging_id")]
        public int BeneficiaryStagingId { get; set; }

        [Column("household_id")]
        public int? HouseholdId { get; set; }

        [Column("household_member_id")]
        public int? HouseholdMemberId { get; set; }

        [Column("card_number")]
        [Required]
        [MaxLength(40)]
        public string CardNumber { get; set; } = string.Empty;

        [Column("qr_payload")]
        [Required]
        [MaxLength(200)]
        public string QrPayload { get; set; } = string.Empty;

        [Column("photo_path")]
        [MaxLength(255)]
        public string? PhotoPath { get; set; }

        [Column("issued_by_user_id")]
        public int IssuedByUserId { get; set; }

        [Column("issued_at")]
        public DateTime IssuedAt { get; set; }

        [Column("last_printed_at")]
        public DateTime? LastPrintedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }
    }

    public class CrsDbContext : DbContext
    {
        public DbSet<CrsValBeneficiary> ValBeneficiaries => Set<CrsValBeneficiary>();
        public DbSet<CrsBeneficiaryStaging> BeneficiaryStagings => Set<CrsBeneficiaryStaging>();
        public DbSet<CrsBeneficiaryDigitalId> BeneficiaryDigitalIds => Set<CrsBeneficiaryDigitalId>();

        public CrsDbContext()
        {
        }

        public CrsDbContext(DbContextOptions<CrsDbContext> options) : base(options)
        {
        }
    }

    public interface ICrsDbContextFactory
    {
        CrsDbContext CreateDbContext();
    }

    public class CrsDbContextFactory : ICrsDbContextFactory
    {
        private readonly ICrsConnectionProvider _connectionProvider;

        public CrsDbContextFactory(ICrsConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        public CrsDbContext CreateDbContext()
        {
            var connectionString = _connectionProvider.GetConnectionString();
            var optionsBuilder = new DbContextOptionsBuilder<CrsDbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            return new CrsDbContext(optionsBuilder.Options);
        }
    }
}

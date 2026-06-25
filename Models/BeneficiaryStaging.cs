using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceShiftingManagement.Models
{
    [Table("BeneficiaryStaging")]
    public class BeneficiaryStaging
    {
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Key]
        public int StagingID { get; set; }

        public long? ResidentsId { get; set; }
        public string? BeneficiaryId { get; set; }
        public string? CivilRegistryId { get; set; }

        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? FullName { get; set; }

        public string? Sex { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Age { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Address { get; set; }

        public bool IsPwd { get; set; }
        public string? PwdIdNo { get; set; }
        public string? DisabilityType { get; set; }
        public string? CauseOfDisability { get; set; }

        public bool IsSenior { get; set; }
        public string? SeniorIdNo { get; set; }

        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
        public int? LinkedHouseholdId { get; set; }
        public int? LinkedHouseholdMemberId { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? ReviewNotes { get; set; }
        public string? PhotoPath { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime ImportedAt { get; set; } = DateTime.Now;
    }

    public enum VerificationStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Verified = 3,
        Duplicate = 4,
        Inactive = 5
    }
}

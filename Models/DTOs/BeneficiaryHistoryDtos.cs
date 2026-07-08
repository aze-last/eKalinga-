using System;

namespace AttendanceShiftingManagement.Models.DTOs
{
    public sealed class BeneficiaryHistoryDto
    {
        public int Id { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string SourceModule { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string ReleaseKind { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class BeneficiarySummaryDto
    {
        public string BeneficiaryId { get; set; } = string.Empty;
        public string CivilRegistryId { get; set; } = string.Empty;
        public int TotalAssistanceRecords { get; set; }
        public decimal TotalCashReleased { get; set; }
        public decimal TotalGoodsReleased { get; set; }
        public decimal AssistanceThisMonth { get; set; }
        public DateTime? LatestAssistance { get; set; }
    }

    public sealed class BeneficiarySearchResultDto
    {
        public int StagingId { get; set; }
        public string BeneficiaryId { get; set; } = string.Empty;
        public string CivilRegistryId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string SexAgeSummary { get; set; } = string.Empty;
        public string FlagsSummary { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
    }
}

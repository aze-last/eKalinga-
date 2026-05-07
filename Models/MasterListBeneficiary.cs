namespace AttendanceShiftingManagement.Models
{
    public sealed class MasterListBeneficiary
    {
        public long Id { get; set; }
        public long ResidentsId { get; set; }
        public string BeneficiaryId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string CivilRegistryId { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsPwd { get; set; }
        public string PwdIdNo { get; set; } = string.Empty;
        public bool IsSenior { get; set; }
        public string SeniorIdNo { get; set; } = string.Empty;
        public string DisabilityType { get; set; } = string.Empty;
        public string CauseOfDisability { get; set; } = string.Empty;
        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();

        public string Initials
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName[..1]);
                if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName[..1]);
                
                if (parts.Count == 0 && !string.IsNullOrWhiteSpace(FullName))
                {
                    var nameParts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (nameParts.Length > 0) parts.Add(nameParts[0][..1]);
                    if (nameParts.Length > 1) parts.Add(nameParts[^1][..1]);
                }

                return string.Join("", parts).ToUpperInvariant();
            }
        }

        public string FlagsSummary
        {
            get
            {
                var flags = new List<string>();
                if (IsSenior)
                {
                    flags.Add("Senior");
                }

                if (IsPwd)
                {
                    flags.Add("PWD");
                }

                return flags.Count == 0 ? "Standard" : string.Join(" | ", flags);
            }
        }

        public string CivilRegistryStatus => string.IsNullOrWhiteSpace(CivilRegistryId) ? "No civil registry link" : "Civil registry linked";
        public bool HasCivilRegistryLink => !string.IsNullOrWhiteSpace(CivilRegistryId);
        public string SexAgeSummary => string.Join(" / ", new[] { Sex, Age }.Where(value => !string.IsNullOrWhiteSpace(value)));
        public string UpdatedAtDisplay => UpdatedAt?.ToString("MMM dd, yyyy hh:mm tt") ?? "--";

        public string VerificationStatusLabel => VerificationStatus.ToString();
        public bool IsApproved => VerificationStatus == VerificationStatus.Approved;
        public bool IsPending => VerificationStatus == VerificationStatus.Pending;
    }
}

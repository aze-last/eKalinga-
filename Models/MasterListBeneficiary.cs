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
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();

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
        public string SexAgeSummary => string.Join(" / ", new[] { Sex, Age }.Where(value => !string.IsNullOrWhiteSpace(value)));
        public string UpdatedAtDisplay => UpdatedAt?.ToString("MMM dd, yyyy hh:mm tt") ?? "--";
    }
}

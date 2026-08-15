using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>UI-only household snapshot for the masterlist detail panel (no EF entities cross to the ViewModel).</summary>
    public sealed record BeneficiaryHouseholdContext(
        bool HasHousehold,
        string HouseholdCode,
        string HeadName,
        string AddressLine,
        string Purok,
        IReadOnlyList<BeneficiaryHouseholdMemberItem> Members);

    public sealed record BeneficiaryHouseholdMemberItem(
        string FullName,
        string RelationshipToHead,
        bool IsSelectedBeneficiary);

    /// <summary>
    /// Read-only household context for the Masterlist &amp; Registry detail panel. Unlike the
    /// distribution variant, this snapshot carries no program/claims data — it only describes
    /// the family composition behind a beneficiary's staged record.
    /// </summary>
    public sealed class BeneficiaryHouseholdContextService
    {
        private readonly LocalDbContext _context;

        public BeneficiaryHouseholdContextService(LocalDbContext context)
        {
            _context = context;
        }

        public static BeneficiaryHouseholdContext Empty { get; } = new(
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<BeneficiaryHouseholdMemberItem>());

        public async Task<BeneficiaryHouseholdContext> GetHouseholdContextAsync(
            int? linkedHouseholdId,
            int? linkedHouseholdMemberId,
            string? beneficiaryId = null)
        {
            if (linkedHouseholdId != null)
            {
                var household = await _context.Households
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.Id == linkedHouseholdId.Value);

                if (household != null)
                {
                    var members = await _context.HouseholdMembers
                        .AsNoTracking()
                        .Where(m => m.HouseholdId == linkedHouseholdId.Value)
                        .OrderBy(m => m.FullName)
                        .Select(m => new { m.Id, m.FullName, m.RelationshipToHead })
                        .ToListAsync();

                    var items = members
                        .Select(m => new BeneficiaryHouseholdMemberItem(
                            m.FullName,
                            m.RelationshipToHead ?? string.Empty,
                            linkedHouseholdMemberId != null && linkedHouseholdMemberId.Value == m.Id))
                        .ToList();

                    return new BeneficiaryHouseholdContext(
                        true,
                        household.HouseholdCode,
                        household.HeadName,
                        household.AddressLine,
                        household.Purok,
                        items);
                }
            }

            // Fallback: Resolve household roster from BeneficiaryStaging using BeneficiaryId pattern
            // (e.g. BEN-2026-692811519-1 -> Family Code: 692811519, Prefix: BEN-2026-692811519-)
            if (!string.IsNullOrWhiteSpace(beneficiaryId))
            {
                var trimmedId = beneficiaryId.Trim();
                var lastDashIndex = trimmedId.LastIndexOf('-');
                if (lastDashIndex > 0)
                {
                    var prefix = trimmedId.Substring(0, lastDashIndex + 1); // e.g. "BEN-2026-692811519-"
                    var rawCode = trimmedId.Substring(0, lastDashIndex); // e.g. "BEN-2026-692811519"
                    var secondLastDash = rawCode.LastIndexOf('-');
                    var householdCode = secondLastDash > 0 ? $"HH-{rawCode.Substring(secondLastDash + 1)}" : rawCode;

                    var stagingMembers = await _context.BeneficiaryStaging
                        .AsNoTracking()
                        .Where(b => b.BeneficiaryId != null && b.BeneficiaryId.StartsWith(prefix))
                        .OrderBy(b => b.BeneficiaryId)
                        .Select(b => new { b.BeneficiaryId, b.FullName, b.Address })
                        .ToListAsync();

                    if (stagingMembers.Count > 0)
                    {
                        var head = stagingMembers.FirstOrDefault(m => m.BeneficiaryId != null && m.BeneficiaryId.EndsWith("-1")) ?? stagingMembers[0];
                        var memberItems = stagingMembers.Select(m =>
                        {
                            var isHead = m.BeneficiaryId != null && m.BeneficiaryId.EndsWith("-1");
                            var isSelected = string.Equals(m.BeneficiaryId, trimmedId, StringComparison.OrdinalIgnoreCase);
                            var relation = isHead ? "Head of Household" : "Family Member";
                            return new BeneficiaryHouseholdMemberItem(m.FullName ?? string.Empty, relation, isSelected);
                        }).ToList();

                        return new BeneficiaryHouseholdContext(
                            true,
                            householdCode,
                            head.FullName ?? string.Empty,
                            head.Address ?? string.Empty,
                            string.Empty,
                            memberItems);
                    }
                }
            }

            return Empty;
        }
    }
}

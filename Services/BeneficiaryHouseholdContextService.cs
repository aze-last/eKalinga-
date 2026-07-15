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
            int? linkedHouseholdMemberId)
        {
            if (linkedHouseholdId == null)
            {
                return Empty;
            }

            var household = await _context.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == linkedHouseholdId.Value);

            if (household == null)
            {
                return Empty;
            }

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
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

/// <summary>
/// The pre-enrollment Household Records Review modal must list the CRS family
/// (siblings sharing the BEN-YYYY-&lt;family&gt;-&lt;member&gt; id stem) when no local
/// household has been linked by an operator.
/// </summary>
public sealed class CrsFamilyBenefitRecordsTests
{
    private static BeneficiaryStaging Seed(
        LocalDbContext context,
        int stagingId,
        string beneficiaryId,
        string fullName)
    {
        var beneficiary = new BeneficiaryStaging
        {
            StagingID = stagingId,
            BeneficiaryId = beneficiaryId,
            FullName = fullName,
            VerificationStatus = VerificationStatus.Approved,
            ReviewedAt = DateTime.Now
        };
        context.BeneficiaryStaging.Add(beneficiary);
        context.SaveChanges();
        return beneficiary;
    }

    [Fact]
    public async Task NoLinkedHousehold_FallsBackToCrsFamilyStem()
    {
        using var context = TestDbContextFactory.CreateContext();
        Seed(context, 27, "BEN-2026-692811519-1", "CAJES, Nazareno Cortes");
        Seed(context, 28, "BEN-2026-692811519-2", "Cagalawan, Josephine Antipuesto");
        Seed(context, 29, "BEN-2026-692811519-3", "Cajes, Jake Kim Cagalawan");
        // Different family — same prefix digits but longer stem must NOT match.
        Seed(context, 30, "BEN-2026-6928115190-1", "Other, Family Member");
        var service = new ProjectDistributionService(context);

        var records = await service.GetHouseholdBenefitRecordsAsync(28);

        Assert.True(records.HasHousehold);
        Assert.Equal("CRS Family BEN-2026-692811519", records.HouseholdCode);
        Assert.Equal("CAJES, Nazareno Cortes", records.HeadName);
        Assert.Equal(3, records.Members.Count);
        Assert.Equal("Registrant", records.Members[0].RelationshipToHead);
        Assert.True(records.Members[1].IsCandidateBeneficiary);
        Assert.DoesNotContain(records.Members, member => member.FullName == "Other, Family Member");
    }

    [Fact]
    public async Task CrsFamilyFallback_CountsClaimsPerMember()
    {
        using var context = TestDbContextFactory.CreateContext();
        var head = Seed(context, 41, "BEN-2026-100200300-1", "Reyes, Juan");
        Seed(context, 42, "BEN-2026-100200300-2", "Reyes, Maria");
        context.AyudaProjectClaims.Add(new AyudaProjectClaim
        {
            AyudaProgramId = 900,
            BeneficiaryStagingId = head.StagingID,
            FullName = "Reyes, Juan",
            ClaimedAt = DateTime.Now
        });
        context.SaveChanges();
        var service = new ProjectDistributionService(context);

        var records = await service.GetHouseholdBenefitRecordsAsync(42);

        Assert.True(records.HasHousehold);
        Assert.Equal(1, records.TotalHouseholdClaims);
        Assert.Equal(1, records.Members.Single(member => member.FullName == "Reyes, Juan").BenefitsReceivedCount);
        Assert.Equal(0, records.Members.Single(member => member.FullName == "Reyes, Maria").BenefitsReceivedCount);
    }

    [Fact]
    public async Task LinkedHousehold_StillTakesPrecedenceOverCrsFamily()
    {
        using var context = TestDbContextFactory.CreateContext();
        var household = new Household { HouseholdCode = "HH-0001", HeadName = "Linked Head" };
        context.Households.Add(household);
        context.SaveChanges();
        var beneficiary = Seed(context, 51, "BEN-2026-555666777-1", "Santos, Pedro");
        beneficiary.LinkedHouseholdId = household.Id;
        context.SaveChanges();
        var service = new ProjectDistributionService(context);

        var records = await service.GetHouseholdBenefitRecordsAsync(51);

        Assert.True(records.HasHousehold);
        Assert.Equal("HH-0001", records.HouseholdCode);
        Assert.Equal("Linked Head", records.HeadName);
    }

    [Fact]
    public async Task NonFamilyPatternId_ReportsNoHousehold()
    {
        using var context = TestDbContextFactory.CreateContext();
        Seed(context, 61, "BEN-0061", "Legacy, Import");
        var service = new ProjectDistributionService(context);

        var records = await service.GetHouseholdBenefitRecordsAsync(61);

        Assert.False(records.HasHousehold);
    }

    [Fact]
    public async Task CrsFamilyMembers_SortNumericallyByMemberNumber()
    {
        using var context = TestDbContextFactory.CreateContext();
        Seed(context, 71, "BEN-2026-900900900-10", "Family, Tenth");
        Seed(context, 72, "BEN-2026-900900900-2", "Family, Second");
        Seed(context, 73, "BEN-2026-900900900-1", "Family, First");
        var service = new ProjectDistributionService(context);

        var records = await service.GetHouseholdBenefitRecordsAsync(71);

        Assert.Equal(
            new[] { "Family, First", "Family, Second", "Family, Tenth" },
            records.Members.Select(member => member.FullName).ToArray());
        Assert.Equal("Family, First", records.HeadName);
    }
}

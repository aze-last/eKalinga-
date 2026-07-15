using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryHouseholdContextServiceTests
{
    [Fact]
    public async Task GetHouseholdContextAsync_WithNullHouseholdId_ReturnsEmpty()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = new BeneficiaryHouseholdContextService(context);

        var result = await service.GetHouseholdContextAsync(null, null);

        Assert.False(result.HasHousehold);
        Assert.Empty(result.Members);
        Assert.Equal(string.Empty, result.HouseholdCode);
    }

    [Fact]
    public async Task GetHouseholdContextAsync_WithLinkedHousehold_ReturnsSnapshotWithSelectedMemberFlag()
    {
        using var context = TestDbContextFactory.CreateContext();
        var household = new Household
        {
            HouseholdCode = "HH-100",
            HeadName = "Maria Santos",
            AddressLine = "Barangay Centro",
            Purok = "Purok 3",
            Status = HouseholdStatus.Active
        };
        context.Households.Add(household);
        context.SaveChanges();

        var memberA = new HouseholdMember
        {
            HouseholdId = household.Id,
            FullName = "Ana Santos",
            RelationshipToHead = "Daughter"
        };
        var memberB = new HouseholdMember
        {
            HouseholdId = household.Id,
            FullName = "Maria Santos",
            RelationshipToHead = "Head"
        };
        context.HouseholdMembers.AddRange(memberA, memberB);
        context.SaveChanges();

        var service = new BeneficiaryHouseholdContextService(context);
        var result = await service.GetHouseholdContextAsync(household.Id, memberA.Id);

        Assert.True(result.HasHousehold);
        Assert.Equal("HH-100", result.HouseholdCode);
        Assert.Equal("Maria Santos", result.HeadName);
        Assert.Equal("Barangay Centro", result.AddressLine);
        Assert.Equal("Purok 3", result.Purok);
        Assert.Equal(2, result.Members.Count);
        // Ordered by FullName: Ana before Maria.
        Assert.Equal("Ana Santos", result.Members[0].FullName);
        Assert.True(result.Members[0].IsSelectedBeneficiary);
        Assert.Equal("Daughter", result.Members[0].RelationshipToHead);
        Assert.Equal("Maria Santos", result.Members[1].FullName);
        Assert.False(result.Members[1].IsSelectedBeneficiary);
    }

    [Fact]
    public async Task GetHouseholdContextAsync_WithMissingHouseholdRow_ReturnsEmpty()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = new BeneficiaryHouseholdContextService(context);

        var result = await service.GetHouseholdContextAsync(9999, 1);

        Assert.False(result.HasHousehold);
        Assert.Empty(result.Members);
    }
}

using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class AssistanceCaseManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingAidRequestForValidatedBeneficiary_AssignsCaseNumber_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.CreateAsync(
            new AssistanceCaseUpsertRequest(
                null,
                null,
                "Ana Ramos",
                "BEN-ANA-001",
                "CRS-ANA-001",
                "Medical assistance",
                AssistanceCasePriority.High,
                AssistanceReleaseKind.Cash,
                5000m,
                new DateTime(2026, 3, 23),
                new DateTime(2026, 3, 30),
                "Hospital confinement support"),
            admin.Id);

        Assert.True(result.IsSuccess);
        var assistanceCase = Assert.Single(context.AssistanceCases);
        Assert.StartsWith("AR-", assistanceCase.CaseNumber);
        Assert.Equal(AssistanceCaseStatus.Pending, assistanceCase.Status);
        Assert.Null(assistanceCase.HouseholdId);
        Assert.Null(assistanceCase.HouseholdMemberId);
        Assert.Equal("Ana Ramos", assistanceCase.ValidatedBeneficiaryName);
        Assert.Equal("BEN-ANA-001", assistanceCase.ValidatedBeneficiaryId);
        Assert.Equal("CRS-ANA-001", assistanceCase.ValidatedCivilRegistryId);
        Assert.Equal("Medical assistance", assistanceCase.AssistanceType);
        Assert.Equal(AssistanceReleaseKind.Cash, assistanceCase.ReleaseKind);
        Assert.Equal(5000m, assistanceCase.RequestedAmount);
        Assert.Equal(5000m, assistanceCase.ApprovedAmount);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("AssistanceCaseCreated", auditLog.Action);
        Assert.Equal(assistanceCase.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task CreateAsync_RejectsLegacyHouseholdOnlySelection()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var member = SeedMember(context, household.Id);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.CreateAsync(
            new AssistanceCaseUpsertRequest(
                household.Id,
                member.Id,
                null,
                null,
                null,
                "Medical assistance",
                AssistanceCasePriority.Medium,
                AssistanceReleaseKind.Goods,
                2500m,
                new DateTime(2026, 3, 27),
                null,
                "Demo request from validated beneficiary"),
            admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Select a validated beneficiary before saving this aid request.", result.Message);
        Assert.Empty(context.AssistanceCases);
    }

    [Fact]
    public async Task UpdateAsync_RewritesLegacyHouseholdCaseToValidatedBeneficiary_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var assistanceCase = SeedAssistanceCase(context, household.Id, null, admin.Id);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.UpdateAsync(
            assistanceCase.Id,
            new AssistanceCaseUpsertRequest(
                null,
                null,
                "Ana Ramos",
                "BEN-ANA-001",
                "CRS-ANA-001",
                "Burial assistance",
                AssistanceCasePriority.Critical,
                AssistanceReleaseKind.Goods,
                10000m,
                assistanceCase.RequestedOn,
                new DateTime(2026, 4, 2),
                "Funeral support"),
            admin.Id);

        Assert.True(result.IsSuccess);
        var updatedCase = Assert.Single(context.AssistanceCases);
        Assert.Null(updatedCase.HouseholdId);
        Assert.Null(updatedCase.HouseholdMemberId);
        Assert.Equal("Ana Ramos", updatedCase.ValidatedBeneficiaryName);
        Assert.Equal("BEN-ANA-001", updatedCase.ValidatedBeneficiaryId);
        Assert.Equal("CRS-ANA-001", updatedCase.ValidatedCivilRegistryId);
        Assert.Equal("Burial assistance", updatedCase.AssistanceType);
        Assert.Equal(AssistanceCasePriority.Critical, updatedCase.Priority);
        Assert.Equal(AssistanceReleaseKind.Goods, updatedCase.ReleaseKind);
        Assert.Equal(10000m, updatedCase.ApprovedAmount);
        Assert.Equal(10000m, updatedCase.RequestedAmount);
        Assert.Null(updatedCase.Notes);

        var auditLog = context.ActivityLogs.Single(log => log.Action == "AssistanceCaseUpdated");
        Assert.Equal(updatedCase.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task ChangeStatusAsync_ApprovesCase_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var assistanceCase = SeedAssistanceCase(context, household.Id, null, admin.Id);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.ChangeStatusAsync(
            assistanceCase.Id,
            AssistanceCaseStatus.Approved,
            admin.Id,
            null);

        Assert.True(result.IsSuccess);
        var updatedCase = Assert.Single(context.AssistanceCases);
        Assert.Equal(AssistanceCaseStatus.Approved, updatedCase.Status);
        Assert.Equal(admin.Id, updatedCase.ReviewedByUserId);
        Assert.Null(updatedCase.ResolutionNotes);

        var auditLog = context.ActivityLogs.Single(log => log.Action == "AssistanceCaseStatusChanged");
        Assert.Equal(updatedCase.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectingWithoutResolutionNotes_AllowsTransition()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var assistanceCase = SeedAssistanceCase(context, household.Id, null, admin.Id);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.ChangeStatusAsync(
            assistanceCase.Id,
            AssistanceCaseStatus.Rejected,
            admin.Id,
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceCaseStatus.Rejected, context.AssistanceCases.Single().Status);
        Assert.Contains(context.ActivityLogs, log => log.Action == "AssistanceCaseStatusChanged");
    }

    [Fact]
    public async Task DeleteAsync_RemovesNonBudgetAidRequest()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var assistanceCase = SeedAssistanceCase(context, household.Id, null, admin.Id);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.DeleteAsync(assistanceCase.Id, admin.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AssistanceCases);
        Assert.Contains(context.ActivityLogs, log => log.Action == "AssistanceCaseDeleted");
    }

    [Fact]
    public async Task DeleteAsync_WhenBudgetAlreadyLinked_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var assistanceCase = SeedAssistanceCase(context, household.Id, null, admin.Id);
        assistanceCase.BudgetLedgerEntryId = 99;
        context.SaveChanges();
        var service = new AssistanceCaseManagementService(context);

        var result = await service.DeleteAsync(assistanceCase.Id, admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Single(context.AssistanceCases);
    }

    private static User SeedAdmin(Data.AppDbContext context)
    {
        var user = new User
        {
            Username = "admin",
            Email = "admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static Household SeedHousehold(Data.AppDbContext context)
    {
        var household = new Household
        {
            HouseholdCode = "HH-CASE-001",
            HeadName = "Lourdes Ramos",
            AddressLine = "Purok 2, Barangay Centro",
            Purok = "Purok 2",
            ContactNumber = "09171234567",
            Status = HouseholdStatus.Active
        };

        context.Households.Add(household);
        context.SaveChanges();
        return household;
    }

    private static HouseholdMember SeedMember(Data.AppDbContext context, int householdId)
    {
        var member = new HouseholdMember
        {
            HouseholdId = householdId,
            FullName = "Ana Ramos",
            RelationshipToHead = "Daughter",
            Occupation = "Vendor",
            IsCashForWorkEligible = false,
            Notes = "Assistance applicant",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.HouseholdMembers.Add(member);
        context.SaveChanges();
        return member;
    }

    private static AssistanceCase SeedAssistanceCase(Data.AppDbContext context, int householdId, int? householdMemberId, int createdByUserId)
    {
        var assistanceCase = new AssistanceCase
        {
            CaseNumber = "AR-20260323-0001",
            HouseholdId = householdId,
            HouseholdMemberId = householdMemberId,
            AssistanceType = "Food assistance",
            ReleaseKind = AssistanceReleaseKind.Cash,
            Priority = AssistanceCasePriority.Medium,
            Status = AssistanceCaseStatus.Pending,
            RequestedAmount = 2500m,
            ApprovedAmount = 2500m,
            RequestedOn = new DateTime(2026, 3, 23),
            Summary = "Initial assistance request",
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AssistanceCases.Add(assistanceCase);
        context.SaveChanges();
        return assistanceCase;
    }
}

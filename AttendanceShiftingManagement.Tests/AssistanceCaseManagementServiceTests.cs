using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class AssistanceCaseManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingCase_AssignsCaseNumber_AndWritesAuditLog()
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
                "Medical assistance",
                AssistanceCasePriority.High,
                5000m,
                null,
                new DateTime(2026, 3, 23),
                new DateTime(2026, 3, 30),
                "Hospital confinement support",
                "Initial intake completed."),
            admin.Id);

        Assert.True(result.IsSuccess);
        var assistanceCase = Assert.Single(context.AssistanceCases);
        Assert.StartsWith("AC-", assistanceCase.CaseNumber);
        Assert.Equal(AssistanceCaseStatus.Pending, assistanceCase.Status);
        Assert.Equal(member.Id, assistanceCase.HouseholdMemberId);
        Assert.Equal("Medical assistance", assistanceCase.AssistanceType);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("AssistanceCaseCreated", auditLog.Action);
        Assert.Equal(assistanceCase.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEditableFields_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var member = SeedMember(context, household.Id);
        var assistanceCase = SeedAssistanceCase(context, household.Id, null, admin.Id);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.UpdateAsync(
            assistanceCase.Id,
            new AssistanceCaseUpsertRequest(
                household.Id,
                member.Id,
                "Burial assistance",
                AssistanceCasePriority.Critical,
                15000m,
                10000m,
                assistanceCase.RequestedOn,
                new DateTime(2026, 4, 2),
                "Funeral support",
                "Supporting documents validated."),
            admin.Id);

        Assert.True(result.IsSuccess);
        var updatedCase = Assert.Single(context.AssistanceCases);
        Assert.Equal(member.Id, updatedCase.HouseholdMemberId);
        Assert.Equal("Burial assistance", updatedCase.AssistanceType);
        Assert.Equal(AssistanceCasePriority.Critical, updatedCase.Priority);
        Assert.Equal(15000m, updatedCase.RequestedAmount);
        Assert.Equal(10000m, updatedCase.ApprovedAmount);
        Assert.Equal("Supporting documents validated.", updatedCase.Notes);

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
            "Approved after case conference.");

        Assert.True(result.IsSuccess);
        var updatedCase = Assert.Single(context.AssistanceCases);
        Assert.Equal(AssistanceCaseStatus.Approved, updatedCase.Status);
        Assert.Equal(admin.Id, updatedCase.ReviewedByUserId);
        Assert.Equal("Approved after case conference.", updatedCase.ResolutionNotes);

        var auditLog = context.ActivityLogs.Single(log => log.Action == "AssistanceCaseStatusChanged");
        Assert.Equal(updatedCase.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectingWithoutResolutionNotes_ReturnsFailure()
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

        Assert.False(result.IsSuccess);
        Assert.Equal(AssistanceCaseStatus.Pending, context.AssistanceCases.Single().Status);
        Assert.DoesNotContain(context.ActivityLogs, log => log.Action == "AssistanceCaseStatusChanged");
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
            CaseNumber = "AC-20260323-0001",
            HouseholdId = householdId,
            HouseholdMemberId = householdMemberId,
            AssistanceType = "Food assistance",
            Priority = AssistanceCasePriority.Medium,
            Status = AssistanceCaseStatus.Pending,
            RequestedAmount = 2500m,
            RequestedOn = new DateTime(2026, 3, 23),
            Summary = "Initial assistance request",
            Notes = "Waiting for review",
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AssistanceCases.Add(assistanceCase);
        context.SaveChanges();
        return assistanceCase;
    }
}

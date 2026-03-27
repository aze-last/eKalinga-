using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryVerificationServiceTests
{
    [Fact]
    public async Task ApproveAsync_CreatesHouseholdMember_UpdatesStatus_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.ApproveAsync(
            new BeneficiaryApprovalRequest(
                stagingRow.StagingID,
                household.Id,
                ExistingHouseholdMemberId: null,
                ReviewNotes: "Imported into household registry."),
            admin.Id);

        Assert.True(result.IsSuccess);
        var member = Assert.Single(context.HouseholdMembers);
        Assert.Equal(household.Id, member.HouseholdId);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal(VerificationStatus.Approved, staged.VerificationStatus);
        Assert.Equal(household.Id, staged.LinkedHouseholdId);
        Assert.Equal(member.Id, staged.LinkedHouseholdMemberId);

        Assert.Equal(2, context.ActivityLogs.Count());
        Assert.Contains(context.ActivityLogs, log => log.Action == "BeneficiaryApproved" && log.EntityId == stagingRow.StagingID);
    }

    [Fact]
    public async Task RejectAsync_UpdatesStatus_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.RejectAsync(stagingRow.StagingID, admin.Id, "Rejected after manual review.");

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationStatus.Rejected, context.BeneficiaryStaging.Single().VerificationStatus);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("BeneficiaryRejected", auditLog.Action);
        Assert.Equal(stagingRow.StagingID, auditLog.EntityId);
    }

    [Fact]
    public async Task ApproveAsync_WhenExistingHouseholdMemberIsSelected_LinksWithoutCreatingNewMember()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var existingMember = SeedMember(context, household.Id);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.ApproveAsync(
            new BeneficiaryApprovalRequest(
                stagingRow.StagingID,
                household.Id,
                existingMember.Id,
                "Matched to an existing household member."),
            admin.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(context.HouseholdMembers);

        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal(VerificationStatus.Approved, staged.VerificationStatus);
        Assert.Equal(household.Id, staged.LinkedHouseholdId);
        Assert.Equal(existingMember.Id, staged.LinkedHouseholdMemberId);
        Assert.Equal(admin.Id, staged.ReviewedByUserId);
        Assert.Equal("Matched to an existing household member.", staged.ReviewNotes);
    }

    [Fact]
    public async Task ApproveAsync_AutoIssuesDigitalIdForApprovedBeneficiary()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.ApproveAsync(
            new BeneficiaryApprovalRequest(
                stagingRow.StagingID,
                household.Id,
                ExistingHouseholdMemberId: null,
                ReviewNotes: "Ready for digital ID issuance."),
            admin.Id);

        Assert.True(result.IsSuccess);

        var digitalId = Assert.Single(context.BeneficiaryDigitalIds);
        var staged = Assert.Single(context.BeneficiaryStaging);

        Assert.Equal(stagingRow.StagingID, digitalId.BeneficiaryStagingId);
        Assert.Equal(household.Id, digitalId.HouseholdId);
        Assert.Equal(staged.LinkedHouseholdMemberId, digitalId.HouseholdMemberId);
        Assert.Equal(admin.Id, digitalId.IssuedByUserId);
        Assert.True(digitalId.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(digitalId.CardNumber));
        Assert.False(string.IsNullOrWhiteSpace(digitalId.QrPayload));
    }

    [Fact]
    public async Task SaveCorrectionsAsync_UpdatesStagedFields_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.SaveCorrectionsAsync(
            new BeneficiaryCorrectionRequest(
                stagingRow.StagingID,
                "BEN-UPDATED",
                "CRS-UPDATED",
                "Elena",
                "M.",
                "Rivera",
                "Elena M. Rivera",
                "Female",
                "1980-01-02",
                "46",
                "Married",
                "Updated address",
                "PWD-01",
                "Senior-01",
                "Visual",
                "Updated notes"),
            admin.Id);

        Assert.True(result.IsSuccess);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal("BEN-UPDATED", staged.BeneficiaryId);
        Assert.Equal("CRS-UPDATED", staged.CivilRegistryId);
        Assert.Equal("Elena M. Rivera", staged.FullName);
        Assert.Equal("Updated address", staged.Address);
        Assert.Equal("Updated notes", staged.ReviewNotes);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("BeneficiaryCorrected", auditLog.Action);
    }

    [Fact]
    public async Task VerifyAsync_SetsVerifiedStatus_AndAuditTrail()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.VerifyAsync(stagingRow.StagingID, admin.Id, "Identity verified.");

        Assert.True(result.IsSuccess);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal(VerificationStatus.Verified, staged.VerificationStatus);
        Assert.Equal("Identity verified.", staged.ReviewNotes);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("BeneficiaryVerified", auditLog.Action);
    }

    [Fact]
    public async Task MarkDuplicateAsync_SetsDuplicateStatus_AndRequiresReason()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.MarkDuplicateAsync(stagingRow.StagingID, admin.Id, "Duplicate of an existing CRS record.");

        Assert.True(result.IsSuccess);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal(VerificationStatus.Duplicate, staged.VerificationStatus);
        Assert.Equal("Duplicate of an existing CRS record.", staged.ReviewNotes);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("BeneficiaryMarkedDuplicate", auditLog.Action);
    }

    [Fact]
    public async Task MarkInactiveAsync_SetsInactiveStatus_AndAuditTrail()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.MarkInactiveAsync(stagingRow.StagingID, admin.Id, "Moved out of barangay.");

        Assert.True(result.IsSuccess);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal(VerificationStatus.Inactive, staged.VerificationStatus);
        Assert.Equal("Moved out of barangay.", staged.ReviewNotes);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("BeneficiaryMarkedInactive", auditLog.Action);
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
            HouseholdCode = "HH-001",
            HeadName = "Maria Santos",
            AddressLine = "Barangay Centro",
            Purok = "Purok 1",
            ContactNumber = "09170000001",
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
            FullName = "Existing Registry Member",
            RelationshipToHead = "Child",
            Occupation = "Student",
            IsCashForWorkEligible = false,
            Notes = "Existing registry record",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.HouseholdMembers.Add(member);
        context.SaveChanges();
        return member;
    }

    private static BeneficiaryStaging SeedStaging(Data.AppDbContext context)
    {
        var row = new BeneficiaryStaging
        {
            BeneficiaryId = "BEN-1",
            CivilRegistryId = "CRS-1",
            FirstName = "Elena",
            LastName = "Rivera",
            FullName = "Elena Rivera",
            VerificationStatus = VerificationStatus.Pending,
            ImportedAt = DateTime.Now
        };

        context.BeneficiaryStaging.Add(row);
        context.SaveChanges();
        return row;
    }
}

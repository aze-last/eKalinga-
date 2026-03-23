using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class GrievanceManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesOpenGrievance_ForImportedBeneficiary_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var staging = SeedStagedBeneficiary(context, "CRS-1002", "BEN-0002");
        var service = new GrievanceManagementService(context);

        var result = await service.CreateAsync(
            new GrievanceCreateRequest(
                staging.StagingID,
                GrievanceType.WrongRelease,
                "Wrong release amount",
                "The beneficiary reported a wrong release amount.",
                null,
                null),
            admin.Id);

        Assert.True(result.IsSuccess);

        var grievance = Assert.Single(context.GrievanceRecords);
        Assert.Equal(staging.StagingID, grievance.StagingId);
        Assert.Equal(staging.CivilRegistryId, grievance.CivilRegistryId);
        Assert.Equal(staging.BeneficiaryId, grievance.BeneficiaryId);
        Assert.Equal(GrievanceStatus.Open, grievance.Status);
        Assert.Equal(admin.Id, grievance.FiledByUserId);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("GrievanceCreated", auditLog.Action);
        Assert.Equal(grievance.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectingWithoutRemarks_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var grievance = SeedGrievance(context, admin.Id);
        var service = new GrievanceManagementService(context);

        var result = await service.ChangeStatusAsync(
            grievance.Id,
            GrievanceStatus.Rejected,
            admin.Id,
            null);

        Assert.False(result.IsSuccess);
        Assert.Equal(GrievanceStatus.Open, context.GrievanceRecords.Single().Status);
        Assert.DoesNotContain(context.ActivityLogs, log => log.Action == "GrievanceStatusChanged");
    }

    [Fact]
    public async Task UpdateAsync_ChangingCoreFieldsWithoutCorrectionRemarks_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var grievance = SeedGrievance(context, admin.Id);
        var service = new GrievanceManagementService(context);

        var result = await service.UpdateAsync(
            grievance.Id,
            new GrievanceUpdateRequest(
                GrievanceType.WrongIdentity,
                "Wrong beneficiary identity",
                "The linked beneficiary needs correction.",
                grievance.AssistanceCaseId,
                grievance.CashForWorkEventId,
                grievance.AssignedToUserId),
            admin.Id,
            null);

        Assert.False(result.IsSuccess);

        var unchanged = context.GrievanceRecords.Single();
        Assert.Equal(GrievanceType.Duplicate, unchanged.Type);
        Assert.Equal("Possible duplicate record", unchanged.Title);
        Assert.DoesNotContain(context.ActivityLogs, log => log.Action == "GrievanceUpdated");
    }

    private static User SeedAdmin(AppDbContext context)
    {
        var user = new User
        {
            Username = "grievance-admin",
            Email = "grievance-admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static BeneficiaryStaging SeedStagedBeneficiary(AppDbContext context, string civilRegistryId, string beneficiaryId)
    {
        var row = new BeneficiaryStaging
        {
            BeneficiaryId = beneficiaryId,
            CivilRegistryId = civilRegistryId,
            FullName = "Ramon Lopez",
            FirstName = "Ramon",
            LastName = "Lopez",
            Address = "Purok 5, Barangay Centro",
            VerificationStatus = VerificationStatus.Verified,
            ImportedAt = new DateTime(2026, 3, 23, 8, 0, 0)
        };

        context.BeneficiaryStaging.Add(row);
        context.SaveChanges();
        return row;
    }

    private static GrievanceRecord SeedGrievance(AppDbContext context, int filedByUserId)
    {
        var staging = SeedStagedBeneficiary(context, "CRS-2001", "BEN-2001");
        var grievance = new GrievanceRecord
        {
            GrievanceNumber = "GR-20260323-0001",
            CivilRegistryId = staging.CivilRegistryId,
            BeneficiaryId = staging.BeneficiaryId,
            StagingId = staging.StagingID,
            Type = GrievanceType.Duplicate,
            Status = GrievanceStatus.Open,
            Title = "Possible duplicate record",
            Description = "Imported row may already exist in another release record.",
            FiledByUserId = filedByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.GrievanceRecords.Add(grievance);
        context.SaveChanges();
        return grievance;
    }
}

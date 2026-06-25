using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryVerificationServiceTests
{
    [Fact]
    public async Task ApproveAsync_UpdatesStatusWithoutCreatingHouseholdLinks_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.ApproveAsync(
            new BeneficiaryApprovalRequest(
                stagingRow.StagingID,
                HouseholdId: 0,
                ExistingHouseholdMemberId: null,
                ReviewNotes: "Approved from validated queue."),
            admin.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.HouseholdMembers);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal(VerificationStatus.Approved, staged.VerificationStatus);
        Assert.Null(staged.LinkedHouseholdId);
        Assert.Null(staged.LinkedHouseholdMemberId);

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
    public async Task ApproveAsync_WithLegacyHouseholdArguments_DoesNotCarryHouseholdLinksForward()
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
        Assert.Null(staged.LinkedHouseholdId);
        Assert.Null(staged.LinkedHouseholdMemberId);
        Assert.Equal(admin.Id, staged.ReviewedByUserId);
        Assert.Equal("Matched to an existing household member.", staged.ReviewNotes);
    }

    [Fact]
    public async Task ApproveAsync_AutoIssuesDigitalIdForApprovedBeneficiary()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.ApproveAsync(
            new BeneficiaryApprovalRequest(
                stagingRow.StagingID,
                HouseholdId: 0,
                ExistingHouseholdMemberId: null,
                ReviewNotes: "Ready for digital ID issuance."),
            admin.Id);

        Assert.True(result.IsSuccess);

        var digitalId = Assert.Single(context.BeneficiaryDigitalIds);
        var staged = Assert.Single(context.BeneficiaryStaging);

        Assert.Equal(stagingRow.StagingID, digitalId.BeneficiaryStagingId);
        Assert.Null(typeof(BeneficiaryDigitalId).GetProperty(nameof(BeneficiaryDigitalId.HouseholdId))!.GetValue(digitalId));
        Assert.Null(typeof(BeneficiaryDigitalId).GetProperty(nameof(BeneficiaryDigitalId.HouseholdMemberId))!.GetValue(digitalId));
        Assert.Null(staged.LinkedHouseholdId);
        Assert.Null(staged.LinkedHouseholdMemberId);
        Assert.Equal(admin.Id, digitalId.IssuedByUserId);
        Assert.True(digitalId.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(digitalId.CardNumber));
        Assert.False(string.IsNullOrWhiteSpace(digitalId.QrPayload));
    }

    [Fact]
    public async Task ApproveAsync_WhenCorrectionsAreProvided_AppliesEditedFieldsBeforeApproval()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context);
        var service = new BeneficiaryVerificationService(context);

        var result = await service.ApproveAsync(
            new BeneficiaryApprovalRequest(
                stagingRow.StagingID,
                HouseholdId: 0,
                ExistingHouseholdMemberId: null,
                ReviewNotes: "Approved with corrected details.",
                Corrections: new BeneficiaryCorrectionRequest(
                    stagingRow.StagingID,
                    "BEN-CORRECTED",
                    "CRS-CORRECTED",
                    "Bien Josef",
                    "Gallur",
                    "Regidor",
                    "Bien Josef G. Regidor",
                    "Male",
                    "2001-02-24",
                    "25",
                    "Single",
                    "Purok 1, Barangay Centro",
                    false,
                    null,
                    false,
                    null,
                    null,
                    null,
                    "Approved with corrected details.")),
            admin.Id);

        Assert.True(result.IsSuccess);

        var staged = Assert.Single(context.BeneficiaryStaging);
        Assert.Equal("BEN-CORRECTED", staged.BeneficiaryId);
        Assert.Equal("CRS-CORRECTED", staged.CivilRegistryId);
        Assert.Equal("Bien Josef G. Regidor", staged.FullName);
        Assert.Equal("Purok 1, Barangay Centro", staged.Address);
        Assert.Equal(VerificationStatus.Approved, staged.VerificationStatus);
        Assert.Null(staged.LinkedHouseholdId);
        Assert.Null(staged.LinkedHouseholdMemberId);
        Assert.Empty(context.HouseholdMembers);
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
                true,
                "PWD-01",
                true,
                "Senior-01",
                "Visual",
                "Updated cause",
                "Updated notes"),
            admin.Id);

        Assert.True(result.IsSuccess);
        var staged = context.BeneficiaryStaging.Single();
        Assert.Equal("BEN-UPDATED", staged.BeneficiaryId);
        Assert.Equal("CRS-UPDATED", staged.CivilRegistryId);
        Assert.Equal("Elena M. Rivera", staged.FullName);
        Assert.Equal("Updated address", staged.Address);
        Assert.True(staged.IsPwd);
        Assert.True(staged.IsSenior);
        Assert.Equal("Updated cause", staged.CauseOfDisability);
        Assert.Equal("Updated notes", staged.ReviewNotes);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("BeneficiaryCorrected", auditLog.Action);
    }

    [Fact]
    public async Task SaveCorrectionsAsync_AllowsApprovedBeneficiary_AndSyncsDependentRecords()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id);
        var stagingRow = SeedStaging(context, VerificationStatus.Approved);

        var assistanceCase = new AssistanceCase
        {
            CaseNumber = "AR-20260408-0001",
            ValidatedBeneficiaryName = stagingRow.FullName,
            ValidatedBeneficiaryId = stagingRow.BeneficiaryId,
            ValidatedCivilRegistryId = stagingRow.CivilRegistryId,
            AssistanceType = "Food Pack",
            ReleaseKind = AssistanceReleaseKind.Goods,
            Priority = AssistanceCasePriority.Medium,
            Status = AssistanceCaseStatus.Approved,
            RequestedOn = DateTime.Today,
            ApprovedAmount = 500m,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        var projectBeneficiary = new AyudaProjectBeneficiary
        {
            AyudaProgramId = program.Id,
            BeneficiaryStagingId = stagingRow.StagingID,
            BeneficiaryId = stagingRow.BeneficiaryId,
            CivilRegistryId = stagingRow.CivilRegistryId,
            FullName = stagingRow.FullName ?? string.Empty,
            AddedByUserId = admin.Id,
            AddedAt = DateTime.Now
        };

        var ledgerEntry = new BeneficiaryAssistanceLedgerEntry
        {
            CivilRegistryId = stagingRow.CivilRegistryId,
            BeneficiaryId = stagingRow.BeneficiaryId,
            SourceModule = BeneficiaryAssistanceSourceModule.ManualHistory,
            SourceRecordId = "manual-history:1",
            ReleaseDate = DateTime.Today,
            Amount = 500m,
            Remarks = "Manual aid history",
            RecordedByUserId = admin.Id,
            CreatedAt = DateTime.Now
        };

        context.AssistanceCases.Add(assistanceCase);
        context.AyudaProjectBeneficiaries.Add(projectBeneficiary);
        context.BeneficiaryAssistanceLedgerEntries.Add(ledgerEntry);
        context.SaveChanges();

        var projectClaim = new AyudaProjectClaim
        {
            AyudaProgramId = program.Id,
            BeneficiaryStagingId = stagingRow.StagingID,
            ProjectBeneficiaryId = projectBeneficiary.Id,
            BeneficiaryId = stagingRow.BeneficiaryId,
            CivilRegistryId = stagingRow.CivilRegistryId,
            FullName = stagingRow.FullName ?? string.Empty,
            ClaimedByUserId = admin.Id,
            ClaimedAt = DateTime.Now
        };

        context.AyudaProjectClaims.Add(projectClaim);
        context.SaveChanges();

        var service = new BeneficiaryVerificationService(context);
        var result = await service.SaveCorrectionsAsync(
            new BeneficiaryCorrectionRequest(
                stagingRow.StagingID,
                "BEN-APPROVED-UPDATED",
                "CRS-APPROVED-UPDATED",
                "Maria",
                "L.",
                "Rivera",
                "Maria L. Rivera",
                "Female",
                "1985-04-01",
                "41",
                "Married",
                "Updated approved address",
                true,
                "PWD-88",
                true,
                "SC-88",
                "Mobility",
                "Accident",
                "Approved profile updated"),
            admin.Id);

        Assert.True(result.IsSuccess);

        var updatedStaging = Assert.Single(context.BeneficiaryStaging);
        Assert.Equal("BEN-APPROVED-UPDATED", updatedStaging.BeneficiaryId);
        Assert.Equal("CRS-APPROVED-UPDATED", updatedStaging.CivilRegistryId);
        Assert.Equal("Maria L. Rivera", updatedStaging.FullName);

        var updatedCase = Assert.Single(context.AssistanceCases);
        Assert.Equal("BEN-APPROVED-UPDATED", updatedCase.ValidatedBeneficiaryId);
        Assert.Equal("CRS-APPROVED-UPDATED", updatedCase.ValidatedCivilRegistryId);
        Assert.Equal("Maria L. Rivera", updatedCase.ValidatedBeneficiaryName);

        var updatedProjectBeneficiary = Assert.Single(context.AyudaProjectBeneficiaries);
        Assert.Equal("BEN-APPROVED-UPDATED", updatedProjectBeneficiary.BeneficiaryId);
        Assert.Equal("CRS-APPROVED-UPDATED", updatedProjectBeneficiary.CivilRegistryId);
        Assert.Equal("Maria L. Rivera", updatedProjectBeneficiary.FullName);

        var updatedProjectClaim = Assert.Single(context.AyudaProjectClaims);
        Assert.Equal("BEN-APPROVED-UPDATED", updatedProjectClaim.BeneficiaryId);
        Assert.Equal("CRS-APPROVED-UPDATED", updatedProjectClaim.CivilRegistryId);
        Assert.Equal("Maria L. Rivera", updatedProjectClaim.FullName);

        var updatedLedgerEntry = Assert.Single(context.BeneficiaryAssistanceLedgerEntries);
        Assert.Equal("BEN-APPROVED-UPDATED", updatedLedgerEntry.BeneficiaryId);
        Assert.Equal("CRS-APPROVED-UPDATED", updatedLedgerEntry.CivilRegistryId);
    }

    [Fact]
    public async Task ReturnToPendingAsync_RevertsApprovedBeneficiary_AndRevokesDigitalId()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedStaging(context, VerificationStatus.Approved);
        context.BeneficiaryDigitalIds.Add(new BeneficiaryDigitalId
        {
            BeneficiaryStagingId = stagingRow.StagingID,
            CardNumber = "BID-000001",
            QrPayload = "ASM-BID|000001|ABC123",
            IssuedByUserId = admin.Id,
            IssuedAt = DateTime.Now,
            IsActive = true
        });
        context.SaveChanges();

        var service = new BeneficiaryVerificationService(context);
        var result = await service.ReturnToPendingAsync(stagingRow.StagingID, admin.Id, "Needs profile re-check.");

        Assert.True(result.IsSuccess);

        var staged = Assert.Single(context.BeneficiaryStaging);
        Assert.Equal(VerificationStatus.Pending, staged.VerificationStatus);
        Assert.Equal("Needs profile re-check.", staged.ReviewNotes);

        var digitalId = Assert.Single(context.BeneficiaryDigitalIds);
        Assert.False(digitalId.IsActive);
        Assert.NotNull(digitalId.RevokedAt);

        Assert.Contains(context.ActivityLogs, log => log.Action == "BeneficiaryReturnedToPending");
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

    private static User SeedAdmin(Data.LocalDbContext context)
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

    private static Household SeedHousehold(Data.LocalDbContext context)
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

    private static HouseholdMember SeedMember(Data.LocalDbContext context, int householdId)
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

    private static AyudaProgram SeedProgram(Data.LocalDbContext context, int createdByUserId)
    {
        var program = new AyudaProgram
        {
            ProgramCode = "PRG-001",
            ProgramName = "Family Support",
            ProgramType = AyudaProgramType.GeneralPurpose,
            DistributionStatus = AyudaProgramDistributionStatus.Open,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
    }

    private static BeneficiaryStaging SeedStaging(Data.LocalDbContext context, VerificationStatus status = VerificationStatus.Pending)
    {
        var row = new BeneficiaryStaging
        {
            BeneficiaryId = "BEN-1",
            CivilRegistryId = "CRS-1",
            FirstName = "Elena",
            LastName = "Rivera",
            FullName = "Elena Rivera",
            VerificationStatus = status,
            ImportedAt = DateTime.Now
        };

        context.BeneficiaryStaging.Add(row);
        context.SaveChanges();
        return row;
    }
}

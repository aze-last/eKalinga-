using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkBudgetIntegrationTests
{
    [Fact]
    public async Task ReleaseEventAsync_ConsumesBudgetAndStoresLedgerReference()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context);
        var program = SeedProgram(context, admin.Id);
        SeedGovernmentSnapshot(context, 5000m);
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Canal Clearing",
            "Sitio Uno",
            new DateTime(2026, 3, 27),
            new TimeSpan(7, 0, 0),
            new TimeSpan(11, 0, 0),
            "Morning batch",
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);

        var result = await service.ReleaseEventAsync(
            cashForWorkEvent.Id,
            program.Id,
            3000m,
            admin.Id,
            "First payout batch");

        Assert.True(result.IsSuccess);

        var updatedEvent = context.CashForWorkEvents.Single();
        Assert.Equal(CashForWorkEventStatus.Completed, updatedEvent.Status);
        Assert.NotNull(updatedEvent.BudgetLedgerEntryId);
        Assert.Equal(3000m, updatedEvent.ReleaseAmount);

        var ledgerEntry = Assert.Single(context.BudgetLedgerEntries);
        Assert.Equal(BudgetLedgerFeatureSource.CashForWork, ledgerEntry.FeatureSource);
        Assert.Equal(1, ledgerEntry.RecipientCount);
        Assert.Equal(3000m, ledgerEntry.TotalAmount);
    }

    [Fact]
    public async Task ReleaseEventAsync_WhenCombinedBudgetIsInsufficient_ReturnsFailureWithoutLedgerEntry()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 2002, "Blocked Beneficiary");
        var program = SeedProgram(context, admin.Id);
        SeedGovernmentSnapshot(context, 1000m);
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Road Clearing",
            "Purok 2",
            new DateTime(2026, 3, 27),
            new TimeSpan(7, 0, 0),
            new TimeSpan(11, 0, 0),
            "Morning batch",
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);

        var result = await service.ReleaseEventAsync(
            cashForWorkEvent.Id,
            program.Id,
            3000m,
            admin.Id,
            "Blocked payout");

        Assert.False(result.IsSuccess);
        Assert.Empty(context.BudgetLedgerEntries);
        Assert.Null(context.CashForWorkEvents.Single().BudgetLedgerEntryId);
    }

    private static User SeedAdmin(Data.AppDbContext context)
    {
        var user = new User
        {
            Username = "c4w-admin",
            Email = "c4w-admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static BeneficiaryStaging SeedApprovedBeneficiary(Data.AppDbContext context, int stagingId = 2001, string fullName = "Joel Cruz")
    {
        var beneficiary = new BeneficiaryStaging
        {
            StagingID = stagingId,
            BeneficiaryId = $"BEN-{stagingId:D4}",
            CivilRegistryId = $"CR-{stagingId:D4}",
            FullName = fullName,
            VerificationStatus = VerificationStatus.Approved,
            ReviewedAt = DateTime.Now
        };

        context.BeneficiaryStaging.Add(beneficiary);
        context.SaveChanges();
        return beneficiary;
    }

    private static AyudaProgram SeedProgram(Data.AppDbContext context, int createdByUserId)
    {
        var program = new AyudaProgram
        {
            ProgramCode = "C4W-2026",
            ProgramName = "Cash for Work 2026",
            ProgramType = AyudaProgramType.CashForWork,
            Description = "Cash-for-work release program",
            CreatedByUserId = createdByUserId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
    }

    private static void SeedGovernmentSnapshot(Data.AppDbContext context, decimal allocatedAmount)
    {
        context.GovernmentBudgetSnapshots.Add(new GovernmentBudgetSnapshot
        {
            OfficeCode = "OFF-2026-0006",
            OfficeName = "Ayuda",
            YearlyBudgetId = 2,
            AllocatedAmount = allocatedAmount,
            SpentAmount = 0m,
            SourceRowId = "2",
            SyncStatus = GovernmentBudgetSyncStatus.Synced,
            SyncedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        context.SaveChanges();
    }
}

using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class AssistanceCaseBudgetIntegrationTests
{
    [Fact]
    public async Task ChangeStatusAsync_ReleasingApprovedCase_ConsumesBudgetAndStoresLedgerReference()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var program = SeedProgram(context, admin.Id);
        SeedGovernmentSnapshot(context, 10000m);
        var assistanceCase = SeedApprovedCase(context, household.Id, admin.Id, program.Id, 4000m);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.ChangeStatusAsync(
            assistanceCase.Id,
            AssistanceCaseStatus.Released,
            admin.Id,
            null);

        Assert.True(result.IsSuccess);

        var updatedCase = context.AssistanceCases.Single();
        Assert.Equal(AssistanceCaseStatus.Released, updatedCase.Status);
        Assert.NotNull(updatedCase.BudgetLedgerEntryId);
        Assert.Equal(AssistanceReleaseKind.Goods, updatedCase.ReleaseKind);

        var ledgerEntry = Assert.Single(context.BudgetLedgerEntries);
        Assert.Equal(BudgetLedgerEntryType.Release, ledgerEntry.EntryType);
        Assert.Equal(BudgetLedgerFeatureSource.AssistanceCase, ledgerEntry.FeatureSource);
        Assert.Equal(AssistanceReleaseKind.Goods, ledgerEntry.ReleaseKind);
        Assert.Equal(4000m, ledgerEntry.TotalAmount);
        Assert.Equal(updatedCase.BudgetLedgerEntryId, ledgerEntry.Id);
    }

    [Fact]
    public async Task ChangeStatusAsync_ReleasingCaseWithoutEnoughCombinedBudget_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var program = SeedProgram(context, admin.Id);
        SeedGovernmentSnapshot(context, 1000m);
        var assistanceCase = SeedApprovedCase(context, household.Id, admin.Id, program.Id, 4000m);
        var service = new AssistanceCaseManagementService(context);

        var result = await service.ChangeStatusAsync(
            assistanceCase.Id,
            AssistanceCaseStatus.Released,
            admin.Id,
            null);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssistanceCaseStatus.Approved, context.AssistanceCases.Single().Status);
        Assert.Empty(context.BudgetLedgerEntries);
    }

    private static User SeedAdmin(Data.AppDbContext context)
    {
        var user = new User
        {
            Username = "case-admin",
            Email = "case-admin@barangay.local",
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
            HouseholdCode = "HH-BUDGET-001",
            HeadName = "Rosalinda Perez",
            AddressLine = "Purok 3, Barangay Centro",
            Purok = "Purok 3",
            ContactNumber = "09175557777",
            Status = HouseholdStatus.Active
        };

        context.Households.Add(household);
        context.SaveChanges();
        return household;
    }

    private static AyudaProgram SeedProgram(Data.AppDbContext context, int createdByUserId)
    {
        var program = new AyudaProgram
        {
            ProgramCode = "AID-001",
            ProgramName = "General Ayuda Release",
            ProgramType = AyudaProgramType.AssistanceCase,
            Description = "Default program for case release",
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

    private static AssistanceCase SeedApprovedCase(Data.AppDbContext context, int householdId, int createdByUserId, int ayudaProgramId, decimal approvedAmount)
    {
        var assistanceCase = new AssistanceCase
        {
            CaseNumber = "AR-20260327-0001",
            HouseholdId = householdId,
            AssistanceType = "Medical assistance",
            Priority = AssistanceCasePriority.High,
            ReleaseKind = AssistanceReleaseKind.Goods,
            Status = AssistanceCaseStatus.Approved,
            RequestedAmount = approvedAmount,
            ApprovedAmount = approvedAmount,
            RequestedOn = new DateTime(2026, 3, 27),
            Summary = "Approved for hospital support",
            CreatedByUserId = createdByUserId,
            ReviewedByUserId = createdByUserId,
            AyudaProgramId = ayudaProgramId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AssistanceCases.Add(assistanceCase);
        context.SaveChanges();
        return assistanceCase;
    }
}

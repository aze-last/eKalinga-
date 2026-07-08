using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BudgetManagementServiceTests
{
    [Fact]
    public async Task RecordPrivateDonationAsync_CreatesDonationAndPrivateLedgerEntry()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new BudgetManagementService(context);

        var result = await service.RecordPrivateDonationAsync(
            new PrivateDonationRequest(
                PrivateDonationDonorType.Organization,
                "Barangay Traders Association",
                5000m,
                new DateTime(2026, 3, 27),
                "DON-001",
                "Initial private support",
                DonationProofType.Check,
                "CHK-9981",
                @"C:\proofs\check-9981.jpg"),
            admin.Id);

        Assert.True(result.IsSuccess);

        var donation = Assert.Single(context.PrivateDonations);
        Assert.Equal("Barangay Traders Association", donation.DonorName);
        Assert.Equal(5000m, donation.Amount);
        Assert.Equal(DonationProofType.Check, donation.ProofType);

        var ledger = Assert.Single(context.BudgetLedgerEntries);
        Assert.Equal(BudgetLedgerEntryType.Donation, ledger.EntryType);
        Assert.Equal(0m, ledger.GovernmentPortion);
        Assert.Equal(5000m, ledger.PrivatePortion);
        Assert.Equal(5000m, ledger.TotalAmount);

        var overview = await service.GetOverviewAsync();
        Assert.Equal(0m, overview.GovernmentAvailable);
        Assert.Equal(5000m, overview.PrivateAvailable);
        Assert.Equal(5000m, overview.CombinedAvailable);
    }

    [Fact]
    public async Task RecordReleaseAsync_UsesGovernmentFirstThenPrivateOverflow()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id);
        SeedGovernmentSnapshot(context, 70000m, 2, "OFF-2026-0006");
        var service = new BudgetManagementService(context);

        await service.RecordPrivateDonationAsync(
            new PrivateDonationRequest(
                PrivateDonationDonorType.Organization,
                "Private Donor",
                30000m,
                new DateTime(2026, 3, 27),
                "DON-002",
                "Supplemental fund",
                DonationProofType.Cash,
                null,
                null),
            admin.Id);

        var result = await service.RecordReleaseAsync(
            new BudgetReleaseRequest(
                program.Id,
                BudgetLedgerFeatureSource.AssistanceCase,
                "assistance:15",
                4,
                AssistanceReleaseKind.Goods,
                80000m,
                new DateTime(2026, 3, 27),
                "Medical assistance release"),
            admin.Id);

        Assert.True(result.IsSuccess);

        var releaseEntry = context.BudgetLedgerEntries.Single(entry => entry.EntryType == BudgetLedgerEntryType.Release);
        Assert.Equal(AssistanceReleaseKind.Goods, releaseEntry.ReleaseKind);
        Assert.Equal(70000m, releaseEntry.GovernmentPortion);
        Assert.Equal(10000m, releaseEntry.PrivatePortion);
        Assert.Equal(80000m, releaseEntry.TotalAmount);

        var overview = await service.GetOverviewAsync();
        Assert.Equal(0m, overview.GovernmentAvailable);
        Assert.Equal(20000m, overview.PrivateAvailable);
        Assert.Equal(20000m, overview.CombinedAvailable);
    }

    [Fact]
    public async Task RecordReleaseAsync_WhenCombinedBudgetIsInsufficient_ReturnsFailureWithoutReleaseLedger()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id);
        SeedGovernmentSnapshot(context, 5000m, 2, "OFF-2026-0006");
        var service = new BudgetManagementService(context);

        await service.RecordPrivateDonationAsync(
            new PrivateDonationRequest(
                PrivateDonationDonorType.Person,
                "Juan Dela Cruz",
                1000m,
                new DateTime(2026, 3, 27),
                "DON-003",
                "Cash donation",
                DonationProofType.Cash,
                null,
                null),
            admin.Id);

        var result = await service.RecordReleaseAsync(
            new BudgetReleaseRequest(
                program.Id,
                BudgetLedgerFeatureSource.AssistanceCase,
                "assistance:16",
                1,
                AssistanceReleaseKind.Cash,
                7000m,
                new DateTime(2026, 3, 27),
                "Blocked release"),
            admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("short", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(context.BudgetLedgerEntries, entry => entry.EntryType == BudgetLedgerEntryType.Release);

        var overview = await service.GetOverviewAsync();
        Assert.Equal(6000m, overview.CombinedAvailable);
    }

    [Fact]
    public async Task CreateProgramAsync_PersistsDistributionFields_AndAllowsMissingDatesWithWarning()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new BudgetManagementService(context);

        var result = await service.CreateProgramAsync(
            new AyudaProgramRequest(
                "DIST-2026",
                "Rice Distribution 2026",
                AyudaProgramType.GeneralPurpose,
                "Quarterly rice support",
                "Rice Assistance",
                AssistanceReleaseKind.Cash,
                1500m,
                "Goods details",
                null,
                null,
                null,
                null,
                null,
                100000m,
                AyudaProgramDistributionStatus.Draft),
            admin.Id);

        Assert.True(result.IsSuccess);
        Assert.Contains("date", result.Message, StringComparison.OrdinalIgnoreCase);

        var program = Assert.Single(context.AyudaPrograms);
        Assert.Equal("Rice Assistance", program.AssistanceType);
        Assert.Equal(1500m, program.UnitAmount);
        Assert.Equal("Goods details", program.ItemDescription);
        Assert.Equal(100000m, program.BudgetCap);
        Assert.Equal(AyudaProgramDistributionStatus.Draft, program.DistributionStatus);
        Assert.Null(program.StartDate);
        Assert.Null(program.EndDate);
    }

    [Fact]
    public async Task RecordReleaseAsync_EvaluatesStrictSourceDonationId_WhenProgramHasSourceDonationId()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        
        var service = new BudgetManagementService(context);

        await service.RecordPrivateDonationAsync(
            new PrivateDonationRequest(
                PrivateDonationDonorType.Person,
                "Private Donor",
                5000m,
                new DateTime(2026, 3, 27),
                "DON-100",
                "Source-based support",
                DonationProofType.Cash,
                null,
                null),
            admin.Id);

        var donation = context.PrivateDonations.First();

        var createResult = await service.CreateProgramAsync(
            new AyudaProgramRequest(
                "SOURCE-01",
                "Source Project",
                AyudaProgramType.GeneralPurpose,
                "Testing strict sources",
                "General Assistance",
                AssistanceReleaseKind.Cash,
                1500m,
                "Goods details",
                null,
                null,
                null,
                null,
                null,
                20000m,
                AyudaProgramDistributionStatus.Open,
                donation.Id,
                null),
            admin.Id);

        Assert.True(createResult.IsSuccess);
        var programId = createResult.ProgramId!.Value;

        var releaseResult1 = await service.RecordReleaseAsync(
            new BudgetReleaseRequest(
                programId,
                BudgetLedgerFeatureSource.ProjectDistribution,
                "project-claim:source:1",
                1,
                AssistanceReleaseKind.Cash,
                1500m,
                new DateTime(2026, 3, 27),
                "First claim against donation"),
            admin.Id);

        Assert.True(releaseResult1.IsSuccess);
        var ledger1 = context.BudgetLedgerEntries.First(e => e.Remarks == "First claim against donation");
        Assert.Equal(0m, ledger1.GovernmentPortion);
        Assert.Equal(1500m, ledger1.PrivatePortion);

        // Remaining should be 3500. Attempt 4000
        var releaseResult2 = await service.RecordReleaseAsync(
            new BudgetReleaseRequest(
                programId,
                BudgetLedgerFeatureSource.ProjectDistribution,
                "project-claim:source:2",
                1,
                AssistanceReleaseKind.Cash,
                4000m,
                new DateTime(2026, 3, 27),
                "Second claim against donation"),
            admin.Id);

        Assert.False(releaseResult2.IsSuccess);
        Assert.Contains("short by", releaseResult2.Message);
    }

    private static User SeedAdmin(Data.LocalDbContext context)
    {
        var user = new User
        {
            Username = "budget-admin",
            Email = "budget-admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static AyudaProgram SeedProgram(Data.LocalDbContext context, int createdByUserId)
    {
        var program = new AyudaProgram
        {
            ProgramCode = "MED-2026",
            ProgramName = "Medical Assistance 2026",
            ProgramType = AyudaProgramType.AssistanceCase,
            Description = "Shared medical support program",
            CreatedByUserId = createdByUserId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
    }

    private static void SeedGovernmentSnapshot(Data.LocalDbContext context, decimal allocatedAmount, int yearlyBudgetId, string officeCode)
    {
        context.GovernmentBudgetSnapshots.Add(new GovernmentBudgetSnapshot
        {
            OfficeCode = officeCode,
            OfficeName = "Ayuda",
            YearlyBudgetId = yearlyBudgetId,
            AllocatedAmount = allocatedAmount,
            SpentAmount = 0m,
            SourceRowId = yearlyBudgetId.ToString(),
            SyncStatus = GovernmentBudgetSyncStatus.Synced,
            SyncedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        context.SaveChanges();
    }
}

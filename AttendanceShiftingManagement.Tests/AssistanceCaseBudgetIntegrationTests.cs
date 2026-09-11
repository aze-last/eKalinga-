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
        SeedGlobalAidRequestBudget(context, admin.Id);
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
        SeedGlobalAidRequestBudget(context, admin.Id);
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

    private static User SeedAdmin(Data.LocalDbContext context)
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

    private static Household SeedHousehold(Data.LocalDbContext context)
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

    private static AyudaProgram SeedProgram(Data.LocalDbContext context, int createdByUserId)
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

    private static void SeedGovernmentSnapshot(Data.LocalDbContext context, decimal allocatedAmount)
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

    private static AssistanceCase SeedApprovedCase(Data.LocalDbContext context, int householdId, int createdByUserId, int ayudaProgramId, decimal approvedAmount)
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

    private static void SeedGlobalAidRequestBudget(Data.LocalDbContext context, int adminId)
    {
        context.AssistanceCaseBudgets.Add(new AssistanceCaseBudget
        {
            BudgetCode = "GLOBAL_AID_BUDGET",
            BudgetName = "Global Aid Budget",
            BudgetCap = 100000m,
            IsActive = true,
            CreatedByUserId = adminId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task AssistanceCaseManagementViewModel_LoadBudgetsAsync_AggregatesAllBudgetModuleRecords_AndReplacesDummyDefault()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);

        // Seed dummy default budget
        context.AssistanceCaseBudgets.Add(new AssistanceCaseBudget
        {
            BudgetCode = "GLOBAL_AID_BUDGET",
            BudgetName = "General Municipal Assistance Fund",
            IsActive = true,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        // Seed other records across budget module
        context.CashForWorkBudgets.Add(new CashForWorkBudget
        {
            BudgetCode = "CFW-2026-001",
            BudgetName = "Community Road Clearing",
            BudgetCap = 250000m,
            IsActive = true,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        context.AyudaPrograms.Add(new AyudaProgram
        {
            ProgramCode = "AYUDA-2026-001",
            ProgramName = "Senior Food Pack Distribution",
            BudgetCap = 150000m,
            IsActive = true,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        context.PrivateDonations.Add(new PrivateDonation
        {
            DonorName = "Rotary Club Foundation",
            Amount = 50000m,
            ProofReferenceNumber = "REF-DON-001",
            ReceivedByUserId = admin.Id,
            CreatedAt = DateTime.Now
        });

        context.GovernmentBudgetSnapshots.Add(new GovernmentBudgetSnapshot
        {
            OfficeCode = "MDRRMO-01",
            OfficeName = "Disaster Risk Reduction Office",
            AllocatedAmount = 500000m,
            SourceRowId = "101",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });

        context.SaveChanges();

        var vm = new ViewModels.AssistanceCaseManagementViewModel(admin, context);
        await vm.LoadBudgetsAsync();

        // Check that AvailableBudgets contains records from all sources
        Assert.Contains(vm.AvailableBudgets, b => b.BudgetCode == "CFW-2026-001" && b.Category == "Cash for Work Project");
        Assert.Contains(vm.AvailableBudgets, b => b.BudgetCode == "AYUDA-2026-001" && b.Category == "Distribution Project");
        Assert.Contains(vm.AvailableBudgets, b => b.Category == "Private Donation" && b.BudgetName == "Rotary Club Foundation");
        Assert.Contains(vm.AvailableBudgets, b => b.BudgetCode == "GOV-MDRRMO-01" && b.Category == "Government Fund");

        // The dummy General Municipal Assistance Fund must be deactivated when real budgets exist
        Assert.DoesNotContain(vm.AvailableBudgets, b => b.BudgetCode == "GLOBAL_AID_BUDGET");

        // DisplayText formatting check
        var ayudaOption = vm.AvailableBudgets.Single(b => b.BudgetCode == "AYUDA-2026-001");
        Assert.Contains("[Distribution Project]", ayudaOption.DisplayText);
        Assert.Contains("Cap: ₱150,000.00", ayudaOption.DisplayText);
    }

    [Fact]
    public async Task AssistanceCaseManagementViewModel_SubmitIntake_LinksToSelectedBudgetAndProgram()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);

        var program = new AyudaProgram
        {
            ProgramCode = "AYUDA-2026-002",
            ProgramName = "Medical Support Distribution",
            BudgetCap = 100000m,
            IsActive = true,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        context.AyudaPrograms.Add(program);
        context.SaveChanges();

        var vm = new ViewModels.AssistanceCaseManagementViewModel(admin, context);
        await vm.LoadBudgetsAsync();

        var selectedBudget = vm.AvailableBudgets.Single(b => b.BudgetCode == "AYUDA-2026-002");
        vm.SelectedIntakeBudget = selectedBudget;
        vm.IntakeCitizenName = "Juan Dela Cruz";
        vm.IntakeSubject = "Hospital Subsidy";
        vm.IntakeDescription = "Requires prescription assistance";
        vm.IntakeAmount = "2500";

        Assert.True(vm.SubmitIntakeCommand.CanExecute(null));
        vm.SubmitIntakeCommand.Execute(null);

        // Wait brief moment for async command
        for (int i = 0; i < 20; i++)
        {
            if (context.AssistanceCases.Any(c => c.Summary == "Hospital Subsidy"))
                break;
            await Task.Delay(50);
        }

        var created = context.AssistanceCases.FirstOrDefault(c => c.Summary == "Hospital Subsidy");
        Assert.NotNull(created);
        Assert.Equal("Juan Dela Cruz", created.ValidatedBeneficiaryName);
        Assert.Equal(program.Id, created.AyudaProgramId);
        Assert.Equal(selectedBudget.Id, created.AssistanceCaseBudgetId);
    }

    [Fact]
    public void ClosedRequest_DisablesAllLifecycleActionsAndDisbursement()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var vm = new ViewModels.AssistanceCaseManagementViewModel(admin, context);

        var requestItem = new ViewModels.CitizenRequestItemViewModel
        {
            Id = 1,
            TicketNumber = "AR-20260910-0001",
            CitizenName = "Pedro Penduko",
            Status = AssistanceCaseStatus.Approved
        };

        vm.SelectedRequest = requestItem;
        Assert.True(vm.CanResolve);
        Assert.True(vm.CanDisburse);
        Assert.True(vm.CanReject);
        Assert.True(vm.CanAddInternalNote);
        Assert.False(vm.IsRequestClosed);

        // Transition to Closed
        requestItem.Status = AssistanceCaseStatus.Closed;
        vm.NotifySelectedRequestStateChanged();

        Assert.False(vm.CanResolve);
        Assert.False(vm.CanDisburse);
        Assert.False(vm.CanReject);
        Assert.False(vm.CanMarkInReview);
        Assert.False(vm.CanStartProcessing);
        Assert.False(vm.CanAddInternalNote);
        Assert.True(vm.IsRequestClosed);
        Assert.Equal("Resolved & Closed", requestItem.StatusLabel);
    }
}


using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionServiceTests
{
    [Fact]
    public async Task AddBeneficiaryAsync_AddsApprovedBeneficiaryToProject_AndBlocksDuplicateMembership()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id);
        var beneficiary = SeedApprovedBeneficiary(context);

        var service = CreateService(context);

        var firstResult = await InvokeAsync(service, "AddBeneficiaryAsync", program.Id, beneficiary.StagingID, admin.Id);
        Assert.True(GetBool(firstResult, "IsSuccess"));

        var duplicateResult = await InvokeAsync(service, "AddBeneficiaryAsync", program.Id, beneficiary.StagingID, admin.Id);
        Assert.False(GetBool(duplicateResult, "IsSuccess"));

        Assert.Equal(1, GetEntityCount(context, "AttendanceShiftingManagement.Models.AyudaProjectBeneficiary"));
    }

    [Fact]
    public async Task EvaluateQualificationAsync_ReturnsQualifiedForIncludedBeneficiary_AndRejectedForNonMember()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id);
        var includedBeneficiary = SeedApprovedBeneficiary(context, 2001, "Juan Included");
        var excludedBeneficiary = SeedApprovedBeneficiary(context, 2002, "Maria Excluded");

        var service = CreateService(context);
        await InvokeAsync(service, "AddBeneficiaryAsync", program.Id, includedBeneficiary.StagingID, admin.Id);

        var includedResult = await InvokeAsync(service, "EvaluateQualificationAsync", program.Id, includedBeneficiary.StagingID);
        Assert.True(GetBool(includedResult, "IsQualified"));
        Assert.False(GetBool(includedResult, "AlreadyClaimed"));

        var excludedResult = await InvokeAsync(service, "EvaluateQualificationAsync", program.Id, excludedBeneficiary.StagingID);
        Assert.False(GetBool(excludedResult, "IsQualified"));
    }

    [Fact]
    public async Task RecordClaimAsync_CreatesOneClaimPerProject_AndBlocksDuplicateClaim()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id, unitAmount: 1500m);
        var beneficiary = SeedApprovedBeneficiary(context);
        SeedGovernmentSnapshot(context, 5000m, 2, "OFF-2026-0006");

        var service = CreateService(context);
        await InvokeAsync(service, "AddBeneficiaryAsync", program.Id, beneficiary.StagingID, admin.Id);

        var firstClaim = await InvokeAsync(service, "RecordClaimAsync", program.Id, beneficiary.StagingID, admin.Id, "ASM-BID|000001|ABC", "Initial claim");
        Assert.True(GetBool(firstClaim, "IsSuccess"));

        var duplicateClaim = await InvokeAsync(service, "RecordClaimAsync", program.Id, beneficiary.StagingID, admin.Id, "ASM-BID|000001|ABC", "Duplicate claim");
        Assert.False(GetBool(duplicateClaim, "IsSuccess"));

        Assert.Equal(1, GetEntityCount(context, "AttendanceShiftingManagement.Models.AyudaProjectClaim"));
        Assert.Equal(1, context.BudgetLedgerEntries.Count(entry => entry.EntryType == BudgetLedgerEntryType.Release));
        Assert.Contains(
            context.BudgetLedgerEntries,
            entry => entry.FeatureSource == BudgetLedgerFeatureSource.ProjectDistribution &&
                     entry.TotalAmount == 1500m &&
                     entry.RecipientCount == 1);
    }

    [Fact]
    public async Task RecordClaimAsync_WhenCombinedBudgetIsInsufficient_ReturnsFailureWithoutClaimOrLedger()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id, unitAmount: 2500m);
        var beneficiary = SeedApprovedBeneficiary(context);
        SeedGovernmentSnapshot(context, 1000m, 2, "OFF-2026-0006");

        var service = CreateService(context);
        await InvokeAsync(service, "AddBeneficiaryAsync", program.Id, beneficiary.StagingID, admin.Id);

        var claimResult = await InvokeAsync(service, "RecordClaimAsync", program.Id, beneficiary.StagingID, admin.Id, "ASM-BID|000001|ABC", "Blocked claim");
        Assert.False(GetBool(claimResult, "IsSuccess"));

        Assert.Equal(0, GetEntityCount(context, "AttendanceShiftingManagement.Models.AyudaProjectClaim"));
        Assert.DoesNotContain(context.BudgetLedgerEntries, entry => entry.EntryType == BudgetLedgerEntryType.Release);
    }

    private static object CreateService(AppDbContext context)
    {
        var serviceType = typeof(AppDbContext).Assembly.GetType("AttendanceShiftingManagement.Services.ProjectDistributionService");
        Assert.NotNull(serviceType);

        var service = Activator.CreateInstance(serviceType!, context);
        Assert.NotNull(service);
        return service!;
    }

    private static async Task<object> InvokeAsync(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var task = method!.Invoke(instance, args) as Task;
        Assert.NotNull(task);
        await task!;

        var resultProperty = task!.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        return resultProperty!.GetValue(task)!;
    }

    private static bool GetBool(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return (bool)(property!.GetValue(instance) ?? false);
    }

    private static int GetEntityCount(AppDbContext context, string entityTypeName)
    {
        var entityType = typeof(AppDbContext).Assembly.GetType(entityTypeName);
        Assert.NotNull(entityType);

        var setMethod = typeof(DbContext)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethod && method.GetParameters().Length == 0)
            .MakeGenericMethod(entityType!);

        var queryable = setMethod.Invoke(context, null) as IQueryable;
        Assert.NotNull(queryable);
        return queryable!.Cast<object>().Count();
    }

    private static User SeedAdmin(AppDbContext context)
    {
        var user = new User
        {
            Username = "distribution-admin",
            Email = "distribution-admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static AyudaProgram SeedProgram(AppDbContext context, int createdByUserId, decimal unitAmount = 1000m)
    {
        var program = new AyudaProgram
        {
            ProgramCode = $"DIST-{Guid.NewGuid():N}"[..12],
            ProgramName = "Project Distribution Program",
            ProgramType = AyudaProgramType.GeneralPurpose,
            Description = "Distribution workflow seed data",
            CreatedByUserId = createdByUserId,
            IsActive = true
        };

        var unitAmountProperty = typeof(AyudaProgram).GetProperty("UnitAmount");
        unitAmountProperty?.SetValue(program, unitAmount);

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
    }

    private static BeneficiaryStaging SeedApprovedBeneficiary(AppDbContext context, int stagingId = 1001, string fullName = "Pedro Beneficiary")
    {
        var beneficiary = new BeneficiaryStaging
        {
            StagingID = stagingId,
            BeneficiaryId = $"BEN-{stagingId:D4}",
            CivilRegistryId = $"CR-{stagingId:D4}",
            FullName = fullName,
            VerificationStatus = VerificationStatus.Approved,
            LinkedHouseholdId = 1,
            LinkedHouseholdMemberId = 1,
            ReviewedAt = DateTime.Now
        };

        context.BeneficiaryStaging.Add(beneficiary);
        context.SaveChanges();
        return beneficiary;
    }

    private static void SeedGovernmentSnapshot(AppDbContext context, decimal allocatedAmount, int yearlyBudgetId, string officeCode)
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

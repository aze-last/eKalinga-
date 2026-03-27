using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AttendanceShiftingManagement.Tests;

public sealed class StartupMigrationTests
{
    [Fact]
    public void AppAssembly_DefinesEfCoreMigrations_ForStartupMigrationMode()
    {
        var migrationTypes = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(migrationTypes);
    }

    [Fact]
    public void AppAssembly_MigrationSnapshot_ContainsBudgetSchema()
    {
        var snapshotType = typeof(AppDbContext).Assembly
            .GetTypes()
            .Single(type => !type.IsAbstract && typeof(ModelSnapshot).IsAssignableFrom(type));

        var snapshot = Activator.CreateInstance(snapshotType, nonPublic: true) as ModelSnapshot;
        Assert.NotNull(snapshot);

        var model = snapshot!.Model;

        Assert.NotNull(model.FindEntityType(typeof(AyudaProgram)));
        Assert.NotNull(model.FindEntityType(typeof(GovernmentBudgetSnapshot)));
        Assert.NotNull(model.FindEntityType(typeof(PrivateDonation)));
        Assert.NotNull(model.FindEntityType(typeof(BudgetLedgerEntry)));
        Assert.NotNull(model.FindEntityType(typeof(SystemRegistration)));
        Assert.NotNull(model.FindEntityType(typeof(BeneficiaryDigitalId)));
        Assert.NotNull(model.FindEntityType(typeof(ScannerSession)));
        Assert.Contains(model.GetEntityTypes(), entity => entity.Name == "AttendanceShiftingManagement.Models.AyudaProjectBeneficiary");
        Assert.Contains(model.GetEntityTypes(), entity => entity.Name == "AttendanceShiftingManagement.Models.AyudaProjectClaim");

        var assistanceCase = model.FindEntityType(typeof(AssistanceCase));
        Assert.NotNull(assistanceCase?.FindProperty(nameof(AssistanceCase.AyudaProgramId)));
        Assert.NotNull(assistanceCase?.FindProperty(nameof(AssistanceCase.BudgetLedgerEntryId)));
        Assert.NotNull(assistanceCase?.FindProperty(nameof(AssistanceCase.ValidatedBeneficiaryName)));
        Assert.NotNull(assistanceCase?.FindProperty(nameof(AssistanceCase.ValidatedBeneficiaryId)));
        Assert.NotNull(assistanceCase?.FindProperty(nameof(AssistanceCase.ValidatedCivilRegistryId)));
        Assert.NotNull(assistanceCase?.FindProperty(nameof(AssistanceCase.ReleaseKind)));

        var budgetLedgerEntry = model.FindEntityType(typeof(BudgetLedgerEntry));
        Assert.NotNull(budgetLedgerEntry?.FindProperty(nameof(BudgetLedgerEntry.ReleaseKind)));

        var ayudaProgram = model.FindEntityType(typeof(AyudaProgram));
        Assert.NotNull(ayudaProgram?.FindProperty("AssistanceType"));
        Assert.NotNull(ayudaProgram?.FindProperty("UnitAmount"));
        Assert.NotNull(ayudaProgram?.FindProperty("ItemDescription"));
        Assert.NotNull(ayudaProgram?.FindProperty("StartDate"));
        Assert.NotNull(ayudaProgram?.FindProperty("EndDate"));
        Assert.NotNull(ayudaProgram?.FindProperty("BudgetCap"));
        Assert.NotNull(ayudaProgram?.FindProperty("DistributionStatus"));

        var cashForWorkEvent = model.FindEntityType(typeof(CashForWorkEvent));
        Assert.NotNull(cashForWorkEvent?.FindProperty(nameof(CashForWorkEvent.AyudaProgramId)));
        Assert.NotNull(cashForWorkEvent?.FindProperty(nameof(CashForWorkEvent.BudgetLedgerEntryId)));
        Assert.NotNull(cashForWorkEvent?.FindProperty(nameof(CashForWorkEvent.ReleaseAmount)));

        var beneficiaryDigitalId = model.FindEntityType(typeof(BeneficiaryDigitalId));
        Assert.NotNull(beneficiaryDigitalId?.FindProperty(nameof(BeneficiaryDigitalId.BeneficiaryStagingId)));
        Assert.NotNull(beneficiaryDigitalId?.FindProperty(nameof(BeneficiaryDigitalId.CardNumber)));
        Assert.NotNull(beneficiaryDigitalId?.FindProperty(nameof(BeneficiaryDigitalId.QrPayload)));
        Assert.NotNull(beneficiaryDigitalId?.FindProperty(nameof(BeneficiaryDigitalId.PhotoPath)));

        var scannerSession = model.FindEntityType(typeof(ScannerSession));
        Assert.NotNull(scannerSession?.FindProperty(nameof(ScannerSession.Mode)));
        Assert.NotNull(scannerSession?.FindProperty(nameof(ScannerSession.SessionToken)));
        Assert.NotNull(scannerSession?.FindProperty(nameof(ScannerSession.PinHash)));
        Assert.NotNull(scannerSession?.FindProperty(nameof(ScannerSession.CashForWorkEventId)));
        Assert.NotNull(scannerSession?.FindProperty("AyudaProgramId"));

        var projectBeneficiary = model.GetEntityTypes()
            .Single(entity => entity.Name == "AttendanceShiftingManagement.Models.AyudaProjectBeneficiary");
        Assert.NotNull(projectBeneficiary.FindProperty("AyudaProgramId"));
        Assert.NotNull(projectBeneficiary.FindProperty("BeneficiaryStagingId"));
        Assert.NotNull(projectBeneficiary.FindProperty("CivilRegistryId"));

        var projectClaim = model.GetEntityTypes()
            .Single(entity => entity.Name == "AttendanceShiftingManagement.Models.AyudaProjectClaim");
        Assert.NotNull(projectClaim.FindProperty("AyudaProgramId"));
        Assert.NotNull(projectClaim.FindProperty("BeneficiaryStagingId"));
        Assert.NotNull(projectClaim.FindProperty("ClaimedAt"));
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class GgmsConsolidatedTransactionReleaseTests
{
    [Fact]
    public async Task AidRequestRelease_WarnsButDoesNotBlock_WhenGgmsSyncFails()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context, "aid-admin");
        var beneficiary = SeedApprovedBeneficiary(context, 1101, "Maria", "Lopez", "Santos");
        var program = SeedProgram(context, admin.Id, "AID-RELEASE", AyudaProgramType.AssistanceCase, 4000m);
        SeedGlobalAidRequestBudget(context, admin.Id);
        SeedGovernmentSnapshot(context, 10000m);
        var assistanceCase = SeedApprovedAssistanceCase(context, admin.Id, program.Id, beneficiary, 4000m);
        var fakeGgms = new FakeGgmsConsolidatedTransactionService
        {
            AssistanceCaseWarning = "remote DB unavailable"
        };
        var service = new AssistanceCaseManagementService(context, ggmsConsolidatedTransactionService: fakeGgms);

        var result = await service.ChangeStatusAsync(
            assistanceCase.Id,
            AssistanceCaseStatus.Released,
            admin.Id,
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceCaseStatus.Released, context.AssistanceCases.Single().Status);
        Assert.Contains("GGMS sync warning", result.Message, StringComparison.Ordinal);
        Assert.Single(fakeGgms.AssistanceCaseReleases);
        Assert.Equal(assistanceCase.Id, fakeGgms.AssistanceCaseReleases[0].Id);
    }

    [Fact]
    public async Task ProjectDistributionClaim_WritesGgmsConsolidatedTransaction()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context, "project-admin");
        var beneficiary = SeedApprovedBeneficiary(context, 1201, "Joel", "Cruz", null);
        var program = SeedProgram(context, admin.Id, "DIST-2026", AyudaProgramType.GeneralPurpose, 1500m);
        SeedGovernmentSnapshot(context, 5000m);
        var fakeGgms = new FakeGgmsConsolidatedTransactionService();
        var service = new ProjectDistributionService(context, ggmsConsolidatedTransactionService: fakeGgms);
        var digitalIdService = new BeneficiaryDigitalIdService(context);
        var digitalId = await digitalIdService.EnsureIssuedAsync(beneficiary.StagingID, admin.Id);

        var addResult = await service.AddBeneficiaryAsync(program.Id, beneficiary.StagingID, admin.Id);
        Assert.True(addResult.IsSuccess);

        var claimResult = await service.RecordClaimAsync(
            program.Id,
            beneficiary.StagingID,
            admin.Id,
            digitalId.QrPayload,
            "Initial claim");

        Assert.True(claimResult.IsSuccess);
        Assert.Single(fakeGgms.ProjectClaimWrites);
        Assert.Equal(program.Id, fakeGgms.ProjectClaimWrites[0].Program?.Id);
        Assert.Equal(beneficiary.BeneficiaryId, fakeGgms.ProjectClaimWrites[0].Claim.BeneficiaryId);
    }

    [Fact]
    public async Task CashForWorkRelease_WritesGgmsConsolidatedTransactions_ForEachReleasedParticipant()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context, "cfw-admin");
        var firstBeneficiary = SeedApprovedBeneficiary(context, 1301, "Ana", "Dela Cruz", "Reyes");
        var secondBeneficiary = SeedApprovedBeneficiary(context, 1302, "Paolo", "Rivera", null);
        SeedGlobalCfwBudget(context, admin.Id);
        SeedGovernmentSnapshot(context, 10000m);
        var fakeGgms = new FakeGgmsConsolidatedTransactionService();
        var service = new CashForWorkService(context, ggmsConsolidatedTransactionService: fakeGgms);

        var cashForWorkEvent = await service.CreateEventAsync(
            "Canal Clearing",
            "Sitio Uno",
            new DateTime(2026, 4, 22),
            new TimeSpan(7, 0, 0),
            new TimeSpan(11, 0, 0),
            "Morning batch",
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, firstBeneficiary.StagingID, admin.Id);
        service.AddParticipant(cashForWorkEvent.Id, secondBeneficiary.StagingID, admin.Id);

        var participantIds = context.CashForWorkParticipants
            .ToDictionary(participant => participant.BeneficiaryStagingId!.Value, participant => participant.Id);

        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantIds[firstBeneficiary.StagingID], participantIds[secondBeneficiary.StagingID]]);

        var result = await service.ReleaseEventAsync(
            cashForWorkEvent.Id,
            3000m,
            admin.Id,
            "Release batch");

        Assert.True(result.IsSuccess);
        Assert.Single(fakeGgms.CashForWorkReleases);
        Assert.Equal(2, fakeGgms.CashForWorkReleases[0].ReleasedParticipantIds.Count);
        Assert.Equal(3000m, fakeGgms.CashForWorkReleases[0].TotalAmount);
    }

    private static User SeedAdmin(AppDbContext context, string username)
    {
        var user = new User
        {
            Username = username,
            Email = $"{username}@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static BeneficiaryStaging SeedApprovedBeneficiary(
        AppDbContext context,
        int stagingId,
        string firstName,
        string lastName,
        string? middleName)
    {
        var beneficiary = new BeneficiaryStaging
        {
            StagingID = stagingId,
            BeneficiaryId = $"BEN-{stagingId:D4}",
            CivilRegistryId = $"CR-{stagingId:D4}",
            FirstName = firstName,
            MiddleName = middleName,
            LastName = lastName,
            FullName = string.Join(" ", new[] { firstName, middleName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            VerificationStatus = VerificationStatus.Approved,
            ReviewedAt = DateTime.Now
        };

        context.BeneficiaryStaging.Add(beneficiary);
        context.SaveChanges();
        return beneficiary;
    }

    private static AyudaProgram SeedProgram(AppDbContext context, int createdByUserId, string programCode, AyudaProgramType programType, decimal unitAmount)
    {
        var program = new AyudaProgram
        {
            ProgramCode = programCode,
            ProgramName = $"{programCode} Program",
            ProgramType = programType,
            AssistanceType = "Financial Assistance",
            DistributionStatus = AyudaProgramDistributionStatus.Open,
            UnitAmount = unitAmount,
            CreatedByUserId = createdByUserId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
    }

    private static AssistanceCase SeedApprovedAssistanceCase(
        AppDbContext context,
        int createdByUserId,
        int ayudaProgramId,
        BeneficiaryStaging beneficiary,
        decimal approvedAmount)
    {
        var assistanceCase = new AssistanceCase
        {
            CaseNumber = "AR-20260422-0001",
            ValidatedBeneficiaryName = beneficiary.FullName,
            ValidatedBeneficiaryId = beneficiary.BeneficiaryId,
            ValidatedCivilRegistryId = beneficiary.CivilRegistryId,
            AssistanceType = "Medical Assistance",
            Priority = AssistanceCasePriority.High,
            ReleaseKind = AssistanceReleaseKind.Cash,
            Status = AssistanceCaseStatus.Approved,
            RequestedAmount = approvedAmount,
            ApprovedAmount = approvedAmount,
            RequestedOn = new DateTime(2026, 4, 22),
            Summary = "Approved assistance release",
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

    private static void SeedGovernmentSnapshot(AppDbContext context, decimal allocatedAmount)
    {
        context.GovernmentBudgetSnapshots.Add(new GovernmentBudgetSnapshot
        {
            OfficeCode = "OFF-2026-0006",
            OfficeName = "eKalinga+",
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

    private static void SeedGlobalAidRequestBudget(AppDbContext context, int adminId)
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

    private static void SeedGlobalCfwBudget(AppDbContext context, int adminId)
    {
        context.CashForWorkBudgets.Add(new CashForWorkBudget
        {
            BudgetCode = "GLOBAL_CFW_BUDGET",
            BudgetName = "Global CFW Budget",
            BudgetCap = 100000m,
            IsActive = true,
            CreatedByUserId = adminId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        context.SaveChanges();
    }

    private sealed class FakeGgmsConsolidatedTransactionService : IGgmsConsolidatedTransactionService
    {
        public string? AssistanceCaseWarning { get; set; }
        public List<AssistanceCase> AssistanceCaseReleases { get; } = [];
        public List<(AyudaProgram? Program, AyudaProjectClaim Claim)> ProjectClaimWrites { get; } = [];
        public List<CashForWorkReleaseCall> CashForWorkReleases { get; } = [];

        public Task<string?> TryWriteAssistanceCaseReleaseAsync(AppDbContext context, AssistanceCase assistanceCase)
        {
            AssistanceCaseReleases.Add(assistanceCase);
            return Task.FromResult(AssistanceCaseWarning);
        }

        public Task<string?> TryWriteProjectDistributionClaimAsync(AppDbContext context, AyudaProgram? program, AyudaProjectClaim claim)
        {
            ProjectClaimWrites.Add((program, claim));
            return Task.FromResult<string?>(null);
        }

        public Task<string?> TryWriteCashForWorkReleaseAsync(
            AppDbContext context,
            CashForWorkEvent cashForWorkEvent,
            IReadOnlyCollection<CashForWorkParticipant> participants,
            IReadOnlyCollection<int> releasedParticipantIds,
            decimal totalAmount)
        {
            CashForWorkReleases.Add(new CashForWorkReleaseCall(
                cashForWorkEvent,
                participants.ToList(),
                releasedParticipantIds.ToList(),
                totalAmount));
            return Task.FromResult<string?>(null);
        }

        public Task<List<GgmsConsolidatedTransaction>> LoadTransactionsAsync(string? projectNameFilter = null)
        {
            return Task.FromResult(new List<GgmsConsolidatedTransaction>());
        }
    }

    private sealed record CashForWorkReleaseCall(
        CashForWorkEvent Event,
        IReadOnlyList<CashForWorkParticipant> Participants,
        IReadOnlyList<int> ReleasedParticipantIds,
        decimal TotalAmount);
}

using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class GgmsProjectSyncServiceTests
{
    [Fact]
    public void BuildProjectDetailsQuery_MatchesLiveGgmsSchema()
    {
        // Act
        var query = GgmsProjectSyncService.BuildProjectDetailsQuery(null);

        // Assert — project_details is filtered by office_code (never the numeric office id)
        Assert.Contains("FROM `project_details`", query, StringComparison.Ordinal);
        Assert.Contains("WHERE details.`office_code` = @officeCode", query, StringComparison.Ordinal);
        Assert.DoesNotContain("office_id", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JOIN", query, StringComparison.OrdinalIgnoreCase);

        // Columns confirmed against the live GGMS DESCRIBE of project_details:
        Assert.Contains("details.`project_details_id`", query, StringComparison.Ordinal);
        Assert.Contains("details.`yearly_budget_id`", query, StringComparison.Ordinal);
        Assert.Contains("details.`project`", query, StringComparison.Ordinal);
        Assert.Contains("details.`description`", query, StringComparison.Ordinal);
        Assert.Contains("details.`system_name`", query, StringComparison.Ordinal);
        Assert.Contains("details.`total_budget`", query, StringComparison.Ordinal);
        Assert.Contains("details.`status`", query, StringComparison.Ordinal);
        Assert.Contains("details.`voucher_code`", query, StringComparison.Ordinal);
        Assert.Contains("details.`create_at`", query, StringComparison.Ordinal);
        Assert.Contains("details.`updated_at`", query, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProjectDetailsQuery_UsesConfiguredTableName_WhenProvided()
    {
        var query = GgmsProjectSyncService.BuildProjectDetailsQuery(" custom_project_details ");

        Assert.Contains("`custom_project_details`", query, StringComparison.Ordinal);
        Assert.DoesNotContain("`project_details`", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOverviewAsync_SubtractsActiveGgmsProjectEarmarks()
    {
        using var context = TestDbContextFactory.CreateContext();
        SeedGgmsProject(context, "OPP-2026-0006", 3000m);
        SeedGgmsProject(context, "OPP-2026-0021", 22234m, status: "active");
        SeedGgmsProject(context, "OPP-2026-0099", 5000m, status: "archived"); // must not earmark
        context.GovernmentBudgetSnapshots.Add(new GovernmentBudgetSnapshot
        {
            OfficeCode = "OFF-2026-0006",
            OfficeName = "Ayuda",
            YearlyBudgetId = 2,
            AllocatedAmount = 70000m,
            SpentAmount = 0m,
            SyncStatus = GovernmentBudgetSyncStatus.Synced,
            SyncedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        context.SaveChanges();

        var service = new BudgetManagementService(context);
        var overview = await service.GetOverviewAsync();

        // 70,000 - (3,000 + 22,234 active earmarks) = 44,766; archived project excluded.
        Assert.Equal(25234m, overview.GovernmentProjectEarmarkTotal);
        Assert.Equal(44766m, overview.GovernmentAvailable);
    }

    [Fact]
    public async Task CreateProgramAsync_LinkedToGgmsProject_DefaultsCapToEnvelopeAndMarksLinked()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var ggmsProject = SeedGgmsProject(context, "OPP-2026-0006", 6007m);

        var service = new BudgetManagementService(context);
        var result = await service.CreateProgramAsync(
            BuildProgramRequest("AYD-GGMS-1", sourceProjectDetailsId: "OPP-2026-0006"),
            admin.Id);

        Assert.True(result.IsSuccess, result.Message);

        var program = Assert.Single(context.AyudaPrograms);
        Assert.Equal("OPP-2026-0006", program.SourceProjectDetailsId);
        Assert.Equal(6007m, program.BudgetCap);
        Assert.True(ggmsProject.IsLinked);
    }

    [Fact]
    public async Task CreateProgramAsync_LinkedToGgmsProject_RejectsCapAboveEnvelope()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        SeedGgmsProject(context, "OPP-2026-0010", 400m);

        var service = new BudgetManagementService(context);
        var result = await service.CreateProgramAsync(
            BuildProgramRequest("AYD-GGMS-2", sourceProjectDetailsId: "OPP-2026-0010", budgetCap: 999m),
            admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot exceed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProgramAsync_LinkedToGgmsProject_RejectsSecondLinkToSameProject()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        SeedGgmsProject(context, "OPP-2026-0021", 22234m);

        var service = new BudgetManagementService(context);
        var first = await service.CreateProgramAsync(
            BuildProgramRequest("AYD-GGMS-3", sourceProjectDetailsId: "OPP-2026-0021"),
            admin.Id);
        Assert.True(first.IsSuccess, first.Message);

        var second = await service.CreateProgramAsync(
            BuildProgramRequest("AYD-GGMS-4", sourceProjectDetailsId: "OPP-2026-0021"),
            admin.Id);

        Assert.False(second.IsSuccess);
        Assert.Contains("already linked", second.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProgramAsync_LinkedToUnknownGgmsProject_FailsWithSyncHint()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);

        var service = new BudgetManagementService(context);
        var result = await service.CreateProgramAsync(
            BuildProgramRequest("AYD-GGMS-5", sourceProjectDetailsId: "OPP-2026-9999"),
            admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Sync GGMS", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AyudaProgramRequest BuildProgramRequest(
        string programCode,
        string? sourceProjectDetailsId = null,
        decimal? budgetCap = null)
    {
        return new AyudaProgramRequest(
            programCode,
            $"Program {programCode}",
            AyudaProgramType.GeneralPurpose,
            "GGMS-linked distribution",
            "Cash Aid",
            AssistanceReleaseKind.Cash,
            500m,
            null,
            null,
            null,
            null,
            DateTime.Today,
            DateTime.Today.AddMonths(1),
            budgetCap,
            AyudaProgramDistributionStatus.Draft,
            SourceDonationId: null,
            SourceGGMSBudgetId: null,
            SourceProjectDetailsId: sourceProjectDetailsId);
    }

    private static GgmsProjectCache SeedGgmsProject(
        Data.LocalDbContext context,
        string projectDetailsId,
        decimal totalBudget,
        string status = "active")
    {
        var cache = new GgmsProjectCache
        {
            ProjectDetailsId = projectDetailsId,
            YearlyBudgetId = 2,
            OfficeCode = "OFF-2026-0006",
            ProjectName = $"GGMS Project {projectDetailsId}",
            TotalBudget = totalBudget,
            Status = status,
            CachedAt = DateTime.Now
        };

        context.GgmsProjectCache.Add(cache);
        context.SaveChanges();
        return cache;
    }

    private static User SeedAdmin(Data.LocalDbContext context)
    {
        var user = new User
        {
            Username = "ggms-project-admin",
            Email = "ggms-project-admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }
}

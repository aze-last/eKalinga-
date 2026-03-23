using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryAssistanceLedgerServiceTests
{
    [Fact]
    public async Task RecordManualEntryAsync_WithoutRemarks_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var staging = SeedStagedBeneficiary(context, "CRS-3001", "BEN-3001");
        var service = new BeneficiaryAssistanceLedgerService(context);

        var result = await service.RecordManualEntryAsync(
            new AssistanceLedgerManualEntryRequest(
                staging.StagingID,
                8500m,
                new DateTime(2026, 3, 23),
                null),
            admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Empty(context.BeneficiaryAssistanceLedgerEntries);
    }

    [Fact]
    public async Task RecordManualEntryAsync_AddsLedgerEntry_AndWarningSummaryReflectsThreshold()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var staging = SeedStagedBeneficiary(context, "CRS-3002", "BEN-3002");
        var service = new BeneficiaryAssistanceLedgerService(context);

        var result = await service.RecordManualEntryAsync(
            new AssistanceLedgerManualEntryRequest(
                staging.StagingID,
                12500m,
                new DateTime(2026, 3, 23),
                "Recorded prior ayuda from legacy paper records."),
            admin.Id);

        Assert.True(result.IsSuccess);

        var entry = Assert.Single(context.BeneficiaryAssistanceLedgerEntries);
        Assert.Equal(staging.CivilRegistryId, entry.CivilRegistryId);
        Assert.Equal(staging.BeneficiaryId, entry.BeneficiaryId);
        Assert.Equal(BeneficiaryAssistanceSourceModule.ManualHistory, entry.SourceModule);

        var summary = await service.GetWarningSummaryAsync(staging.CivilRegistryId, staging.BeneficiaryId, 10000m);
        Assert.Equal(12500m, summary.TotalAmountReceived);
        Assert.True(summary.IsAboveThreshold);
        Assert.Equal(1, summary.EntryCount);
    }

    [Fact]
    public async Task GetWarningSummaryAsync_UsesBeneficiaryIdFallback_WhenCivilRegistryIdIsMissing()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var staging = SeedStagedBeneficiary(context, null, "BEN-4001");
        var service = new BeneficiaryAssistanceLedgerService(context);

        var result = await service.RecordManualEntryAsync(
            new AssistanceLedgerManualEntryRequest(
                staging.StagingID,
                6400m,
                new DateTime(2026, 3, 20),
                "Legacy social pension release."),
            admin.Id);

        Assert.True(result.IsSuccess);

        var summary = await service.GetWarningSummaryAsync(null, "BEN-4001", 10000m);
        Assert.Equal(6400m, summary.TotalAmountReceived);
        Assert.False(summary.IsAboveThreshold);
        Assert.Equal("BEN-4001", summary.BeneficiaryId);
    }

    private static User SeedAdmin(AppDbContext context)
    {
        var user = new User
        {
            Username = "ledger-admin",
            Email = "ledger-admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static BeneficiaryStaging SeedStagedBeneficiary(AppDbContext context, string? civilRegistryId, string? beneficiaryId)
    {
        var row = new BeneficiaryStaging
        {
            BeneficiaryId = beneficiaryId,
            CivilRegistryId = civilRegistryId,
            FullName = "Maria Santos",
            FirstName = "Maria",
            LastName = "Santos",
            Address = "Purok 1, Barangay Centro",
            VerificationStatus = VerificationStatus.Verified,
            ImportedAt = new DateTime(2026, 3, 23, 8, 0, 0)
        };

        context.BeneficiaryStaging.Add(row);
        context.SaveChanges();
        return row;
    }
}

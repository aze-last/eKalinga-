using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryDigitalIdServiceTests
{
    [Fact]
    public async Task LookupByQrPayloadAsync_ReturnsApprovedBeneficiaryAndAllReleaseHistory_WithoutHouseholdLink()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var stagingRow = SeedApprovedStaging(context);
        var service = new BeneficiaryDigitalIdService(context);

        var digitalId = await service.EnsureIssuedAsync(stagingRow.StagingID, admin.Id);

        context.BeneficiaryAssistanceLedgerEntries.AddRange(
            new BeneficiaryAssistanceLedgerEntry
            {
                CivilRegistryId = stagingRow.CivilRegistryId,
                BeneficiaryId = stagingRow.BeneficiaryId,
                SourceModule = BeneficiaryAssistanceSourceModule.AssistanceCase,
                SourceRecordId = "aid:1",
                ReleaseDate = new DateTime(2026, 3, 20),
                Amount = 1200m,
                Remarks = "Food assistance",
                RecordedByUserId = admin.Id
            },
            new BeneficiaryAssistanceLedgerEntry
            {
                CivilRegistryId = stagingRow.CivilRegistryId,
                BeneficiaryId = stagingRow.BeneficiaryId,
                SourceModule = BeneficiaryAssistanceSourceModule.CashForWork,
                SourceRecordId = "c4w:1",
                ReleaseDate = new DateTime(2026, 3, 21),
                Amount = 800m,
                Remarks = "Cash-for-work payout",
                RecordedByUserId = admin.Id
            });
        await context.SaveChangesAsync();

        var lookup = await service.LookupByQrPayloadAsync(digitalId.QrPayload);

        Assert.NotNull(lookup);
        Assert.Equal(stagingRow.StagingID, lookup!.BeneficiaryStagingId);
        Assert.Null(typeof(BeneficiaryDigitalIdLookupResult).GetProperty(nameof(BeneficiaryDigitalIdLookupResult.HouseholdId))!.GetValue(lookup));
        Assert.Null(typeof(BeneficiaryDigitalIdLookupResult).GetProperty(nameof(BeneficiaryDigitalIdLookupResult.HouseholdMemberId))!.GetValue(lookup));
        Assert.Equal("Elena Rivera", lookup.FullName);
        Assert.Equal(2, lookup.ReleaseHistory.Count);
        Assert.Contains(lookup.ReleaseHistory, entry => entry.SourceModule == BeneficiaryAssistanceSourceModule.AssistanceCase);
        Assert.Contains(lookup.ReleaseHistory, entry => entry.SourceModule == BeneficiaryAssistanceSourceModule.CashForWork);
    }

    private static User SeedAdmin(Data.AppDbContext context)
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

    private static BeneficiaryStaging SeedApprovedStaging(Data.AppDbContext context)
    {
        var row = new BeneficiaryStaging
        {
            BeneficiaryId = "BEN-1",
            CivilRegistryId = "CRS-1",
            FirstName = "Elena",
            LastName = "Rivera",
            FullName = "Elena Rivera",
            VerificationStatus = VerificationStatus.Approved,
            ImportedAt = DateTime.Now
        };

        context.BeneficiaryStaging.Add(row);
        context.SaveChanges();
        return row;
    }
}

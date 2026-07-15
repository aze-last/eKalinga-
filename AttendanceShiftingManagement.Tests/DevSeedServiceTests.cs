using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AttendanceShiftingManagement.Tests
{
    public class DevSeedServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DbContextOptions<LocalDbContext> _options;

        public DevSeedServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_dev_seed_{Guid.NewGuid():N}.db");
            _options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Fact]
        public async Task TestDevSeedService_LinksExistingStagingRows_AndSeedsPriorClaim()
        {
            using (var context = new LocalDbContext(_options))
            {
                await context.Database.EnsureCreatedAsync();

                // Seed the 25 approved mock staging rows with UUIDs (not prefixed with "MOCK-")
                // to simulate the state described by the user.
                var mockHouseholds = new[]
                {
                    (Surname: "Mockson", Given: new[] { "Aaron Luis", "Ethan Cole", "Ian Paolo", "Marco Dean", "Quincy Ray", "Ulrich Jace" }),
                    (Surname: "Testa",   Given: new[] { "Bianca Mae", "Faith Anne", "Jasmine Lee", "Nina Grace", "Rhea Sol", "Vina Claire" }),
                    (Surname: "Sample",  Given: new[] { "Carlo Rey", "Gabriel Nilo", "Kevin Noel", "Oscar Jude", "Simon Troy", "Warren Kyle" }),
                    (Surname: "Demo",    Given: new[] { "Diana Joy", "Hazel Rose", "Lara Kim", "Paula Ivy", "Tessa May", "Xyra Jane" }),
                    (Surname: "regidor", Given: new[] { "Bien Josef G" })
                };

                foreach (var family in mockHouseholds)
                {
                    foreach (var given in family.Given)
                    {
                        var staging = new BeneficiaryStaging
                        {
                            BeneficiaryId = Guid.NewGuid().ToString(), // UUID, like the user's data
                            CivilRegistryId = $"CRN-{Guid.NewGuid():N}",
                            LastName = family.Surname,
                            FirstName = given,
                            FullName = $"{given} {family.Surname}",
                            Sex = "Male",
                            Address = "Purok 1",
                            VerificationStatus = VerificationStatus.Pending, // Initially pending
                            LinkedHouseholdId = null, // Empty links
                            LinkedHouseholdMemberId = null
                        };
                        context.BeneficiaryStaging.Add(staging);
                    }
                }
                await context.SaveChangesAsync();

                // Run the seeder with the test context
                var seeder = new DevSeedService(context);
                var result = await seeder.SeedMockHouseholdsAsync(1);

                Assert.False(result.AlreadySeeded);
                Assert.Equal(5, result.HouseholdsCreated);
                Assert.Equal(25, result.BeneficiariesCreated);

                // Verify households are seeded
                var households = await context.Households.ToListAsync();
                Assert.Equal(5, households.Count);
                Assert.Contains(households, h => h.HouseholdCode == "MOCK-HH-01" && h.HeadName == "Aaron Luis Mockson");

                // Verify household members are seeded
                var members = await context.HouseholdMembers.ToListAsync();
                Assert.Equal(25, members.Count);

                // Verify all staging rows are linked to household and member
                var stagingRows = await context.BeneficiaryStaging.ToListAsync();
                Assert.Equal(25, stagingRows.Count);
                Assert.All(stagingRows, s =>
                {
                    Assert.NotNull(s.LinkedHouseholdId);
                    Assert.NotNull(s.LinkedHouseholdMemberId);
                    Assert.Equal(VerificationStatus.Approved, s.VerificationStatus);
                });

                // Verify digital IDs are linked
                var digitalIds = await context.BeneficiaryDigitalIds.ToListAsync();
                Assert.Equal(25, digitalIds.Count);
                Assert.All(digitalIds, d =>
                {
                    Assert.NotNull(d.HouseholdId);
                    Assert.NotNull(d.HouseholdMemberId);
                    Assert.True(d.IsActive);
                });

                // Verify mock program was seeded
                var mockProgram = await context.AyudaPrograms.FirstOrDefaultAsync(p => p.ProgramCode == "MOCK-DIST-01");
                Assert.NotNull(mockProgram);
                Assert.Equal("Medical Assistance", mockProgram.AssistanceType);

                // Verify prior claim was seeded for Ethan Cole Mockson
                var ethanStaging = stagingRows.First(s => s.LastName == "Mockson" && s.FirstName == "Ethan Cole");
                var claim = await context.AyudaProjectClaims
                    .FirstOrDefaultAsync(c => c.AyudaProgramId == mockProgram.Id && c.BeneficiaryStagingId == ethanStaging.StagingID);
                Assert.NotNull(claim);
                Assert.Equal(ethanStaging.LinkedHouseholdId, claim.HouseholdId);
                Assert.Equal(ethanStaging.LinkedHouseholdMemberId, claim.HouseholdMemberId);
                Assert.Equal("Ethan Cole Mockson", claim.FullName);
            }
        }
    }
}

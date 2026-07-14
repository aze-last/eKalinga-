using System.Security.Cryptography;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Additive, idempotent test-data seeder. Inserts a fixed set of mock households, their
    /// members, approved staged beneficiaries and scannable digital IDs so the Distribution
    /// household-verification flow can be validated end-to-end.
    ///
    /// Safety: NEVER deletes rows. Re-running is a no-op once the mock set exists (matched by the
    /// "MOCK-" BeneficiaryId prefix), so it is safe to trigger repeatedly from a dev button.
    /// </summary>
    public sealed class DevSeedService
    {
        // BeneficiaryId prefix used to detect (and never duplicate) previously seeded mock rows.
        private const string MockIdPrefix = "MOCK-";

        // 4 families of 6 (matches docs/mock-beneficiaries-added.txt) + the developer as a solo household.
        private static readonly (string Surname, string Code, string[] Given)[] MockHouseholds =
        {
            ("Mockson", "MOCK-HH-01", new[] { "Aaron Luis", "Ethan Cole", "Ian Paolo", "Marco Dean", "Quincy Ray", "Ulrich Jace" }),
            ("Testa",   "MOCK-HH-02", new[] { "Bianca Mae", "Faith Anne", "Jasmine Lee", "Nina Grace", "Rhea Sol", "Vina Claire" }),
            ("Sample",  "MOCK-HH-03", new[] { "Carlo Rey", "Gabriel Nilo", "Kevin Noel", "Oscar Jude", "Simon Troy", "Warren Kyle" }),
            ("Demo",    "MOCK-HH-04", new[] { "Diana Joy", "Hazel Rose", "Lara Kim", "Paula Ivy", "Tessa May", "Xyra Jane" }),
            ("Regidor", "MOCK-HH-05", new[] { "Bien Josef G" }),
        };

        // Relationship labels cycled through each household (first member is always the head).
        private static readonly string[] Relationships = { "Head", "Spouse", "Son", "Daughter", "Son", "Daughter" };

        public async Task<DevSeedResult> SeedMockHouseholdsAsync(int issuedByUserId)
        {
            await using var context = new LocalDbContext();

            var alreadySeeded = await context.BeneficiaryStaging
                .AsNoTracking()
                .AnyAsync(row => row.BeneficiaryId != null && row.BeneficiaryId.StartsWith(MockIdPrefix));

            if (alreadySeeded)
            {
                return new DevSeedResult(true, 0, 0, "Mock households already present. Nothing to seed.");
            }

            var householdsCreated = 0;
            var beneficiariesCreated = 0;
            var globalIndex = 0;

            foreach (var family in MockHouseholds)
            {
                var headDisplayName = $"{family.Given[0]} {family.Surname}";

                var household = new Household
                {
                    HouseholdCode = family.Code,
                    HeadName = headDisplayName,
                    AddressLine = $"{family.Surname} Residence, Mock Purok",
                    Purok = "Mock Purok",
                    ContactNumber = "0900-000-0000",
                    Status = HouseholdStatus.Active
                };
                context.Households.Add(household);
                await context.SaveChangesAsync();
                householdsCreated++;

                for (var i = 0; i < family.Given.Length; i++)
                {
                    globalIndex++;
                    var given = family.Given[i];
                    var displayName = $"{given} {family.Surname}";

                    var member = new HouseholdMember
                    {
                        HouseholdId = household.Id,
                        FullName = displayName,
                        RelationshipToHead = Relationships[i % Relationships.Length],
                        Occupation = "N/A",
                        IsCashForWorkEligible = true
                    };
                    context.HouseholdMembers.Add(member);
                    await context.SaveChangesAsync();

                    var staging = new BeneficiaryStaging
                    {
                        BeneficiaryId = $"{MockIdPrefix}{globalIndex:D4}",
                        CivilRegistryId = $"MOCK-CRN-{globalIndex:D4}",
                        LastName = family.Surname,
                        FirstName = given,
                        FullName = displayName,
                        Sex = i % 2 == 0 ? "Male" : "Female",
                        Address = household.AddressLine,
                        VerificationStatus = VerificationStatus.Approved,
                        LinkedHouseholdId = household.Id,
                        LinkedHouseholdMemberId = member.Id
                    };
                    context.BeneficiaryStaging.Add(staging);
                    await context.SaveChangesAsync();
                    beneficiariesCreated++;

                    var digitalId = new BeneficiaryDigitalId
                    {
                        BeneficiaryStagingId = staging.StagingID,
                        HouseholdId = household.Id,
                        HouseholdMemberId = member.Id,
                        CardNumber = $"BID-{staging.StagingID:D6}",
                        QrPayload = BuildQrPayload(staging.StagingID),
                        IssuedByUserId = issuedByUserId,
                        IssuedAt = DateTime.Now,
                        IsActive = true
                    };
                    context.BeneficiaryDigitalIds.Add(digitalId);
                    await context.SaveChangesAsync();
                }
            }

            return new DevSeedResult(
                false,
                householdsCreated,
                beneficiariesCreated,
                $"Seeded {householdsCreated} household(s) and {beneficiariesCreated} beneficiary profile(s). " +
                "Enroll them into a project via ADD BENEFICIARIES to test releases.");
        }

        // Mirrors BeneficiaryDigitalIdService.BuildQrPayload so seeded QR codes resolve identically.
        private static string BuildQrPayload(int stagingId)
        {
            var randomSuffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
            return $"ASMBID{stagingId:D6}{randomSuffix}";
        }
    }

    public sealed record DevSeedResult(bool AlreadySeeded, int HouseholdsCreated, int BeneficiariesCreated, string Message);
}

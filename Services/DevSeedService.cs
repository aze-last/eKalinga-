using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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
        private readonly LocalDbContext? _contextOverride;

        public DevSeedService() { }

        public DevSeedService(LocalDbContext contextOverride)
        {
            _contextOverride = contextOverride;
        }

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
            var context = _contextOverride ?? new LocalDbContext();
            try
            {
                // Resolve MySQL Connection for loading ResidentsId
                var residentsMap = new Dictionary<string, (long ResidentsId, string BeneficiaryId)>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var activePreset = ConnectionSettingsService.Load().GetPreset("Local");
                    if (!string.IsNullOrWhiteSpace(activePreset.Server))
                    {
                        var connString = ConnectionSettingsService.BuildConnectionString(activePreset);
                        await using var mySqlConnection = new MySqlConnection(connString);
                        await mySqlConnection.OpenAsync();

                        var query = "SELECT residents_id, beneficiary_id, last_name, first_name FROM val_beneficiaries";
                        await using var cmd = new MySqlCommand(query, mySqlConnection);
                        await using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var resId = reader.GetInt64(0);
                            var benId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            var lastName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                            var firstName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                            residentsMap[$"{firstName.Trim()}|{lastName.Trim()}"] = (resId, benId);
                        }
                    }
                }
                catch
                {
                    // Fallback to empty map if MySQL is offline or not configured during tests/builds
                }

                var householdsCreated = 0;
                var beneficiariesCreated = 0;
                var globalIndex = 0;

                foreach (var family in MockHouseholds)
                {
                    var headDisplayName = $"{family.Given[0]} {family.Surname}";

                    var household = await context.Households
                        .FirstOrDefaultAsync(h => h.HouseholdCode == family.Code);

                    if (household == null)
                    {
                        household = new Household
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
                    }

                    for (var i = 0; i < family.Given.Length; i++)
                    {
                        globalIndex++;
                        var given = family.Given[i];
                        var displayName = $"{given} {family.Surname}";

                        var member = await context.HouseholdMembers
                            .FirstOrDefaultAsync(m => m.HouseholdId == household.Id && m.FullName == displayName);

                        if (member == null)
                        {
                            member = new HouseholdMember
                            {
                                HouseholdId = household.Id,
                                FullName = displayName,
                                RelationshipToHead = Relationships[i % Relationships.Length],
                                Occupation = "N/A",
                                IsCashForWorkEligible = true
                            };
                            context.HouseholdMembers.Add(member);
                            await context.SaveChangesAsync();
                        }

                        // Look for existing staging record by FirstName + LastName (case-insensitive)
                        var staging = await context.BeneficiaryStaging
                            .FirstOrDefaultAsync(s => s.LastName.ToLower() == family.Surname.ToLower() && s.FirstName.ToLower() == given.ToLower());

                        // Resolve ResidentsId and BeneficiaryId from MySQL if available
                        var key = $"{given.Trim()}|{family.Surname.Trim()}";
                        long? resolvedResidentsId = null;
                        string? resolvedBeneficiaryId = null;
                        if (residentsMap.TryGetValue(key, out var mapping))
                        {
                            resolvedResidentsId = mapping.ResidentsId;
                            resolvedBeneficiaryId = mapping.BeneficiaryId;
                        }

                        if (staging == null)
                        {
                            staging = new BeneficiaryStaging
                            {
                                ResidentsId = resolvedResidentsId,
                                BeneficiaryId = resolvedBeneficiaryId ?? $"{MockIdPrefix}{globalIndex:D4}",
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
                        }
                        else
                        {
                            // Update existing unlinked/partially linked record
                            if (staging.LinkedHouseholdId != household.Id || staging.LinkedHouseholdMemberId != member.Id || staging.ResidentsId != resolvedResidentsId || staging.BeneficiaryId != resolvedBeneficiaryId)
                            {
                                staging.LinkedHouseholdId = household.Id;
                                staging.LinkedHouseholdMemberId = member.Id;
                                staging.ResidentsId = resolvedResidentsId;
                                if (!string.IsNullOrEmpty(resolvedBeneficiaryId))
                                {
                                    staging.BeneficiaryId = resolvedBeneficiaryId;
                                }
                                staging.VerificationStatus = VerificationStatus.Approved;
                                context.BeneficiaryStaging.Update(staging);
                                await context.SaveChangesAsync();
                                beneficiariesCreated++;
                            }
                        }

                        // Check digital ID
                        var digitalId = await context.BeneficiaryDigitalIds
                            .FirstOrDefaultAsync(d => d.BeneficiaryStagingId == staging.StagingID);

                        if (digitalId == null)
                        {
                            digitalId = new BeneficiaryDigitalId
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
                        else
                        {
                            if (digitalId.HouseholdId != household.Id || digitalId.HouseholdMemberId != member.Id)
                            {
                                digitalId.HouseholdId = household.Id;
                                digitalId.HouseholdMemberId = member.Id;
                                context.BeneficiaryDigitalIds.Update(digitalId);
                                await context.SaveChangesAsync();
                            }
                        }
                    }
                }

                // Seed mock program and prior claim (Ethan Cole Mockson) for testing the household override/warning flow.
                var mockProgram = await context.AyudaPrograms.FirstOrDefaultAsync(p => p.ProgramCode == "MOCK-DIST-01");
                if (mockProgram == null)
                {
                    mockProgram = new AyudaProgram
                    {
                        ProgramCode = "MOCK-DIST-01",
                        ProgramName = "Mock Medical Aid",
                        ProgramType = AyudaProgramType.GeneralPurpose,
                        AssistanceType = "Medical Assistance",
                        ItemDescription = "Medical Assistance Payout",
                        ReleaseKind = AssistanceReleaseKind.Cash,
                        BudgetCap = 100000,
                        UnitAmount = 5000,
                        DistributionStatus = AyudaProgramDistributionStatus.Open,
                        StartDate = DateTime.Now.AddDays(-10),
                        EndDate = DateTime.Now.AddDays(30),
                        CreatedByUserId = issuedByUserId
                    };
                    context.AyudaPrograms.Add(mockProgram);
                    await context.SaveChangesAsync();
                }

                // Find Ethan Cole Mockson in staging to attach the claim
                var ethanStaging = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(s => s.LastName == "Mockson" && s.FirstName == "Ethan Cole");

                if (ethanStaging != null)
                {
                    var hasClaim = await context.AyudaProjectClaims
                        .AnyAsync(c => c.AyudaProgramId == mockProgram.Id && c.BeneficiaryStagingId == ethanStaging.StagingID);

                    if (!hasClaim)
                    {
                        var claim = new AyudaProjectClaim
                        {
                            AyudaProgramId = mockProgram.Id,
                            BeneficiaryStagingId = ethanStaging.StagingID,
                            HouseholdId = ethanStaging.LinkedHouseholdId,
                            HouseholdMemberId = ethanStaging.LinkedHouseholdMemberId,
                            BeneficiaryId = ethanStaging.BeneficiaryId,
                            CivilRegistryId = ethanStaging.CivilRegistryId,
                            FullName = ethanStaging.FullName ?? "Ethan Cole Mockson",
                            AssistanceTypeSnapshot = mockProgram.AssistanceType,
                            ItemDescriptionSnapshot = mockProgram.ItemDescription,
                            ClaimedByUserId = issuedByUserId,
                            ClaimedAt = DateTime.Now.AddDays(-1)
                        };
                        context.AyudaProjectClaims.Add(claim);
                        await context.SaveChangesAsync();
                    }
                }

                if (householdsCreated == 0 && beneficiariesCreated == 0)
                {
                    return new DevSeedResult(true, 0, 0, "Mock households already present and fully linked. Nothing to seed.");
                }

                return new DevSeedResult(
                    false,
                    householdsCreated,
                    beneficiariesCreated,
                    $"Seeded/linked {householdsCreated} household(s) and {beneficiariesCreated} beneficiary profile(s). " +
                    "Enroll them into a project via ADD BENEFICIARIES to test releases.");
            }
            finally
            {
                if (_contextOverride == null)
                {
                    await context.DisposeAsync();
                }
            }
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

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace AttendanceShiftingManagement.Tests
{
    public class MockActivityGenerator
    {
        [Fact(Skip = "Disabled per GEMINI.md policy regarding demo data")]
        public async Task GenerateMockActivity()
        {
            var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();
            var connectionString = ConnectionSettingsService.GetEffectiveConnectionString();
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            optionsBuilder.UseMySql(connectionString, serverVersion);

            using var context = new LocalDbContext(optionsBuilder.Options);

            // 1. Get Mock Beneficiaries
            var basePath = @"C:\Users\ASUS\source\repos\AttendanceShiftingManagement\AttendanceShiftingManagement\AttendanceShiftingManagement";
            var mockFilePath = Path.Combine(basePath, "docs", "mock-beneficiaries-added.txt");
            var mockNames = File.ReadAllLines(mockFilePath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var beneficiaries = new List<BeneficiaryStaging>();
            foreach (var nameLine in mockNames)
            {
                string lastName, firstName;
                if (nameLine.Contains(","))
                {
                    var parts = nameLine.Split(',', StringSplitOptions.TrimEntries);
                    lastName = parts[0];
                    firstName = parts[1];
                }
                else
                {
                    // Handle "Bien Josef G regidor"
                    var parts = nameLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    firstName = string.Join(" ", parts.Take(parts.Length - 1));
                    lastName = parts.Last();
                }

                var b = await context.BeneficiaryStaging
                    .FirstOrDefaultAsync(x => x.LastName == lastName && x.FirstName == firstName);
                
                if (b != null) beneficiaries.Add(b);
            }

            if (!beneficiaries.Any())
            {
                // Fallback to any staging beneficiaries if mock ones aren't found
                beneficiaries = await context.BeneficiaryStaging.Take(25).ToListAsync();
            }

            if (!beneficiaries.Any()) return;

            // 2. Initialize Budgets
            var aidBudget = await context.AssistanceCaseBudgets.FirstOrDefaultAsync(x => x.BudgetCode == "GLOBAL_AID_BUDGET");
            if (aidBudget == null)
            {
                aidBudget = new AssistanceCaseBudget
                {
                    BudgetCode = "GLOBAL_AID_BUDGET",
                    BudgetName = "Global Ayuda Pool",
                    BudgetCap = 1000000,
                    IsActive = true,
                    CreatedByUserId = 1
                };
                context.AssistanceCaseBudgets.Add(aidBudget);
            }

            var cfwBudget = await context.CashForWorkBudgets.FirstOrDefaultAsync(x => x.BudgetCode == "GLOBAL_CFW_BUDGET");
            if (cfwBudget == null)
            {
                cfwBudget = new CashForWorkBudget
                {
                    BudgetCode = "GLOBAL_CFW_BUDGET",
                    BudgetName = "Global CFW Fund",
                    BudgetCap = 500000,
                    IsActive = true,
                    CreatedByUserId = 1
                };
                context.CashForWorkBudgets.Add(cfwBudget);
            }

            await context.SaveChangesAsync();

            // 3. Create Ayuda Programs (Distribution)
            var programs = new List<AyudaProgram>();
            if (!await context.AyudaPrograms.AnyAsync(x => x.ProgramCode == "DIST-001"))
            {
                var p1 = new AyudaProgram
                {
                    ProgramCode = "DIST-001",
                    ProgramName = "Senior Citizens Cash Aid Q4",
                    ProgramType = AyudaProgramType.AssistanceCase,
                    BudgetCap = 200000,
                    UnitAmount = 3000,
                    DistributionStatus = AyudaProgramDistributionStatus.Open,
                    StartDate = DateTime.Now.AddDays(-10),
                    EndDate = DateTime.Now.AddDays(20),
                    CreatedByUserId = 1
                };
                programs.Add(p1);
            }

            if (!await context.AyudaPrograms.AnyAsync(x => x.ProgramCode == "DIST-002"))
            {
                var p2 = new AyudaProgram
                {
                    ProgramCode = "DIST-002",
                    ProgramName = "Pamaskong Handog 2024",
                    ProgramType = AyudaProgramType.GeneralPurpose,
                    BudgetCap = 150000,
                    ItemDescription = "Grocery Pack (Noche Buena)",
                    ReleaseKind = AssistanceReleaseKind.Goods,
                    DistributionStatus = AyudaProgramDistributionStatus.Open,
                    StartDate = DateTime.Now.AddDays(-5),
                    EndDate = DateTime.Now.AddDays(15),
                    CreatedByUserId = 1
                };
                programs.Add(p2);
            }

            if (programs.Any())
            {
                context.AyudaPrograms.AddRange(programs);
                await context.SaveChangesAsync();
            }
            else
            {
                programs = await context.AyudaPrograms.Take(2).ToListAsync();
            }

            // 4. Generate Assistance Cases
            var random = new Random();
            var statuses = Enum.GetValues<AssistanceCaseStatus>();
            var priorities = Enum.GetValues<AssistanceCasePriority>();
            
            foreach (var b in beneficiaries.Take(10))
            {
                if (await context.AssistanceCases.AnyAsync(x => x.ValidatedCivilRegistryId == b.CivilRegistryId && x.Status == AssistanceCaseStatus.Pending))
                    continue;

                var ac = new AssistanceCase
                {
                    CaseNumber = $"AC-{random.Next(10000, 99999)}",
                    ValidatedBeneficiaryName = b.FullName,
                    ValidatedBeneficiaryId = b.BeneficiaryId,
                    ValidatedCivilRegistryId = b.CivilRegistryId,
                    AssistanceType = "Medical Assistance",
                    Priority = priorities[random.Next(priorities.Length)],
                    Status = statuses[random.Next(statuses.Length)],
                    RequestedAmount = random.Next(1000, 5000),
                    RequestedOn = DateTime.Now.AddDays(-random.Next(1, 30)),
                    AssistanceCaseBudgetId = aidBudget.Id,
                    CreatedByUserId = 1
                };
                if (ac.Status == AssistanceCaseStatus.Approved || ac.Status == AssistanceCaseStatus.Released)
                {
                    ac.ApprovedAmount = ac.RequestedAmount;
                }
                context.AssistanceCases.Add(ac);
            }

            // 5. Distribution Activity
            if (programs.Any())
            {
                var prog = programs[0];
                foreach (var b in beneficiaries.Skip(10).Take(5))
                {
                    if (await context.AyudaProjectBeneficiaries.AnyAsync(x => x.AyudaProgramId == prog.Id && x.BeneficiaryStagingId == b.StagingID))
                        continue;

                    var pb = new AyudaProjectBeneficiary
                    {
                        AyudaProgramId = prog.Id,
                        BeneficiaryStagingId = b.StagingID,
                        FullName = b.FullName ?? "",
                        BeneficiaryId = b.BeneficiaryId,
                        CivilRegistryId = b.CivilRegistryId,
                        Status = DistributionBeneficiaryStatus.Released,
                        AddedByUserId = 1,
                        StatusUpdatedByUserId = 1,
                        StatusUpdatedAt = DateTime.Now.AddHours(-random.Next(1, 48))
                    };
                    context.AyudaProjectBeneficiaries.Add(pb);

                    var claim = new AyudaProjectClaim
                    {
                        AyudaProgramId = prog.Id,
                        BeneficiaryStagingId = b.StagingID,
                        FullName = b.FullName ?? "",
                        BeneficiaryId = b.BeneficiaryId,
                        CivilRegistryId = b.CivilRegistryId,
                        ClaimedByUserId = 1,
                        ClaimedAt = pb.StatusUpdatedAt ?? DateTime.Now
                    };
                    context.AyudaProjectClaims.Add(claim);
                }
            }

            // 6. CFW Activity
            var cfwEvent = await context.CashForWorkEvents.FirstOrDefaultAsync(x => x.Title == "Barangay Coastal Clean-up");
            if (cfwEvent == null)
            {
                cfwEvent = new CashForWorkEvent
                {
                    Title = "Barangay Coastal Clean-up",
                    Location = "Coastal Area Zone 1",
                    EventDate = DateTime.Today.AddDays(-2),
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Status = CashForWorkEventStatus.Completed,
                    EventKind = CashForWorkEventKind.CashForWork,
                    CashForWorkBudgetId = cfwBudget.Id,
                    CreatedByUserId = 1
                };
                context.CashForWorkEvents.Add(cfwEvent);
                await context.SaveChangesAsync();
            }

            foreach (var b in beneficiaries.Skip(15).Take(5))
            {
                if (await context.CashForWorkParticipants.AnyAsync(x => x.EventId == cfwEvent.Id && x.BeneficiaryStagingId == b.StagingID))
                    continue;

                var p = new CashForWorkParticipant
                {
                    EventId = cfwEvent.Id,
                    BeneficiaryStagingId = b.StagingID,
                    AddedByUserId = 1
                };
                context.CashForWorkParticipants.Add(p);
                await context.SaveChangesAsync(); // Need ID for attendance

                var att = new CashForWorkAttendance
                {
                    ParticipantId = p.Id,
                    Status = CashForWorkAttendanceStatus.Present,
                    Source = AttendanceCaptureSource.ScannerSession,
                    AttendanceDate = DateTime.Today.AddDays(-2),
                    RecordedAt = DateTime.Today.AddDays(-2).AddHours(8).AddMinutes(random.Next(1, 60)),
                    RecordedByUserId = 1
                };
                context.CashForWorkAttendances.Add(att);
            }

            // 7. Assets & Borrowing
            var assets = new[] { "Tent A", "Tent B", "Sound System", "Plastic Chairs (Set of 10)" };
            foreach (var assetName in assets)
            {
                if (!await context.BarangayAssets.AnyAsync(x => x.AssetTag == assetName))
                {
                    context.BarangayAssets.Add(new BarangayAsset
                    {
                        AssetTag = assetName,
                        Category = "Equipment",
                        Description = assetName,
                        Status = AssetStatus.Available
                    });
                }
            }
            await context.SaveChangesAsync();

            var availableAssets = await context.BarangayAssets.Where(x => x.Status == AssetStatus.Available).ToListAsync();
            var borrowingBeneficiaries = beneficiaries.Skip(20).Take(2).ToList();
            
            for (int i = 0; i < Math.Min(availableAssets.Count, borrowingBeneficiaries.Count); i++)
            {
                var asset = availableAssets[i];
                var b = borrowingBeneficiaries[i];

                asset.Status = AssetStatus.Borrowed;
                context.EquipmentBorrowings.Add(new EquipmentBorrowing
                {
                    AssetId = asset.Id,
                    BeneficiaryId = b.BeneficiaryId,
                    BeneficiaryName = b.FullName ?? "",
                    BorrowDate = DateTime.Now.AddDays(-1),
                    DueDate = DateTime.Now.AddDays(2),
                    ConditionOut = "Good"
                });
            }

            await context.SaveChangesAsync();
        }
    }
}

using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            EnsureHouseholds(context);
            EnsureBeneficiaryStaging(context);
            var adminUser = context.Users
                .Where(user => user.Role == UserRole.Admin && user.IsActive)
                .OrderBy(user => user.Id)
                .FirstOrDefault();

            if (adminUser != null)
            {
                EnsureAdminProfile(context, adminUser);
                EnsureCashForWorkData(context, adminUser);
            }
        }

        private static void EnsureAdminProfile(AppDbContext context, User adminUser)
        {
            var profile = context.UserProfiles.FirstOrDefault(item => item.UserId == adminUser.Id);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    UserId = adminUser.Id,
                    FullName = "Barangay Administrator",
                    Nickname = "Admin",
                    Address = "Barangay Hall",
                    Phone = string.Empty,
                    UpdatedAt = DateTime.Now
                };

                context.UserProfiles.Add(profile);
                context.SaveChanges();
            }
        }

        private static void EnsureHouseholds(AppDbContext context)
        {
            if (context.Households.Any())
            {
                return;
            }

            var households = new List<Household>
            {
                new()
                {
                    HouseholdCode = "HH-0001",
                    HeadName = "Mario Santos",
                    AddressLine = "Purok 1, Barangay Centro",
                    Purok = "Purok 1",
                    ContactNumber = "09170000001",
                    Status = HouseholdStatus.Active,
                    Members =
                    {
                        new HouseholdMember
                        {
                            FullName = "Mario Santos",
                            RelationshipToHead = "Head",
                            Occupation = "Laborer",
                            IsCashForWorkEligible = true
                        },
                        new HouseholdMember
                        {
                            FullName = "Liza Santos",
                            RelationshipToHead = "Spouse",
                            Occupation = "Vendor",
                            IsCashForWorkEligible = true
                        },
                        new HouseholdMember
                        {
                            FullName = "John Santos",
                            RelationshipToHead = "Son",
                            Occupation = "Student",
                            IsCashForWorkEligible = false
                        }
                    }
                },
                new()
                {
                    HouseholdCode = "HH-0002",
                    HeadName = "Ana Dela Cruz",
                    AddressLine = "Purok 2, Barangay Centro",
                    Purok = "Purok 2",
                    ContactNumber = "09170000002",
                    Status = HouseholdStatus.Active,
                    Members =
                    {
                        new HouseholdMember
                        {
                            FullName = "Ana Dela Cruz",
                            RelationshipToHead = "Head",
                            Occupation = "Housekeeper",
                            IsCashForWorkEligible = true
                        },
                        new HouseholdMember
                        {
                            FullName = "Miguel Dela Cruz",
                            RelationshipToHead = "Son",
                            Occupation = "Tricycle Driver",
                            IsCashForWorkEligible = true
                        }
                    }
                },
                new()
                {
                    HouseholdCode = "HH-0003",
                    HeadName = "Roberto Garcia",
                    AddressLine = "Purok 3, Barangay Centro",
                    Purok = "Purok 3",
                    ContactNumber = "09170000003",
                    Status = HouseholdStatus.Active,
                    Members =
                    {
                        new HouseholdMember
                        {
                            FullName = "Roberto Garcia",
                            RelationshipToHead = "Head",
                            Occupation = "Farmer",
                            IsCashForWorkEligible = true
                        },
                        new HouseholdMember
                        {
                            FullName = "Teresa Garcia",
                            RelationshipToHead = "Spouse",
                            Occupation = "Seamstress",
                            IsCashForWorkEligible = false
                        }
                    }
                }
            };

            context.Households.AddRange(households);
            context.SaveChanges();
        }

        private static void EnsureBeneficiaryStaging(AppDbContext context)
        {
            if (context.BeneficiaryStaging.Any())
            {
                return;
            }

            context.BeneficiaryStaging.AddRange(
                new BeneficiaryStaging
                {
                    ResidentsId = 1001,
                    BeneficiaryId = "BEN-0001",
                    CivilRegistryId = "CRS-1001",
                    LastName = "Rivera",
                    FirstName = "Elena",
                    FullName = "Elena Rivera",
                    Sex = "Female",
                    DateOfBirth = "1978-04-13",
                    Age = "47",
                    MaritalStatus = "Married",
                    Address = "Purok 4, Barangay Centro",
                    IsPwd = false,
                    IsSenior = false,
                    VerificationStatus = VerificationStatus.Pending,
                    ImportedAt = DateTime.Now
                },
                new BeneficiaryStaging
                {
                    ResidentsId = 1002,
                    BeneficiaryId = "BEN-0002",
                    CivilRegistryId = "CRS-1002",
                    LastName = "Lopez",
                    FirstName = "Ramon",
                    FullName = "Ramon Lopez",
                    Sex = "Male",
                    DateOfBirth = "1959-09-20",
                    Age = "66",
                    MaritalStatus = "Widowed",
                    Address = "Purok 5, Barangay Centro",
                    IsPwd = false,
                    IsSenior = true,
                    SeniorIdNo = "SC-00991",
                    VerificationStatus = VerificationStatus.Pending,
                    ImportedAt = DateTime.Now
                });

            context.SaveChanges();
        }

        private static void EnsureCashForWorkData(AppDbContext context, User adminUser)
        {
            if (context.CashForWorkEvents.Any())
            {
                return;
            }

            var eligibleMembers = context.HouseholdMembers
                .Where(member => member.IsCashForWorkEligible)
                .OrderBy(member => member.Id)
                .Take(2)
                .ToList();

            var cashForWorkEvent = new CashForWorkEvent
            {
                Title = "Barangay Clean-Up Drive",
                Location = "Barangay Hall Grounds",
                EventDate = DateTime.Today,
                StartTime = new TimeSpan(7, 0, 0),
                EndTime = new TimeSpan(12, 0, 0),
                Notes = "Initial seeded event for cash-for-work operations.",
                CreatedByUserId = adminUser.Id,
                Status = CashForWorkEventStatus.Open,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            context.CashForWorkEvents.Add(cashForWorkEvent);
            context.SaveChanges();

            foreach (var member in eligibleMembers)
            {
                context.CashForWorkParticipants.Add(new CashForWorkParticipant
                {
                    EventId = cashForWorkEvent.Id,
                    HouseholdMemberId = member.Id,
                    AddedByUserId = adminUser.Id,
                    AddedAt = DateTime.Now
                });
            }

            context.SaveChanges();
        }
    }
}

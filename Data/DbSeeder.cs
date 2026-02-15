using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // 1. Positions
            if (!context.Positions.Any())
            {
                var positions = new List<Position>
                {
                    new Position { Name = "Chicken Expert", Area = PositionArea.Kitchen },
                    new Position { Name = "Batch Grill", Area = PositionArea.Kitchen },
                    new Position { Name = "POS 1", Area = PositionArea.POS },
                    new Position { Name = "POS 2", Area = PositionArea.POS },
                    new Position { Name = "POS 3", Area = PositionArea.POS },
                    new Position { Name = "GEL", Area = PositionArea.Kitchen },
                    new Position { Name = "DT Order Taker", Area = PositionArea.DT },
                    new Position { Name = "DT Cashier", Area = PositionArea.DT },
                    new Position { Name = "DT Presenter", Area = PositionArea.DT },
                    new Position { Name = "Lobby", Area = PositionArea.Lobby }
                };
                context.Positions.AddRange(positions);
                context.SaveChanges();
            }

            var allPositions = context.Positions.ToList();

            // 2. Users (upsert demo accounts so login credentials are always available)
            EnsureDefaultUsers(context);

            // 3. Employees
            EnsureDefaultEmployees(context, allPositions);

            // 4. Holidays
            if (!context.Holidays.Any())
            {
                var holidays = new List<Holiday>
                {
                    new Holiday { HolidayDate = new DateTime(2026, 1, 1), Name = "New Year's Day", IsDoublePay = true },
                    new Holiday { HolidayDate = new DateTime(2026, 12, 25), Name = "Christmas Day", IsDoublePay = true },
                    new Holiday { HolidayDate = new DateTime(2026, 12, 30), Name = "Rizal Day", IsDoublePay = true }
                };
                context.Holidays.AddRange(holidays);
                context.SaveChanges();
            }

            // 5. Create sample shifts and attendance (Basic check)
            if (!context.Shifts.Any())
            {
                // Optional: Seed initial shifts if needed, or leave empty for fresh start
            }

            // 6. Phase 2 recruitment + retention samples
            EnsureRecruitmentCandidates(context);
            EnsureEmployeeExits(context);
        }

        private static void EnsureDefaultUsers(AppDbContext context)
        {
            AddOrUpdateDemoUser(context, "admin", "admin@mcdonald.com", "admin123", UserRole.Admin);
            AddOrUpdateDemoUser(context, "hr", "hr@mcdonald.com", "hr123", UserRole.HRStaff);

            for (int i = 1; i <= 4; i++)
            {
                AddOrUpdateDemoUser(
                    context,
                    $"manager{i}",
                    $"manager{i}@mcdonald.com",
                    "manager123",
                    UserRole.Manager);
            }

            AddOrUpdateDemoUser(context, "crew", "crew@gmail.com", "crew123", UserRole.Crew);
            for (int i = 1; i <= 19; i++)
            {
                AddOrUpdateDemoUser(
                    context,
                    $"crew{i}",
                    $"crew{i}@mcdonald.com",
                    "crew123",
                    UserRole.Crew);
            }

            context.SaveChanges();
        }

        private static void AddOrUpdateDemoUser(
            AppDbContext context,
            string username,
            string email,
            string plainPassword,
            UserRole role)
        {
            var existing = context.Users.FirstOrDefault(u => u.Email == email || u.Username == username);
            if (existing == null)
            {
                context.Users.Add(new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                return;
            }

            bool hasChanges = false;

            if (existing.Username != username)
            {
                existing.Username = username;
                hasChanges = true;
            }

            if (existing.Email != email)
            {
                existing.Email = email;
                hasChanges = true;
            }

            if (existing.Role != role)
            {
                existing.Role = role;
                hasChanges = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                hasChanges = true;
            }

            if (!BCrypt.Net.BCrypt.Verify(plainPassword, existing.PasswordHash))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                hasChanges = true;
            }

            if (hasChanges)
            {
                existing.UpdatedAt = DateTime.Now;
            }
        }

        private static void EnsureDefaultEmployees(AppDbContext context, List<Position> allPositions)
        {
            if (allPositions.Count == 0)
            {
                return;
            }

            int fallbackPositionId = allPositions[0].Id;
            var random = new Random();

            var adminUser = context.Users.FirstOrDefault(u => u.Email == "admin@mcdonald.com");
            if (adminUser != null)
            {
                AddOrUpdateEmployee(
                    context,
                    adminUser.Id,
                    "System Administrator",
                    fallbackPositionId,
                    100.00m,
                    DateTime.Now.AddYears(-2));
            }

            var hrUsers = context.Users
                .Where(u => u.Role == UserRole.HRStaff)
                .OrderBy(u => u.Id)
                .ToList();
            for (int i = 0; i < hrUsers.Count; i++)
            {
                AddOrUpdateEmployee(
                    context,
                    hrUsers[i].Id,
                    $"HR Staff {i + 1}",
                    fallbackPositionId,
                    75.00m,
                    DateTime.Now.AddYears(-1));
            }

            var managers = context.Users
                .Where(u => u.Role == UserRole.Manager)
                .OrderBy(u => u.Id)
                .ToList();
            for (int i = 0; i < managers.Count; i++)
            {
                AddOrUpdateEmployee(
                    context,
                    managers[i].Id,
                    $"Manager {i + 1}",
                    fallbackPositionId,
                    80.00m,
                    DateTime.Now.AddYears(-1));
            }

            var kitchenPos = allPositions.Where(p => p.Area == PositionArea.Kitchen).ToList();
            var posPos = allPositions.Where(p => p.Area == PositionArea.POS).ToList();
            var dtPos = allPositions.Where(p => p.Area == PositionArea.DT).ToList();
            var lobbyPos = allPositions.Where(p => p.Area == PositionArea.Lobby).ToList();

            if (!kitchenPos.Any()) kitchenPos.Add(allPositions[0]);
            if (!posPos.Any()) posPos.Add(allPositions[0]);
            if (!dtPos.Any()) dtPos.Add(allPositions[0]);
            if (!lobbyPos.Any()) lobbyPos.Add(allPositions[0]);

            var crewUsers = context.Users
                .Where(u => u.Role == UserRole.Crew)
                .OrderBy(u => u.Id)
                .ToList();

            for (int i = 0; i < crewUsers.Count; i++)
            {
                Position assignedPosition;
                if (i < 5)
                {
                    assignedPosition = kitchenPos[i % kitchenPos.Count];
                }
                else if (i < 10)
                {
                    assignedPosition = posPos[i % posPos.Count];
                }
                else if (i < 15)
                {
                    assignedPosition = dtPos[i % dtPos.Count];
                }
                else
                {
                    assignedPosition = lobbyPos[i % lobbyPos.Count];
                }

                AddOrUpdateEmployee(
                    context,
                    crewUsers[i].Id,
                    crewUsers[i].Username == "crew" ? "Crew Member Demo" : $"Crew Member {i + 1}",
                    assignedPosition.Id,
                    65.00m,
                    DateTime.Now.AddMonths(-random.Next(1, 12)));
            }

            context.SaveChanges();
        }

        private static void AddOrUpdateEmployee(
            AppDbContext context,
            int userId,
            string fullName,
            int positionId,
            decimal hourlyRate,
            DateTime dateHired)
        {
            var employee = context.Employees.FirstOrDefault(e => e.UserId == userId);
            if (employee == null)
            {
                context.Employees.Add(new Employee
                {
                    UserId = userId,
                    FullName = fullName,
                    PositionId = positionId,
                    HourlyRate = hourlyRate,
                    DateHired = dateHired,
                    Status = EmployeeStatus.Active
                });
                return;
            }

            employee.FullName = fullName;
            employee.PositionId = positionId;
            employee.HourlyRate = hourlyRate;
            employee.Status = EmployeeStatus.Active;
        }

        private static void EnsureRecruitmentCandidates(AppDbContext context)
        {
            if (context.RecruitmentCandidates.Any())
            {
                return;
            }

            var now = DateTime.Now;
            var records = new List<RecruitmentCandidate>
            {
                new RecruitmentCandidate
                {
                    FullName = "Alden Cruz",
                    Email = "alden.cruz@samplemail.com",
                    Source = RecruitmentSource.Referral,
                    Stage = RecruitmentStage.Interview,
                    AppliedAt = now.AddDays(-18),
                    InterviewedAt = now.AddDays(-10),
                    Notes = "Strong kitchen background."
                },
                new RecruitmentCandidate
                {
                    FullName = "Mia Santos",
                    Email = "mia.santos@samplemail.com",
                    Source = RecruitmentSource.JobBoard,
                    Stage = RecruitmentStage.OfferExtended,
                    AppliedAt = now.AddDays(-22),
                    InterviewedAt = now.AddDays(-14),
                    OfferedAt = now.AddDays(-3),
                    Notes = "Offer pending response."
                },
                new RecruitmentCandidate
                {
                    FullName = "Rhea Lim",
                    Email = "rhea.lim@samplemail.com",
                    Source = RecruitmentSource.Campus,
                    Stage = RecruitmentStage.Hired,
                    AppliedAt = now.AddDays(-40),
                    InterviewedAt = now.AddDays(-32),
                    OfferedAt = now.AddDays(-28),
                    HiredAt = now.AddDays(-24),
                    Notes = "Onboarding completed."
                },
                new RecruitmentCandidate
                {
                    FullName = "Jake Dizon",
                    Email = "jake.dizon@samplemail.com",
                    Source = RecruitmentSource.WalkIn,
                    Stage = RecruitmentStage.Applied,
                    AppliedAt = now.AddDays(-4),
                    Notes = "Awaiting screening."
                },
                new RecruitmentCandidate
                {
                    FullName = "Carla Torres",
                    Email = "carla.torres@samplemail.com",
                    Source = RecruitmentSource.SocialMedia,
                    Stage = RecruitmentStage.Rejected,
                    AppliedAt = now.AddDays(-16),
                    InterviewedAt = now.AddDays(-9),
                    Notes = "Did not meet minimum availability."
                }
            };

            context.RecruitmentCandidates.AddRange(records);
            context.SaveChanges();
        }

        private static void EnsureEmployeeExits(AppDbContext context)
        {
            if (context.EmployeeExits.Any())
            {
                return;
            }

            var recordedBy = context.Users
                .Where(u => u.Role == UserRole.HRStaff || u.Role == UserRole.Admin)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (recordedBy == 0)
            {
                return;
            }

            var crewToExit = context.Employees
                .Include(e => e.User)
                .Where(e => e.User.Role == UserRole.Crew && e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(2)
                .ToList();

            if (crewToExit.Count == 0)
            {
                return;
            }

            var exits = new List<EmployeeExit>();
            for (int i = 0; i < crewToExit.Count; i++)
            {
                var employee = crewToExit[i];
                var isVoluntary = i == 0;
                var type = isVoluntary ? EmployeeExitType.Resignation : EmployeeExitType.EndOfContract;
                var reason = isVoluntary
                    ? "Accepted offer closer to home branch."
                    : "Contract period completed.";

                exits.Add(new EmployeeExit
                {
                    EmployeeId = employee.Id,
                    ExitType = type,
                    IsVoluntary = isVoluntary,
                    Reason = reason,
                    LastWorkingDate = DateTime.Today.AddDays(-(14 + i * 7)),
                    RecordedBy = recordedBy,
                    Notes = "Seeded Phase 2 retention sample.",
                    RecordedAt = DateTime.Now.AddDays(-(12 + i * 7))
                });

                employee.Status = EmployeeStatus.Inactive;
            }

            context.EmployeeExits.AddRange(exits);
            context.SaveChanges();
        }
    }
}

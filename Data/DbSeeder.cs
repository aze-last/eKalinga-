using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using BCrypt.Net;

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

            // 2. Users (1 Admin, 4 Managers, 20 Crew)
            if (!context.Users.Any())
            {
                var users = new List<User>();

                // Admin
                users.Add(new User
                {
                    Username = "admin",
                    Email = "admin@mcdonald.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = UserRole.Admin,
                    IsActive = true
                });

                // Managers (4)
                for (int i = 1; i <= 4; i++)
                {
                    users.Add(new User
                    {
                        Username = $"manager{i}",
                        Email = $"manager{i}@mcdonald.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                        Role = UserRole.Manager,
                        IsActive = true
                    });
                }

                // Crew (20)
                for (int i = 1; i <= 20; i++)
                {
                    users.Add(new User
                    {
                        Username = $"crew{i}",
                        Email = $"crew{i}@mcdonald.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("crew123"),
                        Role = UserRole.Crew,
                        IsActive = true
                    });
                }

                context.Users.AddRange(users);
                context.SaveChanges();
            }

            // 3. Employees
            if (!context.Employees.Any())
            {
                var adminUser = context.Users.First(u => u.Role == UserRole.Admin);
                var managers = context.Users.Where(u => u.Role == UserRole.Manager).OrderBy(u => u.Id).ToList();
                var crewUsers = context.Users.Where(u => u.Role == UserRole.Crew).OrderBy(u => u.Id).ToList();

                var employees = new List<Employee>();

                // Administrator
                employees.Add(new Employee
                {
                    UserId = adminUser.Id,
                    FullName = "System Administrator",
                    PositionId = allPositions[0].Id,
                    HourlyRate = 100.00m,
                    DateHired = DateTime.Now.AddYears(-2),
                    Status = EmployeeStatus.Active
                });

                // Managers
                for (int i = 0; i < managers.Count; i++)
                {
                    employees.Add(new Employee
                    {
                        UserId = managers[i].Id,
                        FullName = $"Manager {i + 1}",
                        PositionId = allPositions[0].Id, // Assign to first position or separate Manager position if exists
                        HourlyRate = 80.00m,
                        DateHired = DateTime.Now.AddYears(-1),
                        Status = EmployeeStatus.Active
                    });
                }

                // Crew - Distribute 5 per Area (Kitchen, POS, DT, Lobby)
                var kitchenPos = allPositions.Where(p => p.Area == PositionArea.Kitchen).ToList();
                var posPos = allPositions.Where(p => p.Area == PositionArea.POS).ToList();
                var dtPos = allPositions.Where(p => p.Area == PositionArea.DT).ToList();
                var lobbyPos = allPositions.Where(p => p.Area == PositionArea.Lobby).ToList();

                // Fallback if list is empty, though we seeded it above
                if (!kitchenPos.Any()) kitchenPos.Add(allPositions[0]);
                if (!posPos.Any()) posPos.Add(allPositions[0]);
                if (!dtPos.Any()) dtPos.Add(allPositions[0]);
                if (!lobbyPos.Any()) lobbyPos.Add(allPositions[0]);

                for (int i = 0; i < crewUsers.Count; i++)
                {
                    Position assignedPosition;

                    if (i < 5) // 0-4: Kitchen
                        assignedPosition = kitchenPos[i % kitchenPos.Count];
                    else if (i < 10) // 5-9: POS
                        assignedPosition = posPos[i % posPos.Count];
                    else if (i < 15) // 10-14: DT
                        assignedPosition = dtPos[i % dtPos.Count];
                    else // 15-19: Lobby
                        assignedPosition = lobbyPos[i % lobbyPos.Count];

                    employees.Add(new Employee
                    {
                        UserId = crewUsers[i].Id,
                        FullName = $"Crew Member {i + 1}",
                        PositionId = assignedPosition.Id,
                        HourlyRate = 65.00m,
                        DateHired = DateTime.Now.AddMonths(-new Random().Next(1, 12)),
                        Status = EmployeeStatus.Active
                    });
                }

                context.Employees.AddRange(employees);
                context.SaveChanges();
            }

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
        }
    }
}
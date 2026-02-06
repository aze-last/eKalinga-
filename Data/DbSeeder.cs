using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using BCrypt.Net;

namespace AttendanceShiftingSystem.Desktop.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Check if already seeded
            if (context.Users.Any()) return;

            // 1. Positions
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

            // 2. Users (1 Admin, 2 Managers, 10 Crew)
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@mcdonald.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = UserRole.Admin,
                IsActive = true
            };

            var manager1 = new User
            {
                Username = "manager1",
                Email = "manager1@mcdonald.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                Role = UserRole.Manager,
                IsActive = true
            };

            var manager2 = new User
            {
                Username = "manager2",
                Email = "manager2@mcdonald.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                Role = UserRole.Manager,
                IsActive = true
            };

            var crewUsers = new List<User>();
            for (int i = 1; i <= 10; i++)
            {
                crewUsers.Add(new User
                {
                    Username = $"crew{i}",
                    Email = $"crew{i}@mcdonald.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("crew123"),
                    Role = UserRole.Crew,
                    IsActive = true
                });
            }

            context.Users.Add(adminUser);
            context.Users.Add(manager1);
            context.Users.Add(manager2);
            context.Users.AddRange(crewUsers);
            context.SaveChanges();

            // 3. Employees
            var adminEmployee = new Employee
            {
                UserId = adminUser.Id,
                FullName = "System Administrator",
                PositionId = positions[0].Id,
                HourlyRate = 100.00m,
                DateHired = DateTime.Now.AddYears(-2),
                Status = EmployeeStatus.Active
            };

            var manager1Employee = new Employee
            {
                UserId = manager1.Id,
                FullName = "John Manager",
                PositionId = positions[0].Id,
                HourlyRate = 80.00m,
                DateHired = DateTime.Now.AddYears(-1),
                Status = EmployeeStatus.Active
            };

            var manager2Employee = new Employee
            {
                UserId = manager2.Id,
                FullName = "Jane Manager",
                PositionId = positions[1].Id,
                HourlyRate = 80.00m,
                DateHired = DateTime.Now.AddYears(-1),
                Status = EmployeeStatus.Active
            };

            var crewEmployees = new List<Employee>();
            string[] crewNames = { "Mark Santos", "Lisa Garcia", "Tom Cruz", "Anna Reyes",
                                   "Ben Lopez", "Sarah Kim", "Mike Tan", "Ella Ramos",
                                   "Jake Lee", "Nina Flores" };

            for (int i = 0; i < 10; i++)
            {
                crewEmployees.Add(new Employee
                {
                    UserId = crewUsers[i].Id,
                    FullName = crewNames[i],
                    PositionId = positions[i % positions.Count].Id,
                    HourlyRate = 65.00m,
                    DateHired = DateTime.Now.AddMonths(-6),
                    Status = EmployeeStatus.Active
                });
            }

            context.Employees.Add(adminEmployee);
            context.Employees.Add(manager1Employee);
            context.Employees.Add(manager2Employee);
            context.Employees.AddRange(crewEmployees);
            context.SaveChanges();

            // 4. Holidays
            var holidays = new List<Holiday>
            {
                new Holiday { HolidayDate = new DateTime(2026, 1, 1), Name = "New Year's Day", IsDoublePay = true },
                new Holiday { HolidayDate = new DateTime(2026, 12, 25), Name = "Christmas Day", IsDoublePay = true },
                new Holiday { HolidayDate = new DateTime(2026, 12, 30), Name = "Rizal Day", IsDoublePay = true }
            };
            context.Holidays.AddRange(holidays);
            context.SaveChanges();
        }
    }
}
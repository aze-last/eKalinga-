using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public AuthService()
        {
            _context = new AppDbContext();
            _auditService = new AuditService(_context);
        }

        public User? Login(string usernameOrEmail, string password)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                _auditService.LogActivity(
                    null,
                    "LoginFailed",
                    "User",
                    null,
                    "Failed login attempt due to empty username/email or password.");
                return null;
            }

            var normalizedInput = usernameOrEmail.Trim();

            var user = _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e!.Position)
                .FirstOrDefault(u =>
                    (u.Username == normalizedInput || u.Email == normalizedInput)
                    && u.IsActive);

            if (user == null)
            {
                _auditService.LogActivity(
                    null,
                    "LoginFailed",
                    "User",
                    null,
                    $"Failed login attempt for '{normalizedInput}'.");
                return null;
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (isPasswordValid)
            {
                _auditService.LogActivity(
                    user.Id,
                    "LoginSuccess",
                    "User",
                    user.Id,
                    $"User '{user.Username}' logged in.");
                return user;
            }

            _auditService.LogActivity(
                user.Id,
                "LoginFailed",
                "User",
                user.Id,
                $"Invalid password for user '{user.Username}'.");
            return null;
        }

        public User? GetCurrentUser(int userId)
        {
            return _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e!.Position)
                .FirstOrDefault(u => u.Id == userId);
        }
    }
}

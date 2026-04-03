using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
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
            var user = _context.Users.FirstOrDefault(u =>
                (u.Username == usernameOrEmail || u.Email == usernameOrEmail)
                && u.IsActive);

            if (user == null)
            {
                _auditService.LogActivity(
                    null,
                    "LoginFailed",
                    "User",
                    null,
                    $"Failed login attempt for '{usernameOrEmail}'.");
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
            return _context.Users.FirstOrDefault(u => u.Id == userId);
        }
    }
}

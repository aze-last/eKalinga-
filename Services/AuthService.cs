using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService()
        {
            _context = new AppDbContext();
        }

        public User? Login(string usernameOrEmail, string password)
        {
            var user = _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e.Position)
                .FirstOrDefault(u =>
                    (u.Username == usernameOrEmail || u.Email == usernameOrEmail)
                    && u.IsActive);

            if (user == null) return null;

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            return isPasswordValid ? user : null;
        }

        public User? GetCurrentUser(int userId)
        {
            return _context.Users
                .Include(u => u.Employee)
                    .ThenInclude(e => e.Position)
                .FirstOrDefault(u => u.Id == userId);
        }
    }
}
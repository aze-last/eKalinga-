using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using System.Net.Mail;

namespace AttendanceShiftingManagement.Services
{
    public sealed record InitialAdminState(bool RequiresSetup, string Message);

    public sealed record InitialAdminSetupRequest(
        string Username,
        string Email,
        string Password,
        string FullName);

    public sealed record InitialAdminSetupResult(
        bool IsSuccess,
        string Message,
        User? User = null);

    public static class InitialAdminSetupService
    {
        public static InitialAdminState GetState(AppDbContext context)
        {
            var hasActiveAdmin = context.Users.Any(user => user.Role == UserRole.Admin && user.IsActive);
            if (hasActiveAdmin)
            {
                return new InitialAdminState(false, "An active admin account already exists for this database.");
            }

            var hasAnyUsers = context.Users.Any();
            return hasAnyUsers
                ? new InitialAdminState(true, "No active admin account exists for this database. Create one now to restore access.")
                : new InitialAdminState(true, "Create the first admin account for this database. No default password is shipped with the app.");
        }

        public static InitialAdminSetupResult CreateInitialAdmin(AppDbContext context, InitialAdminSetupRequest request)
        {
            var state = GetState(context);
            if (!state.RequiresSetup)
            {
                return new InitialAdminSetupResult(false, state.Message);
            }

            var username = request.Username.Trim();
            var email = request.Email.Trim();
            var password = request.Password;
            var fullName = request.FullName.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return new InitialAdminSetupResult(false, "Enter the administrator's full name.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return new InitialAdminSetupResult(false, "Enter an admin username.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return new InitialAdminSetupResult(false, "Enter an admin email address.");
            }

            if (!IsValidEmail(email))
            {
                return new InitialAdminSetupResult(false, "Enter a valid admin email address.");
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                return new InitialAdminSetupResult(false, "Use an admin password with at least 8 characters.");
            }

            if (context.Users.Any(user => user.Username == username))
            {
                return new InitialAdminSetupResult(false, "That username is already in use.");
            }

            if (context.Users.Any(user => user.Email == email))
            {
                return new InitialAdminSetupResult(false, "That email is already in use.");
            }

            var timestamp = DateTime.Now;
            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            };

            context.Users.Add(user);
            context.SaveChanges();

            context.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                FullName = fullName,
                Nickname = username,
                Address = "Barangay Hall",
                Phone = string.Empty,
                EmergencyContactName = string.Empty,
                EmergencyContactPhone = string.Empty,
                PhotoPath = string.Empty,
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            });
            context.SaveChanges();

            var auditService = new AuditService(context);
            auditService.LogActivity(
                null,
                "InitialAdminCreated",
                "User",
                user.Id,
                $"Initial admin '{user.Username}' was created for database bootstrap.");

            DbSeeder.Seed(context);

            return new InitialAdminSetupResult(
                true,
                "Initial admin account created. Sign in with the credentials you just set.",
                user);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

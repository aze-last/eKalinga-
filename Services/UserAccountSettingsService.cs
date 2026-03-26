using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using System.Net.Mail;

namespace AttendanceShiftingManagement.Services
{
    public sealed record AccountSettingsSnapshot(
        string FullName,
        string Username,
        string Email,
        string ContactNumber);

    public sealed record AccountSettingsUpdateRequest(
        string FullName,
        string Username,
        string Email,
        string ContactNumber);

    public sealed record PasswordChangeRequest(
        string CurrentPassword,
        string NewPassword,
        string ConfirmPassword);

    public sealed record AccountSettingsResult(
        bool IsSuccess,
        string Message);

    public static class UserAccountSettingsService
    {
        public static AccountSettingsSnapshot Load(AppDbContext context, int userId)
        {
            var user = context.Users.FirstOrDefault(item => item.Id == userId);
            var profile = context.UserProfiles.FirstOrDefault(item => item.UserId == userId);

            return new AccountSettingsSnapshot(
                profile?.FullName ?? string.Empty,
                user?.Username ?? string.Empty,
                user?.Email ?? string.Empty,
                profile?.Phone ?? string.Empty);
        }

        public static AccountSettingsResult SaveAccount(
            AppDbContext context,
            User sessionUser,
            AccountSettingsUpdateRequest request)
        {
            var user = context.Users.FirstOrDefault(item => item.Id == sessionUser.Id);
            if (user == null)
            {
                return new AccountSettingsResult(false, "The signed-in user could not be loaded.");
            }

            var fullName = request.FullName.Trim();
            var username = request.Username.Trim();
            var email = request.Email.Trim();
            var contactNumber = request.ContactNumber.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return new AccountSettingsResult(false, "Enter your full name.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return new AccountSettingsResult(false, "Enter a username.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return new AccountSettingsResult(false, "Enter an email address.");
            }

            if (!IsValidEmail(email))
            {
                return new AccountSettingsResult(false, "Enter a valid email address.");
            }

            if (context.Users.Any(item => item.Id != user.Id && item.Username == username))
            {
                return new AccountSettingsResult(false, "That username is already in use.");
            }

            if (context.Users.Any(item => item.Id != user.Id && item.Email == email))
            {
                return new AccountSettingsResult(false, "That email is already in use.");
            }

            var timestamp = DateTime.Now;
            var profile = context.UserProfiles.FirstOrDefault(item => item.UserId == user.Id);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    UserId = user.Id,
                    CreatedAt = timestamp,
                    Nickname = username,
                    Address = string.Empty,
                    EmergencyContactName = string.Empty,
                    EmergencyContactPhone = string.Empty,
                    PhotoPath = string.Empty
                };
                context.UserProfiles.Add(profile);
            }

            user.Username = username;
            user.Email = email;
            user.UpdatedAt = timestamp;

            profile.FullName = fullName;
            profile.Phone = contactNumber;
            profile.Nickname = string.IsNullOrWhiteSpace(profile.Nickname) ? username : profile.Nickname;
            profile.UpdatedAt = timestamp;

            context.SaveChanges();

            sessionUser.Username = user.Username;
            sessionUser.Email = user.Email;
            sessionUser.UpdatedAt = user.UpdatedAt;

            var auditService = new AuditService(context);
            auditService.LogActivity(
                user.Id,
                "AccountProfileUpdated",
                "User",
                user.Id,
                $"User '{user.Username}' updated account profile settings.");

            return new AccountSettingsResult(true, "Account details saved.");
        }

        public static AccountSettingsResult ChangePassword(
            AppDbContext context,
            User sessionUser,
            PasswordChangeRequest request)
        {
            var user = context.Users.FirstOrDefault(item => item.Id == sessionUser.Id);
            if (user == null)
            {
                return new AccountSettingsResult(false, "The signed-in user could not be loaded.");
            }

            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return new AccountSettingsResult(false, "Enter your current password.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return new AccountSettingsResult(false, "The current password is incorrect.");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return new AccountSettingsResult(false, "Use a new password with at least 8 characters.");
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return new AccountSettingsResult(false, "The new password and confirmation password do not match.");
            }

            if (request.NewPassword == request.CurrentPassword)
            {
                return new AccountSettingsResult(false, "Choose a different password from the current one.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.Now;
            context.SaveChanges();

            sessionUser.PasswordHash = user.PasswordHash;
            sessionUser.UpdatedAt = user.UpdatedAt;

            var auditService = new AuditService(context);
            auditService.LogActivity(
                user.Id,
                "PasswordChanged",
                "User",
                user.Id,
                $"User '{user.Username}' changed account password.");

            return new AccountSettingsResult(true, "Password changed successfully.");
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

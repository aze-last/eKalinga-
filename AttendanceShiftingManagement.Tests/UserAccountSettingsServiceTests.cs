using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class UserAccountSettingsServiceTests
{
    [Fact]
    public void Load_ReturnsCurrentUserAccountSnapshot()
    {
        using var context = TestDbContextFactory.CreateContext();
        var user = SeedUser(context, "captain", "captain@barangay.local", "Password123!", "Barangay Captain", "09170000001");

        var snapshot = UserAccountSettingsService.Load(context, user.Id);

        Assert.Equal("Barangay Captain", snapshot.FullName);
        Assert.Equal("captain", snapshot.Username);
        Assert.Equal("captain@barangay.local", snapshot.Email);
        Assert.Equal("09170000001", snapshot.ContactNumber);
    }

    [Fact]
    public void SaveAccount_WhenUsernameAlreadyExists_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        _ = SeedUser(context, "existing-user", "existing@barangay.local", "Password123!", "Existing User", "09170000001");
        var sessionUser = SeedUser(context, "captain", "captain@barangay.local", "Password123!", "Barangay Captain", "09170000002");

        var result = UserAccountSettingsService.SaveAccount(
            context,
            sessionUser,
            new AccountSettingsUpdateRequest(
                "Barangay Captain",
                "existing-user",
                "captain@barangay.local",
                "09170000002"));

        Assert.False(result.IsSuccess);
        Assert.Equal("That username is already in use.", result.Message);
    }

    [Fact]
    public void SaveAccount_WhenValid_UpdatesUserProfileAndWritesAudit()
    {
        using var context = TestDbContextFactory.CreateContext();
        var sessionUser = SeedUser(context, "captain", "captain@barangay.local", "Password123!", "Barangay Captain", "09170000001");

        var result = UserAccountSettingsService.SaveAccount(
            context,
            sessionUser,
            new AccountSettingsUpdateRequest(
                "Captain Maria Santos",
                "maria.santos",
                "maria@barangay.local",
                "09179998888"));

        Assert.True(result.IsSuccess);

        var storedUser = Assert.Single(context.Users);
        Assert.Equal("maria.santos", storedUser.Username);
        Assert.Equal("maria@barangay.local", storedUser.Email);

        var storedProfile = Assert.Single(context.UserProfiles);
        Assert.Equal("Captain Maria Santos", storedProfile.FullName);
        Assert.Equal("09179998888", storedProfile.Phone);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("AccountProfileUpdated", auditLog.Action);
        Assert.Equal(storedUser.Id, auditLog.EntityId);
        Assert.Equal("maria.santos", sessionUser.Username);
        Assert.Equal("maria@barangay.local", sessionUser.Email);
    }

    [Fact]
    public void ChangePassword_WhenCurrentPasswordIsInvalid_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var sessionUser = SeedUser(context, "captain", "captain@barangay.local", "Password123!", "Barangay Captain", "09170000001");

        var result = UserAccountSettingsService.ChangePassword(
            context,
            sessionUser,
            new PasswordChangeRequest("wrong-password", "NewPassword123!", "NewPassword123!"));

        Assert.False(result.IsSuccess);
        Assert.Equal("The current password is incorrect.", result.Message);
    }

    [Fact]
    public void ChangePassword_WhenValid_HashesNewPasswordAndWritesAudit()
    {
        using var context = TestDbContextFactory.CreateContext();
        var sessionUser = SeedUser(context, "captain", "captain@barangay.local", "Password123!", "Barangay Captain", "09170000001");

        var result = UserAccountSettingsService.ChangePassword(
            context,
            sessionUser,
            new PasswordChangeRequest("Password123!", "NewPassword123!", "NewPassword123!"));

        Assert.True(result.IsSuccess);

        var storedUser = Assert.Single(context.Users);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", storedUser.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("Password123!", storedUser.PasswordHash));

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("PasswordChanged", auditLog.Action);
        Assert.Equal(storedUser.Id, auditLog.EntityId);
        Assert.Equal(storedUser.PasswordHash, sessionUser.PasswordHash);
    }

    private static User SeedUser(
        Data.AppDbContext context,
        string username,
        string email,
        string password,
        string fullName,
        string phone)
    {
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
            Phone = phone,
            Address = "Barangay Hall",
            EmergencyContactName = string.Empty,
            EmergencyContactPhone = string.Empty,
            PhotoPath = string.Empty,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        });
        context.SaveChanges();

        return user;
    }
}

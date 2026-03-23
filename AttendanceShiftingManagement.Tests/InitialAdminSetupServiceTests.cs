using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class InitialAdminSetupServiceTests
{
    [Fact]
    public void GetState_WhenNoAdminExists_RequiresSetup()
    {
        using var context = TestDbContextFactory.CreateContext();

        var state = InitialAdminSetupService.GetState(context);

        Assert.True(state.RequiresSetup);
    }

    [Fact]
    public void CreateInitialAdmin_WhenAdminAlreadyExists_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.Users.Add(new User
        {
            Username = "captain",
            Email = "captain@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("existing-password"),
            Role = UserRole.Admin,
            IsActive = true
        });
        context.SaveChanges();

        var result = InitialAdminSetupService.CreateInitialAdmin(context, new InitialAdminSetupRequest(
            "admin",
            "admin@barangay.local",
            "Password123!",
            "Barangay Administrator"));

        Assert.False(result.IsSuccess);
        Assert.Single(context.Users);
    }

    [Fact]
    public void CreateInitialAdmin_WhenDatabaseHasNoAdmin_CreatesAdminProfileAndAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();

        var result = InitialAdminSetupService.CreateInitialAdmin(context, new InitialAdminSetupRequest(
            "admin",
            "admin@barangay.local",
            "Password123!",
            "Barangay Administrator"));

        Assert.True(result.IsSuccess);

        var admin = Assert.Single(context.Users);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", admin.PasswordHash));

        var profile = Assert.Single(context.UserProfiles);
        Assert.Equal(admin.Id, profile.UserId);
        Assert.Equal("Barangay Administrator", profile.FullName);

        var auditLog = Assert.Single(context.ActivityLogs);
        Assert.Equal("InitialAdminCreated", auditLog.Action);
        Assert.Equal("User", auditLog.Entity);
        Assert.Equal(admin.Id, auditLog.EntityId);
    }
}

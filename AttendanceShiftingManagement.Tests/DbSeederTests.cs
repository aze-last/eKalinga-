using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Tests;

public sealed class DbSeederTests
{
    [Fact]
    public void Seed_WhenAdminAlreadyExists_DoesNotResetCredentials()
    {
        using var context = TestDbContextFactory.CreateContext();
        var originalHash = BCrypt.Net.BCrypt.HashPassword("custom-password");

        context.Users.Add(new User
        {
            Username = "captain",
            Email = "captain@barangay.local",
            PasswordHash = originalHash,
            Role = UserRole.Admin,
            IsActive = true
        });
        context.SaveChanges();

        DbSeeder.Seed(context);

        var admin = Assert.Single(context.Users);
        Assert.Equal("captain", admin.Username);
        Assert.Equal("captain@barangay.local", admin.Email);
        Assert.Equal(originalHash, admin.PasswordHash);
    }
}

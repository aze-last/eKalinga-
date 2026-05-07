using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class SessionAnnouncementServiceTests
{
    [Fact]
    public async Task BuildSnapshotAsync_ReturnsMeaningfulActivityFromPreviousSessionWindow_IncludingSameUser_AndExcludesAuthNoise()
    {
        using var context = TestDbContextFactory.CreateContext();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"session-announcements-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "state.json");

        var admin = new User
        {
            Username = "admin",
            Email = "admin@barangay.local",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(admin);
        context.SaveChanges();

        var firstLogout = new DateTime(2026, 4, 22, 8, 0, 0);
        var secondLogout = new DateTime(2026, 4, 22, 9, 0, 0);

        context.ActivityLogs.AddRange(
            new ActivityLog
            {
                UserId = admin.Id,
                Action = "Approve",
                Entity = "BeneficiaryStaging",
                EntityId = 11,
                Details = "3 beneficiary records were approved.",
                Timestamp = firstLogout.AddMinutes(10)
            },
            new ActivityLog
            {
                UserId = admin.Id,
                Action = "Updated",
                Entity = "BudgetLedgerEntry",
                EntityId = 22,
                Details = "Budget release entry was updated.",
                Timestamp = firstLogout.AddMinutes(15)
            },
            new ActivityLog
            {
                UserId = admin.Id,
                Action = "LoginSuccess",
                Entity = "User",
                EntityId = admin.Id,
                Details = "Should be excluded from popup feed.",
                Timestamp = firstLogout.AddMinutes(20)
            },
            new ActivityLog
            {
                UserId = admin.Id,
                Action = "Logout",
                Entity = "User",
                EntityId = admin.Id,
                Details = "Should be excluded from popup feed.",
                Timestamp = firstLogout.AddMinutes(25)
            },
            new ActivityLog
            {
                UserId = admin.Id,
                Action = "Updated",
                Entity = "Report",
                EntityId = 33,
                Details = "This should not appear because it happened after the latest logout.",
                Timestamp = secondLogout.AddMinutes(5)
            });
        context.SaveChanges();

        var service = new SessionAnnouncementService(() => context, runtimePath);
        service.RecordLogoutCheckpoint(admin.Id, firstLogout);
        service.RecordLogoutCheckpoint(admin.Id, secondLogout);

        var snapshot = await service.BuildSnapshotAsync(admin.Id);

        Assert.True(snapshot.HasUpdates);
        Assert.Equal(2, snapshot.Items.Count);
        Assert.Equal(1, snapshot.ApprovalCount);
        Assert.Equal(1, snapshot.BudgetCount);
        Assert.Contains(snapshot.Items, item => item.Module == "Validated Beneficiaries");
        Assert.Contains(snapshot.Items, item => item.Module == "Budget");
        Assert.DoesNotContain(snapshot.Items, item => item.Module == "Reports");
        Assert.Equal(firstLogout, snapshot.PreviousLogoutAt);
        Assert.Equal(secondLogout, snapshot.LastLogoutAt);

        Directory.Delete(tempDirectory, recursive: true);
    }

    [Fact]
    public async Task BuildSnapshotAsync_WhenNoCheckpointExists_ReturnsEmptySnapshot()
    {
        using var context = TestDbContextFactory.CreateContext();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"session-announcements-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var runtimePath = Path.Combine(tempDirectory, "missing-state.json");

        var service = new SessionAnnouncementService(() => context, runtimePath);
        var snapshot = await service.BuildSnapshotAsync(1);

        Assert.False(snapshot.HasUpdates);
        Assert.Empty(snapshot.Items);

        Directory.Delete(tempDirectory, recursive: true);
    }
}

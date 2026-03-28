using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class ScannerSessionServiceTests
{
    [Fact]
    public async Task CreateAttendanceSessionAsync_GeneratesSixDigitPin_AndBindsTheSelectedEvent()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var cashForWorkEvent = SeedEvent(context, admin.Id);
        var service = new ScannerSessionService(context);

        var session = await service.CreateAttendanceSessionAsync(cashForWorkEvent.Id, admin.Id, TimeSpan.FromMinutes(15));

        Assert.Equal(ScannerSessionMode.Attendance, session.Mode);
        Assert.Equal(cashForWorkEvent.Id, session.CashForWorkEventId);
        Assert.Matches("^[0-9]{6}$", session.Pin);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionToken));

        Assert.True(await service.ValidatePinAsync(session.SessionToken, session.Pin));

        var stored = Assert.Single(context.ScannerSessions);
        Assert.Equal(cashForWorkEvent.Id, stored.CashForWorkEventId);
        Assert.Equal(ScannerSessionMode.Attendance, stored.Mode);
    }

    [Fact]
    public async Task ValidatePinAsync_ReturnsFalse_WhenSessionHasExpired()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new ScannerSessionService(context);

        var session = await service.CreateLookupSessionAsync(admin.Id, TimeSpan.FromMinutes(-1));

        Assert.False(await service.ValidatePinAsync(session.SessionToken, session.Pin));
    }

    [Fact]
    public async Task CreateDistributionSessionAsync_GeneratesSixDigitPin_AndBindsTheSelectedProgram()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var program = SeedProgram(context, admin.Id);
        var service = new ScannerSessionService(context);

        var session = await service.CreateDistributionSessionAsync(program.Id, admin.Id, TimeSpan.FromMinutes(15));

        Assert.Equal(ScannerSessionMode.Distribution, session.Mode);
        Assert.Equal(program.Id, session.AyudaProgramId);
        Assert.Null(session.CashForWorkEventId);
        Assert.Matches("^[0-9]{6}$", session.Pin);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionToken));

        Assert.True(await service.ValidatePinAsync(session.SessionToken, session.Pin));

        var stored = Assert.Single(context.ScannerSessions);
        Assert.Equal(program.Id, stored.AyudaProgramId);
        Assert.Equal(ScannerSessionMode.Distribution, stored.Mode);
    }

    private static User SeedAdmin(Data.AppDbContext context)
    {
        var user = new User
        {
            Username = "admin",
            Email = "admin@barangay.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static CashForWorkEvent SeedEvent(Data.AppDbContext context, int createdByUserId)
    {
        var cashForWorkEvent = new CashForWorkEvent
        {
            Title = "Barangay Clean-Up",
            Location = "Covered Court",
            EventDate = DateTime.Today,
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            CreatedByUserId = createdByUserId,
            Status = CashForWorkEventStatus.Open
        };

        context.CashForWorkEvents.Add(cashForWorkEvent);
        context.SaveChanges();
        return cashForWorkEvent;
    }

    private static AyudaProgram SeedProgram(Data.AppDbContext context, int createdByUserId)
    {
        var program = new AyudaProgram
        {
            ProgramCode = "DIST-SCAN",
            ProgramName = "Distribution Scan Project",
            ProgramType = AyudaProgramType.GeneralPurpose,
            CreatedByUserId = createdByUserId,
            IsActive = true
        };

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
    }
}

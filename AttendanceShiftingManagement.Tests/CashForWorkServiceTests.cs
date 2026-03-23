using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkServiceTests
{
    [Fact]
    public void WriteOperations_AddAuditLogEntries()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var member = SeedMember(context, household.Id);
        var auditService = new AuditService(context);
        var service = new CashForWorkService(context, auditService);

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, member.Id, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);

        var actions = context.ActivityLogs
            .OrderBy(log => log.Id)
            .Select(log => log.Action)
            .ToArray();

        Assert.Equal(
            ["CashForWorkEventCreated", "CashForWorkParticipantAdded", "CashForWorkManualAttendanceSaved"],
            actions);
    }

    [Fact]
    public void SaveAttendanceSelections_DeduplicatesSameParticipantWithinSingleBatch()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var member = SeedMember(context, household.Id, "Pedro Santos");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, member.Id, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;

        var savedCount = service.SaveAttendanceSelections(
            cashForWorkEvent.Id,
            admin.Id,
            [
                new CashForWorkAttendanceReviewItem
                {
                    ExtractedName = "Pedro Santos",
                    MatchStatus = AttendanceMatchStatus.Matched,
                    SuggestedParticipantId = participantId,
                    SuggestedParticipantName = "Pedro Santos",
                    IsSelected = true
                },
                new CashForWorkAttendanceReviewItem
                {
                    ExtractedName = "P Santos",
                    MatchStatus = AttendanceMatchStatus.Matched,
                    SuggestedParticipantId = participantId,
                    SuggestedParticipantName = "Pedro Santos",
                    IsSelected = true
                }
            ]);

        Assert.Equal(1, savedCount);
        Assert.Single(context.CashForWorkAttendances);
    }

    [Fact]
    public void GetReleaseReadySummary_ReturnsAttendanceTotalsForSelectedEvent()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var household = SeedHousehold(context);
        var manualMember = SeedMember(context, household.Id, "Pedro Santos");
        var ocrMember = SeedMember(context, household.Id, "Ana Santos");
        var pendingMember = SeedMember(context, household.Id, "Luis Santos");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, manualMember.Id, admin.Id);
        service.AddParticipant(cashForWorkEvent.Id, ocrMember.Id, admin.Id);
        service.AddParticipant(cashForWorkEvent.Id, pendingMember.Id, admin.Id);

        var participantIds = context.CashForWorkParticipants
            .ToDictionary(participant => participant.HouseholdMemberId, participant => participant.Id);

        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantIds[manualMember.Id]]);
        service.SaveAttendanceSelections(
            cashForWorkEvent.Id,
            admin.Id,
            [
                new CashForWorkAttendanceReviewItem
                {
                    ExtractedName = "Ana Santos",
                    MatchStatus = AttendanceMatchStatus.Matched,
                    SuggestedParticipantId = participantIds[ocrMember.Id],
                    SuggestedParticipantName = "Ana Santos",
                    IsSelected = true
                }
            ]);

        var summary = service.GetReleaseReadySummary(cashForWorkEvent.Id);

        Assert.Equal(cashForWorkEvent.Id, summary.EventId);
        Assert.Equal("Barangay Clean-Up", summary.EventTitle);
        Assert.Equal(3, summary.ApprovedParticipantCount);
        Assert.Equal(2, summary.PresentParticipantCount);
        Assert.Equal(1, summary.PendingParticipantCount);
        Assert.Equal(2, summary.ReleaseReadyParticipantCount);
        Assert.Equal(1, summary.ManualAttendanceCount);
        Assert.Equal(1, summary.OcrAttendanceCount);
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

    private static Household SeedHousehold(Data.AppDbContext context)
    {
        var household = new Household
        {
            HouseholdCode = "HH-001",
            HeadName = "Maria Santos",
            AddressLine = "Barangay Centro",
            Purok = "Purok 1",
            ContactNumber = "09170000001",
            Status = HouseholdStatus.Active
        };

        context.Households.Add(household);
        context.SaveChanges();
        return household;
    }

    private static HouseholdMember SeedMember(Data.AppDbContext context, int householdId, string fullName = "Pedro Santos")
    {
        var member = new HouseholdMember
        {
            HouseholdId = householdId,
            FullName = fullName,
            RelationshipToHead = "Son",
            Occupation = "Laborer",
            IsCashForWorkEligible = true
        };

        context.HouseholdMembers.Add(member);
        context.SaveChanges();
        return member;
    }
}

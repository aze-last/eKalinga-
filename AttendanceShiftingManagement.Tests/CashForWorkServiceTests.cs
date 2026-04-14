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
        var beneficiary = SeedApprovedBeneficiary(context);
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

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);

        var participant = context.CashForWorkParticipants.Single();
        Assert.Equal(beneficiary.StagingID, GetBeneficiaryStagingId(participant));

        var actions = context.ActivityLogs
            .OrderBy(log => log.Id)
            .Select(log => log.Action)
            .ToArray();

        Assert.Equal(
            ["CashForWorkEventCreated", "CashForWorkParticipantAdded", "CashForWorkManualAttendanceSaved"],
            actions);
    }

    [Fact]
    public void GetReleaseReadySummary_ReturnsAttendanceTotalsForSelectedEvent()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var manualBeneficiary = SeedApprovedBeneficiary(context, 2001, "Pedro Santos");
        var secondManualBeneficiary = SeedApprovedBeneficiary(context, 2002, "Ana Santos");
        var pendingBeneficiary = SeedApprovedBeneficiary(context, 2003, "Luis Santos");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, manualBeneficiary.StagingID, admin.Id);
        service.AddParticipant(cashForWorkEvent.Id, secondManualBeneficiary.StagingID, admin.Id);
        service.AddParticipant(cashForWorkEvent.Id, pendingBeneficiary.StagingID, admin.Id);

        var participantIds = context.CashForWorkParticipants
            .ToDictionary(participant => GetBeneficiaryStagingId(participant), participant => participant.Id);

        service.SaveManualAttendance(
            cashForWorkEvent.Id,
            admin.Id,
            [participantIds[manualBeneficiary.StagingID], participantIds[secondManualBeneficiary.StagingID]]);

        var summary = service.GetReleaseReadySummary(cashForWorkEvent.Id);

        Assert.Equal(cashForWorkEvent.Id, summary.EventId);
        Assert.Equal("Barangay Clean-Up", summary.EventTitle);
        Assert.Equal(3, summary.ApprovedParticipantCount);
        Assert.Equal(2, summary.PresentParticipantCount);
        Assert.Equal(1, summary.PendingParticipantCount);
        Assert.Equal(2, summary.ReleaseReadyParticipantCount);
        Assert.Equal(2, summary.ManualAttendanceCount);
        Assert.Contains(summary.ReleaseReadyParticipants, participant => GetStringProperty(participant, "BeneficiaryId") == manualBeneficiary.BeneficiaryId);
        Assert.Contains(summary.ReleaseReadyParticipants, participant => GetStringProperty(participant, "CivilRegistryId") == secondManualBeneficiary.CivilRegistryId);
    }

    [Fact]
    public async Task SaveScannerAttendance_UsesScannerSource_AndPreventsDuplicateAttendance()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 3001, "Pedro Santos");
        var service = new CashForWorkService(context);
        var digitalIdService = new BeneficiaryDigitalIdService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        var digitalId = await digitalIdService.EnsureIssuedAsync(beneficiary.StagingID, admin.Id);

        Assert.True(await service.SaveScannerAttendanceAsync(cashForWorkEvent.Id, admin.Id, participantId, digitalId.QrPayload));
        Assert.False(await service.SaveScannerAttendanceAsync(cashForWorkEvent.Id, admin.Id, participantId, digitalId.QrPayload));

        var attendance = Assert.Single(context.CashForWorkAttendances);
        Assert.Equal(AttendanceCaptureSource.ScannerSession, attendance.Source);
        Assert.Equal(digitalId.QrPayload, attendance.OcrExtractedName);
    }

    [Fact]
    public void AddParticipant_RejectsBeneficiariesThatAreNotApproved()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4001, "Pending Beneficiary", VerificationStatus.Pending);
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        var ex = Assert.Throws<InvalidOperationException>(() => service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id));
        Assert.Contains("approved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddParticipant_RejectsReleasedEvents()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4101, "Released Participant");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Released Event",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        cashForWorkEvent.Status = CashForWorkEventStatus.Completed;
        context.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() => service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id));
        Assert.Contains("can no longer be modified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveManualAttendance_RejectsReleasedEvents()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4201, "Released Attendance");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Released Event",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;

        cashForWorkEvent = context.CashForWorkEvents.Single();
        cashForWorkEvent.Status = CashForWorkEventStatus.Completed;
        context.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() => service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]));
        Assert.Contains("can no longer be modified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveScannerAttendance_RejectsReleasedEvents()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4301, "Released Scanner");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = service.CreateEvent(
            "Released Event",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;

        cashForWorkEvent = context.CashForWorkEvents.Single();
        cashForWorkEvent.Status = CashForWorkEventStatus.Completed;
        context.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveScannerAttendanceAsync(cashForWorkEvent.Id, admin.Id, participantId, "IGNORED-QR"));
        Assert.Contains("can no longer be modified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateEvent_UpdatesSelectedEvent_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            "Initial notes",
            admin.Id);

        var updatedEvent = service.UpdateEvent(
            cashForWorkEvent.Id,
            "Updated Clean-Up",
            "Covered Court",
            DateTime.Today.AddDays(1),
            new TimeSpan(8, 0, 0),
            new TimeSpan(13, 0, 0),
            "Updated notes",
            admin.Id);

        Assert.Equal("Updated Clean-Up", updatedEvent.Title);
        Assert.Equal("Covered Court", updatedEvent.Location);
        Assert.Equal(DateTime.Today.AddDays(1).Date, updatedEvent.EventDate.Date);
        Assert.Equal(new TimeSpan(8, 0, 0), updatedEvent.StartTime);
        Assert.Equal(new TimeSpan(13, 0, 0), updatedEvent.EndTime);
        Assert.Equal("Updated notes", updatedEvent.Notes);
        Assert.Contains(
            context.ActivityLogs.Select(log => log.Action).ToArray(),
            action => string.Equals(action, "CashForWorkEventUpdated", StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteEvent_RemovesEventParticipantsAttendanceAndScannerSessions()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 5001, "Pedro Santos");
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);

        context.ScannerSessions.Add(new ScannerSession
        {
            Mode = ScannerSessionMode.Attendance,
            SessionToken = "session-token",
            PinHash = "pin-hash",
            CashForWorkEventId = cashForWorkEvent.Id,
            CreatedByUserId = admin.Id,
            IsActive = true,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddMinutes(10)
        });
        context.SaveChanges();

        service.DeleteEvent(cashForWorkEvent.Id, admin.Id);

        Assert.Empty(context.CashForWorkAttendances);
        Assert.Empty(context.CashForWorkParticipants);
        Assert.Empty(context.CashForWorkEvents);
        Assert.Empty(context.ScannerSessions);
        Assert.Contains(
            context.ActivityLogs.Select(log => log.Action).ToArray(),
            action => string.Equals(action, "CashForWorkEventDeleted", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdateAttendance_UpdatesSelectedAttendance_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 6001, "Pedro Santos");
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);
        var attendanceId = context.CashForWorkAttendances.Single().Id;

        var updatedAttendance = service.UpdateAttendance(
            attendanceId,
            DateTime.Today.AddDays(1),
            CashForWorkAttendanceStatus.Absent,
            AttendanceCaptureSource.OcrUpload,
            admin.Id);

        Assert.Equal(DateTime.Today.AddDays(1).Date, updatedAttendance.AttendanceDate.Date);
        Assert.Equal(CashForWorkAttendanceStatus.Absent, updatedAttendance.Status);
        Assert.Equal(AttendanceCaptureSource.OcrUpload, updatedAttendance.Source);
        Assert.Contains(
            context.ActivityLogs.Select(log => log.Action).ToArray(),
            action => string.Equals(action, "CashForWorkAttendanceUpdated", StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteAttendance_RemovesSelectedAttendance_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 7001, "Pedro Santos");
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = service.CreateEvent(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            null,
            admin.Id);

        service.AddParticipant(cashForWorkEvent.Id, beneficiary.StagingID, admin.Id);
        var participantId = context.CashForWorkParticipants.Single().Id;
        service.SaveManualAttendance(cashForWorkEvent.Id, admin.Id, [participantId]);
        var attendanceId = context.CashForWorkAttendances.Single().Id;

        service.DeleteAttendance(attendanceId, admin.Id);

        Assert.Empty(context.CashForWorkAttendances);
        Assert.Contains(
            context.ActivityLogs.Select(log => log.Action).ToArray(),
            action => string.Equals(action, "CashForWorkAttendanceDeleted", StringComparison.Ordinal));
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

    private static BeneficiaryStaging SeedApprovedBeneficiary(
        Data.AppDbContext context,
        int stagingId = 1001,
        string fullName = "Pedro Santos",
        VerificationStatus verificationStatus = VerificationStatus.Approved)
    {
        var beneficiary = new BeneficiaryStaging
        {
            StagingID = stagingId,
            BeneficiaryId = $"BEN-{stagingId:D4}",
            CivilRegistryId = $"CR-{stagingId:D4}",
            FullName = fullName,
            VerificationStatus = verificationStatus,
            ReviewedAt = verificationStatus == VerificationStatus.Approved ? DateTime.Now : null
        };

        context.BeneficiaryStaging.Add(beneficiary);
        context.SaveChanges();
        return beneficiary;
    }

    private static int GetBeneficiaryStagingId(CashForWorkParticipant participant)
    {
        var property = typeof(CashForWorkParticipant).GetProperty("BeneficiaryStagingId");
        Assert.NotNull(property);
        return (int)(property!.GetValue(participant) ?? 0);
    }

    private static string? GetStringProperty<T>(T instance, string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.NotNull(property);
        return property!.GetValue(instance)?.ToString();
    }
}

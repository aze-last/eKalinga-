using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkServiceTests
{
    [Fact]
    public async Task WriteOperations_AddAuditLogEntries()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context);
        var auditService = new AuditService(context);
        var service = new CashForWorkService(context, auditService);

        var cashForWorkEvent = await service.CreateEventAsync(
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

        Assert.Contains("CashForWorkEventCreated", actions);
        Assert.Contains("CashForWorkParticipantAdded", actions);
        Assert.Contains("CashForWorkManualAttendanceSaved", actions);
    }

    [Fact]
    public async Task GetReleaseReadySummary_ReturnsAttendanceTotalsForSelectedEvent()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var manualBeneficiary = SeedApprovedBeneficiary(context, 2001, "Pedro Santos");
        var secondManualBeneficiary = SeedApprovedBeneficiary(context, 2002, "Ana Santos");
        var pendingBeneficiary = SeedApprovedBeneficiary(context, 2003, "Luis Santos");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = await service.CreateEventAsync(
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

        var cashForWorkEvent = await service.CreateEventAsync(
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
    public async Task SaveScannerAttendance_ForSeminar_AutoRegistersAttendeeFromQr()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 3002, "Seminar Attendee");
        var service = new CashForWorkService(context, new AuditService(context));
        var digitalIdService = new BeneficiaryDigitalIdService(context);

        var seminarEvent = await service.CreateEventAsync(
            "Barangay Seminar",
            "Session Hall",
            DateTime.Today,
            new TimeSpan(9, 0, 0),
            new TimeSpan(11, 0, 0),
            null,
            admin.Id,
            0m,
            CashForWorkEventKind.Seminar);

        var digitalId = await digitalIdService.EnsureIssuedAsync(beneficiary.StagingID, admin.Id);

        Assert.True(await service.SaveScannerAttendanceAsync(seminarEvent.Id, admin.Id, null, digitalId.QrPayload));

        var participant = Assert.Single(context.CashForWorkParticipants);
        Assert.Equal(beneficiary.StagingID, GetBeneficiaryStagingId(participant));

        var attendance = Assert.Single(context.CashForWorkAttendances);
        Assert.Equal(participant.Id, attendance.ParticipantId);
        Assert.Equal(AttendanceCaptureSource.ScannerSession, attendance.Source);

        Assert.Contains(context.ActivityLogs, log => log.Action == "CashForWorkParticipantAutoAdded");
    }

    [Fact]
    public async Task AddParticipant_RejectsBeneficiariesThatAreNotApproved()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4001, "Pending Beneficiary", VerificationStatus.Pending);
        var service = new CashForWorkService(context);

        var cashForWorkEvent = await service.CreateEventAsync(
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
    public async Task AddParticipant_RejectsReleasedEvents()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4101, "Released Participant");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = await service.CreateEventAsync(
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
    public async Task AddParticipant_ForSeminar_RejectsManualAssignment()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4102, "Seminar Assignment");
        var service = new CashForWorkService(context);

        var seminarEvent = await service.CreateEventAsync(
            "Open Seminar",
            "Hall",
            DateTime.Today,
            new TimeSpan(8, 0, 0),
            new TimeSpan(10, 0, 0),
            null,
            admin.Id,
            0m,
            CashForWorkEventKind.Seminar);

        var ex = Assert.Throws<InvalidOperationException>(() => service.AddParticipant(seminarEvent.Id, beneficiary.StagingID, admin.Id));
        Assert.Contains("scan-based", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveManualAttendance_RejectsReleasedEvents()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4201, "Released Attendance");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = await service.CreateEventAsync(
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
    public async Task SaveManualAttendance_ForSeminar_RejectsManualRecording()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4202, "Seminar Manual");
        var service = new CashForWorkService(context);

        var seminarEvent = await service.CreateEventAsync(
            "Attendance Seminar",
            "Hall",
            DateTime.Today,
            new TimeSpan(8, 0, 0),
            new TimeSpan(10, 0, 0),
            null,
            admin.Id,
            0m,
            CashForWorkEventKind.Seminar);

        context.CashForWorkParticipants.Add(new CashForWorkParticipant
        {
            EventId = seminarEvent.Id,
            BeneficiaryStagingId = beneficiary.StagingID,
            AddedByUserId = admin.Id,
            AddedAt = DateTime.Now
        });
        context.SaveChanges();

        var participantId = context.CashForWorkParticipants.Single().Id;
        var ex = Assert.Throws<InvalidOperationException>(() => service.SaveManualAttendance(seminarEvent.Id, admin.Id, [participantId]));
        Assert.Contains("scan-based", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveScannerAttendance_RejectsReleasedEvents()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 4301, "Released Scanner");
        var service = new CashForWorkService(context);

        var cashForWorkEvent = await service.CreateEventAsync(
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
    public async Task ReleaseEventAsync_ForSeminar_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new CashForWorkService(context);

        var seminarEvent = await service.CreateEventAsync(
            "Attendance Seminar",
            "Hall",
            DateTime.Today,
            new TimeSpan(8, 0, 0),
            new TimeSpan(10, 0, 0),
            null,
            admin.Id,
            0m,
            CashForWorkEventKind.Seminar);

        var result = await service.ReleaseEventAsync(seminarEvent.Id, 1000m, admin.Id, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("attendance-only", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateEvent_UpdatesSelectedEvent_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = await service.CreateEventAsync(
            "Barangay Clean-Up",
            "Hall",
            DateTime.Today,
            new TimeSpan(7, 0, 0),
            new TimeSpan(12, 0, 0),
            "Initial notes",
            admin.Id);

        var updatedEvent = await service.UpdateEventAsync(
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
    public async Task DeleteEvent_RemovesEventParticipantsAttendanceAndScannerSessions()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 5001, "Pedro Santos");
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = await service.CreateEventAsync(
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

        Assert.Empty(context.CashForWorkAttendances.Where(x => !x.IsDeleted));
        Assert.Empty(context.CashForWorkParticipants.Where(x => !x.IsDeleted));
        Assert.Empty(context.CashForWorkEvents.Where(x => !x.IsDeleted));
        Assert.Empty(context.ScannerSessions.Where(x => x.IsActive));
        Assert.Contains(
            context.ActivityLogs.Select(log => log.Action).ToArray(),
            action => string.Equals(action, "CashForWorkEventDeleted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateAttendance_UpdatesSelectedAttendance_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 6001, "Pedro Santos");
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = await service.CreateEventAsync(
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
    public async Task DeleteAttendance_RemovesSelectedAttendance_AndWritesAuditLog()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 7001, "Pedro Santos");
        var service = new CashForWorkService(context, new AuditService(context));

        var cashForWorkEvent = await service.CreateEventAsync(
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

        Assert.Empty(context.CashForWorkAttendances.Where(x => !x.IsDeleted));
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

    private static AyudaProgram SeedProgram(Data.AppDbContext context, int createdByUserId, AyudaProgramType programType)
    {
        var programCode = $"CFW-{Guid.NewGuid():N}";
        var program = new AyudaProgram
        {
            ProgramCode = programCode[..Math.Min(20, programCode.Length)],
            ProgramName = $"{programType} Program",
            ProgramType = programType,
            Description = "Budget release program",
            CreatedByUserId = createdByUserId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        context.AyudaPrograms.Add(program);
        context.SaveChanges();
        return program;
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

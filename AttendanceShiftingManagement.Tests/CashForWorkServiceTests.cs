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
    public void SaveScannerAttendance_UsesScannerSource_AndPreventsDuplicateAttendance()
    {
        using var context = TestDbContextFactory.CreateContext();
        var admin = SeedAdmin(context);
        var beneficiary = SeedApprovedBeneficiary(context, 3001, "Pedro Santos");
        var service = new CashForWorkService(context);

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

        Assert.True(service.SaveScannerAttendance(cashForWorkEvent.Id, admin.Id, participantId, "BEN-QR-001"));
        Assert.False(service.SaveScannerAttendance(cashForWorkEvent.Id, admin.Id, participantId, "BEN-QR-001"));

        var attendance = Assert.Single(context.CashForWorkAttendances);
        Assert.Equal(AttendanceCaptureSource.ScannerSession, attendance.Source);
        Assert.Equal("BEN-QR-001", attendance.OcrExtractedName);
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

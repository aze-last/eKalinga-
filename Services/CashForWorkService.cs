using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record CashForWorkReleaseOperationResult(bool IsSuccess, string Message, int? BudgetLedgerEntryId = null);
    public sealed record CashForWorkEligibleBeneficiary(
        int BeneficiaryStagingId,
        string FullName,
        string? BeneficiaryId,
        string? CivilRegistryId);

    public sealed class CashForWorkService
    {
        private readonly AppDbContext _context;
        private readonly AuditService? _auditService;

        public CashForWorkService(AppDbContext context, AuditService? auditService = null)
        {
            _context = context;
            _auditService = auditService;
        }

        public List<CashForWorkEvent> GetEvents()
        {
            return _context.CashForWorkEvents
                .AsNoTracking()
                .OrderByDescending(e => e.EventDate)
                .ThenBy(e => e.Title)
                .ToList();
        }

        public List<CashForWorkEligibleBeneficiary> GetEligibleBeneficiaries()
        {
            return _context.BeneficiaryStaging
                .AsNoTracking()
                .Where(beneficiary => beneficiary.VerificationStatus == VerificationStatus.Approved)
                .OrderBy(beneficiary => beneficiary.FullName ?? beneficiary.LastName)
                .ThenBy(beneficiary => beneficiary.FirstName)
                .Select(beneficiary => new CashForWorkEligibleBeneficiary(
                    beneficiary.StagingID,
                    BuildDisplayName(beneficiary),
                    NormalizeNullable(beneficiary.BeneficiaryId),
                    NormalizeNullable(beneficiary.CivilRegistryId)))
                .ToList();
        }

        public List<CashForWorkParticipant> GetParticipants(int eventId)
        {
            return _context.CashForWorkParticipants
                .AsNoTracking()
                .Include(participant => participant.Beneficiary)
                .Where(participant => participant.EventId == eventId)
                .ToList()
                .OrderBy(participant => BuildParticipantDisplayName(participant))
                .ToList();
        }

        public List<CashForWorkAttendance> GetAttendanceRecords(int eventId)
        {
            return _context.CashForWorkAttendances
                .AsNoTracking()
                .Include(attendance => attendance.Participant)
                .ThenInclude(participant => participant.Beneficiary)
                .Include(attendance => attendance.RecordedByUser)
                .Where(attendance => attendance.Participant.EventId == eventId)
                .ToList()
                .OrderByDescending(attendance => attendance.RecordedAt)
                .ToList();
        }

        public CashForWorkReleaseReadySummary GetReleaseReadySummary(int eventId)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            var approvedParticipants = GetParticipants(eventId);
            var presentAttendance = GetAttendanceRecords(eventId)
                .Where(attendance =>
                    attendance.AttendanceDate.Date == cashForWorkEvent.EventDate.Date &&
                    attendance.Status == CashForWorkAttendanceStatus.Present)
                .GroupBy(attendance => attendance.ParticipantId)
                .Select(group => group
                    .OrderByDescending(attendance => attendance.RecordedAt)
                    .First())
                .Where(attendance => 
                    attendance.Participant.Beneficiary?.VerificationStatus == VerificationStatus.Approved)
                .OrderBy(attendance => BuildParticipantDisplayName(attendance.Participant))
                .ToList();

            var releaseReadyParticipants = presentAttendance
                .Select(attendance => new CashForWorkReleaseReadyParticipant(
                    attendance.ParticipantId,
                    BuildParticipantDisplayName(attendance.Participant),
                    NormalizeNullable(attendance.Participant.Beneficiary?.BeneficiaryId),
                    NormalizeNullable(attendance.Participant.Beneficiary?.CivilRegistryId),
                    attendance.Source,
                    attendance.RecordedAt))
                .ToList();

            var releaseReadyParticipantCount = releaseReadyParticipants.Count;

            return new CashForWorkReleaseReadySummary(
                cashForWorkEvent.Id,
                cashForWorkEvent.Title,
                cashForWorkEvent.EventDate,
                cashForWorkEvent.Location,
                cashForWorkEvent.Status,
                approvedParticipants.Count,
                releaseReadyParticipantCount,
                Math.Max(0, approvedParticipants.Count - releaseReadyParticipantCount),
                releaseReadyParticipantCount,
                releaseReadyParticipants.Count(participant => participant.Source == AttendanceCaptureSource.Manual),
                releaseReadyParticipants);
        }

        public CashForWorkEvent CreateEvent(string title, string location, DateTime eventDate, TimeSpan startTime, TimeSpan endTime, string? notes, int createdByUserId)
        {
            var cashForWorkEvent = new CashForWorkEvent
            {
                Title = title.Trim(),
                Location = location.Trim(),
                EventDate = eventDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                CreatedByUserId = createdByUserId,
                Status = CashForWorkEventStatus.Open,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.CashForWorkEvents.Add(cashForWorkEvent);
            _context.SaveChanges();
            _auditService?.LogActivity(
                createdByUserId,
                "CashForWorkEventCreated",
                "CashForWorkEvent",
                cashForWorkEvent.Id,
                $"Created event '{cashForWorkEvent.Title}' on {cashForWorkEvent.EventDate:yyyy-MM-dd} at '{cashForWorkEvent.Location}'.");

            return cashForWorkEvent;
        }

        public void AddParticipant(int eventId, int beneficiaryStagingId, int addedByUserId)
        {
            var beneficiary = _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefault(item => item.StagingID == beneficiaryStagingId)
                ?? throw new InvalidOperationException("Approved beneficiary could not be found.");

            if (beneficiary.VerificationStatus != VerificationStatus.Approved)
            {
                throw new InvalidOperationException("Only approved beneficiaries can be included in this event.");
            }

            var exists = _context.CashForWorkParticipants.Any(participant =>
                participant.EventId == eventId &&
                participant.BeneficiaryStagingId == beneficiaryStagingId);

            if (exists)
            {
                throw new InvalidOperationException("Beneficiary is already included in this event.");
            }

            _context.CashForWorkParticipants.Add(new CashForWorkParticipant
            {
                EventId = eventId,
                BeneficiaryStagingId = beneficiaryStagingId,
                AddedByUserId = addedByUserId,
                AddedAt = DateTime.Now
            });

            _context.SaveChanges();

            var participantName = BuildDisplayName(beneficiary);

            var eventTitle = GetEventTitle(eventId);
            _auditService?.LogActivity(
                addedByUserId,
                "CashForWorkParticipantAdded",
                "CashForWorkEvent",
                eventId,
                $"Added participant '{participantName}' to event '{eventTitle}'.");
        }

        public int SaveManualAttendance(int eventId, int recordedByUserId, IEnumerable<int> participantIds)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");
            if (cashForWorkEvent.EventDate.Date > DateTime.Today)
            {
                throw new InvalidOperationException("Attendance cannot be recorded for future events.");
            }

            var selectedParticipantIds = participantIds
                .Distinct()
                .ToList();
            var validParticipantIds = GetValidParticipantIds(eventId);
            var recordedParticipantIds = GetRecordedParticipantIds(eventId, cashForWorkEvent.EventDate.Date);

            foreach (var participantId in selectedParticipantIds)
            {
                if (!validParticipantIds.Contains(participantId) || !recordedParticipantIds.Add(participantId))
                {
                    continue;
                }

                _context.CashForWorkAttendances.Add(new CashForWorkAttendance
                {
                    ParticipantId = participantId,
                    AttendanceDate = cashForWorkEvent.EventDate.Date,
                    Status = CashForWorkAttendanceStatus.Present,
                    Source = AttendanceCaptureSource.Manual,
                    RecordedByUserId = recordedByUserId,
                    RecordedAt = DateTime.Now
                });
            }

            var savedCount = _context.SaveChanges();
            _auditService?.LogActivity(
                recordedByUserId,
                "CashForWorkManualAttendanceSaved",
                "CashForWorkEvent",
                eventId,
                $"Saved {savedCount} manual attendance record(s) for event '{GetEventTitle(eventId)}'.");

            return savedCount;
        }

        public async Task<bool> SaveScannerAttendanceAsync(int eventId, int recordedByUserId, int participantId, string qrPayload)
        {
            var cashForWorkEvent = await _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == eventId);

            if (cashForWorkEvent == null)
            {
                throw new InvalidOperationException("Cash-for-work event was not found.");
            }
            if (cashForWorkEvent.EventDate.Date > DateTime.Today)
            {
                throw new InvalidOperationException("Attendance cannot be recorded for future events.");
            }

            var participant = await _context.CashForWorkParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == participantId && item.EventId == eventId);

            if (participant == null || participant.Beneficiary?.VerificationStatus != VerificationStatus.Approved)
            {
                return false;
            }

            // Verify identity via QR payload
            var digitalIdService = new BeneficiaryDigitalIdService(_context);
            var lookupResult = await digitalIdService.LookupByQrPayloadAsync(qrPayload);

            if (lookupResult == null || lookupResult.BeneficiaryStagingId != participant.BeneficiaryStagingId)
            {
                // Identity mismatch or invalid QR
                return false;
            }

            var attendanceDate = cashForWorkEvent.EventDate.Date;
            var alreadyRecorded = await _context.CashForWorkAttendances
                .AnyAsync(item => item.ParticipantId == participantId && item.AttendanceDate == attendanceDate);

            if (alreadyRecorded)
            {
                return false;
            }

            _context.CashForWorkAttendances.Add(new CashForWorkAttendance
            {
                ParticipantId = participantId,
                AttendanceDate = attendanceDate,
                Status = CashForWorkAttendanceStatus.Present,
                Source = AttendanceCaptureSource.ScannerSession,
                OcrExtractedName = qrPayload.Trim(),
                RecordedByUserId = recordedByUserId,
                RecordedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            
            _auditService?.LogActivity(
                recordedByUserId,
                "CashForWorkScannerAttendanceSaved",
                "CashForWorkEvent",
                eventId,
                $"Saved scanner attendance for participant '{lookupResult.FullName}' (staging #{participant.BeneficiaryStagingId}) in event '{cashForWorkEvent.Title}'.");

            return true;
        }

        public CashForWorkEvent UpdateEvent(int eventId, string title, string location, DateTime eventDate, TimeSpan startTime, TimeSpan endTime, string? notes, int updatedByUserId)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .FirstOrDefault(item => item.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            EnsureEventCanBeModified(cashForWorkEvent);

            cashForWorkEvent.Title = title.Trim();
            cashForWorkEvent.Location = location.Trim();
            cashForWorkEvent.EventDate = eventDate.Date;
            cashForWorkEvent.StartTime = startTime;
            cashForWorkEvent.EndTime = endTime;
            cashForWorkEvent.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            cashForWorkEvent.UpdatedAt = DateTime.Now;

            var attendanceRows = _context.CashForWorkAttendances
                .Include(attendance => attendance.Participant)
                .Where(attendance => attendance.Participant.EventId == eventId)
                .ToList();

            foreach (var attendance in attendanceRows)
            {
                attendance.AttendanceDate = eventDate.Date;
            }

            _context.SaveChanges();
            _auditService?.LogActivity(
                updatedByUserId,
                "CashForWorkEventUpdated",
                "CashForWorkEvent",
                cashForWorkEvent.Id,
                $"Updated event '{cashForWorkEvent.Title}' scheduled on {cashForWorkEvent.EventDate:yyyy-MM-dd}.");

            return cashForWorkEvent;
        }

        public void DeleteEvent(int eventId, int deletedByUserId)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .FirstOrDefault(item => item.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            EnsureEventCanBeModified(cashForWorkEvent);

            var eventTitle = cashForWorkEvent.Title;

            var scannerSessions = _context.ScannerSessions
                .Where(session => session.CashForWorkEventId == eventId)
                .ToList();

            var participants = _context.CashForWorkParticipants
                .Where(participant => participant.EventId == eventId)
                .ToList();

            var participantIds = participants
                .Select(participant => participant.Id)
                .ToList();

            var attendances = _context.CashForWorkAttendances
                .Where(attendance => participantIds.Contains(attendance.ParticipantId))
                .ToList();

            if (attendances.Count > 0)
            {
                _context.CashForWorkAttendances.RemoveRange(attendances);
            }

            if (participants.Count > 0)
            {
                _context.CashForWorkParticipants.RemoveRange(participants);
            }

            if (scannerSessions.Count > 0)
            {
                _context.ScannerSessions.RemoveRange(scannerSessions);
            }

            _context.CashForWorkEvents.Remove(cashForWorkEvent);
            _context.SaveChanges();

            _auditService?.LogActivity(
                deletedByUserId,
                "CashForWorkEventDeleted",
                "CashForWorkEvent",
                eventId,
                $"Deleted event '{eventTitle}' and its related attendance records.");
        }

        public CashForWorkAttendance UpdateAttendance(int attendanceId, DateTime attendanceDate, CashForWorkAttendanceStatus status, AttendanceCaptureSource source, int updatedByUserId)
        {
            var attendance = _context.CashForWorkAttendances
                .Include(item => item.Participant)
                .ThenInclude(participant => participant.Event)
                .FirstOrDefault(item => item.Id == attendanceId)
                ?? throw new InvalidOperationException("Attendance record was not found.");

            EnsureEventCanBeModified(attendance.Participant.Event);

            attendance.AttendanceDate = attendanceDate.Date;
            attendance.Status = status;
            attendance.Source = source;
            attendance.RecordedByUserId = updatedByUserId;
            attendance.RecordedAt = DateTime.Now;

            if (source == AttendanceCaptureSource.Manual)
            {
                attendance.OcrExtractedName = null;
            }

            _context.SaveChanges();
            _auditService?.LogActivity(
                updatedByUserId,
                "CashForWorkAttendanceUpdated",
                "CashForWorkAttendance",
                attendance.Id,
                $"Updated attendance #{attendance.Id} for event '{attendance.Participant.Event.Title}'.");

            return attendance;
        }

        public void DeleteAttendance(int attendanceId, int deletedByUserId)
        {
            var attendance = _context.CashForWorkAttendances
                .Include(item => item.Participant)
                .ThenInclude(participant => participant.Event)
                .FirstOrDefault(item => item.Id == attendanceId)
                ?? throw new InvalidOperationException("Attendance record was not found.");

            EnsureEventCanBeModified(attendance.Participant.Event);

            var eventTitle = attendance.Participant.Event.Title;

            _context.CashForWorkAttendances.Remove(attendance);
            _context.SaveChanges();
            _auditService?.LogActivity(
                deletedByUserId,
                "CashForWorkAttendanceDeleted",
                "CashForWorkAttendance",
                attendanceId,
                $"Deleted attendance #{attendanceId} for event '{eventTitle}'.");
        }

        public async Task<CashForWorkReleaseOperationResult> ReleaseEventAsync(
            int eventId,
            int ayudaProgramId,
            decimal totalAmount,
            int recordedByUserId,
            string? remarks)
        {
            if (totalAmount <= 0)
            {
                return new CashForWorkReleaseOperationResult(false, "Release amount must be greater than zero.");
            }

            var cashForWorkEvent = await _context.CashForWorkEvents
                .FirstOrDefaultAsync(item => item.Id == eventId);

            if (cashForWorkEvent == null)
            {
                return new CashForWorkReleaseOperationResult(false, "Cash-for-work event was not found.");
            }
            if (cashForWorkEvent.EventDate.Date > DateTime.Today)
            {
                return new CashForWorkReleaseOperationResult(false, "Cash-for-work events can only be released on or after the event date.");
            }

            if (cashForWorkEvent.BudgetLedgerEntryId.HasValue)
            {
                return new CashForWorkReleaseOperationResult(false, "This cash-for-work event already has a recorded release.");
            }

            var releaseReadySummary = GetReleaseReadySummary(eventId);
            if (releaseReadySummary.ReleaseReadyParticipantCount <= 0)
            {
                return new CashForWorkReleaseOperationResult(false, "Save attendance before releasing cash-for-work funds.");
            }

            var budgetService = new BudgetManagementService(_context, _auditService);
            var budgetResult = await budgetService.RecordReleaseAsync(
                new BudgetReleaseRequest(
                    ayudaProgramId,
                    BudgetLedgerFeatureSource.CashForWork,
                    $"cash-for-work:{cashForWorkEvent.Id}",
                    releaseReadySummary.ReleaseReadyParticipantCount,
                    AssistanceReleaseKind.Cash,
                    totalAmount,
                    DateTime.Now,
                    remarks ?? cashForWorkEvent.Title),
                recordedByUserId);

            if (!budgetResult.IsSuccess)
            {
                return new CashForWorkReleaseOperationResult(false, budgetResult.Message);
            }

            cashForWorkEvent.AyudaProgramId = ayudaProgramId;
            cashForWorkEvent.BudgetLedgerEntryId = budgetResult.LedgerEntryId;
            cashForWorkEvent.ReleaseAmount = totalAmount;
            cashForWorkEvent.ReleasedAt = DateTime.Now;
            cashForWorkEvent.Status = CashForWorkEventStatus.Completed;
            cashForWorkEvent.UpdatedAt = DateTime.Now;

            // Sync with beneficiary assistance history for each present participant
            var historyService = new BeneficiaryAssistanceLedgerService(_context, _auditService);
            var participantsToRecord = await _context.CashForWorkParticipants
                .AsNoTracking()
                .Include(p => p.Beneficiary)
                .Where(p => p.EventId == eventId)
                .ToListAsync();

            var presentParticipantIds = releaseReadySummary.ReleaseReadyParticipants
                .Select(p => p.ParticipantId)
                .ToHashSet();

            foreach (var participant in participantsToRecord)
            {
                if (!presentParticipantIds.Contains(participant.Id)) continue;

                await historyService.RecordEntryAsync(
                    NormalizeNullable(participant.Beneficiary?.CivilRegistryId),
                    NormalizeNullable(participant.Beneficiary?.BeneficiaryId),
                    BeneficiaryAssistanceSourceModule.CashForWork,
                    $"cfw-payout:{eventId}:{participant.Id}",
                    DateTime.Now,
                    totalAmount / releaseReadySummary.ReleaseReadyParticipantCount,
                    $"Cash-for-work payout for '{cashForWorkEvent.Title}'.",
                    recordedByUserId);
            }

            await _context.SaveChangesAsync();

            if (_auditService != null)
            {
                await _auditService.LogActivityAsync(
                    recordedByUserId,
                    "CashForWorkReleased",
                    "CashForWorkEvent",
                    cashForWorkEvent.Id,
                    $"Released {totalAmount:N2} for event '{cashForWorkEvent.Title}'.");
            }

            return new CashForWorkReleaseOperationResult(true, "Cash-for-work release recorded.", budgetResult.LedgerEntryId);
        }

        private string GetEventTitle(int eventId)
        {
            return _context.CashForWorkEvents
                .AsNoTracking()
                .Where(cashForWorkEvent => cashForWorkEvent.Id == eventId)
                .Select(cashForWorkEvent => cashForWorkEvent.Title)
                .FirstOrDefault() ?? $"event #{eventId}";
        }

        private static void EnsureEventCanBeModified(CashForWorkEvent cashForWorkEvent)
        {
            if (cashForWorkEvent.BudgetLedgerEntryId.HasValue || cashForWorkEvent.Status == CashForWorkEventStatus.Completed)
            {
                throw new InvalidOperationException("Released cash-for-work events can no longer be modified.");
            }
        }

        private HashSet<int> GetValidParticipantIds(int eventId)
        {
            return _context.CashForWorkParticipants
                .AsNoTracking()
                .Include(p => p.Beneficiary)
                .Where(participant => 
                    participant.EventId == eventId && 
                    participant.Beneficiary.VerificationStatus == VerificationStatus.Approved)
                .Select(participant => participant.Id)
                .ToHashSet();
        }

        private HashSet<int> GetRecordedParticipantIds(int eventId, DateTime attendanceDate)
        {
            return _context.CashForWorkAttendances
                .AsNoTracking()
                .Where(attendance =>
                    attendance.Participant.EventId == eventId &&
                    attendance.AttendanceDate.Date == attendanceDate.Date)
                .Select(attendance => attendance.ParticipantId)
                .ToHashSet();
        }

        private static string BuildDisplayName(BeneficiaryStaging beneficiary)
        {
            if (!string.IsNullOrWhiteSpace(beneficiary.FullName))
            {
                return beneficiary.FullName.Trim();
            }

            return string.Join(
                " ",
                new[] { beneficiary.FirstName, beneficiary.MiddleName, beneficiary.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
        }

        private static string BuildParticipantDisplayName(CashForWorkParticipant participant)
        {
            if (participant.Beneficiary != null)
            {
                return BuildDisplayName(participant.Beneficiary);
            }

            return $"Beneficiary #{participant.BeneficiaryStagingId?.ToString() ?? "legacy"}";
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public sealed record CashForWorkReleaseReadySummary(
        int EventId,
        string EventTitle,
        DateTime EventDate,
        string Location,
        CashForWorkEventStatus EventStatus,
        int ApprovedParticipantCount,
        int PresentParticipantCount,
        int PendingParticipantCount,
        int ReleaseReadyParticipantCount,
        int ManualAttendanceCount,
        IReadOnlyList<CashForWorkReleaseReadyParticipant> ReleaseReadyParticipants);

    public sealed record CashForWorkReleaseReadyParticipant(
        int ParticipantId,
        string FullName,
        string? BeneficiaryId,
        string? CivilRegistryId,
        AttendanceCaptureSource Source,
        DateTime RecordedAt);

}

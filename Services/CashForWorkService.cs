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
        private readonly IGgmsConsolidatedTransactionService _ggmsConsolidatedTransactionService;

        public CashForWorkService(
            AppDbContext context,
            AuditService? auditService = null,
            IGgmsConsolidatedTransactionService? ggmsConsolidatedTransactionService = null)
        {
            _context = context;
            _auditService = auditService;
            _ggmsConsolidatedTransactionService = ggmsConsolidatedTransactionService ?? NullGgmsConsolidatedTransactionService.Instance;
        }

        public List<CashForWorkEvent> GetEvents(CashForWorkEventKind? eventKind = null)
        {
            var query = _context.CashForWorkEvents
                .AsNoTracking()
                .Where(cashForWorkEvent => !cashForWorkEvent.IsDeleted)
                .AsQueryable();

            if (eventKind.HasValue)
            {
                query = query.Where(cashForWorkEvent => cashForWorkEvent.EventKind == eventKind.Value);
            }

            return query
                .OrderByDescending(e => e.EventDate)
                .ThenBy(e => e.Title)
                .ToList();
        }

        public List<CashForWorkEvent> GetOpenEvents()
        {
            return _context.CashForWorkEvents
                .AsNoTracking()
                .Where(cashForWorkEvent => !cashForWorkEvent.IsDeleted && cashForWorkEvent.Status == CashForWorkEventStatus.Open)
                .OrderBy(cashForWorkEvent => cashForWorkEvent.EventDate)
                .ThenBy(cashForWorkEvent => cashForWorkEvent.StartTime)
                .ThenBy(cashForWorkEvent => cashForWorkEvent.Title)
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
                .Where(participant => !participant.IsDeleted && participant.EventId == eventId)
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
                .Where(attendance => !attendance.IsDeleted && attendance.Participant.EventId == eventId)
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
                releaseReadyParticipantCount * (cashForWorkEvent.UnitAmount > 0 ? cashForWorkEvent.UnitAmount : (cashForWorkEvent.AyudaProgram?.UnitAmount ?? 0m)),
                releaseReadyParticipants);
        }

        public async Task<CashForWorkEvent> CreateEventAsync(
            string title,
            string location,
            DateTime eventDate,
            TimeSpan startTime,
            TimeSpan endTime,
            string? notes,
            int createdByUserId,
            decimal unitAmount = 0m,
            CashForWorkEventKind eventKind = CashForWorkEventKind.CashForWork,
            DateTime? finishDate = null,
            CashForWorkBenefitType benefitType = CashForWorkBenefitType.None,
            string? benefitDescription = null)
        {
            var resolvedBudgetId = await ResolveGlobalBudgetAsync();

            var cashForWorkEvent = new CashForWorkEvent
            {
                Title = title.Trim(),
                Location = location.Trim(),
                EventDate = eventDate.Date,
                FinishDate = finishDate?.Date,
                StartTime = startTime,
                EndTime = endTime,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                CreatedByUserId = createdByUserId,
                Status = CashForWorkEventStatus.Open,
                EventKind = eventKind,
                BenefitType = benefitType,
                BenefitDescription = benefitDescription,
                UnitAmount = unitAmount,
                CashForWorkBudgetId = resolvedBudgetId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.CashForWorkEvents.Add(cashForWorkEvent);
            await _context.SaveChangesAsync();

            if (_auditService != null)
            {
                await _auditService.LogActivityAsync(
                    createdByUserId,
                    "CashForWorkEventCreated",
                    "CashForWorkEvent",
                    cashForWorkEvent.Id,
                    $"Created event '{cashForWorkEvent.Title}' on {cashForWorkEvent.EventDate:yyyy-MM-dd}.");
            }

            return cashForWorkEvent;
        }

        public void AddParticipant(int eventId, int beneficiaryStagingId, int addedByUserId)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .FirstOrDefault(item => item.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            EnsureEventCanBeModified(cashForWorkEvent);

            if (cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                throw new InvalidOperationException("Seminar attendance is scan-based and does not use beneficiary assignment.");
            }

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

            EnsureEventCanBeModified(cashForWorkEvent);

            if (cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                throw new InvalidOperationException("Seminar attendance is scan-based only.");
            }

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

        public async Task<bool> SaveScannerAttendanceAsync(int eventId, int recordedByUserId, int? participantId, string qrPayload)
        {
            var cashForWorkEvent = await _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == eventId);

            if (cashForWorkEvent == null)
            {
                throw new InvalidOperationException("Cash-for-work event was not found.");
            }

            EnsureEventCanBeModified(cashForWorkEvent);

            if (cashForWorkEvent.EventDate.Date > DateTime.Today)
            {
                throw new InvalidOperationException("Attendance cannot be recorded for future events.");
            }

            // Verify identity via QR payload
            var digitalIdService = new BeneficiaryDigitalIdService(_context);
            var lookupResult = await digitalIdService.LookupByQrPayloadAsync(qrPayload);

            if (lookupResult == null)
            {
                return false;
            }

            var participant = await ResolveScannerParticipantAsync(
                cashForWorkEvent,
                participantId,
                lookupResult.BeneficiaryStagingId,
                recordedByUserId);

            if (participant == null || participant.BeneficiaryStagingId != lookupResult.BeneficiaryStagingId)
            {
                return false;
            }

            var attendanceDate = cashForWorkEvent.EventDate.Date;
            var alreadyRecorded = await _context.CashForWorkAttendances
                .AnyAsync(item => item.ParticipantId == participant.Id && item.AttendanceDate == attendanceDate);

            if (alreadyRecorded)
            {
                return false;
            }

            _context.CashForWorkAttendances.Add(new CashForWorkAttendance
            {
                ParticipantId = participant.Id,
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

        private async Task<CashForWorkParticipant?> ResolveScannerParticipantAsync(
            CashForWorkEvent cashForWorkEvent,
            int? participantId,
            int beneficiaryStagingId,
            int recordedByUserId)
        {
            CashForWorkParticipant? participant = null;

            if (participantId.HasValue)
            {
                participant = await _context.CashForWorkParticipants
                    .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == participantId.Value && item.EventId == cashForWorkEvent.Id);
            }

            if (participant != null)
            {
                var participantBeneficiary = await _context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.StagingID == participant.BeneficiaryStagingId);

                return participantBeneficiary?.VerificationStatus == VerificationStatus.Approved
                    ? participant
                    : null;
            }

            if (cashForWorkEvent.EventKind != CashForWorkEventKind.Seminar)
            {
                return null;
            }

            var existingParticipant = await _context.CashForWorkParticipants
                .FirstOrDefaultAsync(item =>
                    !item.IsDeleted &&
                    item.EventId == cashForWorkEvent.Id &&
                    item.BeneficiaryStagingId == beneficiaryStagingId);

            if (existingParticipant != null)
            {
                return existingParticipant;
            }

            var beneficiary = await _context.BeneficiaryStaging
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StagingID == beneficiaryStagingId);

            if (beneficiary == null || beneficiary.VerificationStatus != VerificationStatus.Approved)
            {
                return null;
            }

            participant = new CashForWorkParticipant
            {
                EventId = cashForWorkEvent.Id,
                BeneficiaryStagingId = beneficiaryStagingId,
                AddedByUserId = recordedByUserId,
                AddedAt = DateTime.Now
            };

            _context.CashForWorkParticipants.Add(participant);
            await _context.SaveChangesAsync();

            _auditService?.LogActivity(
                recordedByUserId,
                "CashForWorkParticipantAutoAdded",
                "CashForWorkEvent",
                cashForWorkEvent.Id,
                $"Auto-registered seminar attendee '{BuildDisplayName(beneficiary)}' for event '{cashForWorkEvent.Title}'.");

            return participant;
        }

        public async Task<CashForWorkEvent> UpdateEventAsync(
            int eventId,
            string title,
            string location,
            DateTime eventDate,
            TimeSpan startTime,
            TimeSpan endTime,
            string? notes,
            int updatedByUserId,
            decimal unitAmount = 0m,
            CashForWorkEventKind eventKind = CashForWorkEventKind.CashForWork,
            DateTime? finishDate = null,
            CashForWorkBenefitType benefitType = CashForWorkBenefitType.None,
            string? benefitDescription = null)
        {
            var cashForWorkEvent = await _context.CashForWorkEvents
                .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == eventId);

            if (cashForWorkEvent == null)
            {
                throw new InvalidOperationException("Cash-for-work event was not found.");
            }

            EnsureEventCanBeModified(cashForWorkEvent);

            var resolvedBudgetId = await ResolveGlobalBudgetAsync();

            cashForWorkEvent.Title = title.Trim();
            cashForWorkEvent.Location = location.Trim();
            cashForWorkEvent.EventDate = eventDate.Date;
            cashForWorkEvent.FinishDate = finishDate?.Date;
            cashForWorkEvent.StartTime = startTime;
            cashForWorkEvent.EndTime = endTime;
            cashForWorkEvent.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            cashForWorkEvent.EventKind = eventKind;
            cashForWorkEvent.BenefitType = benefitType;
            cashForWorkEvent.BenefitDescription = benefitDescription;
            cashForWorkEvent.UnitAmount = unitAmount;
            cashForWorkEvent.CashForWorkBudgetId = resolvedBudgetId;
            cashForWorkEvent.UpdatedAt = DateTime.Now;

            var attendanceRows = await _context.CashForWorkAttendances
                .Include(attendance => attendance.Participant)
                .Where(attendance => !attendance.IsDeleted && attendance.Participant.EventId == eventId)
                .ToListAsync();

            foreach (var attendance in attendanceRows)
            {
                attendance.AttendanceDate = eventDate.Date;
            }

            await _context.SaveChangesAsync();

            if (_auditService != null)
            {
                await _auditService.LogActivityAsync(
                    updatedByUserId,
                    "CashForWorkEventUpdated",
                    "CashForWorkEvent",
                    cashForWorkEvent.Id,
                    $"Updated event '{cashForWorkEvent.Title}' scheduled on {cashForWorkEvent.EventDate:yyyy-MM-dd}.");
            }

            return cashForWorkEvent;
        }

        public void DeleteEvent(int eventId, int deletedByUserId)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .FirstOrDefault(item => !item.IsDeleted && item.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            EnsureEventCanBeModified(cashForWorkEvent);

            var eventTitle = cashForWorkEvent.Title;

            var scannerSessions = _context.ScannerSessions
                .Where(session => session.CashForWorkEventId == eventId)
                .ToList();

            var participants = _context.CashForWorkParticipants
                .Where(participant => !participant.IsDeleted && participant.EventId == eventId)
                .ToList();

            var participantIds = participants
                .Select(participant => participant.Id)
                .ToList();

            var attendances = _context.CashForWorkAttendances
                .Where(attendance => !attendance.IsDeleted && participantIds.Contains(attendance.ParticipantId))
                .ToList();

            foreach (var attendance in attendances)
            {
                attendance.IsDeleted = true;
            }

            foreach (var participant in participants)
            {
                participant.IsDeleted = true;
            }

            foreach (var session in scannerSessions)
            {
                session.IsActive = false;
            }

            cashForWorkEvent.IsDeleted = true;
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
                .FirstOrDefault(item => !item.IsDeleted && item.Id == attendanceId)
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
                .FirstOrDefault(item => !item.IsDeleted && item.Id == attendanceId)
                ?? throw new InvalidOperationException("Attendance record was not found.");

            EnsureEventCanBeModified(attendance.Participant.Event);

            var eventTitle = attendance.Participant.Event.Title;

            attendance.IsDeleted = true;
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
            decimal totalAmount,
            int recordedByUserId,
            string? remarks)
        {
            if (RemoteWriteExecutionService.ShouldRouteToRemote(_context))
            {
                var localBudget = await _context.CashForWorkBudgets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_CFW_BUDGET" && item.IsActive);

                try
                {
                    var remoteResult = await RemoteWriteExecutionService.ExecuteRemoteWriteAsync(
                        _context,
                        async remoteContext =>
                        {
                            if (localBudget != null)
                            {
                                var remoteBudget = await remoteContext.CashForWorkBudgets
                                    .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_CFW_BUDGET");

                                if (remoteBudget == null)
                                {
                                    remoteBudget = new CashForWorkBudget
                                    {
                                        BudgetCode = localBudget.BudgetCode,
                                        BudgetName = localBudget.BudgetName,
                                        Description = localBudget.Description,
                                        BudgetCap = localBudget.BudgetCap,
                                        IsActive = localBudget.IsActive,
                                        CreatedByUserId = recordedByUserId,
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    remoteContext.CashForWorkBudgets.Add(remoteBudget);
                                    await remoteContext.SaveChangesAsync();
                                }
                            }

                            var remoteService = new CashForWorkService(
                                remoteContext,
                                auditService: null,
                                ggmsConsolidatedTransactionService: _ggmsConsolidatedTransactionService);
                            return await remoteService.ReleaseEventAsync(eventId, totalAmount, recordedByUserId, remarks);
                        });

                    if (!remoteResult.IsSuccess)
                    {
                        return remoteResult;
                    }

                    // If remote succeeded, we continue to local update below.
                }
                catch (Exception ex)
                {
                    return new CashForWorkReleaseOperationResult(false, $"Remote release failed. {ex.Message}");
                }
            }

            if (totalAmount <= 0)
            {
                return new CashForWorkReleaseOperationResult(false, "Release amount must be greater than zero.");
            }

            var cashForWorkEvent = await _context.CashForWorkEvents
                .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == eventId);

            if (cashForWorkEvent == null)
            {
                return new CashForWorkReleaseOperationResult(false, "Cash-for-work event was not found.");
            }

            if (cashForWorkEvent.EventDate.Date > DateTime.Today)
            {
                return new CashForWorkReleaseOperationResult(false, "Cash-for-work events can only be released on or after the event date.");
            }

            if (cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                return new CashForWorkReleaseOperationResult(false, "Seminar events are attendance-only and cannot use the payout workflow.");
            }

            if (cashForWorkEvent.BudgetLedgerEntryId.HasValue)
            {
                return new CashForWorkReleaseOperationResult(false, "This cash-for-work event already has a recorded release.");
            }

            var resolvedBudgetId = await ResolveGlobalBudgetAsync();
            if (!resolvedBudgetId.HasValue)
            {
                return new CashForWorkReleaseOperationResult(false, "No active global cash-for-work budget found. Please set one in the Budget module first.");
            }

            var releaseReadySummary = GetReleaseReadySummary(eventId);
            if (releaseReadySummary.ReleaseReadyParticipantCount <= 0)
            {
                return new CashForWorkReleaseOperationResult(false, "Save attendance before releasing cash-for-work funds.");
            }

            var budgetService = new BudgetManagementService(_context, _auditService);
            var budgetResult = await budgetService.RecordReleaseAsync(
                new BudgetReleaseRequest(
                    null,
                    BudgetLedgerFeatureSource.CashForWork,
                    $"cash-for-work:{cashForWorkEvent.Id}",
                    releaseReadySummary.ReleaseReadyParticipantCount,
                    AssistanceReleaseKind.Cash,
                    totalAmount,
                    DateTime.Now,
                    remarks ?? cashForWorkEvent.Title,
                    CashForWorkBudgetId: resolvedBudgetId),
                recordedByUserId);

            if (!budgetResult.IsSuccess)
            {
                if (budgetResult.Message != null && budgetResult.Message.Contains("already has a budget ledger entry", StringComparison.OrdinalIgnoreCase))
                {
                    cashForWorkEvent.Status = CashForWorkEventStatus.Completed;
                    cashForWorkEvent.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return new CashForWorkReleaseOperationResult(true, "Event was already released. Status synchronized.");
                }
                return new CashForWorkReleaseOperationResult(false, budgetResult.Message ?? "Budget recording failed.");
            }

            cashForWorkEvent.CashForWorkBudgetId = resolvedBudgetId;
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
                .Where(p => !p.IsDeleted && p.EventId == eventId)
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

            var ggmsWarningMessage = await _ggmsConsolidatedTransactionService.TryWriteCashForWorkReleaseAsync(
                _context,
                cashForWorkEvent,
                participantsToRecord,
                presentParticipantIds,
                totalAmount);

            if (_auditService != null)
            {
                await _auditService.LogActivityAsync(
                    recordedByUserId,
                    "CashForWorkReleased",
                    "CashForWorkEvent",
                    cashForWorkEvent.Id,
                    $"Released {totalAmount:N2} for event '{cashForWorkEvent.Title}'.");
            }

            var successMessage = "Cash-for-work release recorded.";
            if (!string.IsNullOrWhiteSpace(ggmsWarningMessage))
            {
                successMessage = $"{successMessage} GGMS sync warning: {ggmsWarningMessage}";
            }

            return new CashForWorkReleaseOperationResult(true, successMessage, budgetResult.LedgerEntryId);
        }

        private async Task<int?> ResolveGlobalBudgetAsync()
        {
            var globalBudget = await _context.CashForWorkBudgets
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.BudgetCode == "GLOBAL_CFW_BUDGET" && item.IsActive);

            return globalBudget?.Id;
        }

        private string GetEventTitle(int eventId)
        {
            return _context.CashForWorkEvents
                .AsNoTracking()
                .Where(cashForWorkEvent => !cashForWorkEvent.IsDeleted && cashForWorkEvent.Id == eventId)
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
                    !participant.IsDeleted &&
                    participant.EventId == eventId &&
                    participant.Beneficiary != null &&
                    participant.Beneficiary.VerificationStatus == VerificationStatus.Approved)
                .Select(participant => participant.Id)
                .ToHashSet();
        }

        private HashSet<int> GetRecordedParticipantIds(int eventId, DateTime attendanceDate)
        {
            return _context.CashForWorkAttendances
                .AsNoTracking()
                .Where(attendance =>
                    !attendance.IsDeleted &&
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
        decimal ProposedAmount,
        IReadOnlyList<CashForWorkReleaseReadyParticipant> ReleaseReadyParticipants);

    public sealed record CashForWorkReleaseReadyParticipant(
        int ParticipantId,
        string FullName,
        string? BeneficiaryId,
        string? CivilRegistryId,
        AttendanceCaptureSource Source,
        DateTime RecordedAt);

}

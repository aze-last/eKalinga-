using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed record CashForWorkReleaseOperationResult(bool IsSuccess, string Message, int? BudgetLedgerEntryId = null);

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

        public List<HouseholdMember> GetEligibleMembers()
        {
            return _context.HouseholdMembers
                .AsNoTracking()
                .Include(member => member.Household)
                .Where(member => member.IsCashForWorkEligible && member.Household.Status == HouseholdStatus.Active)
                .OrderBy(member => member.FullName)
                .ToList();
        }

        public List<CashForWorkParticipant> GetParticipants(int eventId)
        {
            return _context.CashForWorkParticipants
                .AsNoTracking()
                .Include(participant => participant.HouseholdMember)
                .ThenInclude(member => member.Household)
                .Where(participant => participant.EventId == eventId)
                .OrderBy(participant => participant.HouseholdMember.FullName)
                .ToList();
        }

        public List<CashForWorkAttendance> GetAttendanceRecords(int eventId)
        {
            return _context.CashForWorkAttendances
                .AsNoTracking()
                .Include(attendance => attendance.Participant)
                .ThenInclude(participant => participant.HouseholdMember)
                .ThenInclude(member => member.Household)
                .Include(attendance => attendance.RecordedByUser)
                .Where(attendance => attendance.Participant.EventId == eventId)
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
                .OrderBy(attendance => attendance.Participant.HouseholdMember.FullName)
                .ToList();

            var releaseReadyParticipants = presentAttendance
                .Select(attendance => new CashForWorkReleaseReadyParticipant(
                    attendance.ParticipantId,
                    attendance.Participant.HouseholdMember.FullName,
                    attendance.Participant.HouseholdMember.Household.HouseholdCode,
                    attendance.Participant.HouseholdMember.Household.Purok,
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
                releaseReadyParticipants.Count(participant => participant.Source == AttendanceCaptureSource.OcrUpload),
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

        public void AddParticipant(int eventId, int householdMemberId, int addedByUserId)
        {
            var exists = _context.CashForWorkParticipants.Any(participant =>
                participant.EventId == eventId &&
                participant.HouseholdMemberId == householdMemberId);

            if (exists)
            {
                throw new InvalidOperationException("Member is already included in this event.");
            }

            _context.CashForWorkParticipants.Add(new CashForWorkParticipant
            {
                EventId = eventId,
                HouseholdMemberId = householdMemberId,
                AddedByUserId = addedByUserId,
                AddedAt = DateTime.Now
            });

            _context.SaveChanges();

            var participantName = _context.HouseholdMembers
                .AsNoTracking()
                .Where(member => member.Id == householdMemberId)
                .Select(member => member.FullName)
                .FirstOrDefault() ?? $"member #{householdMemberId}";

            var eventTitle = GetEventTitle(eventId);
            _auditService?.LogActivity(
                addedByUserId,
                "CashForWorkParticipantAdded",
                "CashForWorkEvent",
                eventId,
                $"Added participant '{participantName}' to event '{eventTitle}'.");
        }

        public async Task<IReadOnlyList<CashForWorkAttendanceReviewItem>> ReviewAttendanceFromImageAsync(
            int eventId,
            string imagePath,
            IOcrService ocrService,
            CancellationToken cancellationToken = default)
        {
            var participants = GetParticipants(eventId)
                .Select(participant => new ParticipantMatchCandidate(
                    participant.Id,
                    participant.HouseholdMember.FullName))
                .ToList();

            if (participants.Count == 0)
            {
                throw new InvalidOperationException("Add participants to the cash-for-work event before processing attendance.");
            }

            var participantNames = participants
                .Select(participant => participant.ParticipantName)
                .ToList();

            var extractedNames = await ocrService.ExtractNamesAsync(imagePath, participantNames, cancellationToken);
            var results = new List<CashForWorkAttendanceReviewItem>();

            foreach (var extractedName in extractedNames)
            {
                var match = ParticipantNameMatcher.FindBestMatch(extractedName, participants);
                results.Add(new CashForWorkAttendanceReviewItem
                {
                    ExtractedName = extractedName,
                    MatchStatus = match.Status,
                    SuggestedParticipantId = match.ParticipantId,
                    SuggestedParticipantName = match.ParticipantName,
                    SimilarityScore = match.Score,
                    IsSelected = match.Status == AttendanceMatchStatus.Matched
                });
            }

            return results;
        }

        public int SaveAttendanceSelections(int eventId, int recordedByUserId, IEnumerable<CashForWorkAttendanceReviewItem> selections)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            var selectedItems = selections
                .Where(item => item.IsSelected && item.SuggestedParticipantId.HasValue)
                .ToList();
            var validParticipantIds = GetValidParticipantIds(eventId);
            var recordedParticipantIds = GetRecordedParticipantIds(eventId, cashForWorkEvent.EventDate.Date);

            foreach (var item in selectedItems)
            {
                var participantId = item.SuggestedParticipantId!.Value;

                if (!validParticipantIds.Contains(participantId) || !recordedParticipantIds.Add(participantId))
                {
                    continue;
                }

                _context.CashForWorkAttendances.Add(new CashForWorkAttendance
                {
                    ParticipantId = participantId,
                    AttendanceDate = cashForWorkEvent.EventDate.Date,
                    Status = CashForWorkAttendanceStatus.Present,
                    Source = AttendanceCaptureSource.OcrUpload,
                    OcrExtractedName = item.ExtractedName,
                    RecordedByUserId = recordedByUserId,
                    RecordedAt = DateTime.Now
                });
            }

            var savedCount = _context.SaveChanges();
            _auditService?.LogActivity(
                recordedByUserId,
                "CashForWorkOcrAttendanceSaved",
                "CashForWorkEvent",
                eventId,
                $"Saved {savedCount} OCR attendance record(s) for event '{GetEventTitle(eventId)}'.");

            return savedCount;
        }

        public int SaveManualAttendance(int eventId, int recordedByUserId, IEnumerable<int> participantIds)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

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

        public bool SaveScannerAttendance(int eventId, int recordedByUserId, int participantId, string qrPayload)
        {
            var cashForWorkEvent = _context.CashForWorkEvents
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == eventId)
                ?? throw new InvalidOperationException("Cash-for-work event was not found.");

            var validParticipantIds = GetValidParticipantIds(eventId);
            if (!validParticipantIds.Contains(participantId))
            {
                return false;
            }

            var recordedParticipantIds = GetRecordedParticipantIds(eventId, cashForWorkEvent.EventDate.Date);
            if (!recordedParticipantIds.Add(participantId))
            {
                return false;
            }

            _context.CashForWorkAttendances.Add(new CashForWorkAttendance
            {
                ParticipantId = participantId,
                AttendanceDate = cashForWorkEvent.EventDate.Date,
                Status = CashForWorkAttendanceStatus.Present,
                Source = AttendanceCaptureSource.ScannerSession,
                OcrExtractedName = string.IsNullOrWhiteSpace(qrPayload) ? null : qrPayload.Trim(),
                RecordedByUserId = recordedByUserId,
                RecordedAt = DateTime.Now
            });

            _context.SaveChanges();
            _auditService?.LogActivity(
                recordedByUserId,
                "CashForWorkScannerAttendanceSaved",
                "CashForWorkEvent",
                eventId,
                $"Saved scanner attendance for participant #{participantId} in event '{GetEventTitle(eventId)}'.");

            return true;
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

        private HashSet<int> GetValidParticipantIds(int eventId)
        {
            return _context.CashForWorkParticipants
                .AsNoTracking()
                .Where(participant => participant.EventId == eventId)
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
        int OcrAttendanceCount,
        IReadOnlyList<CashForWorkReleaseReadyParticipant> ReleaseReadyParticipants);

    public sealed record CashForWorkReleaseReadyParticipant(
        int ParticipantId,
        string FullName,
        string HouseholdCode,
        string Purok,
        AttendanceCaptureSource Source,
        DateTime RecordedAt);

    public sealed class CashForWorkAttendanceReviewItem
    {
        public string ExtractedName { get; set; } = string.Empty;
        public AttendanceMatchStatus MatchStatus { get; set; }
        public int? SuggestedParticipantId { get; set; }
        public string SuggestedParticipantName { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
        public bool IsSelected { get; set; }
    }

    public enum AttendanceMatchStatus
    {
        Matched,
        Possible,
        NotApproved
    }

    internal sealed record ParticipantMatchCandidate(int ParticipantId, string ParticipantName);

    internal sealed record ParticipantMatchResult(
        AttendanceMatchStatus Status,
        int? ParticipantId,
        string ParticipantName,
        double Score);

    internal static class ParticipantNameMatcher
    {
        public static ParticipantMatchResult FindBestMatch(string extractedName, IReadOnlyCollection<ParticipantMatchCandidate> participants)
        {
            var normalizedExtractedName = NormalizeName(extractedName);
            if (string.IsNullOrWhiteSpace(normalizedExtractedName))
            {
                return new ParticipantMatchResult(AttendanceMatchStatus.NotApproved, null, string.Empty, 0);
            }

            ParticipantMatchResult? bestMatch = null;

            foreach (var participant in participants)
            {
                var normalizedParticipantName = NormalizeName(participant.ParticipantName);
                var score = CalculateSimilarity(normalizedExtractedName, normalizedParticipantName);

                if (bestMatch == null || score > bestMatch.Score)
                {
                    bestMatch = new ParticipantMatchResult(
                        score >= 0.95 ? AttendanceMatchStatus.Matched :
                        score >= 0.72 ? AttendanceMatchStatus.Possible :
                        AttendanceMatchStatus.NotApproved,
                        participant.ParticipantId,
                        participant.ParticipantName,
                        score);
                }
            }

            return bestMatch ?? new ParticipantMatchResult(AttendanceMatchStatus.NotApproved, null, string.Empty, 0);
        }

        private static string NormalizeName(string name)
        {
            var buffer = new List<char>(name.Length);
            foreach (var character in name.ToUpperInvariant())
            {
                if (char.IsLetter(character) || char.IsWhiteSpace(character))
                {
                    buffer.Add(character);
                }
            }

            return string.Join(
                " ",
                new string(buffer.ToArray())
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static double CalculateSimilarity(string left, string right)
        {
            if (left == right)
            {
                return 1;
            }

            var leftShort = ToShortName(left);
            var rightShort = ToShortName(right);
            var fullScore = 1.0 - (double)LevenshteinDistance(left, right) / Math.Max(left.Length, right.Length);
            var shortScore = 1.0 - (double)LevenshteinDistance(leftShort, rightShort) / Math.Max(leftShort.Length, rightShort.Length);
            return Math.Max(fullScore, shortScore);
        }

        private static string ToShortName(string name)
        {
            var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length <= 2)
            {
                return name;
            }

            return $"{tokens[0]} {tokens[^1]}";
        }

        private static int LevenshteinDistance(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return right.Length;
            }

            if (string.IsNullOrEmpty(right))
            {
                return left.Length;
            }

            var costs = new int[right.Length + 1];
            for (var index = 0; index <= right.Length; index++)
            {
                costs[index] = index;
            }

            for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
            {
                var previousDiagonal = costs[0];
                costs[0] = leftIndex;

                for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
                {
                    var temp = costs[rightIndex];
                    var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                    costs[rightIndex] = Math.Min(
                        Math.Min(costs[rightIndex] + 1, costs[rightIndex - 1] + 1),
                        previousDiagonal + substitutionCost);
                    previousDiagonal = temp;
                }
            }

            return costs[right.Length];
        }
    }
}

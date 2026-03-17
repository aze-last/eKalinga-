using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public sealed class CashForWorkService
    {
        private readonly AppDbContext _context;

        public CashForWorkService(AppDbContext context)
        {
            _context = context;
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

            foreach (var item in selectedItems)
            {
                var participantId = item.SuggestedParticipantId!.Value;

                var isParticipantValid = _context.CashForWorkParticipants.Any(participant =>
                    participant.Id == participantId &&
                    participant.EventId == eventId);

                if (!isParticipantValid)
                {
                    continue;
                }

                var attendanceExists = _context.CashForWorkAttendances.Any(attendance =>
                    attendance.ParticipantId == participantId &&
                    attendance.AttendanceDate == cashForWorkEvent.EventDate.Date);

                if (attendanceExists)
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

            return _context.SaveChanges();
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

            foreach (var participantId in selectedParticipantIds)
            {
                var isParticipantValid = _context.CashForWorkParticipants.Any(participant =>
                    participant.Id == participantId &&
                    participant.EventId == eventId);

                if (!isParticipantValid)
                {
                    continue;
                }

                var attendanceExists = _context.CashForWorkAttendances.Any(attendance =>
                    attendance.ParticipantId == participantId &&
                    attendance.AttendanceDate == cashForWorkEvent.EventDate.Date);

                if (attendanceExists)
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

            return _context.SaveChanges();
        }
    }

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

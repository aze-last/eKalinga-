using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    public sealed record ScannerSessionCreateResult(
        string SessionToken,
        string Pin,
        ScannerSessionMode Mode,
        int? CashForWorkEventId,
        int? AyudaProgramId,
        DateTime ExpiresAt);

    public sealed class ScannerSessionService
    {
        private readonly AppDbContext _context;

        public ScannerSessionService(AppDbContext context)
        {
            _context = context;
        }

        public Task<ScannerSessionCreateResult> CreateLookupSessionAsync(int createdByUserId, TimeSpan? lifetime = null)
        {
            return CreateAsync(ScannerSessionMode.Lookup, createdByUserId, null, null, lifetime);
        }

        public Task<ScannerSessionCreateResult> CreateAttendanceSessionAsync(int cashForWorkEventId, int createdByUserId, TimeSpan? lifetime = null)
        {
            return CreateAsync(ScannerSessionMode.Attendance, createdByUserId, cashForWorkEventId, null, lifetime);
        }

        public Task<ScannerSessionCreateResult> CreateDistributionSessionAsync(int ayudaProgramId, int createdByUserId, TimeSpan? lifetime = null)
        {
            return CreateAsync(ScannerSessionMode.Distribution, createdByUserId, null, ayudaProgramId, lifetime);
        }

        public async Task<bool> ValidatePinAsync(string sessionToken, string pin)
        {
            var normalizedToken = NormalizeNullable(sessionToken);
            var normalizedPin = NormalizeNullable(pin);
            if (normalizedToken == null || normalizedPin == null)
            {
                return false;
            }

            var session = await _context.ScannerSessions
                .FirstOrDefaultAsync(item => item.SessionToken == normalizedToken);

            if (session == null || !session.IsActive || session.ExpiresAt <= DateTime.Now)
            {
                return false;
            }

            if (!string.Equals(session.PinHash, HashValue(normalizedPin), StringComparison.Ordinal))
            {
                return false;
            }

            session.LastAccessedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ScannerSession?> GetActiveSessionAsync(string sessionToken)
        {
            var normalizedToken = NormalizeNullable(sessionToken);
            if (normalizedToken == null)
            {
                return null;
            }

            return await _context.ScannerSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.SessionToken == normalizedToken &&
                    item.IsActive &&
                    item.ExpiresAt > DateTime.Now);
        }

        private async Task<ScannerSessionCreateResult> CreateAsync(
            ScannerSessionMode mode,
            int createdByUserId,
            int? cashForWorkEventId,
            int? ayudaProgramId,
            TimeSpan? lifetime)
        {
            var pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var session = new ScannerSession
            {
                Mode = mode,
                SessionToken = Guid.NewGuid().ToString("N"),
                PinHash = HashValue(pin),
                CashForWorkEventId = cashForWorkEventId,
                AyudaProgramId = ayudaProgramId,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(lifetime ?? TimeSpan.FromMinutes(15)),
                IsActive = true
            };

            _context.ScannerSessions.Add(session);
            await _context.SaveChangesAsync();

            return new ScannerSessionCreateResult(
                session.SessionToken,
                pin,
                session.Mode,
                session.CashForWorkEventId,
                session.AyudaProgramId,
                session.ExpiresAt);
        }

        private static string HashValue(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

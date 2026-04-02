using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    internal sealed class OtpChallengeSession
    {
        public string Purpose { get; init; } = string.Empty;
        public string RecipientEmail { get; init; } = string.Empty;
        public string Nonce { get; init; } = string.Empty;
        public string CodeHash { get; init; } = string.Empty;
        public DateTimeOffset IssuedAtUtc { get; init; }
        public DateTimeOffset ExpiresAtUtc { get; init; }
        public DateTimeOffset ResendAvailableAtUtc { get; init; }
        public int MaxAttempts { get; init; } = 3;
        public int FailedAttempts { get; set; }

        public int AttemptsRemaining => Math.Max(0, MaxAttempts - FailedAttempts);
        public bool HasAttemptsRemaining => FailedAttempts < MaxAttempts;
    }

    internal sealed class OtpChallengeIssueResult
    {
        public required OtpChallengeSession Session { get; init; }
        public required string Code { get; init; }
    }

    internal sealed class OtpChallengeVerificationResult
    {
        public bool IsSuccess { get; init; }
        public bool RequiresNewCode { get; init; }
        public bool IsExpired { get; init; }
        public bool IsLockedOut { get; init; }
        public int AttemptsRemaining { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    internal static class OtpChallengeService
    {
        private const int CodeLength = 6;

        public static OtpChallengeIssueResult IssueCode(
            string purpose,
            string recipientEmail,
            DateTimeOffset now,
            TimeSpan ttl,
            TimeSpan resendCooldown,
            int maxAttempts = 3)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000)
                .ToString($"D{CodeLength}", CultureInfo.InvariantCulture);
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            return new OtpChallengeIssueResult
            {
                Code = code,
                Session = new OtpChallengeSession
                {
                    Purpose = purpose?.Trim() ?? string.Empty,
                    RecipientEmail = recipientEmail?.Trim() ?? string.Empty,
                    Nonce = nonce,
                    CodeHash = ComputeHash(code, nonce),
                    IssuedAtUtc = now,
                    ExpiresAtUtc = now.Add(ttl),
                    ResendAvailableAtUtc = now.Add(resendCooldown),
                    MaxAttempts = Math.Max(1, maxAttempts)
                }
            };
        }

        public static bool CanResend(OtpChallengeSession? session, DateTimeOffset now)
        {
            return session == null || now >= session.ResendAvailableAtUtc;
        }

        public static OtpChallengeVerificationResult VerifyCode(
            OtpChallengeSession? session,
            string? candidateCode,
            DateTimeOffset now)
        {
            if (session == null)
            {
                return new OtpChallengeVerificationResult
                {
                    RequiresNewCode = true,
                    Message = "Request a new OTP first."
                };
            }

            if (now > session.ExpiresAtUtc)
            {
                return new OtpChallengeVerificationResult
                {
                    RequiresNewCode = true,
                    IsExpired = true,
                    AttemptsRemaining = session.AttemptsRemaining,
                    Message = "The OTP expired. Request a new code."
                };
            }

            if (!session.HasAttemptsRemaining)
            {
                return new OtpChallengeVerificationResult
                {
                    RequiresNewCode = true,
                    IsLockedOut = true,
                    AttemptsRemaining = 0,
                    Message = "Maximum OTP attempts reached. Request a new code."
                };
            }

            if (string.IsNullOrWhiteSpace(candidateCode))
            {
                return new OtpChallengeVerificationResult
                {
                    AttemptsRemaining = session.AttemptsRemaining,
                    Message = "Enter the 6-digit OTP first."
                };
            }

            var candidateHash = ComputeHash(candidateCode.Trim(), session.Nonce);
            if (FixedTimeEquals(candidateHash, session.CodeHash))
            {
                return new OtpChallengeVerificationResult
                {
                    IsSuccess = true,
                    AttemptsRemaining = session.AttemptsRemaining,
                    Message = "OTP verified successfully."
                };
            }

            session.FailedAttempts++;
            var attemptsRemaining = session.AttemptsRemaining;
            var lockedOut = !session.HasAttemptsRemaining;

            return new OtpChallengeVerificationResult
            {
                AttemptsRemaining = attemptsRemaining,
                RequiresNewCode = lockedOut,
                IsLockedOut = lockedOut,
                Message = lockedOut
                    ? "Maximum OTP attempts reached. Request a new code."
                    : $"Incorrect OTP. {attemptsRemaining} attempt(s) remaining."
            };
        }

        private static string ComputeHash(string code, string nonce)
        {
            using var sha256 = SHA256.Create();
            var payload = Encoding.UTF8.GetBytes($"{nonce}:{code}");
            return Convert.ToBase64String(sha256.ComputeHash(payload));
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);

            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}

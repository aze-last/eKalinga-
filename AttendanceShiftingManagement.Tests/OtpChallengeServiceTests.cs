using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class OtpChallengeServiceTests
{
    [Fact]
    public void IssueCode_CreatesSixDigitCode_WithExpiryAndCooldown()
    {
        var now = new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero);

        var result = OtpChallengeService.IssueCode(
            "Protected Settings",
            "ops@example.com",
            now,
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(45));

        Assert.Matches(@"^\d{6}$", result.Code);
        Assert.Equal("Protected Settings", result.Session.Purpose);
        Assert.Equal("ops@example.com", result.Session.RecipientEmail);
        Assert.Equal(now.AddMinutes(3), result.Session.ExpiresAtUtc);
        Assert.Equal(now.AddSeconds(45), result.Session.ResendAvailableAtUtc);
        Assert.Equal(3, result.Session.AttemptsRemaining);
    }

    [Fact]
    public void VerifyCode_ReturnsSuccess_ForMatchingOtp()
    {
        var now = new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero);
        var issued = OtpChallengeService.IssueCode(
            "Protected Settings",
            "ops@example.com",
            now,
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(45));

        var result = OtpChallengeService.VerifyCode(issued.Session, issued.Code, now.AddSeconds(15));

        Assert.True(result.IsSuccess);
        Assert.False(result.RequiresNewCode);
        Assert.Equal(3, result.AttemptsRemaining);
    }

    [Fact]
    public void VerifyCode_LocksSession_AfterThreeFailedAttempts()
    {
        var now = new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero);
        var issued = OtpChallengeService.IssueCode(
            "Protected Settings",
            "ops@example.com",
            now,
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(45));

        var first = OtpChallengeService.VerifyCode(issued.Session, "111111", now.AddSeconds(5));
        var second = OtpChallengeService.VerifyCode(issued.Session, "222222", now.AddSeconds(10));
        var third = OtpChallengeService.VerifyCode(issued.Session, "333333", now.AddSeconds(15));

        Assert.False(first.IsSuccess);
        Assert.Equal(2, first.AttemptsRemaining);
        Assert.False(first.RequiresNewCode);

        Assert.False(second.IsSuccess);
        Assert.Equal(1, second.AttemptsRemaining);
        Assert.False(second.RequiresNewCode);

        Assert.False(third.IsSuccess);
        Assert.True(third.IsLockedOut);
        Assert.True(third.RequiresNewCode);
        Assert.Equal(0, third.AttemptsRemaining);
    }

    [Fact]
    public void VerifyCode_RequiresNewCode_WhenExpired()
    {
        var now = new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero);
        var issued = OtpChallengeService.IssueCode(
            "Protected Settings",
            "ops@example.com",
            now,
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(45));

        var result = OtpChallengeService.VerifyCode(issued.Session, issued.Code, now.AddMinutes(3).AddSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsExpired);
        Assert.True(result.RequiresNewCode);
    }
}

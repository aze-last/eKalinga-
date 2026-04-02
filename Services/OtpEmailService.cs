using System.Net;
using System.Net.Mail;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    public sealed class OtpEmailSendResult
    {
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public static class OtpEmailService
    {
        public static Task<OtpEmailSendResult> SendOtpAsync(
            string recipientEmail,
            string otpCode,
            string purposeLabel,
            TimeSpan expiry)
        {
            var settings = OtpEmailSettingsService.Load();
            return SendOtpAsync(settings, recipientEmail, otpCode, purposeLabel, expiry);
        }

        internal static async Task<OtpEmailSendResult> SendOtpAsync(
            OtpEmailSettingsModel settings,
            string recipientEmail,
            string otpCode,
            string purposeLabel,
            TimeSpan expiry)
        {
            if (!OtpEmailSettingsService.IsConfigured(settings))
            {
                return new OtpEmailSendResult
                {
                    IsSuccess = false,
                    Message = "OTP email sender is not configured yet. Complete the Brevo SMTP settings first."
                };
            }

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return new OtpEmailSendResult
                {
                    IsSuccess = false,
                    Message = "A valid recipient email is required before sending OTP."
                };
            }

            var subject = $"eKalinga+ OTP for {purposeLabel}";
            var body = BuildBody(otpCode, purposeLabel, expiry);

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(settings.SenderEmail, settings.SenderDisplayName),
                    Subject = subject,
                    Body = body,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8,
                    IsBodyHtml = false
                };

                message.To.Add(recipientEmail.Trim());

                using var client = new SmtpClient(settings.SmtpHost, settings.Port)
                {
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    EnableSsl = settings.UseSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(settings.Username, settings.Password)
                };

                await client.SendMailAsync(message);

                return new OtpEmailSendResult
                {
                    IsSuccess = true,
                    Message = $"OTP sent to {recipientEmail.Trim()}."
                };
            }
            catch (Exception ex)
            {
                return new OtpEmailSendResult
                {
                    IsSuccess = false,
                    Message = $"Failed to send OTP email: {ex.Message}"
                };
            }
        }

        private static string BuildBody(string otpCode, string purposeLabel, TimeSpan expiry)
        {
            var expiryMinutes = Math.Max(1, (int)Math.Round(expiry.TotalMinutes));

            return
$@"Your eKalinga+ OTP for {purposeLabel}

OTP Code: {otpCode}

This code expires in {expiryMinutes} minute(s).
If you did not request this OTP, you can ignore this email.";
        }
    }
}

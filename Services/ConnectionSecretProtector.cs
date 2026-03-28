using System.Security.Cryptography;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    internal static class ConnectionSecretProtector
    {
        private const string ProtectedValuePrefix = "dpapi:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AttendanceShiftingManagement.ConnectionSettings");

        public static string Protect(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal))
            {
                return value;
            }

            var plainBytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return ProtectedValuePrefix + Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (!value.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal))
            {
                return value;
            }

            try
            {
                var protectedPayload = value[ProtectedValuePrefix.Length..];
                var protectedBytes = Convert.FromBase64String(protectedPayload);
                var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AttendanceShiftingManagement.Services
{
    public static class AppVersionService
    {
        public static string GetCurrentVersion()
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(processPath);
                var rawVersion = versionInfo.ProductVersion;
                if (string.IsNullOrWhiteSpace(rawVersion))
                {
                    rawVersion = versionInfo.FileVersion;
                }

                var sanitized = SanitizeVersion(rawVersion);
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    return sanitized;
                }
            }

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            return SanitizeVersion(assemblyVersion) ?? "1.0.0.0";
        }

        internal static string? SanitizeVersion(string? rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return null;
            }

            var sanitized = rawVersion
                .Split(new[] { '+', '-' }, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }
    }
}

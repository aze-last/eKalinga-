using Microsoft.Extensions.Configuration;

namespace AttendanceShiftingManagement.Services
{
    public static class FeatureFlagService
    {
        private static bool? _enableDemoRoleSwitch;
        private static bool? _requireFingerprintForAttendance;

        public static bool EnableDemoRoleSwitch
        {
            get
            {
                if (_enableDemoRoleSwitch.HasValue)
                {
                    return _enableDemoRoleSwitch.Value;
                }

                try
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true)
                        .Build();

                    _enableDemoRoleSwitch = config.GetValue("AppSettings:EnableDemoRoleSwitch", false);
                }
                catch
                {
                    _enableDemoRoleSwitch = false;
                }

                return _enableDemoRoleSwitch.Value;
            }
        }

        public static bool RequireFingerprintForAttendance
        {
            get
            {
                if (_requireFingerprintForAttendance.HasValue)
                {
                    return _requireFingerprintForAttendance.Value;
                }

                try
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true)
                        .Build();

                    _requireFingerprintForAttendance = config.GetValue("AppSettings:RequireFingerprintForAttendance", false);
                }
                catch
                {
                    _requireFingerprintForAttendance = false;
                }

                return _requireFingerprintForAttendance.Value;
            }
        }
    }
}

using Microsoft.Extensions.Configuration;

namespace AttendanceShiftingManagement.Services
{
    public static class FeatureFlagService
    {
        private static bool? _enableDemoRoleSwitch;

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
    }
}

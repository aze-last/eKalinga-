using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Services
{
    public static class SessionContext
    {
        public static User? LoginUser { get; private set; }
        public static User? ActiveUser { get; private set; }

        public static bool IsImpersonating =>
            LoginUser != null &&
            ActiveUser != null &&
            LoginUser.Id != ActiveUser.Id;

        public static bool CanReturnToAdmin =>
            FeatureFlagService.EnableDemoRoleSwitch &&
            IsImpersonating &&
            LoginUser?.Role == UserRole.Admin;

        public static User? AdminUser =>
            LoginUser?.Role == UserRole.Admin
                ? LoginUser
                : null;

        public static void Start(User user)
        {
            LoginUser = user;
            ActiveUser = user;
        }

        public static void StartImpersonation(User adminUser, User targetUser)
        {
            if (adminUser.Role != UserRole.Admin)
            {
                throw new InvalidOperationException("Only Admin can start impersonation.");
            }

            if (LoginUser == null || LoginUser.Id != adminUser.Id)
            {
                LoginUser = adminUser;
            }

            ActiveUser = targetUser;
        }

        public static void EndImpersonation()
        {
            if (LoginUser != null)
            {
                ActiveUser = LoginUser;
            }
        }

        public static void Clear()
        {
            LoginUser = null;
            ActiveUser = null;
        }
    }
}

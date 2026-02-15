using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace AttendanceShiftingManagement.Services
{
    public static class RoleSwitchService
    {
        public static bool IsEnabled => FeatureFlagService.EnableDemoRoleSwitch;

        public static bool CanUseSwitcher(User currentUser)
        {
            if (!IsEnabled)
            {
                return false;
            }

            return SessionContext.IsImpersonating || currentUser.Role == UserRole.Admin;
        }

        public static bool CanReturnToAdmin => SessionContext.CanReturnToAdmin;

        public static List<User> GetSwitchableUsers(int currentUserId)
        {
            using var context = new AppDbContext();
            return context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Id != currentUserId)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToList();
        }

        public static bool SwitchToUser(int targetUserId, Window? currentWindow, User currentUser)
        {
            if (!IsEnabled)
            {
                MessageBox.Show("Demo role switch is disabled in appsettings.", "Feature Disabled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (!CanUseSwitcher(currentUser))
            {
                MessageBox.Show("Only Admin can switch roles.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            using var context = new AppDbContext();
            var targetUser = context.Users.AsNoTracking().FirstOrDefault(u => u.Id == targetUserId && u.IsActive);
            if (targetUser == null)
            {
                MessageBox.Show("Target account not found or inactive.", "Switch Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var activeBeforeSwitch = SessionContext.ActiveUser ?? currentUser;
            var adminUser = SessionContext.AdminUser ?? currentUser;
            if (adminUser.Role != UserRole.Admin)
            {
                MessageBox.Show("Only Admin can switch roles.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            SessionContext.StartImpersonation(adminUser, targetUser);

            LogImpersonationEvent(
                adminUser.Id,
                "ImpersonationStart",
                targetUser.Id,
                $"Admin '{adminUser.Username}' switched from '{activeBeforeSwitch.Username}' to '{targetUser.Username}'.");

            NavigateToRoleWindow(targetUser, currentWindow);
            return true;
        }

        public static bool ReturnToAdmin(Window? currentWindow)
        {
            if (!CanReturnToAdmin || SessionContext.LoginUser == null || SessionContext.ActiveUser == null)
            {
                return false;
            }

            var adminUser = SessionContext.LoginUser;
            var activeUser = SessionContext.ActiveUser;

            SessionContext.EndImpersonation();

            LogImpersonationEvent(
                adminUser.Id,
                "ImpersonationEnd",
                adminUser.Id,
                $"Admin '{adminUser.Username}' returned from '{activeUser.Username}' to Admin session.");

            NavigateToRoleWindow(adminUser, currentWindow);
            return true;
        }

        public static void HandleLogout()
        {
            if (SessionContext.IsImpersonating &&
                SessionContext.LoginUser != null &&
                SessionContext.ActiveUser != null)
            {
                LogImpersonationEvent(
                    SessionContext.LoginUser.Id,
                    "ImpersonationEnd",
                    SessionContext.LoginUser.Id,
                    $"Admin '{SessionContext.LoginUser.Username}' ended impersonation by logging out from '{SessionContext.ActiveUser.Username}'.");
            }

            SessionContext.Clear();
        }

        private static void NavigateToRoleWindow(User user, Window? currentWindow)
        {
            Window nextWindow = user.Role switch
            {
                UserRole.Manager => new ManagerMainWindow(user),
                UserRole.Crew => new CrewMainWindow(user),
                _ => new MainWindow(user)
            };

            Application.Current.MainWindow = nextWindow;
            nextWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            nextWindow.Show();
            currentWindow?.Close();
        }

        private static void LogImpersonationEvent(int adminUserId, string action, int entityId, string details)
        {
            using var context = new AppDbContext();
            var auditService = new AuditService(context);
            auditService.LogActivity(adminUserId, action, "User", entityId, details);
        }
    }
}

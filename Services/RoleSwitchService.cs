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

            return currentUser.IsActive;
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

        public static bool OpenRoleSwitcher(User currentUser, Window owner)
        {
            if (!IsEnabled)
            {
                MessageBox.Show("Demo role switch is disabled in appsettings.", "Feature Disabled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (!CanUseSwitcher(currentUser))
            {
                MessageBox.Show("Role switching is unavailable for this account.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var dialog = new RoleSwitchWindow(currentUser)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true || !dialog.SelectedUserId.HasValue)
            {
                return false;
            }

            return SwitchToUser(dialog.SelectedUserId.Value, owner, currentUser);
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
                MessageBox.Show("Role switching is unavailable for this account.", "Access Denied",
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

            if (!ConfirmPrivilegedSwitch(currentUser, targetUser, currentWindow))
            {
                return false;
            }

            var activeBeforeSwitch = SessionContext.ActiveUser ?? currentUser;
            var adminUser = SessionContext.AdminUser;

            if (adminUser != null && targetUser.Id == adminUser.Id)
            {
                SessionContext.EndImpersonation();

                LogAuditEvent(
                    adminUser.Id,
                    "ImpersonationEnd",
                    adminUser.Id,
                    $"Admin '{adminUser.Username}' returned from '{activeBeforeSwitch.Username}' to Admin session via switcher.");

                NavigateToRoleWindow(adminUser, currentWindow);
                return true;
            }

            if (adminUser != null)
            {
                SessionContext.StartImpersonation(adminUser, targetUser);

                LogAuditEvent(
                    adminUser.Id,
                    "ImpersonationStart",
                    targetUser.Id,
                    $"Admin '{adminUser.Username}' switched from '{activeBeforeSwitch.Username}' to '{targetUser.Username}'.");

                NavigateToRoleWindow(targetUser, currentWindow);
                return true;
            }

            SessionContext.Start(targetUser);

            LogAuditEvent(
                currentUser.Id,
                "DemoRoleSwitch",
                targetUser.Id,
                $"Demo role switch from '{activeBeforeSwitch.Username}' to '{targetUser.Username}'.");

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

            LogAuditEvent(
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
                LogAuditEvent(
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

        private static bool ConfirmPrivilegedSwitch(User currentUser, User targetUser, Window? owner)
        {
            if (targetUser.Role != UserRole.Admin || currentUser.Role == UserRole.Admin || SessionContext.AdminUser != null)
            {
                return true;
            }

            var passwordDialog = new RoleSwitchPasswordWindow(targetUser)
            {
                Owner = owner
            };

            if (passwordDialog.ShowDialog() != true)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(passwordDialog.EnteredPassword) ||
                !BCrypt.Net.BCrypt.Verify(passwordDialog.EnteredPassword, targetUser.PasswordHash))
            {
                MessageBox.Show("Invalid Admin password. Switch cancelled.", "Switch Cancelled",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static void LogAuditEvent(int actorUserId, string action, int entityId, string details)
        {
            using var context = new AppDbContext();
            var auditService = new AuditService(context);
            auditService.LogActivity(actorUserId, action, "User", entityId, details);
        }
    }
}

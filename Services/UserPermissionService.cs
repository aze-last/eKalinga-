using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public static class UserPermissionService
    {
        private static UserPermission? _current;
        private static User? _user;

        public static void LoadForUser(User user)
        {
            _user = user;
            if (user.Role == UserRole.SuperAdmin)
            {
                _current = null; // Exempt
                return;
            }

            using var context = new LocalDbContext();
            _current = context.UserPermissions
                .AsNoTracking()
                .FirstOrDefault(p => p.UserId == user.Id);
        }

        public static void Clear()
        {
            _current = null;
            _user = null;
        }

        private static bool NeedsCheck => _user != null && _user.Role != UserRole.SuperAdmin;

        private static bool Check(Func<UserPermission, bool> selector)
        {
            if (!NeedsCheck) return true;
            if (_current == null) return true; // Default to open if no row exists
            return selector(_current);
        }

        public static bool CanAccessDashboard => Check(p => p.CanAccessDashboard);
        public static bool CanAccessMasterList => Check(p => p.CanAccessMasterList);
        public static bool CanAccessAssistanceCases => Check(p => p.CanAccessAssistanceCases);
        public static bool CanAccessBudget => Check(p => p.CanAccessBudget);
        public static bool CanAccessDistribution => Check(p => p.CanAccessDistribution);
        public static bool CanAccessCashForWork => Check(p => p.CanAccessCashForWork);
        public static bool CanAccessSeminarAttendance => Check(p => p.CanAccessSeminarAttendance);
        public static bool CanAccessBorrowing => Check(p => p.CanAccessBorrowing);
        public static bool CanAccessReports => Check(p => p.CanAccessReports);
        public static bool CanAccessGgmsTransactions => Check(p => p.CanAccessGgmsTransactions);
        public static bool CanAccessAppDatabase => Check(p => p.CanAccessAppDatabase);
        public static bool CanAccessGgmsBudgetSource => Check(p => p.CanAccessGgmsBudgetSource);
        public static bool CanAccessScanningPortal => Check(p => p.CanAccessScanningPortal);

        public static bool CanManageUsers => _user?.Role == UserRole.SuperAdmin;
    }
}

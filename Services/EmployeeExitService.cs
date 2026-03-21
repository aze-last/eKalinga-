using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class EmployeeExitService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public EmployeeExitService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
        }

        public List<EmployeeExit> GetExitRecords()
        {
            return _context.EmployeeExits
                .AsNoTracking()
                .Include(e => e.Employee)
                .ThenInclude(emp => emp.Position)
                .Include(e => e.RecordedByUser)
                .OrderByDescending(e => e.RecordedAt)
                .ToList();
        }

        public EmployeeExit RecordExit(
            int employeeId,
            EmployeeExitType exitType,
            bool isVoluntary,
            DateTime lastWorkingDate,
            string reason,
            string? notes,
            int recordedByUserId)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new Exception("Exit reason is required.");
            }

            var employee = _context.Employees
                .Include(e => e.User)
                .FirstOrDefault(e => e.Id == employeeId);

            if (employee == null)
            {
                throw new Exception("Employee not found.");
            }

            var exit = new EmployeeExit
            {
                EmployeeId = employeeId,
                ExitType = exitType,
                IsVoluntary = isVoluntary,
                LastWorkingDate = lastWorkingDate.Date,
                Reason = reason.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                RecordedBy = recordedByUserId,
                RecordedAt = DateTime.Now
            };

            _context.EmployeeExits.Add(exit);
            employee.Status = EmployeeStatus.Inactive;
            _context.SaveChanges();

            _auditService.LogActivity(
                recordedByUserId,
                "EmployeeExitRecorded",
                "Employee",
                employee.Id,
                $"Exit recorded for employee {employee.FullName}. Type={exitType}, Voluntary={isVoluntary}, LastDay={lastWorkingDate:yyyy-MM-dd}.");

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Turnover,
                action: "exit_recorded",
                entityId: exit.Id,
                actorUserId: recordedByUserId);

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Employee,
                action: "status_inactive",
                entityId: employee.Id,
                actorUserId: recordedByUserId);

            return exit;
        }
    }
}

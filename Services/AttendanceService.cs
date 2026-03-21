using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public class AttendanceService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public AttendanceService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
        }

        public void TimeIn(int employeeId, DateTime? nowOverride = null)
        {
            var now = nowOverride ?? DateTime.Now;
            var today = now.Date;

            var shift = _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .FirstOrDefault(sa =>
                    sa.EmployeeId == employeeId &&
                    sa.Shift.ShiftDate.Date == today);

            if (shift == null)
                throw new Exception("No shift assigned today.");

            bool onApprovedLeave = _context.LeaveRequests.Any(lr =>
                lr.EmployeeId == employeeId &&
                lr.Status == LeaveStatus.Approved &&
                lr.StartDate.Date <= today &&
                lr.EndDate.Date >= today);

            if (onApprovedLeave)
                throw new Exception("Cannot clock in while on approved leave.");

            if (_context.Attendances.Any(a =>
                a.EmployeeId == employeeId &&
                a.Status == AttendanceStatus.Open))
                throw new Exception("Already clocked in.");

            var attendance = new Attendance
            {
                EmployeeId = employeeId,
                ShiftId = shift.ShiftId,
                TimeIn = now,
                Status = AttendanceStatus.Open
            };

            _context.Attendances.Add(attendance);

            _context.SaveChanges();

            var userId = _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => (int?)e.UserId)
                .FirstOrDefault();

            _auditService.LogActivity(
                userId,
                "TimeIn",
                "Attendance",
                attendance.Id,
                $"Employee {employeeId} clocked in at {attendance.TimeIn:yyyy-MM-dd HH:mm:ss}.");
        }

        public void TimeOut(int employeeId, DateTime? nowOverride = null)
        {
            var now = nowOverride ?? DateTime.Now;

            var attendance = _context.Attendances
                .Include(a => a.Shift)
                .FirstOrDefault(a =>
                    a.EmployeeId == employeeId &&
                    a.Status == AttendanceStatus.Open);

            if (attendance == null)
                throw new Exception("No open attendance.");

            attendance.TimeOut = now;

            var timeIn = attendance.TimeIn ?? now;
            var total = attendance.TimeOut.Value - timeIn;
            attendance.TotalHours = Math.Max(0m, (decimal)total.TotalHours);

            var sched = attendance.Shift.EndTime - attendance.Shift.StartTime;
            if (sched < TimeSpan.Zero)
            {
                // Overnight shift crossing midnight.
                sched += TimeSpan.FromDays(1);
            }
            var scheduledHours = (decimal)sched.TotalHours;

            attendance.OvertimeHours = Math.Max(0, attendance.TotalHours - scheduledHours);
            attendance.Status = AttendanceStatus.Closed;

            _context.SaveChanges();

            var userId = _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => (int?)e.UserId)
                .FirstOrDefault();

            _auditService.LogActivity(
                userId,
                "TimeOut",
                "Attendance",
                attendance.Id,
                $"Employee {employeeId} clocked out at {attendance.TimeOut:yyyy-MM-dd HH:mm:ss}. TotalHours={attendance.TotalHours:N2}, Overtime={attendance.OvertimeHours:N2}.");
        }

        public List<Attendance> GetRecentAttendance(int employeeId, int count)
        {
            return _context.Attendances
                .Include(a => a.Shift)
                .Where(a => a.EmployeeId == employeeId)
                .OrderByDescending(a => a.TimeIn)
                .Take(count)
                .ToList();
        }

        public Attendance? GetActiveAttendance(int employeeId)
        {
            return _context.Attendances
                .Include(a => a.Shift)
                .FirstOrDefault(a =>
                    a.EmployeeId == employeeId &&
                    a.Status == AttendanceStatus.Open);
        }
    }
}

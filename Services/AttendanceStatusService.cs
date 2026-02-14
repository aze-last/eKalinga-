using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public class AttendanceStatusService
    {
        private readonly AppDbContext _context;
        private const int LateMinutes = 5;
        private const int AbsentMinutes = 20;

        public AttendanceStatusService(AppDbContext context)
        {
            _context = context;
        }

        public List<AttendanceStatusRow> GetTodayStatuses(DateTime now)
        {
            var today = now.Date;

            var assignments = _context.ShiftAssignments
                .Include(sa => sa.Shift).ThenInclude(s => s.Position)
                .Include(sa => sa.Employee).ThenInclude(e => e.Position)
                .Where(sa => sa.Shift.ShiftDate.Date == today)
                .ToList();

            var attendances = _context.Attendances
                .Include(a => a.Shift)
                .Where(a => a.Shift.ShiftDate.Date == today)
                .ToList();

            var attendanceByAssignment = attendances
                .GroupBy(a => new { a.EmployeeId, a.ShiftId })
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.TimeIn).First());

            var employeeIds = assignments.Select(a => a.EmployeeId).Distinct().ToList();
            var employeesOnLeave = _context.LeaveRequests
                .Where(lr =>
                    employeeIds.Contains(lr.EmployeeId) &&
                    lr.Status == LeaveStatus.Approved &&
                    lr.StartDate.Date <= today &&
                    lr.EndDate.Date >= today)
                .Select(lr => lr.EmployeeId)
                .Distinct()
                .ToHashSet();

            var results = new List<AttendanceStatusRow>();

            foreach (var sa in assignments)
            {
                var shift = sa.Shift;
                var shiftStart = today.Add(shift.StartTime);
                var shiftEnd = today.Add(shift.EndTime);
                if (shiftEnd <= shiftStart)
                {
                    // Overnight shift crossing midnight.
                    shiftEnd = shiftEnd.AddDays(1);
                }
                var lateThreshold = shiftStart.AddMinutes(LateMinutes);
                var absentThreshold = shiftStart.AddMinutes(AbsentMinutes);

                attendanceByAssignment.TryGetValue(
                    new { sa.EmployeeId, sa.ShiftId },
                    out var attendance);

                bool onLeave = employeesOnLeave.Contains(sa.EmployeeId);

                var status = ResolveStatus(now, attendance, shiftStart, shiftEnd, lateThreshold, absentThreshold, onLeave);
                var color = ResolveStatusColor(status);

                results.Add(new AttendanceStatusRow
                {
                    EmployeeName = sa.Employee.FullName,
                    PositionName = sa.Employee.Position?.Name ?? shift.Position?.Name ?? "General",
                    ShiftTime = $"{shiftStart:hh:mm tt} - {shiftEnd:hh:mm tt}",
                    TimeIn = attendance?.TimeIn,
                    TimeOut = attendance?.TimeOut,
                    Status = status,
                    StatusColor = color
                });
            }

            return results;
        }

        private static string ResolveStatus(
            DateTime now,
            Attendance? attendance,
            DateTime shiftStart,
            DateTime shiftEnd,
            DateTime lateThreshold,
            DateTime absentThreshold,
            bool onLeave)
        {
            if (onLeave)
            {
                return "On Leave";
            }

            if (attendance != null &&
                (attendance.OvertimeHours > 0 ||
                 (attendance.Status == AttendanceStatus.Open && now > shiftEnd)))
            {
                return "Overtime";
            }

            if (attendance == null || !attendance.TimeIn.HasValue)
            {
                if (now >= absentThreshold)
                {
                    return "Absent";
                }

                if (now >= lateThreshold)
                {
                    return "Late";
                }

                return "Scheduled";
            }

            var timeIn = attendance.TimeIn.Value;
            if (timeIn > absentThreshold)
            {
                return "Absent";
            }

            if (timeIn > lateThreshold)
            {
                return "Late";
            }

            if (attendance.TimeOut.HasValue && attendance.TimeOut.Value < shiftEnd)
            {
                return "Early Leave";
            }

            return "On Time";
        }

        private static string ResolveStatusColor(string status)
        {
            return status switch
            {
                "Absent" => "#DC2626",
                "Late" => "#F59E0B",
                "Overtime" => "#7C3AED",
                "On Leave" => "#2563EB",
                "Early Leave" => "#FB7185",
                "On Time" => "#10B981",
                _ => "#64748B"
            };
        }
    }
}

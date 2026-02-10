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
                .Include(sa => sa.Employee)
                .Where(sa => sa.Shift.ShiftDate == today)
                .ToList();

            var attendances = _context.Attendances
                .Include(a => a.Shift)
                .Where(a => a.Shift.ShiftDate == today)
                .ToList();

            var attendanceByShiftId = attendances
                .GroupBy(a => a.ShiftId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.TimeIn).First());

            var results = new List<AttendanceStatusRow>();

            foreach (var sa in assignments)
            {
                var shift = sa.Shift;
                var shiftStart = today.Add(shift.StartTime);
                var shiftEnd = today.Add(shift.EndTime);
                var lateThreshold = shiftStart.AddMinutes(LateMinutes);
                var absentThreshold = shiftStart.AddMinutes(AbsentMinutes);

                attendanceByShiftId.TryGetValue(shift.Id, out var attendance);

                var status = ResolveStatus(now, attendance, shiftStart, shiftEnd, lateThreshold, absentThreshold);
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

        private static string ResolveStatus(DateTime now, Attendance? attendance, DateTime shiftStart, DateTime shiftEnd, DateTime lateThreshold, DateTime absentThreshold)
        {
            if (attendance != null && attendance.Status == AttendanceStatus.Open && now > shiftEnd)
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

            return "On Time";
        }

        private static string ResolveStatusColor(string status)
        {
            return status switch
            {
                "Absent" => "#DC2626",
                "Late" => "#F59E0B",
                "Overtime" => "#7C3AED",
                "On Time" => "#10B981",
                _ => "#64748B"
            };
        }
    }
}

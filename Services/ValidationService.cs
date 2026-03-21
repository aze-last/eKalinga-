using System;
using System.Linq;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class ValidationService
    {
        private readonly AppDbContext _context;

        public ValidationService(AppDbContext context)
        {
            _context = context;
        }

        public bool IsShiftOverlapping(int employeeId, DateTime date, TimeSpan start, TimeSpan end, int? excludeShiftId = null)
        {
            var query = _context.Shifts
                .Include(s => s.ShiftAssignments)
                .Where(s => s.ShiftDate.Date == date.Date &&
                            s.ShiftAssignments.Any(sa => sa.EmployeeId == employeeId));

            if (excludeShiftId.HasValue)
            {
                query = query.Where(s => s.Id != excludeShiftId.Value);
            }

            var conflictingShifts = query.ToList();

            foreach (var shift in conflictingShifts)
            {
                // Check for overlap: (StartA < EndB) and (EndA > StartB)
                if (start < shift.EndTime && end > shift.StartTime)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ValidateDailyWorkHours(int employeeId, DateTime date, double newShiftHours)
        {
            var existingHours = _context.Shifts
                .Where(s => s.ShiftDate.Date == date.Date &&
                            s.ShiftAssignments.Any(sa => sa.EmployeeId == employeeId))
                .AsEnumerable()
                .Sum(s =>
                {
                    var duration = s.EndTime - s.StartTime;
                    if (duration < TimeSpan.Zero)
                    {
                        // Overnight shift crossing midnight.
                        duration += TimeSpan.FromDays(1);
                    }

                    return duration.TotalHours;
                });

            // Policy: Max 12 hours per day
            return (existingHours + newShiftHours) <= 12;
        }

        public bool ValidateWeeklyWorkDays(int employeeId, DateTime weekStart, int newShiftDays = 1)
        {
            var weekEnd = weekStart.AddDays(7);

            var uniqueWorkDays = _context.Shifts
                .Where(s => s.ShiftDate >= weekStart && s.ShiftDate < weekEnd &&
                            s.ShiftAssignments.Any(sa => sa.EmployeeId == employeeId))
                .Select(s => s.ShiftDate.Date)
                .Distinct()
                .Count();

            // Policy: Attempt to keep to 5 days, but hard limit could be 6 or 7.
            // Let's warn if > 5, block if > 6? 
            // For now, let's just return true if acceptable (<= 6 days)
            return (uniqueWorkDays + Math.Max(1, newShiftDays)) <= 6;
        }
    }
}

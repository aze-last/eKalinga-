using System;
using System.Collections.Generic;
using System.Linq;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class AutoSchedulingService
    {
        private readonly AppDbContext _context;
        private readonly ValidationService _validationService;
        private readonly Random _random = new Random();

        public AutoSchedulingService(AppDbContext context, ValidationService validationService)
        {
            _context = context;
            _validationService = validationService;
        }

        public List<Shift> GenerateWeeklyDraft(DateTime weekStart, int createdByUserId)
        {
            var newShifts = new List<Shift>();
            var activeEmployees = _context.Employees
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();

            var managers = activeEmployees.Where(e => e.Position.Name.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var crew = activeEmployees.Except(managers).ToList();

            // 1. Assign Managers (Coverage: 1 Opener, 1 Closer per day)
            // Manager Opener: 6am - 3pm (9h)
            // Manager Closer: 2pm - 11pm (9h)
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);

                // Only assign managers if we have any
                if (managers.Any())
                {
                    // Opener
                    var mgrOpener = PickRandom(managers);
                    if (mgrOpener != null)
                    {
                        AddShift(newShifts, date, new TimeSpan(6, 0, 0), new TimeSpan(15, 0, 0), mgrOpener, createdByUserId);

                        // Closer (ensure different manager if possible)
                        var mgrCloser = PickRandom(managers.Where(m => m.Id != mgrOpener.Id).ToList()) ?? mgrOpener;
                        AddShift(newShifts, date, new TimeSpan(14, 0, 0), new TimeSpan(23, 0, 0), mgrCloser, createdByUserId);
                    }
                }
            }

            // 2. Assign Crew (Target 3-5 days/week per person)
            foreach (var emp in crew)
            {
                // Determine work days for this employee (e.g., 5 random days)
                int daysToWork = _random.Next(3, 6); // 3 to 5
                var workDaysIndices = Enumerable.Range(0, 7).OrderBy(x => _random.Next()).Take(daysToWork).ToList();

                foreach (var dayIndex in workDaysIndices)
                {
                    var date = weekStart.AddDays(dayIndex);

                    // Determine Shift Type
                    // 20% Opener (6-12), 20% Closer (5-11), 60% Mid (11-5, 12-6, etc.)
                    int roll = _random.Next(100);
                    TimeSpan start, end;

                    if (roll < 20) // Opener
                    {
                        start = new TimeSpan(6, 0, 0);
                        end = new TimeSpan(12, 0, 0);
                    }
                    else if (roll < 40) // Closer
                    {
                        start = new TimeSpan(17, 0, 0);
                        end = new TimeSpan(23, 0, 0);
                    }
                    else // Mid
                    {
                        // Randomize mid start between 10am and 1pm
                        int startHour = _random.Next(10, 14);
                        start = new TimeSpan(startHour, 0, 0);
                        end = start.Add(new TimeSpan(6, 0, 0));
                    }

                    // Check validation
                    if (!_validationService.IsShiftOverlapping(emp.Id, date, start, end))
                    {
                        AddShift(newShifts, date, start, end, emp, createdByUserId);
                    }
                }
            }

            return newShifts;
        }

        private void AddShift(List<Shift> shifts, DateTime date, TimeSpan start, TimeSpan end, Employee emp, int createdBy)
        {
            var shift = new Shift
            {
                ShiftDate = date,
                StartTime = start,
                EndTime = end,
                PositionId = emp.PositionId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            };

            shift.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = emp.Id });
            shifts.Add(shift);
        }

        private T? PickRandom<T>(List<T> list) where T : class
        {
            if (list == null || list.Count == 0) return default;
            return list[_random.Next(list.Count)];
        }
    }
}

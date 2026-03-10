using System;
using System.Collections.Generic;
using System.Linq;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class SchedulingResult
    {
        public List<Shift> Shifts { get; set; } = new();
        public double FairnessScore { get; set; }
        public string DistributionSummary { get; set; } = string.Empty;
        public int TotalManagers { get; set; }
        public int TotalCrew { get; set; }
        public double AverageAssignedShifts { get; set; }
        public double ShiftDistributionStandardDeviation { get; set; }
        public int MinimumAssignedShifts { get; set; }
        public int MaximumAssignedShifts { get; set; }
        public int UnassignedEmployees { get; set; }
    }

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

        public SchedulingResult GenerateWeeklyDraft(DateTime weekStart, int createdByUserId, IReadOnlyCollection<int>? includedEmployeeIds = null)
        {
            var result = new SchedulingResult();
            var newShifts = new List<Shift>();
            var employeeQuery = _context.Employees
                .Include(e => e.Position)
                .Include(e => e.User)
                .Where(e => e.Status == EmployeeStatus.Active)
                .AsQueryable();

            if (includedEmployeeIds is { Count: > 0 })
            {
                employeeQuery = employeeQuery.Where(e => includedEmployeeIds.Contains(e.Id));
            }

            var activeEmployees = employeeQuery.ToList();

            var managers = activeEmployees.Where(e =>
                    e.User != null && (e.User.Role == UserRole.Manager || e.User.Role == UserRole.ShiftManager)
                    || e.Position.Name.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            var crew = activeEmployees.Except(managers).ToList();

            result.TotalManagers = managers.Count;
            result.TotalCrew = crew.Count;

            // 1. Assign Managers (Coverage: 1 Opener, 1 Closer per day)
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);

                if (managers.Any())
                {
                    var mgrOpener = PickEmployeeForShift(managers, newShifts, date, new TimeSpan(6, 0, 0), new TimeSpan(15, 0, 0));
                    if (mgrOpener != null)
                    {
                        AddShift(newShifts, date, new TimeSpan(6, 0, 0), new TimeSpan(15, 0, 0), mgrOpener, createdByUserId);

                        var mgrCloser = PickEmployeeForShift(
                            managers,
                            newShifts,
                            date,
                            new TimeSpan(14, 0, 0),
                            new TimeSpan(23, 0, 0),
                            mgrOpener.Id);

                        if (mgrCloser != null)
                        {
                            AddShift(newShifts, date, new TimeSpan(14, 0, 0), new TimeSpan(23, 0, 0), mgrCloser, createdByUserId);
                        }
                    }
                }
            }

            // 2. Assign Crew (Target 3-5 days/week per person)
            foreach (var emp in crew.OrderBy(e => GetAssignedShiftCount(newShifts, e.Id)).ThenBy(_ => _random.Next()))
            {
                int daysToWork = _random.Next(3, 6);
                var workDaysIndices = Enumerable.Range(0, 7).OrderBy(x => _random.Next()).Take(daysToWork).ToList();

                foreach (var dayIndex in workDaysIndices)
                {
                    var date = weekStart.AddDays(dayIndex);
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
                        int startHour = _random.Next(10, 14);
                        start = new TimeSpan(startHour, 0, 0);
                        end = start.Add(new TimeSpan(6, 0, 0));
                    }

                    if (CanAssignShift(newShifts, emp.Id, date, start, end))
                    {
                        AddShift(newShifts, date, start, end, emp, createdByUserId);
                    }
                }
            }

            result.Shifts = newShifts;
            CalculateFairness(result, activeEmployees);

            return result;
        }

        private void CalculateFairness(SchedulingResult result, List<Employee> allEmps)
        {
            if (!allEmps.Any())
            {
                result.FairnessScore = 0;
                result.DistributionSummary = "No active employees were available for scheduling.";
                return;
            }

            var shiftCounts = allEmps.ToDictionary(e => e.Id, _ => 0);
            foreach (var assignment in result.Shifts.SelectMany(s => s.ShiftAssignments))
            {
                if (shiftCounts.ContainsKey(assignment.EmployeeId))
                {
                    shiftCounts[assignment.EmployeeId]++;
                }
            }

            if (!shiftCounts.Any())
            {
                result.FairnessScore = 0;
                result.DistributionSummary = "No shifts were generated.";
                return;
            }

            double avgShifts = shiftCounts.Values.Average();
            double variance = shiftCounts.Values.Select(v => Math.Pow(v - avgShifts, 2)).Sum() / shiftCounts.Count;
            double stdDev = Math.Sqrt(variance);
            double totalAssigned = shiftCounts.Values.Sum();
            double sumSquares = shiftCounts.Values.Select(v => Math.Pow(v, 2)).Sum();

            // Use Jain's fairness index so 100 means perfectly even distribution across the included employees.
            result.FairnessScore = totalAssigned <= 0 || sumSquares <= 0
                ? 0
                : Math.Round(((totalAssigned * totalAssigned) / (shiftCounts.Count * sumSquares)) * 100, 1);

            int min = shiftCounts.Values.Min();
            int max = shiftCounts.Values.Max();
            int unassignedEmployees = shiftCounts.Values.Count(v => v == 0);

            result.AverageAssignedShifts = Math.Round(avgShifts, 2);
            result.ShiftDistributionStandardDeviation = Math.Round(stdDev, 2);
            result.MinimumAssignedShifts = min;
            result.MaximumAssignedShifts = max;
            result.UnassignedEmployees = unassignedEmployees;
            result.DistributionSummary =
                $"Shift counts range from {min} to {max} per employee. Average: {avgShifts:F1}. " +
                $"Unassigned employees: {unassignedEmployees}. Standard deviation: {stdDev:F2}.";
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

        private bool CanAssignShift(List<Shift> pendingShifts, int employeeId, DateTime date, TimeSpan start, TimeSpan end)
        {
            return !_validationService.IsShiftOverlapping(employeeId, date, start, end)
                && !HasPendingShiftConflict(pendingShifts, employeeId, date, start, end);
        }

        private Employee? PickEmployeeForShift(
            List<Employee> employees,
            List<Shift> pendingShifts,
            DateTime date,
            TimeSpan start,
            TimeSpan end,
            int? excludeEmployeeId = null)
        {
            return employees
                .Where(e => !excludeEmployeeId.HasValue || e.Id != excludeEmployeeId.Value)
                .Where(e => CanAssignShift(pendingShifts, e.Id, date, start, end))
                .OrderBy(e => GetAssignedShiftCount(pendingShifts, e.Id))
                .ThenBy(_ => _random.Next())
                .FirstOrDefault();
        }

        private static bool HasPendingShiftConflict(List<Shift> pendingShifts, int employeeId, DateTime date, TimeSpan start, TimeSpan end)
        {
            return pendingShifts.Any(shift =>
                shift.ShiftDate.Date == date.Date
                && shift.ShiftAssignments.Any(assignment => assignment.EmployeeId == employeeId)
                && start < shift.EndTime
                && end > shift.StartTime);
        }

        private static int GetAssignedShiftCount(List<Shift> pendingShifts, int employeeId)
        {
            return pendingShifts.Count(shift => shift.ShiftAssignments.Any(assignment => assignment.EmployeeId == employeeId));
        }
    }
}

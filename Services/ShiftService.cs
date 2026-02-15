using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class ShiftService
    {
        private readonly AppDbContext _context;

        public ShiftService(AppDbContext context)
        {
            _context = context;
        }

        public void CreateWeeklyShifts(
            DateTime weekStart,
            List<DayOfWeek> days,
            TimeSpan start,
            TimeSpan end,
            int positionId,
            int managerUserId,
            List<int> employeeIds)
        {
            var normalizedWeekStart = weekStart.Date;

            foreach (var day in days)
            {
                int dayOffset = ((int)day - (int)DayOfWeek.Monday + 7) % 7;
                var date = normalizedWeekStart.AddDays(dayOffset);

                var shift = new Shift
                {
                    ShiftDate = date.Date,
                    StartTime = start,
                    EndTime = end,
                    PositionId = positionId,
                    CreatedBy = managerUserId
                };

                _context.Shifts.Add(shift);
                _context.SaveChanges();

                foreach (var empId in employeeIds)
                {
                    var sameDayAssignments = _context.ShiftAssignments
                        .Include(sa => sa.Shift)
                        .Where(sa =>
                            sa.EmployeeId == empId &&
                            sa.Shift.ShiftDate.Date == date.Date)
                        .ToList();

                    bool overlap = sameDayAssignments.Any(sa =>
                        TimeRangesOverlap(start, end, sa.Shift.StartTime, sa.Shift.EndTime));

                    if (overlap)
                        throw new Exception("Overlapping shift detected.");

                    _context.ShiftAssignments.Add(new ShiftAssignment
                    {
                        ShiftId = shift.Id,
                        EmployeeId = empId
                    });
                }
            }

            _context.SaveChanges();

            DashboardEventBus.Instance.Publish(
                DashboardDataDomain.Shift,
                action: "batch_created",
                entityId: null,
                actorUserId: managerUserId);
        }

        public List<WeeklyScheduleDto> GetWeeklySchedule(DateTime start, DateTime end, int managerId)
        {
            var from = start.Date;
            var to = end.Date;

            return _context.ShiftAssignments
                .Include(sa => sa.Employee)
                .Include(sa => sa.Shift)
                .ThenInclude(s => s.Position)
                .Where(sa => sa.Shift.ShiftDate.Date >= from &&
                             sa.Shift.ShiftDate.Date <= to &&
                             sa.Shift.CreatedBy == managerId)
                .Select(sa => new WeeklyScheduleDto
                {
                    EmployeeName = sa.Employee.FullName,
                    Date = sa.Shift.ShiftDate,
                    Display = $"{sa.Shift.StartTime:hh\\:mm}-{sa.Shift.EndTime:hh\\:mm} {sa.Shift.Position.Name}"
                })
                .ToList();
        }
        public List<ShiftAssignment> GetEmployeeWeeklySchedule(int employeeId, DateTime start, DateTime end)
        {
            var from = start.Date;
            var to = end.Date;

            return _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .ThenInclude(s => s.Position)
                .Where(sa => sa.EmployeeId == employeeId &&
                             sa.Shift.ShiftDate.Date >= from &&
                             sa.Shift.ShiftDate.Date <= to)
                .OrderBy(sa => sa.Shift.ShiftDate)
                .ToList();
        }

        public ShiftAssignment? GetEmployeeShiftForDate(int employeeId, DateTime date)
        {
            return _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .ThenInclude(s => s.Position)
                .FirstOrDefault(sa => sa.EmployeeId == employeeId &&
                                    sa.Shift.ShiftDate.Date == date.Date);
        }

        private static bool TimeRangesOverlap(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB)
        {
            var aRanges = ExpandTimeRanges(startA, endA);
            var bRanges = ExpandTimeRanges(startB, endB);
            return aRanges.Any(a => bRanges.Any(b => a.Start < b.End && a.End > b.Start));
        }

        private static List<(double Start, double End)> ExpandTimeRanges(TimeSpan start, TimeSpan end)
        {
            double startMinutes = start.TotalMinutes;
            double endMinutes = end.TotalMinutes;

            if (endMinutes <= startMinutes)
            {
                return new List<(double Start, double End)>
                {
                    (startMinutes, endMinutes + 1440),
                    (startMinutes - 1440, endMinutes)
                };
            }

            return new List<(double Start, double End)>
            {
                (startMinutes, endMinutes),
                (startMinutes + 1440, endMinutes + 1440)
            };
        }
    }
}

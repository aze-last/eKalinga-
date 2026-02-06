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
            foreach (var day in days)
            {
                var date = weekStart.AddDays((int)day);

                var shift = new Shift
                {
                    ShiftDate = date,
                    StartTime = start,
                    EndTime = end,
                    PositionId = positionId,
                    CreatedBy = managerUserId
                };

                _context.Shifts.Add(shift);
                _context.SaveChanges();

                foreach (var empId in employeeIds)
                {
                    bool overlap = _context.ShiftAssignments
                        .Include(sa => sa.Shift)
                        .Any(sa =>
                            sa.EmployeeId == empId &&
                            sa.Shift.ShiftDate == date &&
                            start < sa.Shift.EndTime &&
                            end > sa.Shift.StartTime);

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
        }

        public List<WeeklyScheduleDto> GetWeeklySchedule(DateTime start, DateTime end, int managerId)
        {
            return _context.ShiftAssignments
                .Include(sa => sa.Employee)
                .Include(sa => sa.Shift)
                .ThenInclude(s => s.Position)
                .Where(sa => sa.Shift.ShiftDate >= start &&
                             sa.Shift.ShiftDate <= end &&
                             sa.Shift.CreatedBy == managerId)
                .Select(sa => new WeeklyScheduleDto
                {
                    EmployeeName = sa.Employee.FullName,
                    Date = sa.Shift.ShiftDate,
                    Display = $"{sa.Shift.StartTime:hh\\:mm}-{sa.Shift.EndTime:hh\\:mm} {sa.Shift.Position.Name}"
                })
                .ToList();
        }
    }
}

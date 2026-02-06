using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

public class AttendanceService
{
    private readonly AppDbContext _context;

    public AttendanceService(AppDbContext context)
    {
        _context = context;
    }

    public void TimeIn(int employeeId)
    {
        var today = DateTime.Today;

        var shift = _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .FirstOrDefault(sa =>
                sa.EmployeeId == employeeId &&
                sa.Shift.ShiftDate == today);

        if (shift == null)
            throw new Exception("No shift assigned today.");

        if (_context.Attendances.Any(a =>
            a.EmployeeId == employeeId &&
            a.Status == AttendanceStatus.Open))
            throw new Exception("Already clocked in.");

        _context.Attendances.Add(new Attendance
        {
            EmployeeId = employeeId,
            ShiftId = shift.ShiftId,
            TimeIn = DateTime.Now,
            Status = AttendanceStatus.Open
        });

        _context.SaveChanges();
    }

    public void TimeOut(int employeeId)
    {
        var attendance = _context.Attendances
            .Include(a => a.Shift)
            .FirstOrDefault(a =>
                a.EmployeeId == employeeId &&
                a.Status == AttendanceStatus.Open);

        if (attendance == null)
            throw new Exception("No open attendance.");

        attendance.TimeOut = DateTime.Now;

        var total = attendance.TimeOut.Value - attendance.TimeIn.Value;
        attendance.TotalHours = (decimal)total.TotalHours;

        var sched = attendance.Shift.EndTime - attendance.Shift.StartTime;
        var scheduledHours = (decimal)sched.TotalHours;

        attendance.OvertimeHours = Math.Max(0, attendance.TotalHours - scheduledHours);
        attendance.Status = AttendanceStatus.Closed;

        _context.SaveChanges();
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class PayrollService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private const decimal OVERTIME_MULTIPLIER = 1.25m;
        private const decimal HOLIDAY_MULTIPLIER = 2.0m;
        private const decimal LATE_GRACE_MINUTES = 5.0m;

        public PayrollService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
        }

        public List<PayrollItem> GeneratePayroll(DateTime startDate, DateTime endDate)
        {
            var payrollItems = new List<PayrollItem>();

            // Get all active employees
            var employees = _context.Employees
                .Include(e => e.Position)
                .Include(e => e.User)
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();

            // Get holidays in date range
            var holidays = _context.Holidays
                .Where(h => h.HolidayDate >= startDate && h.HolidayDate <= endDate && h.IsDoublePay)
                .ToList();

            foreach (var employee in employees)
            {
                // Get closed attendance records for this employee in date range
                var attendances = _context.Attendances
                    .Include(a => a.Shift)
                    .Where(a => a.EmployeeId == employee.Id &&
                               a.TimeIn.HasValue &&
                               a.TimeOut.HasValue &&
                               a.Status == AttendanceStatus.Closed &&
                               a.TimeIn.Value.Date >= startDate.Date &&
                               a.TimeIn.Value.Date <= endDate.Date)
                    .ToList();

                decimal totalHours = 0;
                decimal overtimeHours = 0;
                decimal holidayHours = 0;
                decimal lateMinutes = 0;
                decimal earlyLeaveMinutes = 0;

                foreach (var attendance in attendances)
                {
                    totalHours += attendance.TotalHours;
                    overtimeHours += attendance.OvertimeHours;

                    var (late, early) = CalculateAttendanceTimeAdjustments(attendance);
                    lateMinutes += late;
                    earlyLeaveMinutes += early;

                    // Check if attendance date is a holiday
                    var attendanceDate = attendance.TimeIn!.Value.Date;
                    if (holidays.Any(h => h.HolidayDate.Date == attendanceDate))
                    {
                        holidayHours += attendance.TotalHours;
                    }
                }

                // Calculate pay
                decimal regularHours = Math.Max(0, totalHours - overtimeHours - holidayHours);
                decimal regularPay = regularHours * employee.HourlyRate;
                decimal overtimePay = overtimeHours * employee.HourlyRate * OVERTIME_MULTIPLIER;
                decimal holidayPay = holidayHours * employee.HourlyRate * HOLIDAY_MULTIPLIER;
                decimal grossPay = regularPay + overtimePay + holidayPay;

                decimal deductionRatePerMinute = employee.HourlyRate / 60m;
                decimal deductionAmount = Math.Round(
                    (lateMinutes + earlyLeaveMinutes) * deductionRatePerMinute,
                    2,
                    MidpointRounding.AwayFromZero);

                decimal netPay = Math.Max(0m, grossPay - deductionAmount);

                payrollItems.Add(new PayrollItem
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName,
                    TotalHours = totalHours,
                    OvertimeHours = overtimeHours,
                    HolidayHours = holidayHours,
                    RegularPay = regularPay,
                    OvertimePay = overtimePay,
                    HolidayPay = holidayPay,
                    GrossPay = grossPay,
                    LateMinutes = lateMinutes,
                    EarlyLeaveMinutes = earlyLeaveMinutes,
                    DeductionAmount = deductionAmount,
                    NetPay = netPay,
                    TotalPay = netPay
                });
            }

            return payrollItems;
        }

        public void SavePayroll(List<PayrollItem> items, DateTime startDate, DateTime endDate, int generatedByUserId)
        {
            foreach (var item in items)
            {
                var payroll = new Payroll
                {
                    EmployeeId = item.EmployeeId,
                    PeriodStart = startDate,
                    PeriodEnd = endDate,
                    RegularPay = item.RegularPay,
                    OvertimePay = item.OvertimePay,
                    HolidayPay = item.HolidayPay,
                    TotalPay = item.NetPay,
                    GeneratedAt = DateTime.Now,
                    GeneratedBy = generatedByUserId
                };

                _context.Payrolls.Add(payroll);
            }

            _context.SaveChanges();

            _auditService.LogActivity(
                generatedByUserId,
                "PayrollSaved",
                "Payroll",
                null,
                $"Saved payroll for {items.Count} employees ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}), NetTotal={items.Sum(i => i.NetPay):N2}.");
        }
        public PayrollItem GetEmployeeEarningsEstimate(int employeeId, DateTime startDate, DateTime endDate)
        {
            var employee = _context.Employees
                .Include(e => e.Position)
                .FirstOrDefault(e => e.Id == employeeId);

            if (employee == null) return new PayrollItem();

            var holidays = _context.Holidays
                .Where(h => h.HolidayDate >= startDate && h.HolidayDate <= endDate && h.IsDoublePay)
                .ToList();

            var attendances = _context.Attendances
                .Include(a => a.Shift)
                .Where(a => a.EmployeeId == employeeId &&
                           a.Status == AttendanceStatus.Closed &&
                           a.TimeIn.HasValue &&
                           a.TimeIn.Value.Date >= startDate.Date &&
                           a.TimeIn.Value.Date <= endDate.Date)
                .ToList();

            decimal totalHours = attendances.Sum(a => a.TotalHours);
            decimal overtimeHours = attendances.Sum(a => a.OvertimeHours);
            decimal holidayHours = 0;
            decimal lateMinutes = 0;
            decimal earlyLeaveMinutes = 0;

            foreach (var attendance in attendances)
            {
                var date = attendance.TimeIn!.Value.Date;
                if (holidays.Any(h => h.HolidayDate.Date == date))
                {
                    holidayHours += attendance.TotalHours;
                }

                var (late, early) = CalculateAttendanceTimeAdjustments(attendance);
                lateMinutes += late;
                earlyLeaveMinutes += early;
            }

            decimal regularHours = Math.Max(0, totalHours - overtimeHours - holidayHours);
            decimal regularPay = regularHours * employee.HourlyRate;
            decimal overtimePay = overtimeHours * employee.HourlyRate * OVERTIME_MULTIPLIER;
            decimal holidayPay = holidayHours * employee.HourlyRate * HOLIDAY_MULTIPLIER;
            decimal grossPay = regularPay + overtimePay + holidayPay;

            decimal deductionRatePerMinute = employee.HourlyRate / 60m;
            decimal deductionAmount = Math.Round(
                (lateMinutes + earlyLeaveMinutes) * deductionRatePerMinute,
                2,
                MidpointRounding.AwayFromZero);
            decimal netPay = Math.Max(0m, grossPay - deductionAmount);

            return new PayrollItem
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.FullName,
                TotalHours = totalHours,
                OvertimeHours = overtimeHours,
                HolidayHours = holidayHours,
                RegularPay = regularPay,
                OvertimePay = overtimePay,
                HolidayPay = holidayPay,
                GrossPay = grossPay,
                LateMinutes = lateMinutes,
                EarlyLeaveMinutes = earlyLeaveMinutes,
                DeductionAmount = deductionAmount,
                NetPay = netPay,
                TotalPay = netPay
            };
        }

        private static (decimal LateMinutes, decimal EarlyLeaveMinutes) CalculateAttendanceTimeAdjustments(Attendance attendance)
        {
            if (!attendance.TimeIn.HasValue || !attendance.TimeOut.HasValue)
            {
                return (0m, 0m);
            }

            var shiftDate = attendance.Shift.ShiftDate.Date;
            var shiftStart = shiftDate.Add(attendance.Shift.StartTime);
            var shiftEnd = shiftDate.Add(attendance.Shift.EndTime);
            if (shiftEnd <= shiftStart)
            {
                // Overnight shift crossing midnight.
                shiftEnd = shiftEnd.AddDays(1);
            }

            decimal lateMinutes = Math.Max(
                0m,
                (decimal)(attendance.TimeIn.Value - shiftStart).TotalMinutes - LATE_GRACE_MINUTES);

            decimal earlyLeaveMinutes = Math.Max(
                0m,
                (decimal)(shiftEnd - attendance.TimeOut.Value).TotalMinutes);

            return (lateMinutes, earlyLeaveMinutes);
        }
    }

    // Helper class for payroll calculations
    public class PayrollItem
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal HolidayHours { get; set; }
        public decimal RegularPay { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal HolidayPay { get; set; }
        public decimal GrossPay { get; set; }
        public decimal LateMinutes { get; set; }
        public decimal EarlyLeaveMinutes { get; set; }
        public decimal DeductionAmount { get; set; }
        public decimal NetPay { get; set; }
        public decimal TotalPay { get; set; }
    }
}

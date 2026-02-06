using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Services
{
    public class PayrollService
    {
        private readonly AppDbContext _context;
        private const decimal OVERTIME_MULTIPLIER = 1.25m;
        private const decimal HOLIDAY_MULTIPLIER = 2.0m;

        public PayrollService()
        {
            _context = new AppDbContext();
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

                foreach (var attendance in attendances)
                {
                    totalHours += attendance.TotalHours;
                    overtimeHours += attendance.OvertimeHours;

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
                decimal totalPay = regularPay + overtimePay + holidayPay;

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
                    TotalPay = totalPay
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
                    TotalPay = item.TotalPay,
                    GeneratedAt = DateTime.Now,
                    GeneratedBy = generatedByUserId
                };

                _context.Payrolls.Add(payroll);
            }

            _context.SaveChanges();
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
        public decimal TotalPay { get; set; }
    }
}
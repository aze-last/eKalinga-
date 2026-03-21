using System;

namespace AttendanceShiftingManagement.Models
{
    public class PayrollResult
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public decimal RegularHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal GrossPay { get; set; }
        public decimal NetPay { get; set; }
    }
}

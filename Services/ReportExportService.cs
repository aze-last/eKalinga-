using AttendanceShiftingManagement.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AttendanceShiftingManagement.Services
{
    public class ReportExportService
    {
        public void ExportPayrollCsv(IEnumerable<PayrollItem> items, string filePath, DateTime startDate, DateTime endDate)
        {
            var rows = items.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("ASMS Payroll Report");
            sb.AppendLine($"Period,{startDate:yyyy-MM-dd},{endDate:yyyy-MM-dd}");
            sb.AppendLine($"GeneratedAt,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Employee,TotalHours,OvertimeHours,HolidayHours,RegularPay,OvertimePay,HolidayPay,GrossPay,LateMinutes,EarlyLeaveMinutes,DeductionAmount,NetPay");

            foreach (var item in rows)
            {
                sb.AppendLine(string.Join(",",
                    Escape(item.EmployeeName),
                    item.TotalHours.ToString("F2", CultureInfo.InvariantCulture),
                    item.OvertimeHours.ToString("F2", CultureInfo.InvariantCulture),
                    item.HolidayHours.ToString("F2", CultureInfo.InvariantCulture),
                    item.RegularPay.ToString("F2", CultureInfo.InvariantCulture),
                    item.OvertimePay.ToString("F2", CultureInfo.InvariantCulture),
                    item.HolidayPay.ToString("F2", CultureInfo.InvariantCulture),
                    item.GrossPay.ToString("F2", CultureInfo.InvariantCulture),
                    item.LateMinutes.ToString("F0", CultureInfo.InvariantCulture),
                    item.EarlyLeaveMinutes.ToString("F0", CultureInfo.InvariantCulture),
                    item.DeductionAmount.ToString("F2", CultureInfo.InvariantCulture),
                    item.NetPay.ToString("F2", CultureInfo.InvariantCulture)));
            }

            sb.AppendLine();
            sb.AppendLine(string.Join(",",
                "Totals",
                rows.Sum(i => i.TotalHours).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.OvertimeHours).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.HolidayHours).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.RegularPay).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.OvertimePay).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.HolidayPay).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.GrossPay).ToString("F2", CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                rows.Sum(i => i.DeductionAmount).ToString("F2", CultureInfo.InvariantCulture),
                rows.Sum(i => i.NetPay).ToString("F2", CultureInfo.InvariantCulture)));

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportAttendanceCsv(IEnumerable<AttendanceDto> items, string filePath, DateTime reportDate)
        {
            var rows = items.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("ASMS Daily Attendance Report");
            sb.AppendLine($"ReportDate,{reportDate:yyyy-MM-dd}");
            sb.AppendLine($"GeneratedAt,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Name,Position,TimeIn,TimeOut,Status");

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",",
                    Escape(row.Name),
                    Escape(row.Position),
                    Escape(row.TimeIn?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty),
                    Escape(row.TimeOut?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty),
                    Escape(row.Status)));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}

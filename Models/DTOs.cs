using System;

namespace AttendanceShiftingManagement.Models
{
    public class AttendanceDto
    {
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string ClockInTime => TimeIn?.ToString("hh:mm tt") ?? "---";
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#64748B";
    }

    public class WeeklyScheduleDto
    {
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Display { get; set; } = string.Empty;

        // Properties for the UI grid (optional mapping)
        public string MondayShift { get; set; } = string.Empty;
        public string TuesdayShift { get; set; } = string.Empty;
        public string WednesdayShift { get; set; } = string.Empty;
        public string ThursdayShift { get; set; } = string.Empty;
        public string FridayShift { get; set; } = string.Empty;
        public string SaturdayShift { get; set; } = string.Empty;
        public string SundayShift { get; set; } = string.Empty;
    }

    public class AttendanceStatusRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string ShiftTime { get; set; } = string.Empty;
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#64748B";
        public string ClockInTime => TimeIn?.ToString("hh:mm tt") ?? "---";
        public string ClockOutTime => TimeOut?.ToString("hh:mm tt") ?? "---";
    }
}

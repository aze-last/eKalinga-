using System;

namespace AttendanceShiftingManagement.Models
{
    public class AttendanceDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string Status { get; set; } = string.Empty;
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
}

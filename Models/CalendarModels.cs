using System.Windows.Media;

namespace AttendanceShiftingManagement.Models
{
    public class EmployeeWeeklySchedule
    {
        public string EmployeeName { get; set; } = string.Empty;
        public DayCell Monday { get; set; } = new DayCell();
        public DayCell Tuesday { get; set; } = new DayCell();
        public DayCell Wednesday { get; set; } = new DayCell();
        public DayCell Thursday { get; set; } = new DayCell();
        public DayCell Friday { get; set; } = new DayCell();
        public DayCell Saturday { get; set; } = new DayCell();
        public DayCell Sunday { get; set; } = new DayCell();
    }

    public class DayCell
    {
        public string TimeDisplay { get; set; } = "OFF";
        public string PositionName { get; set; } = string.Empty;
        public Brush CellColor { get; set; } = new SolidColorBrush(Color.FromRgb(224, 224, 224)); // Default Gray
        public Brush ForegroundColor { get; set; } = Brushes.Black;
    }
}

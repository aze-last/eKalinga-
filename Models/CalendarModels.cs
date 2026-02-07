using AttendanceShiftingManagement.Helpers;
using System;
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

    public class DayCell : ObservableObject
    {
        private string _timeDisplay = "OFF";
        private string _positionName = string.Empty;
        private Brush _cellColor = new SolidColorBrush(Color.FromRgb(224, 224, 224));
        private Brush _foregroundColor = Brushes.Black;
        private bool _isEditing;
        private string _startTimeText = string.Empty;
        private string _endTimeText = string.Empty;
        private int _positionId;

        public int EmployeeId { get; set; }
        public int ShiftAssignmentId { get; set; }
        public int ShiftId { get; set; }
        public DateTime Date { get; set; }
        public bool HasShift { get; set; }

        public string OriginalStartTimeText { get; set; } = string.Empty;
        public string OriginalEndTimeText { get; set; } = string.Empty;
        public int OriginalPositionId { get; set; }

        public string TimeDisplay
        {
            get => _timeDisplay;
            set => SetProperty(ref _timeDisplay, value);
        }

        public string PositionName
        {
            get => _positionName;
            set => SetProperty(ref _positionName, value);
        }

        public Brush CellColor
        {
            get => _cellColor;
            set => SetProperty(ref _cellColor, value);
        }

        public Brush ForegroundColor
        {
            get => _foregroundColor;
            set => SetProperty(ref _foregroundColor, value);
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public string StartTimeText
        {
            get => _startTimeText;
            set => SetProperty(ref _startTimeText, value);
        }

        public string EndTimeText
        {
            get => _endTimeText;
            set => SetProperty(ref _endTimeText, value);
        }

        public int PositionId
        {
            get => _positionId;
            set => SetProperty(ref _positionId, value);
        }
    }
}

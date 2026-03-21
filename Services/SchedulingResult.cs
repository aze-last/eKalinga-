using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Services
{
    public class SchedulingResult
    {
        public List<Shift> Shifts { get; set; } = new();
        public double FairnessScore { get; set; }
        public int TotalManagers { get; set; }
        public int TotalCrew { get; set; }
        public double AverageAssignedShifts { get; set; }
        public string DistributionSummary { get; set; } = string.Empty;
    }
}

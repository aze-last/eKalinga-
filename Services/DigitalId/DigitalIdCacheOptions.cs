namespace AttendanceShiftingManagement.Services
{
    public class DigitalIdCacheOptions
    {
        public int PhotoRetentionDays { get; set; } = 30;
        public int StatusRetentionDays { get; set; } = 7;
        public int OfflineValidityHours { get; set; } = 24;
    }
}

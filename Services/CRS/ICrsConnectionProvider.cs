namespace AttendanceShiftingManagement.Services
{
    public interface ICrsConnectionProvider
    {
        string GetConnectionString();
    }

    public class CrsConnectionProvider : ICrsConnectionProvider
    {
        public string GetConnectionString()
        {
            var settings = ConnectionSettingsService.Load();
            var remotePreset = settings.GetPreset("Remote");
            return ConnectionSettingsService.BuildConnectionString(remotePreset);
        }
    }
}

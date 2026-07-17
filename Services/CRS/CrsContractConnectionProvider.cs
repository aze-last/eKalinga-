namespace AttendanceShiftingManagement.Services
{
    /// <summary>
    /// Connection string source for the e-Kard CRS verification contract database.
    /// Separate from <see cref="CrsConnectionProvider"/> (which points at the app's own
    /// AMS remote mirror). Always applies GuidFormat=None per the contract — the CRS
    /// SyncId columns are CHAR(36) and MySqlConnector would otherwise auto-map them
    /// to System.Guid and fail.
    /// </summary>
    public interface ICrsContractConnectionProvider
    {
        string GetConnectionString();
        bool IsConfigured();
    }

    public class CrsContractConnectionProvider : ICrsContractConnectionProvider
    {
        public string GetConnectionString()
        {
            var options = CrsContractRuntimeOptions.Load();
            return ConnectionSettingsService.BuildConnectionString(options.CrsContractConnection, guidFormatNone: true);
        }

        public bool IsConfigured()
        {
            var options = CrsContractRuntimeOptions.Load();
            return ConnectionSettingsService.IsPresetConfigured(options.CrsContractConnection);
        }
    }
}

namespace AttendanceShiftingManagement.Services
{
    public static class AppUpdateCoordinator
    {
        private static readonly SemaphoreSlim CheckGate = new(1, 1);
        private static UpdateCheckResult _latestResult = new()
        {
            Status = UpdateCheckStatus.NotChecked,
            CurrentVersion = AppVersionService.GetCurrentVersion(),
            Message = "No update check has run yet."
        };

        public static UpdateCheckResult LatestResult => _latestResult;

        public static void StartBackgroundCheck()
        {
            _ = Task.Run(async () =>
            {
                var preferences = AppPreferencesService.Load();
                if (!preferences.CheckForUpdatesOnStartup)
                {
                    return;
                }

                await CheckNowAsync(preferences.UpdateManifestUrl);
            });
        }

        public static async Task<UpdateCheckResult> CheckNowAsync(string? manifestUrl = null, CancellationToken cancellationToken = default)
        {
            var preferences = AppPreferencesService.Load();
            var effectiveManifestUrl = string.IsNullOrWhiteSpace(manifestUrl)
                ? preferences.UpdateManifestUrl
                : manifestUrl.Trim();

            await CheckGate.WaitAsync(cancellationToken);
            try
            {
                _latestResult = await UpdateCheckService.CheckForUpdatesAsync(effectiveManifestUrl, cancellationToken);
                return _latestResult;
            }
            finally
            {
                CheckGate.Release();
            }
        }
    }
}

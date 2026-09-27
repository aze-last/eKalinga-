using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace AttendanceShiftingManagement.Services
{
    internal static class RemoteWriteExecutionService
    {
        private const string RemotePresetKey = "Remote";
        private static readonly AsyncLocal<int> RemoteWriteDepth = new();

        public static bool ShouldRouteToRemote(LocalDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (IsInMemoryProvider(context) || IsRemoteWriteInProgress())
            {
                return false;
            }

            var settings = ConnectionSettingsService.Load();
            if (ConnectionSettingsService.IsLanPresetKey(settings.SelectedPreset))
            {
                return false;
            }

            var remotePreset = settings.GetPreset(RemotePresetKey);
            if (!ConnectionSettingsService.IsPresetConfigured(remotePreset) || !remotePreset.IsEnabled)
            {
                return false;
            }

            return !IsRemoteContext(context, settings);
        }

        public static async Task<TResult> ExecuteRemoteWriteAsync<TResult>(
            LocalDbContext currentContext,
            Func<LocalDbContext, Task<TResult>> remoteAction,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(currentContext);
            ArgumentNullException.ThrowIfNull(remoteAction);

            var settings = ConnectionSettingsService.Load();
            var remotePreset = settings.GetPreset(RemotePresetKey);
            if (!ConnectionSettingsService.IsPresetConfigured(remotePreset))
            {
                throw new InvalidOperationException("Remote app database is not configured.");
            }

            var remoteConnectionString = ConnectionSettingsService.BuildConnectionString(remotePreset);
            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseMySql(remoteConnectionString, ServerVersion.AutoDetect(remoteConnectionString))
                .Options;

            await using var remoteContext = new LocalDbContext(options);
            EnterRemoteWriteScope();
            TResult result;
            try
            {
                result = await remoteAction(remoteContext);
            }
            finally
            {
                ExitRemoteWriteScope();
            }

            return result;
        }

        private static bool IsRemoteContext(LocalDbContext context, ConnectionSettingsModel settings)
        {
            if (!string.Equals(context.Database.ProviderName, "Pomelo.EntityFrameworkCore.MySql", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remotePreset = settings.GetPreset(RemotePresetKey);
            if (!ConnectionSettingsService.IsPresetConfigured(remotePreset))
            {
                return false;
            }

            var currentConnectionString = context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(currentConnectionString))
            {
                return false;
            }

            var remoteConnectionString = ConnectionSettingsService.BuildConnectionString(remotePreset);
            if (string.Equals(currentConnectionString, remoteConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var currentBuilder = new MySqlConnector.MySqlConnectionStringBuilder(currentConnectionString);
                var remoteBuilder = new MySqlConnector.MySqlConnectionStringBuilder(remoteConnectionString);

                return string.Equals(currentBuilder.Server, remoteBuilder.Server, StringComparison.OrdinalIgnoreCase)
                    && currentBuilder.Port == remoteBuilder.Port
                    && string.Equals(currentBuilder.Database, remoteBuilder.Database, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(currentBuilder.UserID, remoteBuilder.UserID, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInMemoryProvider(LocalDbContext context)
        {
            return string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal);
        }

        private static bool IsRemoteWriteInProgress()
        {
            return RemoteWriteDepth.Value > 0;
        }

        private static void EnterRemoteWriteScope()
        {
            RemoteWriteDepth.Value++;
        }

        private static void ExitRemoteWriteScope()
        {
            if (RemoteWriteDepth.Value > 0)
            {
                RemoteWriteDepth.Value--;
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using AttendanceShiftingManagement.Services;
using System;

namespace AttendanceShiftingManagement.Data
{
	public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		internal const string PresetOverrideEnvironmentVariable = "ASM_DB_PRESET";
		internal const string ConnectionStringOverrideEnvironmentVariable = "ASM_DB_CONNECTION_STRING";

		public AppDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
			var settings = ConnectionSettingsService.Load();
			var connectionString = ResolveConnectionString(
				settings,
				Environment.GetEnvironmentVariable(PresetOverrideEnvironmentVariable),
				Environment.GetEnvironmentVariable(ConnectionStringOverrideEnvironmentVariable));

			optionsBuilder.UseMySql(
				connectionString,
				ServerVersion.AutoDetect(connectionString)
			);

			return new AppDbContext(optionsBuilder.Options);
		}

		internal static string ResolveConnectionString(
			ConnectionSettingsModel settings,
			string? presetOverride,
			string? connectionStringOverride)
		{
			ArgumentNullException.ThrowIfNull(settings);

			if (!string.IsNullOrWhiteSpace(connectionStringOverride))
			{
				return connectionStringOverride.Trim();
			}

			var presetKey = string.IsNullOrWhiteSpace(presetOverride)
				? settings.SelectedPreset
				: presetOverride.Trim();

			if (!settings.Presets.ContainsKey(presetKey))
			{
				throw new InvalidOperationException($"Database preset '{presetKey}' was not found. Set {ConnectionStringOverrideEnvironmentVariable} or use a valid preset.");
			}

			return ConnectionSettingsService.BuildConnectionString(settings.GetPreset(presetKey));
		}
	}
}

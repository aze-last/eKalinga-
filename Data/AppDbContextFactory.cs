using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using AttendanceShiftingManagement.Services;
using System;

namespace AttendanceShiftingManagement.Data
{
	public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

			var connectionString = ConnectionSettingsService.BuildConnectionString(new DatabaseConnectionPreset
			{
				DisplayName = "Local",
				Server = "127.0.0.1",
				Port = 3306,
				Database = "attendance_shifting_db",
				Username = "root",
				Password = "codenameHylux122818"
			});

			optionsBuilder.UseMySql(
				connectionString,
				new MySqlServerVersion(new Version(8, 0, 36))
			);

			return new AppDbContext(optionsBuilder.Options);
		}
	}
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace AttendanceShiftingManagement.Data
{
	public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

			var connectionString =
				"server=localhost;database=attendance_shifting_db;user=root;password=codenameHylux122818;";

			optionsBuilder.UseMySql(
				connectionString,
				new MySqlServerVersion(new Version(8, 0, 36))
			);

			return new AppDbContext(optionsBuilder.Options);
		}
	}
}

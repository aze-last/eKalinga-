using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
namespace AttendanceShiftingManagement.Data
{


    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                var connectionString = config.GetConnectionString("DefaultConnection");
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure enum to string conversions
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<Position>()
                .Property(p => p.Area)
                .HasConversion<string>();

            modelBuilder.Entity<Employee>()
                .Property(e => e.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Attendance>()
                .Property(a => a.Status)
                .HasConversion<string>();

            modelBuilder.Entity<LeaveRequest>()
                .Property(lr => lr.Type)
                .HasConversion<string>();

            modelBuilder.Entity<LeaveRequest>()
                .Property(lr => lr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Notification>()
                .Property(n => n.Type)
                .HasConversion<string>();

            modelBuilder.Entity<UserProfile>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            modelBuilder.Entity<UserPreference>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // Unique constraint for shift assignments
            modelBuilder.Entity<ShiftAssignment>()
                .HasIndex(sa => new { sa.ShiftId, sa.EmployeeId })
                .IsUnique();

            // Unique constraint for user-employee relationship
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.UserId)
                .IsUnique();
        }
    }
}

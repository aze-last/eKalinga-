using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public class AuditService
    {
        private readonly AppDbContext _context;

        public AuditService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Log an activity
        /// </summary>
        public void LogActivity(int? userId, string action, string entity, int? entityId, string details, string ipAddress = "127.0.0.1")
        {
            var log = new ActivityLog
            {
                UserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                Details = details,
                IpAddress = ipAddress,
                Timestamp = DateTime.Now
            };

            _context.ActivityLogs.Add(log);
            _context.SaveChanges();
        }

        /// <summary>
        /// Get activity logs with filtering
        /// </summary>
        public List<ActivityLog> GetActivityLogs(DateTime? startDate = null, DateTime? endDate = null, string? entity = null, int? userId = null, int count = 100)
        {
            var query = _context.ActivityLogs
                .Include(al => al.User)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(al => al.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(al => al.Timestamp <= endDate.Value);

            if (!string.IsNullOrEmpty(entity))
                query = query.Where(al => al.Entity == entity);

            if (userId.HasValue)
                query = query.Where(al => al.UserId == userId.Value);

            return query
                .OrderByDescending(al => al.Timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Get user activity logs
        /// </summary>
        public List<ActivityLog> GetUserActivityLogs(int userId, int count = 50)
        {
            return _context.ActivityLogs
                .Where(al => al.UserId == userId)
                .OrderByDescending(al => al.Timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Get entity activity logs
        /// </summary>
        public List<ActivityLog> GetEntityActivityLogs(string entity, int entityId, int count = 50)
        {
            return _context.ActivityLogs
                .Include(al => al.User)
                .Where(al => al.Entity == entity && al.EntityId == entityId)
                .OrderByDescending(al => al.Timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Delete old activity logs (for maintenance)
        /// </summary>
        public void DeleteOldLogs(DateTime olderThan)
        {
            var oldLogs = _context.ActivityLogs
                .Where(al => al.Timestamp < olderThan)
                .ToList();

            _context.ActivityLogs.RemoveRange(oldLogs);
            _context.SaveChanges();
        }
    }
}

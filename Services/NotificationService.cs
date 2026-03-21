using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using System;
using System.Collections.Generic;

namespace AttendanceShiftingManagement.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public void Create(int userId, NotificationType type, string title, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();
        }

        public void CreateForUsers(IEnumerable<int> userIds, NotificationType type, string title, string message)
        {
            foreach (var userId in userIds)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = type,
                    Title = title,
                    Message = message,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }

            _context.SaveChanges();
        }
    }
}

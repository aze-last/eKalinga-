using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Create a new notification
        /// </summary>
        public Notification CreateNotification(int userId, NotificationType type, string title, string message, string? actionUrl = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ActionUrl = actionUrl,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            return notification;
        }

        /// <summary>
        /// Get unread notifications for a user
        /// </summary>
        public List<Notification> GetUnreadNotifications(int userId)
        {
            return _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Get all notifications for a user
        /// </summary>
        public List<Notification> GetAllNotifications(int userId, int count = 20)
        {
            return _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        public int GetUnreadCount(int userId)
        {
            return _context.Notifications
                .Count(n => n.UserId == userId && !n.IsRead);
        }

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        public void MarkAsRead(int notificationId)
        {
            var notification = _context.Notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Mark all notifications as read for a user
        /// </summary>
        public void MarkAllAsRead(int userId)
        {
            var notifications = _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToList();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            _context.SaveChanges();
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        public void DeleteNotification(int notificationId)
        {
            var notification = _context.Notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Delete all read notifications for a user
        /// </summary>
        public void DeleteReadNotifications(int userId)
        {
            var notifications = _context.Notifications
                .Where(n => n.UserId == userId && n.IsRead)
                .ToList();

            _context.Notifications.RemoveRange(notifications);
            _context.SaveChanges();
        }
    }
}

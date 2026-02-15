using System;

namespace AttendanceShiftingManagement.Services
{
    public enum DashboardDataDomain
    {
        Attendance,
        Employee,
        Shift,
        Leave,
        Payroll
    }

    public sealed class DashboardDataChangedEventArgs : EventArgs
    {
        public DashboardDataChangedEventArgs(
            DashboardDataDomain domain,
            string action,
            int? entityId,
            int? actorUserId,
            DateTime occurredAt)
        {
            Domain = domain;
            Action = action;
            EntityId = entityId;
            ActorUserId = actorUserId;
            OccurredAt = occurredAt;
        }

        public DashboardDataDomain Domain { get; }
        public string Action { get; }
        public int? EntityId { get; }
        public int? ActorUserId { get; }
        public DateTime OccurredAt { get; }
    }

    public sealed class DashboardEventBus
    {
        private DashboardEventBus()
        {
        }

        public static DashboardEventBus Instance { get; } = new();

        public event EventHandler<DashboardDataChangedEventArgs>? DashboardDataChanged;

        public void Publish(
            DashboardDataDomain domain,
            string action = "updated",
            int? entityId = null,
            int? actorUserId = null)
        {
            DashboardDataChanged?.Invoke(
                this,
                new DashboardDataChangedEventArgs(domain, action, entityId, actorUserId, DateTime.Now));
        }
    }
}

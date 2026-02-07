using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ManagerDashboardViewModel : ObservableObject
    {
        private int _totalCrew;
        private int _onDutyCount;
        private int _pendingShifts;
        private string _totalWeekHours = "0.0";

        public int TotalCrew { get => _totalCrew; set => SetProperty(ref _totalCrew, value); }
        public int OnDutyCount { get => _onDutyCount; set => SetProperty(ref _onDutyCount, value); }
        public int PendingShifts { get => _pendingShifts; set => SetProperty(ref _pendingShifts, value); }
        public string TotalWeekHours { get => _totalWeekHours; set => SetProperty(ref _totalWeekHours, value); }

        public ObservableCollection<AttendanceDto> TodayAttendance { get; }

        public ManagerDashboardViewModel(AppDbContext ctx, User user)
        {
            // Calculate Stats
            TotalCrew = ctx.Employees.Count(e => e.Status == EmployeeStatus.Active);
            OnDutyCount = ctx.Attendances.Count(a => a.Status == AttendanceStatus.Open);

            // For now, pending shifts as mock or actual if you have a field for it
            PendingShifts = 0;

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            var totalHours = ctx.Attendances
                .Where(a => a.TimeIn >= startOfWeek && a.Status == AttendanceStatus.Closed)
                .Sum(a => (double)a.TotalHours);
            TotalWeekHours = totalHours.ToString("N1");

            var attendanceList = ctx.Attendances
                   .Include(a => a.Employee)
                   .ThenInclude(e => e.Position)
                   .Where(a => a.TimeIn.HasValue && a.TimeIn.Value.Date == DateTime.Today)
                   .OrderByDescending(a => a.TimeIn)
                   .Select(a => new AttendanceDto
                   {
                       Name = a.Employee.FullName,
                       Position = a.Employee.Position.Name,
                       TimeIn = a.TimeIn,
                       TimeOut = a.TimeOut,
                       Status = a.Status == AttendanceStatus.Open ? "ACTIVE" : "CLOSED",
                       StatusColor = a.Status == AttendanceStatus.Open ? "#10B981" : "#64748B"
                   }).ToList();

            TodayAttendance = new ObservableCollection<AttendanceDto>(attendanceList);
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ManagerDashboardViewModel : ObservableObject
    {
        public ObservableCollection<AttendanceDto> TodayAttendance { get; }

        public ManagerDashboardViewModel(AppDbContext ctx, User user)
        {
            var attendanceList = ctx.Attendances
                   .Where(a => a.TimeIn.HasValue && a.TimeIn.Value.Date == DateTime.Today)
                   .Select(a => new AttendanceDto
                   {
                       Name = a.Employee.FullName,
                       TimeIn = a.TimeIn,
                       TimeOut = a.TimeOut,
                       Status = a.Status.ToString()
                   }).ToList();

            TodayAttendance = new ObservableCollection<AttendanceDto>(attendanceList);
        }
    }
}

using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Models;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class CrewDashboardViewModel : ObservableObject
    {
        private readonly AttendanceService _attendance;
        private readonly int _employeeId;

        public ICommand TimeInCommand { get; }
        public ICommand TimeOutCommand { get; }
        public string StatusText { get; set; } = "Ready";

        public CrewDashboardViewModel(AttendanceService service, int employeeId)
        {
            _attendance = service;
            _employeeId = employeeId;

            TimeInCommand = new RelayCommand(_ => _attendance.TimeIn(_employeeId));
            TimeOutCommand = new RelayCommand(_ => _attendance.TimeOut(_employeeId));
        }
    }
}

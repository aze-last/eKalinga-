using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Windows.Input;
using System.Linq;
using System;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ShiftsManagementViewModel : ObservableObject
    {
        private readonly ShiftService _service;
        private readonly User _manager;

        public ICommand CreateShiftCommand { get; }
        public DateTime WeekStart { get; set; } = DateTime.Today;
        public object SelectedDays { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public Position SelectedPosition { get; set; }
        public System.Collections.Generic.List<Employee> SelectedCrew { get; set; } = new();

        public ShiftsManagementViewModel(ShiftService service, User manager)
        {
            _service = service;
            _manager = manager;

            CreateShiftCommand = new RelayCommand(_ => CreateShift());
        }

        private void CreateShift()
        {
            // Implementation placeholder to match original logic
        }
    }
}

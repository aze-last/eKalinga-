using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ShiftsManagementViewModel : ObservableObject
    {
        private readonly ShiftService _shiftService;
        private readonly Data.AppDbContext _context;
        private DateTime _selectedDay;
        private Position? _selectedPosition;
        private TimeSpan _startTime = new TimeSpan(6, 0, 0);
        private TimeSpan _endTime = new TimeSpan(14, 0, 0);
        private readonly User _currentUser;

        public DateTime SelectedDay
        {
            get => _selectedDay;
            set
            {
                if (SetProperty(ref _selectedDay, value))
                {
                    LoadRoster();
                }
            }
        }

        public ObservableCollection<RosterRow> Roster { get; } = new();
        public ObservableCollection<Position> Positions { get; } = new();
        public ObservableCollection<EmployeeSelectable> Employees { get; } = new();

        public Position? SelectedPosition { get => _selectedPosition; set => SetProperty(ref _selectedPosition, value); }
        public TimeSpan StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }
        public TimeSpan EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }

        public List<string> HoursLabels { get; } = new();

        public ICommand CreateBatchCommand { get; }
        public ICommand RefreshCommand { get; }

        public ShiftsManagementViewModel(User user)
        {
            _currentUser = user;
            _context = new Data.AppDbContext();
            _shiftService = new ShiftService(_context);

            SelectedDay = DateTime.Today;

            CreateBatchCommand = new RelayCommand(_ => ExecuteBatchCreate(), _ => SelectedPosition != null && Employees.Any(e => e.IsSelected));
            RefreshCommand = new RelayCommand(_ => LoadRoster());

            // Initialize labels for 6 AM to 11 PM
            for (int h = 6; h <= 23; h++)
            {
                DateTime dt = DateTime.Today.AddHours(h);
                HoursLabels.Add(dt.ToString("htt"));
            }

            LoadMetadata();
            LoadRoster();
        }

        private void LoadMetadata()
        {
            Positions.Clear();
            foreach (var p in _context.Positions.ToList()) Positions.Add(p);

            Employees.Clear();
            foreach (var e in _context.Employees.Include(e => e.Position).Where(e => e.Status == EmployeeStatus.Active).ToList())
                Employees.Add(new EmployeeSelectable { Employee = e });

            if (Positions.Any()) SelectedPosition = Positions.First();
        }

        private void LoadRoster()
        {
            Roster.Clear();
            var assignments = _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .ThenInclude(s => s.Position)
                .Include(sa => sa.Employee)
                .Where(sa => sa.Shift.ShiftDate == SelectedDay)
                .ToList();

            var employeesInRoster = _context.Employees
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();

            foreach (var emp in employeesInRoster)
            {
                var row = new RosterRow { EmployeeName = emp.FullName };
                var empAssignments = assignments.Where(a => a.EmployeeId == emp.Id).ToList();

                foreach (var assign in empAssignments)
                {
                    row.FillShift(assign.Shift.StartTime, assign.Shift.EndTime, assign.Shift.Position.Name);
                }
                Roster.Add(row);
            }
        }

        private void ExecuteBatchCreate()
        {
            try
            {
                var selectedEmps = Employees.Where(e => e.IsSelected).Select(e => e.Employee.Id).ToList();

                // Create for the whole week starting from SelectedDay (or we can assume Monday)
                var startOfWeek = SelectedDay.AddDays(-(int)SelectedDay.DayOfWeek + (int)DayOfWeek.Monday);
                var days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();

                _shiftService.CreateWeeklyShifts(
                    startOfWeek,
                    days,
                    StartTime,
                    EndTime,
                    SelectedPosition!.Id,
                    _currentUser.Id,
                    selectedEmps);

                System.Windows.MessageBox.Show("Weekly shifts created successfully!", "Success");
                LoadRoster();
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += "\n\nInner: " + ex.InnerException.Message;
                System.Windows.MessageBox.Show(msg, "Error");
            }
        }
    }

    public class RosterRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public ObservableCollection<ShiftSegment> Segments { get; } = new();

        public RosterRow()
        {
            // Initialize 18 slots (6 AM to 11 PM)
            for (int i = 0; i < 18; i++) Segments.Add(new ShiftSegment { IsActive = false });
        }

        public void FillShift(TimeSpan start, TimeSpan end, string position)
        {
            int startIdx = start.Hours - 6;
            int endIdx = end.Hours - 6;

            for (int i = Math.Max(0, startIdx); i < Math.Min(18, endIdx); i++)
            {
                Segments[i].IsActive = true;
                Segments[i].Label = position;
            }
        }
    }

    public class ShiftSegment : ObservableObject
    {
        private bool _isActive;
        private string _label = string.Empty;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public string Label { get => _label; set => SetProperty(ref _label, value); }
    }

    public class EmployeeSelectable : ObservableObject
    {
        private bool _isSelected;
        public Employee Employee { get; set; } = null!;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    }
}

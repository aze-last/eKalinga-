using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public class WeeklyCalendarViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly int? _employeeId;
        private readonly int _managerUserId;
        private DateTime _currentWeekStart;
        private string _weekRangeDisplay = string.Empty;

        // Header Properties
        private string _mondayHeader = string.Empty;
        private string _tuesdayHeader = string.Empty;
        private string _wednesdayHeader = string.Empty;
        private string _thursdayHeader = string.Empty;
        private string _fridayHeader = string.Empty;
        private string _saturdayHeader = string.Empty;
        private string _sundayHeader = string.Empty;

        public DateTime CurrentWeekStart
        {
            get => _currentWeekStart;
            set
            {
                if (SetProperty(ref _currentWeekStart, value))
                {
                    UpdateWeekHeaders();
                    LoadWeeklySchedule();
                }
            }
        }

        public string WeekRangeDisplay
        {
            get => _weekRangeDisplay;
            set => SetProperty(ref _weekRangeDisplay, value);
        }

        public string MondayHeader { get => _mondayHeader; set => SetProperty(ref _mondayHeader, value); }
        public string TuesdayHeader { get => _tuesdayHeader; set => SetProperty(ref _tuesdayHeader, value); }
        public string WednesdayHeader { get => _wednesdayHeader; set => SetProperty(ref _wednesdayHeader, value); }
        public string ThursdayHeader { get => _thursdayHeader; set => SetProperty(ref _thursdayHeader, value); }
        public string FridayHeader { get => _fridayHeader; set => SetProperty(ref _fridayHeader, value); }
        public string SaturdayHeader { get => _saturdayHeader; set => SetProperty(ref _saturdayHeader, value); }
        public string SundayHeader { get => _sundayHeader; set => SetProperty(ref _sundayHeader, value); }

        public ObservableCollection<EmployeeWeeklySchedule> EmployeeWeeklySchedules { get; set; }
        public ObservableCollection<ScheduleWarning> ScheduleWarnings { get; set; }
        public ObservableCollection<Position> Positions { get; set; }

        public event Action<string>? ScrollToEmployeeRequested;
        public bool IsManagerView => !_employeeId.HasValue;

        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }
        public ICommand TodayCommand { get; }

        public WeeklyCalendarViewModel(int? employeeId = null, int managerUserId = 0)
        {
            _context = new AppDbContext();
            _employeeId = employeeId;
            _managerUserId = managerUserId;
            EmployeeWeeklySchedules = new ObservableCollection<EmployeeWeeklySchedule>();
            ScheduleWarnings = new ObservableCollection<ScheduleWarning>();
            Positions = new ObservableCollection<Position>();

            LoadPositions();

            PreviousWeekCommand = new RelayCommand(_ => CurrentWeekStart = CurrentWeekStart.AddDays(-7));
            NextWeekCommand = new RelayCommand(_ => CurrentWeekStart = CurrentWeekStart.AddDays(7));
            TodayCommand = new RelayCommand(_ => CurrentWeekStart = GetMondayOfWeek(DateTime.Today));
            ScrollToEmployeeCommand = new RelayCommand(param => ExecuteScrollToEmployee(param));
            BeginEditCommand = new RelayCommand(param => ExecuteBeginEdit(param));
            CancelEditCommand = new RelayCommand(param => ExecuteCancelEdit(param));
            SaveEditCommand = new RelayCommand(param => ExecuteSaveEdit(param));
            DeleteDayCommand = new RelayCommand(param => ExecuteDeleteDay(param));
            AddDayCommand = new RelayCommand(param => ExecuteAddDay(param));

            // Initialize to current week
            CurrentWeekStart = GetMondayOfWeek(DateTime.Today);
        }

        public ICommand ScrollToEmployeeCommand { get; }
        public ICommand BeginEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand DeleteDayCommand { get; }
        public ICommand AddDayCommand { get; }

        private void LoadPositions()
        {
            var positions = _context.Positions
                .OrderBy(p => p.Name)
                .ToList();
            Positions = new ObservableCollection<Position>(positions);
        }

        private DateTime GetMondayOfWeek(DateTime date)
        {
            try
            {
                int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
                return date.AddDays(-diff).Date;
            }
            catch
            {
                return DateTime.Today;
            }
        }

        private void UpdateWeekHeaders()
        {
            var weekEnd = CurrentWeekStart.AddDays(6);
            WeekRangeDisplay = $"{CurrentWeekStart:MMM d} - {weekEnd:MMM d, yyyy}";

            MondayHeader = $"MON\n{CurrentWeekStart:MM/dd}";
            TuesdayHeader = $"TUE\n{CurrentWeekStart.AddDays(1):MM/dd}";
            WednesdayHeader = $"WED\n{CurrentWeekStart.AddDays(2):MM/dd}";
            ThursdayHeader = $"THU\n{CurrentWeekStart.AddDays(3):MM/dd}";
            FridayHeader = $"FRI\n{CurrentWeekStart.AddDays(4):MM/dd}";
            SaturdayHeader = $"SAT\n{CurrentWeekStart.AddDays(5):MM/dd}";
            SundayHeader = $"SUN\n{CurrentWeekStart.AddDays(6):MM/dd}";
        }

        private void LoadWeeklySchedule()
        {
            try
            {
                EmployeeWeeklySchedules.Clear();
                ScheduleWarnings.Clear();
                var weekEnd = CurrentWeekStart.AddDays(7); // Fetch logic is < weekEnd, so it covers up to next Monday 00:00

                // Get all active employees
                var employees = _context.Employees
                    .Include(e => e.Position)
                    .Where(e => e.Status == EmployeeStatus.Active)
                    .OrderBy(e => e.FullName)
                    .ToList();
                if (_employeeId.HasValue)
                {
                    employees = employees.Where(e => e.Id == _employeeId.Value).ToList();
                }

                // Get all shift assignments for this week
                // Optimization: fetch date range
                var assignments = _context.ShiftAssignments
                    .Include(sa => sa.Shift)
                        .ThenInclude(s => s.Position)
                    .Include(sa => sa.Employee)
                    .Where(sa => sa.Shift.ShiftDate >= CurrentWeekStart && sa.Shift.ShiftDate < weekEnd)
                    .ToList();
                if (_employeeId.HasValue)
                {
                    assignments = assignments.Where(a => a.EmployeeId == _employeeId.Value).ToList();
                }

                foreach (var employee in employees)
                {
                    var schedule = new EmployeeWeeklySchedule
                    {
                        EmployeeName = employee.FullName
                    };

                    int workingDays = 0;
                    // Fill in each day (0=Mon, 6=Sun)
                    for (int dayOffset = 0; dayOffset < 7; dayOffset++)
                    {
                        var currentDay = CurrentWeekStart.AddDays(dayOffset);

                        var dayAssignments = assignments
                            .Where(a => a.EmployeeId == employee.Id && a.Shift.ShiftDate.Date == currentDay.Date)
                            .OrderBy(a => a.Shift.StartTime) // If multiple, take first
                            .ToList();

                        DayCell cell;
                        if (dayAssignments.Any())
                        {
                            workingDays++;
                            var firstShift = dayAssignments.First().Shift;

                            // Color logic: Blue for regular
                            var bgBrush = new SolidColorBrush(Color.FromRgb(66, 165, 245)); // #42A5F5
                            var fgBrush = Brushes.White;

                            cell = new DayCell
                            {
                                EmployeeId = employee.Id,
                                ShiftAssignmentId = dayAssignments.First().Id,
                                ShiftId = firstShift.Id,
                                Date = currentDay.Date,
                                HasShift = true,
                                StartTimeText = $"{DateTime.Today.Add(firstShift.StartTime):hh:mm tt}",
                                EndTimeText = $"{DateTime.Today.Add(firstShift.EndTime):hh:mm tt}",
                                PositionId = firstShift.PositionId,
                                TimeDisplay = $"{DateTime.Today.Add(firstShift.StartTime):hh:mm tt}-{DateTime.Today.Add(firstShift.EndTime):hh:mm tt}",
                                PositionName = firstShift.Position?.Name ?? "General",
                                CellColor = bgBrush,
                                ForegroundColor = fgBrush
                            };
                        }
                        else
                        {
                            // OFF
                            cell = new DayCell
                            {
                                EmployeeId = employee.Id,
                                ShiftAssignmentId = 0,
                                ShiftId = 0,
                                Date = currentDay.Date,
                                HasShift = false,
                                TimeDisplay = "OFF",
                                PositionName = "",
                                CellColor = new SolidColorBrush(Color.FromRgb(241, 245, 249)), // #F1F5F9 (slate-50)
                                ForegroundColor = new SolidColorBrush(Color.FromRgb(148, 163, 184)) // #94A3B8 (slate-400)
                            };
                        }

                        // Assign to correct property
                        switch (dayOffset)
                        {
                            case 0: schedule.Monday = cell; break;
                            case 1: schedule.Tuesday = cell; break;
                            case 2: schedule.Wednesday = cell; break;
                            case 3: schedule.Thursday = cell; break;
                            case 4: schedule.Friday = cell; break;
                            case 5: schedule.Saturday = cell; break;
                            case 6: schedule.Sunday = cell; break;
                        }
                    }

                    EmployeeWeeklySchedules.Add(schedule);

                    if (!_employeeId.HasValue)
                    {
                        var daysOff = 7 - workingDays;
                        if (workingDays == 7)
                        {
                            ScheduleWarnings.Add(new ScheduleWarning(employee.FullName, $"{employee.FullName} is scheduled all 7 days this week."));
                        }
                        else if (daysOff <= 1)
                        {
                            ScheduleWarnings.Add(new ScheduleWarning(employee.FullName, $"{employee.FullName} has only {daysOff} day off this week."));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading schedule: {ex.Message}");
            }
        }

        private void ExecuteScrollToEmployee(object? param)
        {
            if (param is string name && !string.IsNullOrWhiteSpace(name))
            {
                ScrollToEmployeeRequested?.Invoke(name);
            }
        }

        private void ExecuteBeginEdit(object? param)
        {
            if (!IsManagerView)
            {
                return;
            }

            if (param is not DayCell cell || !cell.HasShift)
            {
                return;
            }

            cell.OriginalStartTimeText = cell.StartTimeText;
            cell.OriginalEndTimeText = cell.EndTimeText;
            cell.OriginalPositionId = cell.PositionId;
            cell.IsEditing = true;
        }

        private void ExecuteAddDay(object? param)
        {
            if (!IsManagerView)
            {
                return;
            }

            if (param is not DayCell cell || cell.HasShift)
            {
                return;
            }

            cell.OriginalStartTimeText = string.Empty;
            cell.OriginalEndTimeText = string.Empty;
            cell.OriginalPositionId = 0;
            cell.StartTimeText = "09:00 AM";
            cell.EndTimeText = "05:00 PM";
            cell.PositionId = Positions.FirstOrDefault()?.Id ?? 0;
            cell.IsEditing = true;
        }

        private void ExecuteCancelEdit(object? param)
        {
            if (param is not DayCell cell)
            {
                return;
            }

            cell.StartTimeText = cell.OriginalStartTimeText;
            cell.EndTimeText = cell.OriginalEndTimeText;
            cell.PositionId = cell.OriginalPositionId;
            cell.IsEditing = false;
        }

        private void ExecuteSaveEdit(object? param)
        {
            if (!IsManagerView)
            {
                return;
            }

            if (param is not DayCell cell)
            {
                return;
            }

            if (cell.PositionId == 0)
            {
                System.Windows.MessageBox.Show("Please select a position.", "Missing Position",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParse(cell.StartTimeText, out var startDt) ||
                !DateTime.TryParse(cell.EndTimeText, out var endDt))
            {
                System.Windows.MessageBox.Show("Invalid time format. Use e.g. 9:00 AM.", "Invalid Time",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var start = startDt.TimeOfDay;
            var end = endDt.TimeOfDay;
            if (start >= end)
            {
                System.Windows.MessageBox.Show("End time must be after start time.", "Invalid Time",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var assignment = _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .FirstOrDefault(sa => sa.Id == cell.ShiftAssignmentId);

            bool overlap = _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .Any(sa =>
                    sa.EmployeeId == cell.EmployeeId &&
                    sa.Id != cell.ShiftAssignmentId &&
                    sa.Shift.ShiftDate.Date == cell.Date.Date &&
                    start < sa.Shift.EndTime &&
                    end > sa.Shift.StartTime);

            if (overlap)
            {
                System.Windows.MessageBox.Show("Overlapping shift detected.", "Invalid Schedule",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            int createdBy;
            int oldShiftId = 0;
            if (assignment != null)
            {
                createdBy = assignment.Shift.CreatedBy;
                oldShiftId = assignment.ShiftId;
            }
            else if (_managerUserId != 0)
            {
                createdBy = _managerUserId;
            }
            else
            {
                createdBy = _context.Shifts
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => s.CreatedBy)
                    .FirstOrDefault();

                if (createdBy == 0)
                {
                    System.Windows.MessageBox.Show("Unable to determine shift creator.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            var newShift = new Shift
            {
                ShiftDate = cell.Date.Date,
                StartTime = start,
                EndTime = end,
                PositionId = cell.PositionId,
                CreatedBy = createdBy
            };

            _context.Shifts.Add(newShift);
            _context.SaveChanges();

            if (assignment != null)
            {
                assignment.ShiftId = newShift.Id;
                _context.SaveChanges();
            }
            else
            {
                _context.ShiftAssignments.Add(new ShiftAssignment
                {
                    ShiftId = newShift.Id,
                    EmployeeId = cell.EmployeeId
                });
                _context.SaveChanges();
            }

            if (oldShiftId != 0)
            {
                bool oldShiftHasAssignments = _context.ShiftAssignments.Any(sa => sa.ShiftId == oldShiftId);
                if (!oldShiftHasAssignments)
                {
                    var oldShift = _context.Shifts.FirstOrDefault(s => s.Id == oldShiftId);
                    if (oldShift != null)
                    {
                        _context.Shifts.Remove(oldShift);
                        _context.SaveChanges();
                    }
                }
            }

            cell.IsEditing = false;
            LoadWeeklySchedule();
        }

        private void ExecuteDeleteDay(object? param)
        {
            if (!IsManagerView)
            {
                return;
            }

            if (param is not DayCell cell || !cell.HasShift)
            {
                return;
            }

            var confirm = System.Windows.MessageBox.Show("Delete this day from the schedule?", "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            var assignment = _context.ShiftAssignments
                .Include(sa => sa.Shift)
                .FirstOrDefault(sa => sa.Id == cell.ShiftAssignmentId);

            if (assignment == null)
            {
                return;
            }

            var shiftId = assignment.ShiftId;
            _context.ShiftAssignments.Remove(assignment);
            _context.SaveChanges();

            bool hasOtherAssignments = _context.ShiftAssignments.Any(sa => sa.ShiftId == shiftId);
            if (!hasOtherAssignments)
            {
                var shift = _context.Shifts.FirstOrDefault(s => s.Id == shiftId);
                if (shift != null)
                {
                    _context.Shifts.Remove(shift);
                    _context.SaveChanges();
                }
            }

            LoadWeeklySchedule();
        }

        public sealed class ScheduleWarning
        {
            public ScheduleWarning(string employeeName, string message)
            {
                EmployeeName = employeeName;
                Message = message;
            }

            public string EmployeeName { get; }
            public string Message { get; }
        }
    }
}

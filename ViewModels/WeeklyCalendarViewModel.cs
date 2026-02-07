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

        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }
        public ICommand TodayCommand { get; }

        public WeeklyCalendarViewModel(int? employeeId = null)
        {
            _context = new AppDbContext();
            _employeeId = employeeId;
            EmployeeWeeklySchedules = new ObservableCollection<EmployeeWeeklySchedule>();

            PreviousWeekCommand = new RelayCommand(_ => CurrentWeekStart = CurrentWeekStart.AddDays(-7));
            NextWeekCommand = new RelayCommand(_ => CurrentWeekStart = CurrentWeekStart.AddDays(7));
            TodayCommand = new RelayCommand(_ => CurrentWeekStart = GetMondayOfWeek(DateTime.Today));

            // Initialize to current week
            CurrentWeekStart = GetMondayOfWeek(DateTime.Today);
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
                            var firstShift = dayAssignments.First().Shift;

                            // Color logic: Blue for regular
                            var bgBrush = new SolidColorBrush(Color.FromRgb(66, 165, 245)); // #42A5F5
                            var fgBrush = Brushes.White;

                            cell = new DayCell
                            {
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
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading schedule: {ex.Message}");
            }
        }
    }
}

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
        public ObservableCollection<EmployeeSelectable> Employees { get; } = new();

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

            CreateBatchCommand = new RelayCommand(_ => ExecuteBatchCreate(), _ => Employees.Any(e => e.IsSelected));
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

        public ICommand EditShiftCommand => new RelayCommand(obj =>
        {
            if (obj is ShiftSegment segment && segment.IsActive && segment.ShiftId > 0)
            {
                // Logic to edit shift. For now, let's keep it simple:
                // We could use a Dialog, but we need a View references or a Dialog Service.
                // Or we can just prompt Update.
                // Let's implement a simple "ShiftDetails" property that binds to an edit form in the UI.

                SelectedShiftToEdit = _context.Shifts.Include(s => s.Position).FirstOrDefault(s => s.Id == segment.ShiftId);
                IsEditingShift = true;
            }
        });

        private Shift? _selectedShiftToEdit;
        public Shift? SelectedShiftToEdit
        {
            get => _selectedShiftToEdit;
            set => SetProperty(ref _selectedShiftToEdit, value);
        }

        private bool _isEditingShift;
        public bool IsEditingShift
        {
            get => _isEditingShift;
            set => SetProperty(ref _isEditingShift, value);
        }

        public ICommand SaveShiftEditCommand => new RelayCommand(_ =>
        {
            if (SelectedShiftToEdit != null)
            {
                _context.Update(SelectedShiftToEdit);
                _context.SaveChanges();
                IsEditingShift = false;
                System.Windows.MessageBox.Show("Shift updated successfully.", "Success");
                LoadRoster();
            }
        });

        public ICommand CancelEditCommand => new RelayCommand(_ =>
        {
            IsEditingShift = false;
            SelectedShiftToEdit = null;
        });

        private void LoadMetadata()
        {
            Employees.Clear();
            foreach (var e in _context.Employees.Include(e => e.Position).Where(e => e.Status == EmployeeStatus.Active).ToList())
                Employees.Add(new EmployeeSelectable { Employee = e });
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
                    row.FillShift(assign.Shift.StartTime, assign.Shift.EndTime, assign.Shift.Position.Name, assign.Shift.Id);
                }
                Roster.Add(row);
            }
        }

        private void ExecuteBatchCreate()
        {
            try
            {
                var selectedEmps = Employees.Where(e => e.IsSelected).Select(e => e.Employee).ToList();

                if (!selectedEmps.Any())
                {
                    System.Windows.MessageBox.Show("Please select at least one employee.", "Warning");
                    return;
                }

                // 1. Separate Managers and Crew
                var managers = selectedEmps.Where(e => e.Position.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase)).ToList();
                var crew = selectedEmps.Except(managers).ToList();

                // 2. Define Time Slots
                // Crew Slots (6 Hours)
                var slotOpenerStart = new TimeSpan(6, 0, 0);   // 6 AM - 12 PM
                var slotOpenerEnd = new TimeSpan(12, 0, 0);

                var slotMidStart = new TimeSpan(11, 0, 0);     // 11 AM - 5 PM
                var slotMidEnd = new TimeSpan(17, 0, 0);

                var slotCloserStart = new TimeSpan(17, 0, 0);  // 5 PM - 11 PM
                var slotCloserEnd = new TimeSpan(23, 0, 0);

                // Manager Slots (9 Hours)
                var mgrOpenerStart = new TimeSpan(6, 0, 0);    // 6 AM - 3 PM
                var mgrOpenerEnd = new TimeSpan(15, 0, 0);

                var mgrCloserStart = new TimeSpan(14, 0, 0);   // 2 PM - 11 PM
                var mgrCloserEnd = new TimeSpan(23, 0, 0);

                // 3. Generate Week Dates
                var currentDay = SelectedDay;
                int diff = (7 + (currentDay.DayOfWeek - DayOfWeek.Monday)) % 7;
                var startOfWeek = currentDay.AddDays(-1 * diff).Date;
                var weekDates = Enumerable.Range(0, 7).Select(i => startOfWeek.AddDays(i)).ToList();

                var newShifts = new List<Shift>();

                // 4. Assign Managers (2 per day: Opener, Closer)
                int mgrIndex = 0;
                if (managers.Any())
                {
                    foreach (var date in weekDates)
                    {
                        for (int shiftNum = 1; shiftNum <= 2; shiftNum++)
                        {
                            var mgr = managers[mgrIndex % managers.Count];
                            mgrIndex++;

                            var start = shiftNum == 1 ? mgrOpenerStart : mgrCloserStart;
                            var end = shiftNum == 1 ? mgrOpenerEnd : mgrCloserEnd;

                            CreateShift(mgr, date, start, end, newShifts);
                        }
                    }
                }

                // 5. Assign Crew (Target ~5 shifts/week per person)
                var crewByPosition = crew.GroupBy(e => e.PositionId).ToList();

                foreach (var group in crewByPosition)
                {
                    var stationCrew = group.ToList();
                    int crewCount = stationCrew.Count;
                    if (crewCount == 0) continue;

                    // Calculate slots needed per day to give everyone ~5 shifts
                    // Total Shifts Needed = Crew * 5
                    // Slots Per Day = Total / 7 (Rounded Up)
                    int totalShiftsNeeded = crewCount * 5;
                    int slotsPerDay = (int)Math.Ceiling((double)totalShiftsNeeded / 7.0);

                    // Ensure at least 3 slots (Opener, Mid, Closer) if we have enough people
                    if (slotsPerDay < 3 && crewCount >= 3) slotsPerDay = 3;

                    int crewIndex = 0;

                    foreach (var date in weekDates)
                    {
                        for (int i = 0; i < slotsPerDay; i++)
                        {
                            var c = stationCrew[crewIndex % crewCount];
                            crewIndex++;

                            TimeSpan start, end;

                            // Distribution priority: Opener -> Closer -> Mid 1 -> Mid 2 -> Opener 2...
                            if (i == 0) { start = slotOpenerStart; end = slotOpenerEnd; }
                            else if (i == 1) { start = slotCloserStart; end = slotCloserEnd; }
                            else if (i == 2) { start = slotMidStart; end = slotMidEnd; }
                            else if (i == 3) { start = slotMidStart; end = slotMidEnd; } // Second Mid
                            else if (i % 3 == 0) { start = slotOpenerStart; end = slotOpenerEnd; } // Extra Opener
                            else if (i % 3 == 1) { start = slotCloserStart; end = slotCloserEnd; } // Extra Closer
                            else { start = slotMidStart; end = slotMidEnd; } // Extra Mid

                            CreateShift(c, date, start, end, newShifts);
                        }
                    }
                }

                // 6. Save Changes
                _context.Shifts.AddRange(newShifts);
                _context.SaveChanges();

                System.Windows.MessageBox.Show($"Weekly shifts created successfully!\nGenerated {newShifts.Count} shifts based on {selectedEmps.Count} employees.", "Success");
                LoadRoster();
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += "\n\nInner: " + ex.InnerException.Message;
                System.Windows.MessageBox.Show(msg, "Error");
            }
        }

        private void CreateShift(Employee emp, DateTime date, TimeSpan start, TimeSpan end, List<Shift> shifts)
        {
            var shift = new Shift
            {
                ShiftDate = date,
                StartTime = start,
                EndTime = end,
                PositionId = emp.PositionId,
                CreatedBy = _currentUser.Id,
                CreatedAt = DateTime.Now
            };

            // EF Core will fix up the assignment when we add it to the Shift's collection
            shift.ShiftAssignments.Add(new ShiftAssignment
            {
                EmployeeId = emp.Id
            });

            shifts.Add(shift);
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

        public void FillShift(TimeSpan start, TimeSpan end, string position, int shiftId)
        {
            int startIdx = start.Hours - 6;
            int endIdx = end.Hours - 6;

            for (int i = Math.Max(0, startIdx); i < Math.Min(18, endIdx); i++)
            {
                Segments[i].IsActive = true;
                Segments[i].Label = position;
                Segments[i].ShiftId = shiftId;
            }
        }
    }

    public class ShiftSegment : ObservableObject
    {
        private bool _isActive;
        private string _label = string.Empty;
        private int _shiftId;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public string Label { get => _label; set => SetProperty(ref _label, value); }
        public int ShiftId { get => _shiftId; set => SetProperty(ref _shiftId, value); }
    }

    public class EmployeeSelectable : ObservableObject
    {
        private bool _isSelected;
        public Employee Employee { get; set; } = null!;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    }
}

using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
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
        public ICollectionView RosterView { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyRosterFilter();
                }
            }
        }
        public ObservableCollection<EmployeeSelectable> Employees { get; } = new();

        public TimeSpan StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }
        public TimeSpan EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }

        public List<string> HoursLabels { get; } = new();

        public ICommand CreateBatchCommand { get; }
        public ICommand RefreshCommand { get; }

        private readonly AutoSchedulingService _autoSchedulingService;
        private readonly ValidationService _validationService;

        public ShiftsManagementViewModel(User user)
        {
            _currentUser = user;
            _context = new Data.AppDbContext();
            _shiftService = new ShiftService(_context);
            _validationService = new ValidationService(_context);
            _autoSchedulingService = new AutoSchedulingService(_context, _validationService);

            SelectedDay = DateTime.Today;

            RosterView = CollectionViewSource.GetDefaultView(Roster);
            CreateBatchCommand = new RelayCommand(_ => ExecuteBatchCreate());
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
                // Validation Check
                // We need the Employee ID. The shift has ShiftAssignments.
                // Assuming single assignment for now as per current logic.
                var assignment = _context.ShiftAssignments.FirstOrDefault(sa => sa.ShiftId == SelectedShiftToEdit.Id);
                if (assignment != null)
                {
                    if (_validationService.IsShiftOverlapping(assignment.EmployeeId, SelectedShiftToEdit.ShiftDate,
                        SelectedShiftToEdit.StartTime, SelectedShiftToEdit.EndTime, SelectedShiftToEdit.Id))
                    {
                        System.Windows.MessageBox.Show("This shift overlaps with another shift for this employee.\nPlease choose a different time.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return;
                    }
                }

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

        public ICommand DeleteShiftCommand => new RelayCommand(_ =>
        {
            if (SelectedShiftToEdit != null)
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to delete this shift?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Remove assignments first
                    var assignments = _context.ShiftAssignments.Where(sa => sa.ShiftId == SelectedShiftToEdit.Id).ToList();
                    _context.ShiftAssignments.RemoveRange(assignments);

                    _context.Shifts.Remove(SelectedShiftToEdit);
                    _context.SaveChanges();

                    IsEditingShift = false;
                    SelectedShiftToEdit = null;
                    LoadRoster();
                    System.Windows.MessageBox.Show("Shift deleted successfully.", "Success");
                }
            }
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

            ApplyRosterFilter();
        }

        private void ApplyRosterFilter()
        {
            if (RosterView == null)
            {
                return;
            }

            var query = SearchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                RosterView.Filter = null;
            }
            else
            {
                RosterView.Filter = item =>
                {
                    if (item is not RosterRow row)
                    {
                        return false;
                    }

                    return row.EmployeeName.Contains(query, StringComparison.OrdinalIgnoreCase);
                };
            }

            RosterView.Refresh();
        }

        private void ExecuteBatchCreate()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Generate SMART DRAFT schedule for the week of {SelectedDay:MMM dd}?\n\nThis will create a randomized, fair schedule for all active employees.",
                    "Confirm Auto-Schedule",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // 1. Calculate Week Start
                    var currentDay = SelectedDay;
                    int diff = (7 + (currentDay.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var startOfWeek = currentDay.AddDays(-1 * diff).Date;

                    // 2. Helper: Clear existing shifts first (optional, or just add to them?)
                    // For a "Draft", usually we want a clean slate or it gets messy. 
                    // Let's ask user or just do it. Let's assume clear for now to avoid duplicates.
                    // But wait, user might want to fill gaps? 
                    // User prompt says "Generate Draft", usually implies fresh.
                    // Let's just generate. Validation will skip overlaps if we didn't clear.

                    var newShifts = _autoSchedulingService.GenerateWeeklyDraft(startOfWeek, _currentUser.Id);

                    if (newShifts.Any())
                    {
                        _context.Shifts.AddRange(newShifts);
                        _context.SaveChanges();
                        System.Windows.MessageBox.Show($"Draft schedule generated with {newShifts.Count} shifts!", "Success");
                        LoadRoster();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("No shifts could be generated. Check valid employees or existing schedule conflicts.", "Info");
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += "\n\nInner: " + ex.InnerException.Message;
                System.Windows.MessageBox.Show(msg, "Error");
            }
        }


        public ICommand ClearShiftsCommand => new RelayCommand(_ => ExecuteClearShifts());

        private void ExecuteClearShifts()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to clear ALL shifts for the week of {SelectedDay:MMM dd}?",
                    "Confirm Clear Schedule",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    var currentDay = SelectedDay;
                    int diff = (7 + (currentDay.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var startOfWeek = currentDay.AddDays(-1 * diff).Date;
                    var endOfWeek = startOfWeek.AddDays(7).Date;

                    var shiftsToDelete = _context.Shifts
                        .Where(s => s.ShiftDate >= startOfWeek && s.ShiftDate < endOfWeek)
                        .ToList();

                    if (shiftsToDelete.Any())
                    {
                        // Remove related assignments first if cascade delete isn't set (though EF usually handles this if configured)
                        // Explicitly removing assignments just in case
                        var shiftIds = shiftsToDelete.Select(s => s.Id).ToList();
                        var assignmentsToDelete = _context.ShiftAssignments.Where(sa => shiftIds.Contains(sa.ShiftId)).ToList();

                        _context.ShiftAssignments.RemoveRange(assignmentsToDelete);
                        _context.Shifts.RemoveRange(shiftsToDelete);
                        _context.SaveChanges();

                        System.Windows.MessageBox.Show("Weekly schedule cleared successfully.", "Success");
                        LoadRoster();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("No shifts found to clear for this week.", "Info");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error clearing shifts: {ex.Message}", "Error");
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

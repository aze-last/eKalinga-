using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class RetentionTurnoverViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly EmployeeExitService _employeeExitService;
        private readonly int _recordedByUserId;
        private readonly List<Employee> _allEmployees = new();

        private int _activeHeadcount;
        private int _exitsThisMonth;
        private int _voluntaryExitsThisMonth;
        private int _involuntaryExitsThisMonth;
        private string _turnoverRatePercent = "0%";
        private string _ninetyDayRetentionPercent = "0%";
        private string _searchText = string.Empty;
        private SelectionOption<PositionArea?>? _selectedDepartmentFilter;
        private SelectionOption<int?>? _selectedRecorderFilter;

        private Employee? _selectedEmployee;
        private EmployeeExitType _selectedExitType = EmployeeExitType.Resignation;
        private bool _isVoluntary = true;
        private DateTime _lastWorkingDate = DateTime.Today;
        private string _reason = string.Empty;
        private string _notes = string.Empty;

        public ObservableCollection<EmployeeExit> ExitRecords { get; } = new();
        public ICollectionView ExitRecordsView { get; }
        public ObservableCollection<Employee> ActiveEmployees { get; } = new();
        public ObservableCollection<SelectionOption<PositionArea?>> DepartmentFilterOptions { get; } = new();
        public ObservableCollection<SelectionOption<int?>> RecorderFilterOptions { get; } = new();
        public ObservableCollection<TurnoverTrendPoint> TurnoverTrends { get; } = new();
        public ObservableCollection<string> TurnoverAlerts { get; } = new();
        public Array ExitTypes => Enum.GetValues(typeof(EmployeeExitType));

        public int ActiveHeadcount
        {
            get => _activeHeadcount;
            set => SetProperty(ref _activeHeadcount, value);
        }

        public int ExitsThisMonth
        {
            get => _exitsThisMonth;
            set => SetProperty(ref _exitsThisMonth, value);
        }

        public int VoluntaryExitsThisMonth
        {
            get => _voluntaryExitsThisMonth;
            set => SetProperty(ref _voluntaryExitsThisMonth, value);
        }

        public int InvoluntaryExitsThisMonth
        {
            get => _involuntaryExitsThisMonth;
            set => SetProperty(ref _involuntaryExitsThisMonth, value);
        }

        public string TurnoverRatePercent
        {
            get => _turnoverRatePercent;
            set => SetProperty(ref _turnoverRatePercent, value);
        }

        public string NinetyDayRetentionPercent
        {
            get => _ninetyDayRetentionPercent;
            set => SetProperty(ref _ninetyDayRetentionPercent, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                    ComputeMetrics();
                    BuildTrendData();
                    BuildAlerts();
                }
            }
        }

        public SelectionOption<PositionArea?>? SelectedDepartmentFilter
        {
            get => _selectedDepartmentFilter;
            set
            {
                if (SetProperty(ref _selectedDepartmentFilter, value))
                {
                    RefreshActiveEmployeeList();
                    ApplyFilter();
                    ComputeMetrics();
                    BuildTrendData();
                    BuildAlerts();
                }
            }
        }

        public SelectionOption<int?>? SelectedRecorderFilter
        {
            get => _selectedRecorderFilter;
            set
            {
                if (SetProperty(ref _selectedRecorderFilter, value))
                {
                    ApplyFilter();
                    ComputeMetrics();
                    BuildTrendData();
                    BuildAlerts();
                }
            }
        }

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set => SetProperty(ref _selectedEmployee, value);
        }

        public EmployeeExitType SelectedExitType
        {
            get => _selectedExitType;
            set
            {
                if (SetProperty(ref _selectedExitType, value))
                {
                    IsVoluntary = value is EmployeeExitType.Resignation or EmployeeExitType.Retirement;
                }
            }
        }

        public bool IsVoluntary
        {
            get => _isVoluntary;
            set => SetProperty(ref _isVoluntary, value);
        }

        public DateTime LastWorkingDate
        {
            get => _lastWorkingDate;
            set => SetProperty(ref _lastWorkingDate, value);
        }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public ICommand RecordExitCommand { get; }
        public ICommand RefreshCommand { get; }

        public RetentionTurnoverViewModel(int recordedByUserId)
        {
            _recordedByUserId = recordedByUserId;
            _context = new AppDbContext();
            _employeeExitService = new EmployeeExitService(_context);

            ExitRecordsView = CollectionViewSource.GetDefaultView(ExitRecords);
            RecordExitCommand = new RelayCommand(_ => ExecuteRecordExit());
            RefreshCommand = new RelayCommand(_ => LoadData());
            InitializeFilters();

            WeakEventManager<DashboardEventBus, DashboardDataChangedEventArgs>.AddHandler(
                DashboardEventBus.Instance,
                nameof(DashboardEventBus.DashboardDataChanged),
                OnDataChanged);

            LoadData();
        }

        private void InitializeFilters()
        {
            DepartmentFilterOptions.Clear();
            DepartmentFilterOptions.Add(new SelectionOption<PositionArea?> { Label = "All Departments", Value = null });
            foreach (PositionArea area in Enum.GetValues(typeof(PositionArea)))
            {
                DepartmentFilterOptions.Add(new SelectionOption<PositionArea?>
                {
                    Label = area.ToString(),
                    Value = area
                });
            }
            SelectedDepartmentFilter = DepartmentFilterOptions.FirstOrDefault();

            RecorderFilterOptions.Clear();
            RecorderFilterOptions.Add(new SelectionOption<int?> { Label = "All Recorders", Value = null });
            SelectedRecorderFilter = RecorderFilterOptions.FirstOrDefault();
        }

        private void OnDataChanged(object? sender, DashboardDataChangedEventArgs args)
        {
            if (args.Domain != DashboardDataDomain.Turnover && args.Domain != DashboardDataDomain.Employee)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void ExecuteRecordExit()
        {
            if (SelectedEmployee == null)
            {
                MessageBox.Show("Please select an employee.", "Missing Employee",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                MessageBox.Show("Please enter an exit reason.", "Missing Reason",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Record exit for {SelectedEmployee.FullName}? This will set the employee status to Inactive.",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _employeeExitService.RecordExit(
                    SelectedEmployee.Id,
                    SelectedExitType,
                    IsVoluntary,
                    LastWorkingDate.Date,
                    Reason,
                    Notes,
                    _recordedByUserId);

                SelectedEmployee = null;
                SelectedExitType = EmployeeExitType.Resignation;
                IsVoluntary = true;
                LastWorkingDate = DateTime.Today;
                Reason = string.Empty;
                Notes = string.Empty;

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Record Exit",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            _allEmployees.Clear();
            var employees = _context.Employees
                .AsNoTracking()
                .Include(e => e.Position)
                .OrderBy(e => e.FullName)
                .ToList();

            foreach (var employee in employees)
            {
                _allEmployees.Add(employee);
            }

            ExitRecords.Clear();
            foreach (var exit in _employeeExitService.GetExitRecords())
            {
                ExitRecords.Add(exit);
            }

            RefreshRecorderFilterOptions();
            RefreshActiveEmployeeList();
            ApplyFilter();
            ComputeMetrics();
            BuildTrendData();
            BuildAlerts();
        }

        private void RefreshRecorderFilterOptions()
        {
            int? selectedRecorderId = SelectedRecorderFilter?.Value;

            var recorderPairs = ExitRecords
                .Where(e => e.RecordedByUser != null)
                .Select(e => new { e.RecordedByUser.Id, e.RecordedByUser.Username })
                .Distinct()
                .OrderBy(x => x.Username)
                .ToList();

            RecorderFilterOptions.Clear();
            RecorderFilterOptions.Add(new SelectionOption<int?> { Label = "All Recorders", Value = null });
            foreach (var recorder in recorderPairs)
            {
                RecorderFilterOptions.Add(new SelectionOption<int?>
                {
                    Label = recorder.Username,
                    Value = recorder.Id
                });
            }

            SelectedRecorderFilter = RecorderFilterOptions
                .FirstOrDefault(option => option.Value == selectedRecorderId)
                ?? RecorderFilterOptions.FirstOrDefault();
        }

        private void RefreshActiveEmployeeList()
        {
            ActiveEmployees.Clear();

            var activeEmployees = _allEmployees
                .Where(e => e.Status == EmployeeStatus.Active);

            if (SelectedDepartmentFilter?.Value is PositionArea department)
            {
                activeEmployees = activeEmployees.Where(e => e.Position.Area == department);
            }

            foreach (var employee in activeEmployees.OrderBy(e => e.FullName))
            {
                ActiveEmployees.Add(employee);
            }

            if (SelectedEmployee != null && ActiveEmployees.All(e => e.Id != SelectedEmployee.Id))
            {
                SelectedEmployee = null;
            }
        }

        private bool MatchesDepartment(EmployeeExit exit)
        {
            return SelectedDepartmentFilter?.Value is not PositionArea department
                || (exit.Employee?.Position?.Area == department);
        }

        private bool MatchesRecorder(EmployeeExit exit)
        {
            return SelectedRecorderFilter?.Value is not int recorderId
                || exit.RecordedBy == recorderId;
        }

        private void ComputeMetrics()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var scopedEmployees = _allEmployees.AsEnumerable();
            if (SelectedDepartmentFilter?.Value is PositionArea department)
            {
                scopedEmployees = scopedEmployees.Where(e => e.Position.Area == department);
            }
            var scopedEmployeesList = scopedEmployees.ToList();

            ActiveHeadcount = scopedEmployeesList.Count(e => e.Status == EmployeeStatus.Active);

            var scopedExits = ExitRecords
                .Where(MatchesDepartment)
                .Where(MatchesRecorder)
                .ToList();

            var monthExits = scopedExits
                .Where(e => e.LastWorkingDate >= monthStart && e.LastWorkingDate < monthEnd)
                .ToList();

            ExitsThisMonth = monthExits.Count;
            VoluntaryExitsThisMonth = monthExits.Count(e => e.IsVoluntary);
            InvoluntaryExitsThisMonth = monthExits.Count(e => !e.IsVoluntary);

            int totalHeadcount = scopedEmployeesList.Count;
            TurnoverRatePercent = totalHeadcount == 0
                ? "0%"
                : $"{Math.Round((double)ExitsThisMonth / totalHeadcount * 100, MidpointRounding.AwayFromZero):0.0}%";

            var ninetyDaysAgo = today.AddDays(-90);
            var recentHires = scopedEmployeesList
                .Where(e => e.DateHired >= ninetyDaysAgo && e.DateHired <= today)
                .ToList();

            if (recentHires.Count == 0)
            {
                NinetyDayRetentionPercent = "0%";
            }
            else
            {
                int retained = recentHires.Count(e => e.Status == EmployeeStatus.Active);
                NinetyDayRetentionPercent = $"{Math.Round((double)retained / recentHires.Count * 100, MidpointRounding.AwayFromZero):0.0}%";
            }
        }

        private void BuildTrendData()
        {
            TurnoverTrends.Clear();

            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthStarts = Enumerable.Range(0, 6)
                .Select(offset => currentMonth.AddMonths(-5 + offset))
                .ToList();

            var scopedExits = ExitRecords
                .Where(MatchesDepartment)
                .Where(MatchesRecorder)
                .ToList();

            var trendPoints = monthStarts.Select(monthStart =>
            {
                var monthEnd = monthStart.AddMonths(1);
                var exits = scopedExits
                    .Where(e => e.LastWorkingDate >= monthStart && e.LastWorkingDate < monthEnd)
                    .ToList();

                return new TurnoverTrendPoint
                {
                    MonthLabel = monthStart.ToString("MMM yyyy"),
                    VoluntaryCount = exits.Count(e => e.IsVoluntary),
                    InvoluntaryCount = exits.Count(e => !e.IsVoluntary)
                };
            }).ToList();

            int maxCount = Math.Max(1, trendPoints.Max(p => Math.Max(p.VoluntaryCount, p.InvoluntaryCount)));
            foreach (var point in trendPoints)
            {
                point.VoluntaryBarWidth = point.VoluntaryCount == 0 ? 0 : Math.Max(8, point.VoluntaryCount * 160.0 / maxCount);
                point.InvoluntaryBarWidth = point.InvoluntaryCount == 0 ? 0 : Math.Max(8, point.InvoluntaryCount * 160.0 / maxCount);
                TurnoverTrends.Add(point);
            }
        }

        private void BuildAlerts()
        {
            TurnoverAlerts.Clear();

            if (double.TryParse(TurnoverRatePercent.TrimEnd('%'), out var turnoverRate) && turnoverRate >= 8.0)
            {
                TurnoverAlerts.Add($"High turnover risk: {TurnoverRatePercent} this month.");
            }

            if (InvoluntaryExitsThisMonth > VoluntaryExitsThisMonth)
            {
                TurnoverAlerts.Add("Involuntary exits are higher than voluntary exits this month.");
            }

            if (double.TryParse(NinetyDayRetentionPercent.TrimEnd('%'), out var retention) && retention < 85.0)
            {
                TurnoverAlerts.Add($"90-day retention is low at {NinetyDayRetentionPercent}.");
            }

            if (TurnoverAlerts.Count == 0)
            {
                TurnoverAlerts.Add("No turnover risks detected for the current selection.");
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? string.Empty;
            ExitRecordsView.Filter = item =>
            {
                if (item is not EmployeeExit exit)
                {
                    return false;
                }

                var employeeName = exit.Employee?.FullName ?? string.Empty;
                bool matchesQuery = string.IsNullOrWhiteSpace(query)
                    || employeeName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || exit.ExitType.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    || exit.Reason.Contains(query, StringComparison.OrdinalIgnoreCase);

                return matchesQuery && MatchesDepartment(exit) && MatchesRecorder(exit);
            };

            ExitRecordsView.Refresh();
        }
    }

    public class TurnoverTrendPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public int VoluntaryCount { get; set; }
        public int InvoluntaryCount { get; set; }
        public double VoluntaryBarWidth { get; set; }
        public double InvoluntaryBarWidth { get; set; }
    }
}

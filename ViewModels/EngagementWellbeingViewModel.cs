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
    public class EngagementWellbeingViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly EngagementService _engagementService;
        private readonly int _actorUserId;
        private readonly List<Employee> _allEmployees = new();

        private string _searchText = string.Empty;
        private SelectionOption<PositionArea?>? _selectedDepartmentFilter;
        private Employee? _selectedEmployee;
        private DateTime _surveyDate = DateTime.Today;
        private string _enpsScoreText = "15";
        private string _engagementScoreText = "75";
        private string _wellbeingScoreText = "78";
        private BurnoutRiskLevel _selectedBurnoutRisk = BurnoutRiskLevel.Low;
        private string _comments = string.Empty;
        private double _averageEnps;
        private double _averageEngagementScore;
        private double _averageWellbeingScore;
        private double _absenteeismRate;
        private double _surveyParticipationRate;
        private int _highBurnoutCount;

        public ObservableCollection<EngagementSurvey> Surveys { get; } = new();
        public ICollectionView SurveysView { get; }
        public ObservableCollection<Employee> ActiveEmployees { get; } = new();
        public ObservableCollection<SelectionOption<PositionArea?>> DepartmentFilterOptions { get; } = new();
        public ObservableCollection<string> Alerts { get; } = new();
        public Array BurnoutRiskLevels => Enum.GetValues(typeof(BurnoutRiskLevel));

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
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
                    RefreshActiveEmployees();
                    ApplyFilter();
                    ComputeMetrics();
                    BuildAlerts();
                }
            }
        }

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set => SetProperty(ref _selectedEmployee, value);
        }

        public DateTime SurveyDate
        {
            get => _surveyDate;
            set => SetProperty(ref _surveyDate, value);
        }

        public string EnpsScoreText
        {
            get => _enpsScoreText;
            set => SetProperty(ref _enpsScoreText, value);
        }

        public string EngagementScoreText
        {
            get => _engagementScoreText;
            set => SetProperty(ref _engagementScoreText, value);
        }

        public string WellbeingScoreText
        {
            get => _wellbeingScoreText;
            set => SetProperty(ref _wellbeingScoreText, value);
        }

        public BurnoutRiskLevel SelectedBurnoutRisk
        {
            get => _selectedBurnoutRisk;
            set => SetProperty(ref _selectedBurnoutRisk, value);
        }

        public string Comments
        {
            get => _comments;
            set => SetProperty(ref _comments, value);
        }

        public double AverageEnps
        {
            get => _averageEnps;
            set => SetProperty(ref _averageEnps, value);
        }

        public double AverageEngagementScore
        {
            get => _averageEngagementScore;
            set => SetProperty(ref _averageEngagementScore, value);
        }

        public double AverageWellbeingScore
        {
            get => _averageWellbeingScore;
            set => SetProperty(ref _averageWellbeingScore, value);
        }

        public double AbsenteeismRate
        {
            get => _absenteeismRate;
            set => SetProperty(ref _absenteeismRate, value);
        }

        public double SurveyParticipationRate
        {
            get => _surveyParticipationRate;
            set => SetProperty(ref _surveyParticipationRate, value);
        }

        public int HighBurnoutCount
        {
            get => _highBurnoutCount;
            set => SetProperty(ref _highBurnoutCount, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand SubmitSurveyCommand { get; }

        public EngagementWellbeingViewModel(int actorUserId)
        {
            _actorUserId = actorUserId;
            _context = new AppDbContext();
            _engagementService = new EngagementService(_context);

            SurveysView = CollectionViewSource.GetDefaultView(Surveys);
            RefreshCommand = new RelayCommand(_ => LoadData());
            SubmitSurveyCommand = new RelayCommand(_ => ExecuteSubmitSurvey());

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
            DepartmentFilterOptions.Add(new SelectionOption<PositionArea?> { Label = "All Teams", Value = null });
            foreach (PositionArea area in Enum.GetValues(typeof(PositionArea)))
            {
                DepartmentFilterOptions.Add(new SelectionOption<PositionArea?>
                {
                    Label = area.ToString(),
                    Value = area
                });
            }

            SelectedDepartmentFilter = DepartmentFilterOptions.FirstOrDefault();
        }

        private void OnDataChanged(object? sender, DashboardDataChangedEventArgs args)
        {
            if (args.Domain != DashboardDataDomain.Engagement &&
                args.Domain != DashboardDataDomain.Attendance &&
                args.Domain != DashboardDataDomain.Employee &&
                args.Domain != DashboardDataDomain.Leave &&
                args.Domain != DashboardDataDomain.Shift)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void ExecuteSubmitSurvey()
        {
            if (SelectedEmployee == null)
            {
                MessageBox.Show("Please select an employee.", "Missing Employee",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(EnpsScoreText, out int enpsScore))
            {
                MessageBox.Show("eNPS score must be a whole number.", "Invalid eNPS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(EngagementScoreText, out decimal engagementScore))
            {
                MessageBox.Show("Engagement score must be numeric.", "Invalid Engagement Score",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(WellbeingScoreText, out decimal wellbeingScore))
            {
                MessageBox.Show("Wellbeing score must be numeric.", "Invalid Wellbeing Score",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _engagementService.SubmitSurvey(
                    SelectedEmployee.Id,
                    SurveyDate.Date,
                    enpsScore,
                    engagementScore,
                    wellbeingScore,
                    SelectedBurnoutRisk,
                    Comments,
                    _actorUserId);

                SelectedEmployee = null;
                SurveyDate = DateTime.Today;
                EnpsScoreText = "15";
                EngagementScoreText = "75";
                WellbeingScoreText = "78";
                SelectedBurnoutRisk = BurnoutRiskLevel.Low;
                Comments = string.Empty;

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Submit Survey",
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
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.FullName)
                .ToList();
            foreach (var employee in employees)
            {
                _allEmployees.Add(employee);
            }

            Surveys.Clear();
            foreach (var survey in _engagementService.GetSurveys())
            {
                Surveys.Add(survey);
            }

            RefreshActiveEmployees();
            ApplyFilter();
            ComputeMetrics();
            BuildAlerts();
        }

        private void RefreshActiveEmployees()
        {
            ActiveEmployees.Clear();

            var scoped = _allEmployees.AsEnumerable();
            if (SelectedDepartmentFilter?.Value is PositionArea area)
            {
                scoped = scoped.Where(e => e.Position.Area == area);
            }

            foreach (var employee in scoped.OrderBy(e => e.FullName))
            {
                ActiveEmployees.Add(employee);
            }

            if (SelectedEmployee != null && ActiveEmployees.All(e => e.Id != SelectedEmployee.Id))
            {
                SelectedEmployee = null;
            }
        }

        private void ComputeMetrics()
        {
            var scopedSurveys = Surveys.Where(MatchesDepartment).ToList();
            var today = DateTime.Today;
            var monthWindowStart = today.AddDays(-30);
            var recentSurveys = scopedSurveys
                .Where(s => s.SurveyDate.Date >= monthWindowStart)
                .ToList();

            var baseSet = recentSurveys.Count > 0 ? recentSurveys : scopedSurveys;

            AverageEnps = baseSet.Count == 0
                ? 0.0
                : Math.Round(baseSet.Average(s => s.EnpsScore), 1);

            AverageEngagementScore = baseSet.Count == 0
                ? 0.0
                : Math.Round((double)baseSet.Average(s => s.EngagementScore), 1);

            AverageWellbeingScore = baseSet.Count == 0
                ? 0.0
                : Math.Round((double)baseSet.Average(s => s.WellbeingScore), 1);

            HighBurnoutCount = baseSet.Count(s => s.BurnoutRisk == BurnoutRiskLevel.High);

            var scopedEmployees = _allEmployees.Where(MatchesDepartment).ToList();
            var surveyedEmployeeIds = recentSurveys
                .Select(s => s.EmployeeId)
                .Distinct()
                .Count();
            SurveyParticipationRate = scopedEmployees.Count == 0
                ? 0.0
                : Math.Round((double)surveyedEmployeeIds / scopedEmployees.Count * 100, 1);

            AbsenteeismRate = Math.Round(ComputeTodayAbsenteeismRate(scopedEmployees.Select(e => e.Id).ToHashSet()), 1);
        }

        private double ComputeTodayAbsenteeismRate(HashSet<int> scopedEmployeeIds)
        {
            if (scopedEmployeeIds.Count == 0)
            {
                return 0.0;
            }

            var now = DateTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);

            var assignments = _context.ShiftAssignments
                .AsNoTracking()
                .Include(sa => sa.Shift)
                .Where(sa =>
                    scopedEmployeeIds.Contains(sa.EmployeeId) &&
                    sa.Shift.ShiftDate >= today &&
                    sa.Shift.ShiftDate < tomorrow)
                .ToList();

            if (assignments.Count == 0)
            {
                return 0.0;
            }

            var onLeaveEmployeeIds = _context.LeaveRequests
                .AsNoTracking()
                .Where(lr =>
                    scopedEmployeeIds.Contains(lr.EmployeeId) &&
                    lr.Status == LeaveStatus.Approved &&
                    lr.StartDate.Date <= today &&
                    lr.EndDate.Date >= today)
                .Select(lr => lr.EmployeeId)
                .Distinct()
                .ToHashSet();

            var presentPairs = _context.Attendances
                .AsNoTracking()
                .Include(a => a.Shift)
                .Where(a =>
                    scopedEmployeeIds.Contains(a.EmployeeId) &&
                    a.Shift.ShiftDate >= today &&
                    a.Shift.ShiftDate < tomorrow &&
                    a.TimeIn.HasValue)
                .Select(a => new { a.EmployeeId, a.ShiftId })
                .Distinct()
                .AsEnumerable()
                .Select(a => (a.EmployeeId, a.ShiftId))
                .ToHashSet();

            int consideredAssignments = 0;
            int absences = 0;

            foreach (var assignment in assignments)
            {
                if (onLeaveEmployeeIds.Contains(assignment.EmployeeId))
                {
                    continue;
                }

                var shiftStart = today.Add(assignment.Shift.StartTime);
                var absentThreshold = shiftStart.AddMinutes(20);
                if (now < absentThreshold)
                {
                    continue;
                }

                consideredAssignments++;
                if (!presentPairs.Contains((assignment.EmployeeId, assignment.ShiftId)))
                {
                    absences++;
                }
            }

            return consideredAssignments == 0
                ? 0.0
                : (double)absences / consideredAssignments * 100.0;
        }

        private void BuildAlerts()
        {
            Alerts.Clear();

            if (AverageEnps < 0)
            {
                Alerts.Add($"eNPS is negative ({AverageEnps:0.0}). Team sentiment needs intervention.");
            }

            if (AverageEngagementScore < 70)
            {
                Alerts.Add($"Engagement score is low at {AverageEngagementScore:0.0}%.");
            }

            if (AverageWellbeingScore < 70)
            {
                Alerts.Add($"Wellbeing score is low at {AverageWellbeingScore:0.0}%.");
            }

            if (AbsenteeismRate > 8)
            {
                Alerts.Add($"Absenteeism is high today at {AbsenteeismRate:0.0}%.");
            }

            if (HighBurnoutCount >= 3)
            {
                Alerts.Add($"{HighBurnoutCount} recent high-burnout responses were detected.");
            }

            if (Alerts.Count == 0)
            {
                Alerts.Add("No major engagement or wellbeing risks in the selected team.");
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            SurveysView.Filter = item =>
            {
                if (item is not EngagementSurvey survey)
                {
                    return false;
                }

                bool matchesQuery = string.IsNullOrWhiteSpace(query)
                    || (survey.Employee?.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                    || survey.BurnoutRisk.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (survey.Comments ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);

                return matchesQuery && MatchesDepartment(survey);
            };

            SurveysView.Refresh();
        }

        private bool MatchesDepartment(EngagementSurvey survey)
        {
            return SelectedDepartmentFilter?.Value is not PositionArea area
                || survey.Employee?.Position?.Area == area;
        }

        private bool MatchesDepartment(Employee employee)
        {
            return SelectedDepartmentFilter?.Value is not PositionArea area
                || employee.Position.Area == area;
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class PerformanceMetricsViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly PerformanceService _performanceService;
        private readonly int _actorUserId;

        private string _searchText = string.Empty;
        private SelectionOption<PositionArea?>? _selectedDepartmentFilter;
        private double _goalCompletionRate;
        private double _avgReviewScore;
        private double _avgManagerFeedbackScore;
        private double _trainingCompletionRate;
        private double _avgTrainingEffectiveness;
        private int _overdueGoalsCount;

        public ObservableCollection<PerformanceGoal> Goals { get; } = new();
        public ObservableCollection<TrainingRecord> TrainingRecords { get; } = new();
        public ICollectionView GoalsView { get; }
        public ICollectionView TrainingRecordsView { get; }
        public ObservableCollection<SelectionOption<PositionArea?>> DepartmentFilterOptions { get; } = new();
        public ObservableCollection<string> RiskFlags { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilters();
                    ComputeMetrics();
                    BuildRiskFlags();
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
                    ApplyFilters();
                    ComputeMetrics();
                    BuildRiskFlags();
                }
            }
        }

        public double GoalCompletionRate
        {
            get => _goalCompletionRate;
            set => SetProperty(ref _goalCompletionRate, value);
        }

        public double AvgReviewScore
        {
            get => _avgReviewScore;
            set => SetProperty(ref _avgReviewScore, value);
        }

        public double AvgManagerFeedbackScore
        {
            get => _avgManagerFeedbackScore;
            set => SetProperty(ref _avgManagerFeedbackScore, value);
        }

        public double TrainingCompletionRate
        {
            get => _trainingCompletionRate;
            set => SetProperty(ref _trainingCompletionRate, value);
        }

        public double AvgTrainingEffectiveness
        {
            get => _avgTrainingEffectiveness;
            set => SetProperty(ref _avgTrainingEffectiveness, value);
        }

        public int OverdueGoalsCount
        {
            get => _overdueGoalsCount;
            set => SetProperty(ref _overdueGoalsCount, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand AdvanceGoalProgressCommand { get; }
        public ICommand CompleteGoalCommand { get; }
        public ICommand CompleteTrainingCommand { get; }

        public PerformanceMetricsViewModel(int actorUserId)
        {
            _actorUserId = actorUserId;
            _context = new AppDbContext();
            _performanceService = new PerformanceService(_context);

            GoalsView = CollectionViewSource.GetDefaultView(Goals);
            TrainingRecordsView = CollectionViewSource.GetDefaultView(TrainingRecords);

            RefreshCommand = new RelayCommand(_ => LoadData());
            AdvanceGoalProgressCommand = new RelayCommand(param => ExecuteAdvanceGoalProgress(param));
            CompleteGoalCommand = new RelayCommand(param => ExecuteCompleteGoal(param));
            CompleteTrainingCommand = new RelayCommand(param => ExecuteCompleteTraining(param));

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
            if (args.Domain != DashboardDataDomain.Performance && args.Domain != DashboardDataDomain.Employee)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void ExecuteAdvanceGoalProgress(object? parameter)
        {
            if (parameter is not PerformanceGoal goal)
            {
                return;
            }

            try
            {
                _performanceService.AdvanceGoalProgress(goal.Id, 10m, _actorUserId);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Update Goal",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteCompleteGoal(object? parameter)
        {
            if (parameter is not PerformanceGoal goal)
            {
                return;
            }

            try
            {
                _performanceService.MarkGoalCompleted(goal.Id, _actorUserId);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Complete Goal",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteCompleteTraining(object? parameter)
        {
            if (parameter is not TrainingRecord training)
            {
                return;
            }

            try
            {
                _performanceService.MarkTrainingCompleted(training.Id, 4.2m, _actorUserId);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Complete Training",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            Goals.Clear();
            foreach (var goal in _performanceService.GetGoals())
            {
                Goals.Add(goal);
            }

            TrainingRecords.Clear();
            foreach (var training in _performanceService.GetTrainingRecords())
            {
                TrainingRecords.Add(training);
            }

            ApplyFilters();
            ComputeMetrics();
            BuildRiskFlags();
        }

        private void ComputeMetrics()
        {
            var scopedGoals = Goals.Where(MatchesDepartment).ToList();
            var scopedTrainings = TrainingRecords.Where(MatchesDepartment).ToList();

            GoalCompletionRate = scopedGoals.Count == 0
                ? 0.0
                : Math.Round((double)scopedGoals.Average(g => g.CompletionPercent), 1);

            AvgReviewScore = scopedGoals.Count == 0 || !scopedGoals.Any(g => g.ReviewScore.HasValue)
                ? 0.0
                : Math.Round((double)scopedGoals.Where(g => g.ReviewScore.HasValue).Average(g => g.ReviewScore!.Value), 2);

            AvgManagerFeedbackScore = scopedGoals.Count == 0 || !scopedGoals.Any(g => g.ManagerFeedbackScore.HasValue)
                ? 0.0
                : Math.Round((double)scopedGoals.Where(g => g.ManagerFeedbackScore.HasValue).Average(g => g.ManagerFeedbackScore!.Value), 2);

            int completedTrainings = scopedTrainings.Count(t => t.Status == TrainingStatus.Completed);
            TrainingCompletionRate = scopedTrainings.Count == 0
                ? 0.0
                : Math.Round((double)completedTrainings / scopedTrainings.Count * 100, 1);

            AvgTrainingEffectiveness = scopedTrainings.Count == 0 || !scopedTrainings.Any(t => t.EffectivenessScore.HasValue)
                ? 0.0
                : Math.Round((double)scopedTrainings.Where(t => t.EffectivenessScore.HasValue).Average(t => t.EffectivenessScore!.Value), 2);

            OverdueGoalsCount = scopedGoals.Count(g =>
                g.Status == PerformanceGoalStatus.Overdue ||
                (g.DueDate.Date < DateTime.Today && g.CompletionPercent < 100m));
        }

        private void BuildRiskFlags()
        {
            RiskFlags.Clear();

            if (GoalCompletionRate < 70.0)
            {
                RiskFlags.Add($"Goal completion is low at {GoalCompletionRate:0.0}%.");
            }

            if (TrainingCompletionRate < 75.0)
            {
                RiskFlags.Add($"Training completion is below target at {TrainingCompletionRate:0.0}%.");
            }

            if (AvgReviewScore > 0 && AvgReviewScore < 3.5)
            {
                RiskFlags.Add($"Average review score is below baseline at {AvgReviewScore:0.00}/5.");
            }

            if (OverdueGoalsCount > 0)
            {
                RiskFlags.Add($"{OverdueGoalsCount} goal(s) are overdue and need manager action.");
            }

            if (RiskFlags.Count == 0)
            {
                RiskFlags.Add("No major performance risks in the current team selection.");
            }
        }

        private void ApplyFilters()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            GoalsView.Filter = item =>
            {
                if (item is not PerformanceGoal goal)
                {
                    return false;
                }

                bool matchesQuery = string.IsNullOrWhiteSpace(query)
                    || goal.GoalTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (goal.Employee?.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                    || goal.Status.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

                return matchesQuery && MatchesDepartment(goal);
            };

            TrainingRecordsView.Filter = item =>
            {
                if (item is not TrainingRecord training)
                {
                    return false;
                }

                bool matchesQuery = string.IsNullOrWhiteSpace(query)
                    || training.TrainingName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (training.Employee?.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                    || training.Status.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

                return matchesQuery && MatchesDepartment(training);
            };

            GoalsView.Refresh();
            TrainingRecordsView.Refresh();
        }

        private bool MatchesDepartment(PerformanceGoal goal)
        {
            return SelectedDepartmentFilter?.Value is not PositionArea area
                || goal.Employee?.Position?.Area == area;
        }

        private bool MatchesDepartment(TrainingRecord training)
        {
            return SelectedDepartmentFilter?.Value is not PositionArea area
                || training.Employee?.Position?.Area == area;
        }
    }
}

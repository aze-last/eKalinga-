using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class RecruitmentMetricsViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly RecruitmentService _recruitmentService;

        private int _openPipelineCount;
        private int _hiresThisMonth;
        private string _avgTimeToHireDays = "0.0";
        private string _offerAcceptanceRate = "0%";
        private string _estimatedCostPerHire = "0.00";
        private string _searchText = string.Empty;
        private SelectionOption<RecruitmentSource?>? _selectedSourceFilter;
        private SelectionOption<RecruitmentStage?>? _selectedStageFilter;

        private string _candidateFullName = string.Empty;
        private string _candidateEmail = string.Empty;
        private RecruitmentSource _selectedSource = RecruitmentSource.JobBoard;
        private DateTime _appliedAt = DateTime.Today;
        private string _expectedSalaryText = string.Empty;
        private string _candidateNotes = string.Empty;

        public ObservableCollection<RecruitmentCandidate> Candidates { get; } = new();
        public ICollectionView CandidatesView { get; }
        public ObservableCollection<SelectionOption<RecruitmentSource?>> SourceFilterOptions { get; } = new();
        public ObservableCollection<SelectionOption<RecruitmentStage?>> StageFilterOptions { get; } = new();
        public ObservableCollection<RecruitmentTrendPoint> RecruitmentTrends { get; } = new();

        public Array Sources => Enum.GetValues(typeof(RecruitmentSource));

        public int OpenPipelineCount
        {
            get => _openPipelineCount;
            set => SetProperty(ref _openPipelineCount, value);
        }

        public int HiresThisMonth
        {
            get => _hiresThisMonth;
            set => SetProperty(ref _hiresThisMonth, value);
        }

        public string AvgTimeToHireDays
        {
            get => _avgTimeToHireDays;
            set => SetProperty(ref _avgTimeToHireDays, value);
        }

        public string OfferAcceptanceRate
        {
            get => _offerAcceptanceRate;
            set => SetProperty(ref _offerAcceptanceRate, value);
        }

        public string EstimatedCostPerHire
        {
            get => _estimatedCostPerHire;
            set => SetProperty(ref _estimatedCostPerHire, value);
        }

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

        public SelectionOption<RecruitmentSource?>? SelectedSourceFilter
        {
            get => _selectedSourceFilter;
            set
            {
                if (SetProperty(ref _selectedSourceFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public SelectionOption<RecruitmentStage?>? SelectedStageFilter
        {
            get => _selectedStageFilter;
            set
            {
                if (SetProperty(ref _selectedStageFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string CandidateFullName
        {
            get => _candidateFullName;
            set => SetProperty(ref _candidateFullName, value);
        }

        public string CandidateEmail
        {
            get => _candidateEmail;
            set => SetProperty(ref _candidateEmail, value);
        }

        public RecruitmentSource SelectedSource
        {
            get => _selectedSource;
            set => SetProperty(ref _selectedSource, value);
        }

        public DateTime AppliedAt
        {
            get => _appliedAt;
            set => SetProperty(ref _appliedAt, value);
        }

        public string ExpectedSalaryText
        {
            get => _expectedSalaryText;
            set => SetProperty(ref _expectedSalaryText, value);
        }

        public string CandidateNotes
        {
            get => _candidateNotes;
            set => SetProperty(ref _candidateNotes, value);
        }

        public ICommand AddCandidateCommand { get; }
        public ICommand AdvanceStageCommand { get; }
        public ICommand MarkHiredCommand { get; }
        public ICommand RejectCandidateCommand { get; }
        public ICommand RefreshCommand { get; }

        public RecruitmentMetricsViewModel()
        {
            _context = new AppDbContext();
            _recruitmentService = new RecruitmentService(_context);

            CandidatesView = CollectionViewSource.GetDefaultView(Candidates);
            InitializeFilters();

            AddCandidateCommand = new RelayCommand(_ => ExecuteAddCandidate());
            AdvanceStageCommand = new RelayCommand(param => ExecuteAdvanceStage(param));
            MarkHiredCommand = new RelayCommand(param => ExecuteMarkHired(param));
            RejectCandidateCommand = new RelayCommand(param => ExecuteRejectCandidate(param));
            RefreshCommand = new RelayCommand(_ => LoadData());

            WeakEventManager<DashboardEventBus, DashboardDataChangedEventArgs>.AddHandler(
                DashboardEventBus.Instance,
                nameof(DashboardEventBus.DashboardDataChanged),
                OnDataChanged);

            LoadData();
        }

        private void InitializeFilters()
        {
            SourceFilterOptions.Clear();
            SourceFilterOptions.Add(new SelectionOption<RecruitmentSource?> { Label = "All Sources", Value = null });
            foreach (RecruitmentSource source in Enum.GetValues(typeof(RecruitmentSource)))
            {
                SourceFilterOptions.Add(new SelectionOption<RecruitmentSource?>
                {
                    Label = source.ToString(),
                    Value = source
                });
            }
            SelectedSourceFilter = SourceFilterOptions.FirstOrDefault();

            StageFilterOptions.Clear();
            StageFilterOptions.Add(new SelectionOption<RecruitmentStage?> { Label = "All Stages", Value = null });
            foreach (RecruitmentStage stage in Enum.GetValues(typeof(RecruitmentStage)))
            {
                StageFilterOptions.Add(new SelectionOption<RecruitmentStage?>
                {
                    Label = stage.ToString(),
                    Value = stage
                });
            }
            SelectedStageFilter = StageFilterOptions.FirstOrDefault();
        }

        private void OnDataChanged(object? sender, DashboardDataChangedEventArgs args)
        {
            if (args.Domain != DashboardDataDomain.Recruitment)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void ExecuteAddCandidate()
        {
            try
            {
                decimal? expectedSalary = null;
                if (!string.IsNullOrWhiteSpace(ExpectedSalaryText))
                {
                    if (!decimal.TryParse(ExpectedSalaryText, out var parsed))
                    {
                        MessageBox.Show("Expected salary must be a valid number.", "Invalid Value",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    expectedSalary = parsed;
                }

                _recruitmentService.AddCandidate(
                    CandidateFullName,
                    CandidateEmail,
                    SelectedSource,
                    AppliedAt.Date,
                    expectedSalary,
                    CandidateNotes);

                CandidateFullName = string.Empty;
                CandidateEmail = string.Empty;
                ExpectedSalaryText = string.Empty;
                CandidateNotes = string.Empty;
                AppliedAt = DateTime.Today;
                SelectedSource = RecruitmentSource.JobBoard;
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Add Candidate",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteAdvanceStage(object? parameter)
        {
            if (parameter is not RecruitmentCandidate candidate)
            {
                return;
            }

            try
            {
                _recruitmentService.AdvanceStage(candidate.Id);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Update Stage",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteMarkHired(object? parameter)
        {
            if (parameter is not RecruitmentCandidate candidate)
            {
                return;
            }

            try
            {
                _recruitmentService.MarkHired(candidate.Id);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Mark Hired",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteRejectCandidate(object? parameter)
        {
            if (parameter is not RecruitmentCandidate candidate)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Reject candidate {candidate.FullName}?",
                "Confirm Rejection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _recruitmentService.Reject(candidate.Id);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Reject Candidate",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            Candidates.Clear();
            foreach (var candidate in _recruitmentService.GetCandidates())
            {
                Candidates.Add(candidate);
            }

            var pipelineStages = new[]
            {
                RecruitmentStage.Applied,
                RecruitmentStage.Screening,
                RecruitmentStage.Interview,
                RecruitmentStage.OfferExtended
            };

            OpenPipelineCount = Candidates.Count(c => pipelineStages.Contains(c.Stage));
            HiresThisMonth = Candidates.Count(c =>
                c.HiredAt.HasValue &&
                c.HiredAt.Value.Year == DateTime.Today.Year &&
                c.HiredAt.Value.Month == DateTime.Today.Month);

            var hiredCandidates = Candidates
                .Where(c => c.HiredAt.HasValue && c.AppliedAt <= c.HiredAt.Value)
                .ToList();

            AvgTimeToHireDays = hiredCandidates.Count == 0
                ? "0.0"
                : hiredCandidates
                    .Average(c => (c.HiredAt!.Value.Date - c.AppliedAt.Date).TotalDays)
                    .ToString("0.0");

            int offers = Candidates.Count(c => c.OfferedAt.HasValue || c.Stage == RecruitmentStage.OfferExtended || c.Stage == RecruitmentStage.Hired);
            int accepted = Candidates.Count(c => c.Stage == RecruitmentStage.Hired);
            OfferAcceptanceRate = offers == 0
                ? "0%"
                : $"{Math.Round((double)accepted / offers * 100, MidpointRounding.AwayFromZero):0}%";

            var hiredWithExpectedCost = hiredCandidates
                .Where(c => c.ExpectedSalary.HasValue)
                .ToList();
            EstimatedCostPerHire = hiredWithExpectedCost.Count == 0
                ? "0.00"
                : hiredWithExpectedCost
                    .Average(c => c.ExpectedSalary!.Value)
                    .ToString("N2");

            ApplyFilter();
            BuildTrendData();
        }

        private void BuildTrendData()
        {
            RecruitmentTrends.Clear();

            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthStarts = Enumerable.Range(0, 6)
                .Select(offset => currentMonth.AddMonths(-5 + offset))
                .ToList();

            var rawPoints = monthStarts.Select(monthStart =>
            {
                var monthEnd = monthStart.AddMonths(1);
                int appliedCount = Candidates.Count(c => c.AppliedAt >= monthStart && c.AppliedAt < monthEnd);
                int hiredCount = Candidates.Count(c => c.HiredAt.HasValue && c.HiredAt.Value >= monthStart && c.HiredAt.Value < monthEnd);

                return new RecruitmentTrendPoint
                {
                    MonthLabel = monthStart.ToString("MMM yyyy"),
                    AppliedCount = appliedCount,
                    HiredCount = hiredCount
                };
            }).ToList();

            int maxCount = Math.Max(1, rawPoints.Max(p => Math.Max(p.AppliedCount, p.HiredCount)));
            foreach (var point in rawPoints)
            {
                point.AppliedBarWidth = point.AppliedCount == 0 ? 0 : Math.Max(8, point.AppliedCount * 160.0 / maxCount);
                point.HiredBarWidth = point.HiredCount == 0 ? 0 : Math.Max(8, point.HiredCount * 160.0 / maxCount);
                RecruitmentTrends.Add(point);
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? string.Empty;
            var sourceFilter = SelectedSourceFilter?.Value;
            var stageFilter = SelectedStageFilter?.Value;

            CandidatesView.Filter = item =>
            {
                if (item is not RecruitmentCandidate candidate)
                {
                    return false;
                }

                bool matchesQuery = string.IsNullOrWhiteSpace(query)
                    || candidate.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || candidate.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stage.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    || candidate.Source.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

                bool matchesSource = sourceFilter == null || candidate.Source == sourceFilter.Value;
                bool matchesStage = stageFilter == null || candidate.Stage == stageFilter.Value;

                return matchesQuery && matchesSource && matchesStage;
            };

            CandidatesView.Refresh();
        }
    }

    public class RecruitmentTrendPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public int AppliedCount { get; set; }
        public int HiredCount { get; set; }
        public double AppliedBarWidth { get; set; }
        public double HiredBarWidth { get; set; }
    }
}

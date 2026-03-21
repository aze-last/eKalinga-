using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class DeiMetricsViewModel : ObservableObject
    {
        private readonly AppDbContext _context;

        private int _totalActiveEmployees;
        private double _averageHourlyRate;
        private double _payEquityGapPercent;
        private string _highestPayArea = "-";
        private string _lowestPayArea = "-";

        public ObservableCollection<DeiAreaMetric> AreaMetrics { get; } = new();
        public ObservableCollection<string> Alerts { get; } = new();

        public int TotalActiveEmployees
        {
            get => _totalActiveEmployees;
            set => SetProperty(ref _totalActiveEmployees, value);
        }

        public double AverageHourlyRate
        {
            get => _averageHourlyRate;
            set => SetProperty(ref _averageHourlyRate, value);
        }

        public double PayEquityGapPercent
        {
            get => _payEquityGapPercent;
            set => SetProperty(ref _payEquityGapPercent, value);
        }

        public string HighestPayArea
        {
            get => _highestPayArea;
            set => SetProperty(ref _highestPayArea, value);
        }

        public string LowestPayArea
        {
            get => _lowestPayArea;
            set => SetProperty(ref _lowestPayArea, value);
        }

        public ICommand RefreshCommand { get; }

        public DeiMetricsViewModel()
        {
            _context = new AppDbContext();
            RefreshCommand = new RelayCommand(_ => LoadData());

            WeakEventManager<DashboardEventBus, DashboardDataChangedEventArgs>.AddHandler(
                DashboardEventBus.Instance,
                nameof(DashboardEventBus.DashboardDataChanged),
                OnDataChanged);

            LoadData();
        }

        private void OnDataChanged(object? sender, DashboardDataChangedEventArgs args)
        {
            if (args.Domain != DashboardDataDomain.Employee && args.Domain != DashboardDataDomain.Payroll)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            var employees = _context.Employees
                .AsNoTracking()
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();

            TotalActiveEmployees = employees.Count;
            AverageHourlyRate = employees.Count == 0
                ? 0.0
                : Math.Round((double)employees.Average(e => e.HourlyRate), 2);

            var grouped = employees
                .GroupBy(e => e.Position.Area)
                .ToDictionary(g => g.Key, g => g.ToList());

            var hiresWindowStart = DateTime.Today.AddDays(-90);
            AreaMetrics.Clear();
            foreach (PositionArea area in Enum.GetValues(typeof(PositionArea)))
            {
                grouped.TryGetValue(area, out var list);
                list ??= new List<Employee>();

                var headcount = list.Count;
                var avgRate = headcount == 0 ? 0m : list.Average(e => e.HourlyRate);
                var hiresLast90Days = list.Count(e => e.DateHired.Date >= hiresWindowStart);
                var representationPercent = TotalActiveEmployees == 0
                    ? 0.0
                    : (double)headcount / TotalActiveEmployees * 100.0;

                AreaMetrics.Add(new DeiAreaMetric
                {
                    Area = area.ToString(),
                    Headcount = headcount,
                    RepresentationPercent = Math.Round(representationPercent, 1),
                    AverageHourlyRate = Math.Round((double)avgRate, 2),
                    HiresLast90Days = hiresLast90Days
                });
            }

            var nonZeroRates = AreaMetrics
                .Where(a => a.AverageHourlyRate > 0.0)
                .ToList();

            if (nonZeroRates.Count == 0)
            {
                HighestPayArea = "-";
                LowestPayArea = "-";
                PayEquityGapPercent = 0.0;
            }
            else
            {
                var highest = nonZeroRates.OrderByDescending(a => a.AverageHourlyRate).First();
                var lowest = nonZeroRates.OrderBy(a => a.AverageHourlyRate).First();
                HighestPayArea = $"{highest.Area} ({highest.AverageHourlyRate:N2})";
                LowestPayArea = $"{lowest.Area} ({lowest.AverageHourlyRate:N2})";
                PayEquityGapPercent = highest.AverageHourlyRate <= 0.0
                    ? 0.0
                    : Math.Round((highest.AverageHourlyRate - lowest.AverageHourlyRate) / highest.AverageHourlyRate * 100.0, 1);
            }

            BuildAlerts();
        }

        private void BuildAlerts()
        {
            Alerts.Clear();

            if (PayEquityGapPercent > 20.0)
            {
                Alerts.Add($"Pay equity spread is high at {PayEquityGapPercent:0.0}% across areas.");
            }

            var underRepresentedAreas = AreaMetrics
                .Where(a => a.Headcount > 0 && a.RepresentationPercent < 10.0)
                .Select(a => a.Area)
                .ToList();
            if (underRepresentedAreas.Count > 0)
            {
                Alerts.Add($"Low representation detected in: {string.Join(", ", underRepresentedAreas)}.");
            }

            if (AreaMetrics.Sum(a => a.HiresLast90Days) < 3)
            {
                Alerts.Add("Hiring activity in the last 90 days is low for DEI tracking confidence.");
            }

            if (Alerts.Count == 0)
            {
                Alerts.Add("No major DEI and pay-equity risk signals from current workforce data.");
            }
        }
    }

    public class DeiAreaMetric
    {
        public string Area { get; set; } = string.Empty;
        public int Headcount { get; set; }
        public double RepresentationPercent { get; set; }
        public double AverageHourlyRate { get; set; }
        public int HiresLast90Days { get; set; }
    }
}

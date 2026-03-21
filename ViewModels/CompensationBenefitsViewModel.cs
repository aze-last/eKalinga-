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
    public class CompensationBenefitsViewModel : ObservableObject
    {
        private readonly AppDbContext _context;

        private string _searchText = string.Empty;
        private SelectionOption<PositionArea?>? _selectedDepartmentFilter;
        private decimal _monthlyPayrollTotal;
        private decimal _monthlyOvertimeTotal;
        private decimal _averageNetPay;
        private int _monthlyApprovedLeaveCount;
        private decimal _yearToDatePayrollTotal;

        public ObservableCollection<Payroll> PayrollRecords { get; } = new();
        public ICollectionView PayrollRecordsView { get; }
        public ObservableCollection<CompensationTrendPoint> MonthlyTrend { get; } = new();
        public ObservableCollection<SelectionOption<PositionArea?>> DepartmentFilterOptions { get; } = new();
        public ObservableCollection<string> Alerts { get; } = new();

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
                    ApplyFilter();
                    ComputeMetrics();
                    BuildTrend();
                    BuildAlerts();
                }
            }
        }

        public decimal MonthlyPayrollTotal
        {
            get => _monthlyPayrollTotal;
            set => SetProperty(ref _monthlyPayrollTotal, value);
        }

        public decimal MonthlyOvertimeTotal
        {
            get => _monthlyOvertimeTotal;
            set => SetProperty(ref _monthlyOvertimeTotal, value);
        }

        public decimal AverageNetPay
        {
            get => _averageNetPay;
            set => SetProperty(ref _averageNetPay, value);
        }

        public int MonthlyApprovedLeaveCount
        {
            get => _monthlyApprovedLeaveCount;
            set => SetProperty(ref _monthlyApprovedLeaveCount, value);
        }

        public decimal YearToDatePayrollTotal
        {
            get => _yearToDatePayrollTotal;
            set => SetProperty(ref _yearToDatePayrollTotal, value);
        }

        public ICommand RefreshCommand { get; }

        public CompensationBenefitsViewModel()
        {
            _context = new AppDbContext();

            PayrollRecordsView = CollectionViewSource.GetDefaultView(PayrollRecords);
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
            if (args.Domain != DashboardDataDomain.Payroll &&
                args.Domain != DashboardDataDomain.Employee &&
                args.Domain != DashboardDataDomain.Leave)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            PayrollRecords.Clear();
            var rows = _context.Payrolls
                .AsNoTracking()
                .Include(p => p.Employee)
                .ThenInclude(e => e.Position)
                .Include(p => p.GeneratedByUser)
                .OrderByDescending(p => p.PeriodEnd)
                .ToList();

            foreach (var row in rows)
            {
                PayrollRecords.Add(row);
            }

            ApplyFilter();
            ComputeMetrics();
            BuildTrend();
            BuildAlerts();
        }

        private IEnumerable<Payroll> GetScopedPayroll()
        {
            var scoped = PayrollRecords.AsEnumerable();
            if (SelectedDepartmentFilter?.Value is PositionArea area)
            {
                scoped = scoped.Where(p => p.Employee?.Position?.Area == area);
            }

            return scoped;
        }

        private void ComputeMetrics()
        {
            var scoped = GetScopedPayroll().ToList();
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var yearStart = new DateTime(today.Year, 1, 1);

            var monthly = scoped
                .Where(p => p.PeriodEnd >= monthStart && p.PeriodEnd < monthEnd)
                .ToList();

            MonthlyPayrollTotal = monthly.Sum(p => p.TotalPay);
            MonthlyOvertimeTotal = monthly.Sum(p => p.OvertimePay);
            AverageNetPay = monthly.Count == 0
                ? 0m
                : Math.Round(monthly.Average(p => p.TotalPay), 2);

            YearToDatePayrollTotal = scoped
                .Where(p => p.PeriodEnd >= yearStart && p.PeriodEnd < yearStart.AddYears(1))
                .Sum(p => p.TotalPay);

            var approvedLeaves = _context.LeaveRequests
                .AsNoTracking()
                .Include(lr => lr.Employee)
                .ThenInclude(e => e.Position)
                .Where(lr =>
                    lr.Status == LeaveStatus.Approved &&
                    lr.StartDate < monthEnd &&
                    lr.EndDate >= monthStart)
                .ToList();

            if (SelectedDepartmentFilter?.Value is PositionArea areaFilter)
            {
                approvedLeaves = approvedLeaves
                    .Where(lr => lr.Employee?.Position?.Area == areaFilter)
                    .ToList();
            }

            MonthlyApprovedLeaveCount = approvedLeaves.Count;
        }

        private void BuildTrend()
        {
            MonthlyTrend.Clear();
            var scoped = GetScopedPayroll().ToList();

            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var months = Enumerable.Range(0, 6)
                .Select(offset => currentMonth.AddMonths(-5 + offset))
                .ToList();

            var points = months.Select(monthStart =>
            {
                var monthEnd = monthStart.AddMonths(1);
                var set = scoped.Where(p => p.PeriodEnd >= monthStart && p.PeriodEnd < monthEnd).ToList();
                return new CompensationTrendPoint
                {
                    MonthLabel = monthStart.ToString("MMM yyyy"),
                    TotalPay = set.Sum(x => x.TotalPay),
                    OvertimePay = set.Sum(x => x.OvertimePay)
                };
            }).ToList();

            var maxTotal = points.Count == 0 ? 1m : Math.Max(1m, points.Max(p => p.TotalPay));
            foreach (var point in points)
            {
                point.TotalPayBarWidth = point.TotalPay <= 0 ? 0 : Math.Max(8.0, (double)(point.TotalPay / maxTotal * 180m));
                point.OvertimePayBarWidth = point.OvertimePay <= 0 ? 0 : Math.Max(8.0, (double)(point.OvertimePay / maxTotal * 180m));
                MonthlyTrend.Add(point);
            }
        }

        private void BuildAlerts()
        {
            Alerts.Clear();

            if (MonthlyPayrollTotal == 0m)
            {
                Alerts.Add("No payroll records for the selected month/team.");
            }

            if (MonthlyPayrollTotal > 0m)
            {
                var overtimeRate = MonthlyOvertimeTotal / MonthlyPayrollTotal * 100m;
                if (overtimeRate > 18m)
                {
                    Alerts.Add($"Overtime cost is high at {overtimeRate:N1}% of monthly payroll.");
                }
            }

            if (MonthlyApprovedLeaveCount > 8)
            {
                Alerts.Add($"{MonthlyApprovedLeaveCount} approved leaves this month may impact labor planning.");
            }

            if (Alerts.Count == 0)
            {
                Alerts.Add("Compensation and leave utilization are within expected range.");
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            PayrollRecordsView.Filter = item =>
            {
                if (item is not Payroll payroll)
                {
                    return false;
                }

                bool matchesQuery = string.IsNullOrWhiteSpace(query)
                    || (payroll.Employee?.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (payroll.Employee?.Position?.Area.ToString() ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (payroll.GeneratedByUser?.Username ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);

                bool matchesDepartment = SelectedDepartmentFilter?.Value is not PositionArea area
                    || payroll.Employee?.Position?.Area == area;

                return matchesQuery && matchesDepartment;
            };

            PayrollRecordsView.Refresh();
        }
    }

    public class CompensationTrendPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public decimal TotalPay { get; set; }
        public decimal OvertimePay { get; set; }
        public double TotalPayBarWidth { get; set; }
        public double OvertimePayBarWidth { get; set; }
    }
}

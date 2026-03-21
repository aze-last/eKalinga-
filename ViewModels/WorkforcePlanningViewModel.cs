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
    public class WorkforcePlanningViewModel : ObservableObject
    {
        private readonly AppDbContext _context;

        private int _activeHeadcount;
        private int _openPipelineCount;
        private int _exitsLast30Days;
        private int _forecastedHeadcount;
        private int _requiredShiftSlots;
        private int _assignedShiftSlots;
        private double _coveragePercent;

        public ObservableCollection<WorkforceAreaGap> AreaGaps { get; } = new();
        public ObservableCollection<string> Alerts { get; } = new();

        public int ActiveHeadcount
        {
            get => _activeHeadcount;
            set => SetProperty(ref _activeHeadcount, value);
        }

        public int OpenPipelineCount
        {
            get => _openPipelineCount;
            set => SetProperty(ref _openPipelineCount, value);
        }

        public int ExitsLast30Days
        {
            get => _exitsLast30Days;
            set => SetProperty(ref _exitsLast30Days, value);
        }

        public int ForecastedHeadcount
        {
            get => _forecastedHeadcount;
            set => SetProperty(ref _forecastedHeadcount, value);
        }

        public int RequiredShiftSlots
        {
            get => _requiredShiftSlots;
            set => SetProperty(ref _requiredShiftSlots, value);
        }

        public int AssignedShiftSlots
        {
            get => _assignedShiftSlots;
            set => SetProperty(ref _assignedShiftSlots, value);
        }

        public double CoveragePercent
        {
            get => _coveragePercent;
            set => SetProperty(ref _coveragePercent, value);
        }

        public ICommand RefreshCommand { get; }

        public WorkforcePlanningViewModel()
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
            if (args.Domain is not (DashboardDataDomain.Employee
                or DashboardDataDomain.Shift
                or DashboardDataDomain.Recruitment
                or DashboardDataDomain.Turnover))
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(LoadData));
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            var today = DateTime.Today;
            var horizon = today.AddDays(8);

            var activeEmployees = _context.Employees
                .AsNoTracking()
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToList();
            ActiveHeadcount = activeEmployees.Count;

            var openStages = new[]
            {
                RecruitmentStage.Applied,
                RecruitmentStage.Screening,
                RecruitmentStage.Interview,
                RecruitmentStage.OfferExtended
            };
            OpenPipelineCount = _context.RecruitmentCandidates
                .AsNoTracking()
                .Count(c => openStages.Contains(c.Stage));

            ExitsLast30Days = _context.EmployeeExits
                .AsNoTracking()
                .Count(e => e.LastWorkingDate >= today.AddDays(-30) && e.LastWorkingDate <= today);

            int likelyHires = _context.RecruitmentCandidates
                .AsNoTracking()
                .Count(c => c.Stage == RecruitmentStage.OfferExtended || c.Stage == RecruitmentStage.Hired);
            ForecastedHeadcount = ActiveHeadcount + likelyHires - ExitsLast30Days;

            var shifts = _context.Shifts
                .AsNoTracking()
                .Include(s => s.Position)
                .Where(s => s.ShiftDate >= today && s.ShiftDate < horizon)
                .ToList();

            var assignments = _context.ShiftAssignments
                .AsNoTracking()
                .Include(sa => sa.Shift)
                .ThenInclude(s => s.Position)
                .Where(sa => sa.Shift.ShiftDate >= today && sa.Shift.ShiftDate < horizon)
                .ToList();

            RequiredShiftSlots = shifts.Sum(s => RequiredSlotsByArea(s.Position.Area));
            AssignedShiftSlots = assignments.Count;
            CoveragePercent = RequiredShiftSlots == 0
                ? 0.0
                : Math.Round((double)AssignedShiftSlots / RequiredShiftSlots * 100.0, 1);

            BuildAreaGaps(shifts, assignments, activeEmployees);
            BuildAlerts();
        }

        private void BuildAreaGaps(List<Shift> shifts, List<ShiftAssignment> assignments, List<Employee> activeEmployees)
        {
            AreaGaps.Clear();

            foreach (PositionArea area in Enum.GetValues(typeof(PositionArea)))
            {
                var areaShifts = shifts.Where(s => s.Position.Area == area).ToList();
                var areaAssignments = assignments.Where(a => a.Shift.Position.Area == area).ToList();
                var required = areaShifts.Sum(s => RequiredSlotsByArea(s.Position.Area));
                var assigned = areaAssignments.Count;
                var assignedEmployees = areaAssignments.Select(a => a.EmployeeId).Distinct().Count();
                var availableEmployees = activeEmployees.Count(e => e.Position.Area == area);

                AreaGaps.Add(new WorkforceAreaGap
                {
                    Area = area.ToString(),
                    RequiredSlots = required,
                    AssignedSlots = assigned,
                    AvailableEmployees = availableEmployees,
                    AssignedEmployees = assignedEmployees,
                    Gap = Math.Max(0, required - assigned)
                });
            }
        }

        private void BuildAlerts()
        {
            Alerts.Clear();

            if (CoveragePercent < 85.0)
            {
                Alerts.Add($"Upcoming shift coverage is low at {CoveragePercent:0.0}%.");
            }

            var highGaps = AreaGaps.Where(g => g.Gap >= 4).ToList();
            if (highGaps.Count > 0)
            {
                Alerts.Add($"Critical scheduling gaps: {string.Join(", ", highGaps.Select(g => g.Area))}.");
            }

            if (ForecastedHeadcount < ActiveHeadcount)
            {
                Alerts.Add($"Projected next-month headcount drops from {ActiveHeadcount} to {ForecastedHeadcount}.");
            }

            if (OpenPipelineCount < ExitsLast30Days)
            {
                Alerts.Add("Recruitment pipeline is below recent attrition pressure.");
            }

            if (Alerts.Count == 0)
            {
                Alerts.Add("Workforce planning indicators are stable for the next 7 days.");
            }
        }

        private static int RequiredSlotsByArea(PositionArea area)
        {
            return area == PositionArea.Lobby ? 2 : 3;
        }
    }

    public class WorkforceAreaGap
    {
        public string Area { get; set; } = string.Empty;
        public int RequiredSlots { get; set; }
        public int AssignedSlots { get; set; }
        public int AvailableEmployees { get; set; }
        public int AssignedEmployees { get; set; }
        public int Gap { get; set; }
    }
}

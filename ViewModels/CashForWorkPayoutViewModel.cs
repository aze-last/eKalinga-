using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class CfwPendingWorkerListItem
    {
        public int ParticipantId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string BeneficiaryId { get; init; } = string.Empty;
        public string RecordedAtText { get; init; } = string.Empty;
    }

    public sealed class CfwPaidEventListItem
    {
        public int EventId { get; init; }
        public string EventTitle { get; init; } = string.Empty;
        public string AmountText { get; init; } = string.Empty;
        public int RecipientCount { get; init; }
        public string ReleasedAtText { get; init; } = string.Empty;
    }

    public sealed class CashForWorkPayoutViewModel : ObservableObject
    {
        private const int PageSize = 25;

        private static readonly Brush NeutralBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BE123C"));
        private static readonly Brush WarningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#854D0E"));

        private readonly User _currentUser;

        private CashForWorkBudget? _selectedCfwProject;
        private CashForWorkEvent? _selectedEvent;
        private string _statusMessage = "Select a cash-for-work project to begin.";
        private Brush _statusBrush = NeutralBrush;
        private string _remainingBudgetText = "—";
        private int _totalWorkers;
        private int _pendingCount;
        private int _paidCount;
        private decimal _dailyRate;
        private string _scannedWorkerName = string.Empty;
        private string _scannedWorkerId = string.Empty;
        private string _scannedWorkerStatus = string.Empty;
        private Brush _scannedWorkerStatusColor = NeutralBrush;
        private int _scannedAttendanceDays;
        private decimal _scannedAmountDue;
        private bool _isBusy;
        private bool _isReleaseConfirmVisible;
        private int _releaseReadyCount;
        private decimal _releaseProposedAmount;
        private int _pendingPage = 1;
        private int _paidPage = 1;

        private List<CfwPendingWorkerListItem> _allPendingWorkers = new();
        private List<CfwPaidEventListItem> _allPaidEvents = new();

        private readonly RelayCommand _processScanCommand;
        private readonly RelayCommand _openReleaseConfirmCommand;
        private readonly RelayCommand _confirmReleaseCommand;
        private readonly RelayCommand _cancelReleaseCommand;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _prevPendingPageCommand;
        private readonly RelayCommand _nextPendingPageCommand;
        private readonly RelayCommand _prevPaidPageCommand;
        private readonly RelayCommand _nextPaidPageCommand;

        internal event Action? RequestScannerFocus;

        public CashForWorkPayoutViewModel(User currentUser)
        {
            _currentUser = currentUser;

            _processScanCommand = new RelayCommand(async param => await ExecuteProcessScanAsync(param as string), _ => !IsBusy && SelectedEvent != null);
            _openReleaseConfirmCommand = new RelayCommand(async _ => await OpenReleaseConfirmAsync(), _ => !IsBusy && SelectedEvent != null && PendingCount > 0);
            _confirmReleaseCommand = new RelayCommand(async _ => await ExecuteConfirmReleaseAsync(), _ => !IsBusy && IsReleaseConfirmVisible);
            _cancelReleaseCommand = new RelayCommand(_ => IsReleaseConfirmVisible = false);
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _prevPendingPageCommand = new RelayCommand(_ => ChangePendingPage(-1), _ => _pendingPage > 1);
            _nextPendingPageCommand = new RelayCommand(_ => ChangePendingPage(1), _ => _pendingPage < PendingTotalPages);
            _prevPaidPageCommand = new RelayCommand(_ => ChangePaidPage(-1), _ => _paidPage > 1);
            _nextPaidPageCommand = new RelayCommand(_ => ChangePaidPage(1), _ => _paidPage < PaidTotalPages);

            CfwProjects = new ObservableCollection<CashForWorkBudget>();
            ProjectEvents = new ObservableCollection<CashForWorkEvent>();
            PendingWorkers = new ObservableCollection<CfwPendingWorkerListItem>();
            PaidEvents = new ObservableCollection<CfwPaidEventListItem>();

            _ = LoadAsync();
        }

        public ObservableCollection<CashForWorkBudget> CfwProjects { get; }
        public ObservableCollection<CashForWorkEvent> ProjectEvents { get; }
        public ObservableCollection<CfwPendingWorkerListItem> PendingWorkers { get; }
        public ObservableCollection<CfwPaidEventListItem> PaidEvents { get; }

        public ICommand ProcessScanCommand => _processScanCommand;
        public ICommand OpenReleaseConfirmCommand => _openReleaseConfirmCommand;
        public ICommand ConfirmReleaseCommand => _confirmReleaseCommand;
        public ICommand CancelReleaseCommand => _cancelReleaseCommand;
        public ICommand RefreshCommand => _refreshCommand;
        public ICommand PrevPendingPageCommand => _prevPendingPageCommand;
        public ICommand NextPendingPageCommand => _nextPendingPageCommand;
        public ICommand PrevPaidPageCommand => _prevPaidPageCommand;
        public ICommand NextPaidPageCommand => _nextPaidPageCommand;

        public CashForWorkBudget? SelectedCfwProject
        {
            get => _selectedCfwProject;
            set
            {
                if (SetProperty(ref _selectedCfwProject, value))
                {
                    _ = LoadProjectDetailsAsync();
                    OnPropertyChanged(nameof(ModuleTitle));
                    OnPropertyChanged(nameof(WorkerLabel));
                    OnPropertyChanged(nameof(WorkersLabel));
                    OnPropertyChanged(nameof(ScannedWorkerHeader));
                    OnPropertyChanged(nameof(EventWorkersHeader));
                    OnPropertyChanged(nameof(ReleaseReadyWorkersHeader));
                    OnPropertyChanged(nameof(WorkersPresentHeader));
                    OnPropertyChanged(nameof(DaysPresentHeader));
                    OnPropertyChanged(nameof(DailyRateHeader));
                    OnPropertyChanged(nameof(AmountDueHeader));
                    OnPropertyChanged(nameof(EventLabel));
                    OnPropertyChanged(nameof(ReleaseConfirmTitle));
                    OnPropertyChanged(nameof(ConfirmReleaseButtonText));
                }
            }
        }


        public string ModuleTitle => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "Seminar Distribution" : "Cash-for-Work Payout";
        public string WorkerLabel => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "ATTENDEE" : "WORKER";
        public string WorkersLabel => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "ATTENDEES" : "WORKERS";
        public string ScannedWorkerHeader => $"SCANNED {WorkerLabel}";
        public string EventWorkersHeader => $"EVENT {WorkersLabel}";
        public string ReleaseReadyWorkersHeader => $"RELEASE-READY {WorkersLabel} (PRESENT)";
        public string WorkersPresentHeader => $"{WorkersLabel} present";
        public string DaysPresentHeader => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "Attended sessions" : "Days present";
        public string DailyRateHeader => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "Benefit unit value" : "Daily rate";
        public string AmountDueHeader => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "Benefit value due" : "Amount due";
        public string EventLabel => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "SEMINAR EVENT" : "WORK EVENT";
        public string ReleaseConfirmTitle => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "Confirm Seminar Distribution" : "Confirm Event Payout";
        public string ConfirmReleaseButtonText => SelectedCfwProject?.BudgetCode?.StartsWith("SEM-") == true ? "CONFIRM DISTRIBUTION" : "CONFIRM RELEASE";

        public CashForWorkEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (SetProperty(ref _selectedEvent, value))
                {
                    _ = LoadEventDetailsAsync();
                }
            }
        }

        public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
        public Brush StatusBrush { get => _statusBrush; private set => SetProperty(ref _statusBrush, value); }
        public string RemainingBudgetText { get => _remainingBudgetText; private set => SetProperty(ref _remainingBudgetText, value); }
        public int TotalWorkers { get => _totalWorkers; private set => SetProperty(ref _totalWorkers, value); }
        public int PendingCount { get => _pendingCount; private set => SetProperty(ref _pendingCount, value); }
        public int PaidCount { get => _paidCount; private set => SetProperty(ref _paidCount, value); }
        public decimal DailyRate { get => _dailyRate; private set => SetProperty(ref _dailyRate, value); }
        public string ScannedWorkerName { get => _scannedWorkerName; private set => SetProperty(ref _scannedWorkerName, value); }
        public string ScannedWorkerId { get => _scannedWorkerId; private set => SetProperty(ref _scannedWorkerId, value); }
        public string ScannedWorkerStatus { get => _scannedWorkerStatus; private set => SetProperty(ref _scannedWorkerStatus, value); }
        public Brush ScannedWorkerStatusColor { get => _scannedWorkerStatusColor; private set => SetProperty(ref _scannedWorkerStatusColor, value); }
        public int ScannedAttendanceDays { get => _scannedAttendanceDays; private set => SetProperty(ref _scannedAttendanceDays, value); }
        public decimal ScannedAmountDue { get => _scannedAmountDue; private set => SetProperty(ref _scannedAmountDue, value); }
        public int ReleaseReadyCount { get => _releaseReadyCount; private set => SetProperty(ref _releaseReadyCount, value); }
        public decimal ReleaseProposedAmount { get => _releaseProposedAmount; private set => SetProperty(ref _releaseProposedAmount, value); }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public bool IsReleaseConfirmVisible
        {
            get => _isReleaseConfirmVisible;
            private set => SetProperty(ref _isReleaseConfirmVisible, value);
        }

        private int PendingTotalPages => Math.Max(1, (_allPendingWorkers.Count + PageSize - 1) / PageSize);
        private int PaidTotalPages => Math.Max(1, (_allPaidEvents.Count + PageSize - 1) / PageSize);

        public string PendingPaginationText => $"Page {_pendingPage} of {PendingTotalPages}";
        public string PaidPaginationText => $"Page {_paidPage} of {PaidTotalPages}";

        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                SetNeutralStatus("Loading projects...");

                await using var context = new LocalDbContext();
                var budgetService = new BudgetManagementService(context);
                var budgets = (await budgetService.GetCashForWorkBudgetsAsync())
                    .Where(b => b.BudgetCode != "GLOBAL_CFW_BUDGET")
                    .ToList();

                var previousSelectionId = SelectedCfwProject?.Id;
                CfwProjects.Clear();
                foreach (var budget in budgets)
                {
                    CfwProjects.Add(budget);
                }

                var restored = budgets.FirstOrDefault(b => b.Id == previousSelectionId);
                if (restored != null)
                {
                    _selectedCfwProject = restored;
                    OnPropertyChanged(nameof(SelectedCfwProject));
                    await LoadProjectDetailsAsync();
                }
                else if (SelectedCfwProject == null)
                {
                    SetNeutralStatus(budgets.Count > 0
                        ? "Select a cash-for-work project to begin."
                        : "No cash-for-work projects yet. Create one in the Budget module.");
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Load failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadProjectDetailsAsync()
        {
            if (SelectedCfwProject == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var projectId = SelectedCfwProject.Id;
                await using var context = new LocalDbContext();

                var spent = await context.BudgetLedgerEntries.AsNoTracking()
                    .Where(e => e.EntryType == BudgetLedgerEntryType.Release && e.CashForWorkBudgetId == projectId)
                    .SumAsync(e => (decimal?)e.TotalAmount) ?? 0m;

                RemainingBudgetText = SelectedCfwProject.BudgetCap.HasValue
                    ? $"PHP {SelectedCfwProject.BudgetCap.Value - spent:N2}"
                    : $"No cap (PHP {spent:N2} released)";

                var events = await context.CashForWorkEvents.AsNoTracking()
                    .Where(e => !e.IsDeleted && e.CashForWorkBudgetId == projectId &&
                                (e.EventKind == CashForWorkEventKind.CashForWork || e.EventKind == CashForWorkEventKind.Seminar))
                    .OrderByDescending(e => e.EventDate)
                    .ToListAsync();

                ProjectEvents.Clear();
                foreach (var cfwEvent in events.Where(e => e.Status == CashForWorkEventStatus.Open))
                {
                    ProjectEvents.Add(cfwEvent);
                }

                _allPaidEvents = events
                    .Where(e => e.Status == CashForWorkEventStatus.Completed && e.ReleasedAt.HasValue)
                    .Select(e => new CfwPaidEventListItem
                    {
                        EventId = e.Id,
                        EventTitle = e.Title,
                        AmountText = $"PHP {e.ReleaseAmount ?? 0m:N2}",
                        ReleasedAtText = e.ReleasedAt?.ToString("yyyy-MM-dd hh:mm tt") ?? string.Empty
                    })
                    .ToList();
                PaidCount = _allPaidEvents.Count;
                _paidPage = 1;
                RefreshPaidPage();

                ClearScannedWorker();

                var openEvent = ProjectEvents.FirstOrDefault();
                _selectedEvent = openEvent;
                OnPropertyChanged(nameof(SelectedEvent));

                if (openEvent != null)
                {
                    await LoadEventDetailsAsync();
                    SetSuccessStatus($"Project '{SelectedCfwProject.BudgetName}' loaded.");
                }
                else
                {
                    DailyRate = SelectedCfwProject.DailyRate ?? 0m;
                    TotalWorkers = 0;
                    PendingCount = 0;
                    _allPendingWorkers = new List<CfwPendingWorkerListItem>();
                    _pendingPage = 1;
                    RefreshPendingPage();
                    SetNeutralStatus(SelectedCfwProject.BudgetCode.StartsWith("SEM-")
                        ? "No open seminar event for this project. Create one in the Seminar Attendance module."
                        : "No open work event for this project. Create one in the Cash-for-Work module (Attendance & Payouts).");
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to load project: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RequestScannerFocus?.Invoke();
            }
        }

        private async Task LoadEventDetailsAsync()
        {
            if (SelectedEvent == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var eventId = SelectedEvent.Id;
                await using var context = new LocalDbContext();
                var cfwService = new CashForWorkService(context);

                DailyRate = SelectedEvent.UnitAmount > 0
                    ? SelectedEvent.UnitAmount
                    : SelectedCfwProject?.DailyRate ?? 0m;

                TotalWorkers = await context.CashForWorkParticipants.AsNoTracking()
                    .CountAsync(p => !p.IsDeleted && p.EventId == eventId);

                var summary = cfwService.GetReleaseReadySummary(eventId);
                ReleaseReadyCount = summary.ReleaseReadyParticipantCount;
                ReleaseProposedAmount = summary.ProposedAmount > 0
                    ? summary.ProposedAmount
                    : summary.ReleaseReadyParticipantCount * DailyRate;

                _allPendingWorkers = summary.ReleaseReadyParticipants
                    .Select(p => new CfwPendingWorkerListItem
                    {
                        ParticipantId = p.ParticipantId,
                        FullName = p.FullName,
                        BeneficiaryId = p.BeneficiaryId ?? string.Empty,
                        RecordedAtText = p.RecordedAt.ToString("yyyy-MM-dd hh:mm tt")
                    })
                    .ToList();
                PendingCount = _allPendingWorkers.Count;
                _pendingPage = 1;
                RefreshPendingPage();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to load event: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RequestScannerFocus?.Invoke();
            }
        }

        private async Task ExecuteProcessScanAsync(string? qrPayload)
        {
            if (string.IsNullOrWhiteSpace(qrPayload) || SelectedEvent == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var eventId = SelectedEvent.Id;
                await using var context = new LocalDbContext();

                var digitalIdService = new BeneficiaryDigitalIdService(context);
                var lookup = await digitalIdService.LookupByQrPayloadAsync(qrPayload);
                if (lookup == null)
                {
                    ClearScannedWorker();
                    ScannedWorkerStatus = "ID NOT RECOGNIZED";
                    ScannedWorkerStatusColor = ErrorBrush;
                    SetErrorStatus("Scanned ID could not be resolved.");
                    return;
                }

                ScannedWorkerName = lookup.FullName;
                ScannedWorkerId = lookup.BeneficiaryId ?? string.Empty;

                var participant = await context.CashForWorkParticipants.AsNoTracking()
                    .FirstOrDefaultAsync(p => !p.IsDeleted && p.EventId == eventId && p.BeneficiaryStagingId == lookup.BeneficiaryStagingId);

                if (participant == null)
                {
                    ScannedWorkerStatus = "NOT ENROLLED IN THIS EVENT";
                    ScannedWorkerStatusColor = WarningBrush;
                    ScannedAttendanceDays = 0;
                    ScannedAmountDue = 0m;
                    SetErrorStatus($"{lookup.FullName} is not a participant of '{SelectedEvent.Title}'.");
                    return;
                }

                var alreadyToday = await context.CashForWorkAttendances.AsNoTracking()
                    .AnyAsync(a => !a.IsDeleted && a.ParticipantId == participant.Id && a.AttendanceDate == DateTime.Today);

                if (alreadyToday)
                {
                    ScannedWorkerStatus = "ALREADY MARKED PRESENT TODAY";
                    ScannedWorkerStatusColor = WarningBrush;
                }
                else
                {
                    var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: null);
                    var saved = await cfwService.SaveScannerAttendanceAsync(eventId, _currentUser.Id, participant.Id, qrPayload);
                    if (!saved)
                    {
                        ScannedWorkerStatus = "ATTENDANCE NOT RECORDED";
                        ScannedWorkerStatusColor = ErrorBrush;
                        SetErrorStatus($"Could not record attendance for {lookup.FullName}.");
                        return;
                    }

                    ScannedWorkerStatus = "MARKED PRESENT";
                    ScannedWorkerStatusColor = SuccessBrush;
                }

                ScannedAttendanceDays = await context.CashForWorkAttendances.AsNoTracking()
                    .CountAsync(a => !a.IsDeleted && a.ParticipantId == participant.Id && a.Status == CashForWorkAttendanceStatus.Present);
                ScannedAmountDue = ScannedAttendanceDays * DailyRate;

                SetSuccessStatus($"{lookup.FullName}: {ScannedAttendanceDays} day{(ScannedAttendanceDays == 1 ? "" : "s")} present.");
                await LoadEventDetailsAsync();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Scan failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RequestScannerFocus?.Invoke();
            }
        }

        private async Task OpenReleaseConfirmAsync()
        {
            if (SelectedEvent == null)
            {
                return;
            }

            await LoadEventDetailsAsync();
            if (ReleaseReadyCount <= 0)
            {
                SetErrorStatus("No release-ready attendance recorded for this event.");
                return;
            }

            if (ReleaseProposedAmount <= 0)
            {
                SetErrorStatus("Set a unit amount on the event (or daily rate on the project) before releasing.");
                return;
            }

            IsReleaseConfirmVisible = true;
        }

        private async Task ExecuteConfirmReleaseAsync()
        {
            if (SelectedEvent == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                SetNeutralStatus("Recording release...");

                await using var context = new LocalDbContext();
                var auditService = new AuditService(context);
                var cfwService = new CashForWorkService(context, auditService);
                var result = await cfwService.ReleaseEventAsync(
                    SelectedEvent.Id,
                    ReleaseProposedAmount,
                    _currentUser.Id,
                    remarks: null);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                IsReleaseConfirmVisible = false;
                ClearScannedWorker();
                SetSuccessStatus(result.Message);
                await LoadProjectDetailsAsync();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Release failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                RequestScannerFocus?.Invoke();
            }
        }

        private void ChangePendingPage(int delta)
        {
            _pendingPage = Math.Clamp(_pendingPage + delta, 1, PendingTotalPages);
            RefreshPendingPage();
        }

        private void ChangePaidPage(int delta)
        {
            _paidPage = Math.Clamp(_paidPage + delta, 1, PaidTotalPages);
            RefreshPaidPage();
        }

        private void RefreshPendingPage()
        {
            PendingWorkers.Clear();
            foreach (var item in _allPendingWorkers.Skip((_pendingPage - 1) * PageSize).Take(PageSize))
            {
                PendingWorkers.Add(item);
            }
            OnPropertyChanged(nameof(PendingPaginationText));
        }

        private void RefreshPaidPage()
        {
            PaidEvents.Clear();
            foreach (var item in _allPaidEvents.Skip((_paidPage - 1) * PageSize).Take(PageSize))
            {
                PaidEvents.Add(item);
            }
            OnPropertyChanged(nameof(PaidPaginationText));
        }

        private void ClearScannedWorker()
        {
            ScannedWorkerName = string.Empty;
            ScannedWorkerId = string.Empty;
            ScannedWorkerStatus = string.Empty;
            ScannedWorkerStatusColor = NeutralBrush;
            ScannedAttendanceDays = 0;
            ScannedAmountDue = 0m;
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = SuccessBrush;
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = ErrorBrush;
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = NeutralBrush;
        }
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class CashForWorkOcrViewModel : ObservableObject
    {
        private static readonly Brush NeutralBrush = CreateBrush("#6B7280");
        private static readonly Brush SuccessBrush = CreateBrush("#1A7A4A");
        private static readonly Brush ErrorBrush = CreateBrush("#991B1B");
        private static readonly Brush WarningBrush = CreateBrush("#9A6700");

        private readonly User _currentUser;
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly CashForWorkService _cashForWorkService;
        private readonly ReportsService _reportsService;
        private readonly ReportDocumentService _documentService;
        private readonly RelayCommand _saveEventCommand;
        private readonly RelayCommand _addParticipantCommand;
        private readonly RelayCommand _saveManualAttendanceCommand;
        private readonly RelayCommand _releaseBudgetCommand;
        private readonly RelayCommand _createAttendanceScannerSessionCommand;
        private readonly RelayCommand _openCreateEventPanelCommand;
        private readonly RelayCommand _openSeminarPanelCommand;
        private readonly RelayCommand _openAddBeneficiariesPanelCommand;
        private readonly RelayCommand _openScanAttendancePanelCommand;
        private readonly RelayCommand _openPayoutPanelCommand;
        private readonly RelayCommand _openAnnouncementsPanelCommand;
        private readonly RelayCommand _closePanelCommand;
        private readonly RelayCommand _refreshWorkspaceCommand;
        private readonly RelayCommand _printAttendanceSheetCommand;

        private CashForWorkEvent? _selectedEvent;
        private AyudaProgram? _selectedAyudaProgram;
        private CashForWorkEligibleBeneficiaryOption? _selectedEligibleBeneficiary;
        private CashForWorkWorkspacePanel _activePanel;
        private CashForWorkEventKind _eventEditorKind = CashForWorkEventKind.CashForWork;
        private bool _isBusy;
        private int _historyRequestVersion;
        private string _drawerTitle = "Workspace";
        private string _drawerSubtitle = "Select an action from the left rail.";
        private string _eventTitle = string.Empty;
        private string _eventLocation = string.Empty;
        private string _eventNotes = string.Empty;
        private DateTime _eventDate = DateTime.Today;
        private string _eventStartTime = "07:00";
        private string _eventEndTime = "12:00";
        private string _statusMessage = "Select an event from the dropdown or open the Create Event panel.";
        private Brush _statusBrush = NeutralBrush;
        private string _attendanceSummary = "Attendance records will appear after you select an event.";
        private string _releaseAmountText = string.Empty;
        private string _releaseSummaryEventLabel = "No event selected.";
        private string _releaseSummaryStatusText = "Select an event to build a payout summary.";
        private string _releaseSummaryDetail = "Release details appear here once an event is selected.";
        private Brush _releaseSummaryStatusBrush = NeutralBrush;
        private int _approvedParticipantCount;
        private int _presentParticipantCount;
        private int _pendingParticipantCount;
        private int _manualAttendanceCount;
        private string _attendanceScannerSessionUrl = string.Empty;
        private string _attendanceScannerSessionPin = string.Empty;
        private string _attendanceScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _attendanceScannerQrImage;
        private string _historyTitle = "Attendance History";
        private string _historySubtitle = "Select an event to load the printable attendance sheet.";
        private string _historyRangeSummary = "--";
        private string _historyProgramSummary = "--";
        private string _historyLayoutSummary = "Suggested layout: A4 Portrait";
        private string _historyExportHint = "Print and choose Microsoft Print to PDF when a PDF file is needed.";
        private DataView? _historyPreviewRows;
        private ReportsSnapshot? _historySnapshot;

        public CashForWorkOcrViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _context = new AppDbContext();
            _auditService = new AuditService(_context);
            _cashForWorkService = new CashForWorkService(_context, _auditService);
            _reportsService = new ReportsService();
            _documentService = new ReportDocumentService();

            Events = new ObservableCollection<CashForWorkEvent>();
            EligibleBeneficiaries = new ObservableCollection<CashForWorkEligibleBeneficiaryOption>();
            Participants = new ObservableCollection<CashForWorkParticipantListItem>();
            SavedAttendanceRows = new ObservableCollection<CashForWorkSavedAttendanceRow>();
            AyudaPrograms = new ObservableCollection<AyudaProgram>();
            OpenAnnouncements = new ObservableCollection<CashForWorkAnnouncementItem>();
            HistoryMetrics = new ObservableCollection<ReportsMetricItem>();
            HistoryHighlights = new ObservableCollection<string>();

            _saveEventCommand = new RelayCommand(_ => ExecuteSaveEvent(), _ => !IsBusy);
            _addParticipantCommand = new RelayCommand(_ => ExecuteAddParticipant(), _ => !IsBusy);
            _saveManualAttendanceCommand = new RelayCommand(_ => ExecuteSaveManualAttendance(), _ => !IsBusy);
            _releaseBudgetCommand = new RelayCommand(async _ => await ExecuteReleaseBudgetAsync(), _ => !IsBusy);
            _createAttendanceScannerSessionCommand = new RelayCommand(async _ => await ExecuteCreateAttendanceScannerSessionAsync(), _ => !IsBusy);
            _openCreateEventPanelCommand = new RelayCommand(_ => OpenEventEditorPanel(CashForWorkEventKind.CashForWork), _ => !IsBusy);
            _openSeminarPanelCommand = new RelayCommand(_ => OpenEventEditorPanel(CashForWorkEventKind.Seminar), _ => !IsBusy);
            _openAddBeneficiariesPanelCommand = new RelayCommand(_ => OpenBeneficiariesPanel(), _ => !IsBusy);
            _openScanAttendancePanelCommand = new RelayCommand(_ => OpenScanAttendancePanel(), _ => !IsBusy);
            _openPayoutPanelCommand = new RelayCommand(_ => OpenPayoutPanel(), _ => !IsBusy);
            _openAnnouncementsPanelCommand = new RelayCommand(_ => OpenAnnouncementsPanel(), _ => !IsBusy);
            _closePanelCommand = new RelayCommand(_ => ClosePanel());
            _refreshWorkspaceCommand = new RelayCommand(async _ => await RefreshWorkspaceAsync(), _ => !IsBusy);
            _printAttendanceSheetCommand = new RelayCommand(_ => PrintAttendanceSheet(), _ => !IsBusy && _historySnapshot != null);

            LoadAyudaPrograms();
            LoadEligibleBeneficiaries();
            LoadAnnouncements();
            LoadEvents();
            ClearHistorySnapshot();
        }

        public ObservableCollection<CashForWorkEvent> Events { get; }
        public ObservableCollection<CashForWorkEligibleBeneficiaryOption> EligibleBeneficiaries { get; }
        public ObservableCollection<CashForWorkParticipantListItem> Participants { get; }
        public ObservableCollection<CashForWorkSavedAttendanceRow> SavedAttendanceRows { get; }
        public ObservableCollection<AyudaProgram> AyudaPrograms { get; }
        public ObservableCollection<CashForWorkAnnouncementItem> OpenAnnouncements { get; }
        public ObservableCollection<ReportsMetricItem> HistoryMetrics { get; }
        public ObservableCollection<string> HistoryHighlights { get; }

        public ICommand SaveEventCommand => _saveEventCommand;
        public ICommand AddParticipantCommand => _addParticipantCommand;
        public ICommand SaveManualAttendanceCommand => _saveManualAttendanceCommand;
        public ICommand ReleaseBudgetCommand => _releaseBudgetCommand;
        public ICommand CreateAttendanceScannerSessionCommand => _createAttendanceScannerSessionCommand;
        public ICommand OpenCreateEventPanelCommand => _openCreateEventPanelCommand;
        public ICommand OpenSeminarPanelCommand => _openSeminarPanelCommand;
        public ICommand OpenAddBeneficiariesPanelCommand => _openAddBeneficiariesPanelCommand;
        public ICommand OpenScanAttendancePanelCommand => _openScanAttendancePanelCommand;
        public ICommand OpenPayoutPanelCommand => _openPayoutPanelCommand;
        public ICommand OpenAnnouncementsPanelCommand => _openAnnouncementsPanelCommand;
        public ICommand ClosePanelCommand => _closePanelCommand;
        public ICommand RefreshWorkspaceCommand => _refreshWorkspaceCommand;
        public ICommand PrintAttendanceSheetCommand => _printAttendanceSheetCommand;

        public CashForWorkEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (!SetProperty(ref _selectedEvent, value))
                {
                    return;
                }

                LoadParticipants();
                LoadSavedAttendance();
                LoadReleaseSummary();
                LoadSelectedProgram();
                ResetScannerSession();
                RefreshDrawerCopy();
                RefreshSelectedEventFlags();
                _ = LoadHistorySnapshotAsync(value?.Id);

                if (value == null)
                {
                    SetNeutralStatus("Select an event from the dropdown or open the Create Event panel.");
                }
                else
                {
                    SetSuccessStatus($"Loaded {value.WorkspaceLabel}.");
                }
            }
        }

        public AyudaProgram? SelectedAyudaProgram
        {
            get => _selectedAyudaProgram;
            set => SetProperty(ref _selectedAyudaProgram, value);
        }

        public CashForWorkEligibleBeneficiaryOption? SelectedEligibleBeneficiary
        {
            get => _selectedEligibleBeneficiary;
            set => SetProperty(ref _selectedEligibleBeneficiary, value);
        }

        public CashForWorkWorkspacePanel ActivePanel
        {
            get => _activePanel;
            private set
            {
                if (SetProperty(ref _activePanel, value))
                {
                    RefreshDrawerVisibilityFlags();
                }
            }
        }

        public string DrawerTitle
        {
            get => _drawerTitle;
            private set => SetProperty(ref _drawerTitle, value);
        }

        public string DrawerSubtitle
        {
            get => _drawerSubtitle;
            private set => SetProperty(ref _drawerSubtitle, value);
        }

        public string EventTitle
        {
            get => _eventTitle;
            set => SetProperty(ref _eventTitle, value);
        }

        public string EventLocation
        {
            get => _eventLocation;
            set => SetProperty(ref _eventLocation, value);
        }

        public string EventNotes
        {
            get => _eventNotes;
            set => SetProperty(ref _eventNotes, value);
        }

        public DateTime EventDate
        {
            get => _eventDate;
            set => SetProperty(ref _eventDate, value);
        }

        public string EventStartTime
        {
            get => _eventStartTime;
            set => SetProperty(ref _eventStartTime, value);
        }

        public string EventEndTime
        {
            get => _eventEndTime;
            set => SetProperty(ref _eventEndTime, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        public string AttendanceSummary
        {
            get => _attendanceSummary;
            private set => SetProperty(ref _attendanceSummary, value);
        }

        public string ReleaseAmountText
        {
            get => _releaseAmountText;
            set => SetProperty(ref _releaseAmountText, value);
        }

        public string ReleaseSummaryEventLabel
        {
            get => _releaseSummaryEventLabel;
            private set => SetProperty(ref _releaseSummaryEventLabel, value);
        }

        public string ReleaseSummaryStatusText
        {
            get => _releaseSummaryStatusText;
            private set => SetProperty(ref _releaseSummaryStatusText, value);
        }

        public string ReleaseSummaryDetail
        {
            get => _releaseSummaryDetail;
            private set => SetProperty(ref _releaseSummaryDetail, value);
        }

        public Brush ReleaseSummaryStatusBrush
        {
            get => _releaseSummaryStatusBrush;
            private set => SetProperty(ref _releaseSummaryStatusBrush, value);
        }

        public int ApprovedParticipantCount
        {
            get => _approvedParticipantCount;
            private set => SetProperty(ref _approvedParticipantCount, value);
        }

        public int PresentParticipantCount
        {
            get => _presentParticipantCount;
            private set => SetProperty(ref _presentParticipantCount, value);
        }

        public int PendingParticipantCount
        {
            get => _pendingParticipantCount;
            private set => SetProperty(ref _pendingParticipantCount, value);
        }

        public int ManualAttendanceCount
        {
            get => _manualAttendanceCount;
            private set => SetProperty(ref _manualAttendanceCount, value);
        }

        public string AttendanceScannerSessionUrl
        {
            get => _attendanceScannerSessionUrl;
            private set => SetProperty(ref _attendanceScannerSessionUrl, value);
        }

        public string AttendanceScannerSessionPin
        {
            get => _attendanceScannerSessionPin;
            private set => SetProperty(ref _attendanceScannerSessionPin, value);
        }

        public string AttendanceScannerSessionExpiresAtText
        {
            get => _attendanceScannerSessionExpiresAtText;
            private set => SetProperty(ref _attendanceScannerSessionExpiresAtText, value);
        }

        public BitmapSource? AttendanceScannerQrImage
        {
            get => _attendanceScannerQrImage;
            private set => SetProperty(ref _attendanceScannerQrImage, value);
        }

        public string HistoryTitle
        {
            get => _historyTitle;
            private set => SetProperty(ref _historyTitle, value);
        }

        public string HistorySubtitle
        {
            get => _historySubtitle;
            private set => SetProperty(ref _historySubtitle, value);
        }

        public string HistoryRangeSummary
        {
            get => _historyRangeSummary;
            private set => SetProperty(ref _historyRangeSummary, value);
        }

        public string HistoryProgramSummary
        {
            get => _historyProgramSummary;
            private set => SetProperty(ref _historyProgramSummary, value);
        }

        public string HistoryLayoutSummary
        {
            get => _historyLayoutSummary;
            private set => SetProperty(ref _historyLayoutSummary, value);
        }

        public string HistoryExportHint
        {
            get => _historyExportHint;
            private set => SetProperty(ref _historyExportHint, value);
        }

        public DataView? HistoryPreviewRows
        {
            get => _historyPreviewRows;
            private set => SetProperty(ref _historyPreviewRows, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (!SetProperty(ref _isBusy, value))
                {
                    return;
                }

                _saveEventCommand.RaiseCanExecuteChanged();
                _addParticipantCommand.RaiseCanExecuteChanged();
                _saveManualAttendanceCommand.RaiseCanExecuteChanged();
                _releaseBudgetCommand.RaiseCanExecuteChanged();
                _createAttendanceScannerSessionCommand.RaiseCanExecuteChanged();
                _openCreateEventPanelCommand.RaiseCanExecuteChanged();
                _openSeminarPanelCommand.RaiseCanExecuteChanged();
                _openAddBeneficiariesPanelCommand.RaiseCanExecuteChanged();
                _openScanAttendancePanelCommand.RaiseCanExecuteChanged();
                _openPayoutPanelCommand.RaiseCanExecuteChanged();
                _openAnnouncementsPanelCommand.RaiseCanExecuteChanged();
                _refreshWorkspaceCommand.RaiseCanExecuteChanged();
                _printAttendanceSheetCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasSelectedEvent => SelectedEvent != null;
        public bool HasOpenAnnouncements => OpenAnnouncements.Count > 0;
        public Visibility SelectedEventVisibility => HasSelectedEvent ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NoSelectedEventVisibility => HasSelectedEvent ? Visibility.Collapsed : Visibility.Visible;
        public bool IsDrawerOpen => ActivePanel != CashForWorkWorkspacePanel.None;
        public Visibility DrawerVisibility => IsDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EventEditorVisibility => ActivePanel == CashForWorkWorkspacePanel.EventEditor ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BeneficiariesVisibility => ActivePanel == CashForWorkWorkspacePanel.Beneficiaries ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ScanAttendanceVisibility => ActivePanel == CashForWorkWorkspacePanel.ScanAttendance ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PayoutVisibility => ActivePanel == CashForWorkWorkspacePanel.Payout ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AnnouncementsVisibility => ActivePanel == CashForWorkWorkspacePanel.Announcements ? Visibility.Visible : Visibility.Collapsed;
        public string SelectedEventLabel => SelectedEvent?.WorkspaceLabel ?? "No event selected";
        public string EventEditorKindLabel => _eventEditorKind == CashForWorkEventKind.Seminar ? "Seminar" : "Cash-for-Work";
        public string EventEditorSubmitLabel => _eventEditorKind == CashForWorkEventKind.Seminar
            ? "CREATE SEMINAR"
            : "CREATE EVENT";

        private void LoadEvents(int? preferredEventId = null)
        {
            var selectedEventId = preferredEventId ?? SelectedEvent?.Id;
            Events.Clear();

            foreach (var cashForWorkEvent in _cashForWorkService.GetEvents())
            {
                Events.Add(cashForWorkEvent);
            }

            SelectedEvent = selectedEventId.HasValue
                ? Events.FirstOrDefault(item => item.Id == selectedEventId.Value)
                : null;
        }

        private void LoadAyudaPrograms()
        {
            var selectedProgramId = SelectedAyudaProgram?.Id;
            AyudaPrograms.Clear();

            foreach (var program in _context.AyudaPrograms
                         .AsNoTracking()
                         .Where(item => item.IsActive)
                         .OrderBy(item => item.ProgramName))
            {
                AyudaPrograms.Add(program);
            }

            SelectedAyudaProgram = selectedProgramId.HasValue
                ? AyudaPrograms.FirstOrDefault(item => item.Id == selectedProgramId.Value)
                : null;
        }

        private void LoadEligibleBeneficiaries()
        {
            EligibleBeneficiaries.Clear();
            foreach (var beneficiary in _cashForWorkService.GetEligibleBeneficiaries())
            {
                EligibleBeneficiaries.Add(CashForWorkEligibleBeneficiaryOption.FromServiceModel(beneficiary));
            }
        }

        private void LoadAnnouncements()
        {
            OpenAnnouncements.Clear();

            foreach (var cashForWorkEvent in _cashForWorkService.GetOpenEvents())
            {
                OpenAnnouncements.Add(new CashForWorkAnnouncementItem
                {
                    EventId = cashForWorkEvent.Id,
                    Title = cashForWorkEvent.Title,
                    Kind = cashForWorkEvent.EventKind == CashForWorkEventKind.Seminar ? "Seminar" : "Cash-for-Work",
                    Location = cashForWorkEvent.Location,
                    DateLabel = cashForWorkEvent.EventDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                    Status = cashForWorkEvent.Status.ToString(),
                    Summary = cashForWorkEvent.WorkspaceAnnouncementLabel
                });
            }

            OnPropertyChanged(nameof(HasOpenAnnouncements));
        }

        private void LoadParticipants()
        {
            Participants.Clear();
            if (SelectedEvent == null)
            {
                AttendanceSummary = "Attendance records will appear after you select an event.";
                return;
            }

            foreach (var participant in _cashForWorkService.GetParticipants(SelectedEvent.Id))
            {
                Participants.Add(new CashForWorkParticipantListItem
                {
                    ParticipantId = participant.Id,
                    BeneficiaryStagingId = participant.BeneficiaryStagingId ?? 0,
                    FullName = BuildParticipantName(participant),
                    BeneficiaryId = NormalizeNullable(participant.Beneficiary?.BeneficiaryId) ?? "--",
                    CivilRegistryId = NormalizeNullable(participant.Beneficiary?.CivilRegistryId) ?? "--"
                });
            }
        }

        private void LoadSavedAttendance()
        {
            SavedAttendanceRows.Clear();
            if (SelectedEvent == null)
            {
                AttendanceSummary = "Attendance records will appear after you select an event.";
                return;
            }

            var attendanceRecords = _cashForWorkService.GetAttendanceRecords(SelectedEvent.Id);
            var presentParticipantIds = attendanceRecords
                .Where(record =>
                    record.AttendanceDate.Date == SelectedEvent.EventDate.Date &&
                    record.Status == CashForWorkAttendanceStatus.Present)
                .Select(record => record.ParticipantId)
                .ToHashSet();

            foreach (var participant in Participants)
            {
                participant.IsMarkedPresent = presentParticipantIds.Contains(participant.ParticipantId);
            }

            foreach (var record in attendanceRecords)
            {
                SavedAttendanceRows.Add(new CashForWorkSavedAttendanceRow
                {
                    AttendanceId = record.Id,
                    ParticipantId = record.ParticipantId,
                    AttendanceDate = record.AttendanceDate,
                    FullName = BuildParticipantName(record.Participant),
                    BeneficiaryId = NormalizeNullable(record.Participant.Beneficiary?.BeneficiaryId) ?? "--",
                    CivilRegistryId = NormalizeNullable(record.Participant.Beneficiary?.CivilRegistryId) ?? "--",
                    StatusValue = record.Status,
                    Status = record.Status.ToString(),
                    SourceValue = record.Source,
                    Source = record.Source.ToString(),
                    RecordedAt = record.RecordedAt
                });
            }

            AttendanceSummary = $"{SavedAttendanceRows.Count} attendance record(s) captured for {SelectedEvent.Title}.";
        }

        private void LoadSelectedProgram()
        {
            SelectedAyudaProgram = SelectedEvent?.AyudaProgramId.HasValue == true
                ? AyudaPrograms.FirstOrDefault(program => program.Id == SelectedEvent.AyudaProgramId.Value)
                : null;

            ReleaseAmountText = SelectedEvent?.ReleaseAmount?.ToString("N2", CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private void LoadReleaseSummary()
        {
            if (SelectedEvent == null)
            {
                ResetReleaseSummary();
                return;
            }

            var summary = _cashForWorkService.GetReleaseReadySummary(SelectedEvent.Id);
            ReleaseSummaryEventLabel = $"{summary.EventTitle} | {summary.EventDate:MMM dd, yyyy} | {summary.Location}";
            ApprovedParticipantCount = summary.ApprovedParticipantCount;
            PresentParticipantCount = summary.PresentParticipantCount;
            PendingParticipantCount = summary.PendingParticipantCount;
            ManualAttendanceCount = summary.ManualAttendanceCount;

            var presentation = BuildReleaseSummaryPresentation(SelectedEvent, summary);
            ReleaseSummaryStatusText = presentation.StatusText;
            ReleaseSummaryDetail = presentation.Detail;
            ReleaseSummaryStatusBrush = presentation.StatusBrush;
        }

        private void ResetReleaseSummary()
        {
            ReleaseSummaryEventLabel = "No event selected.";
            ReleaseSummaryStatusText = "Select an event to build a payout summary.";
            ReleaseSummaryDetail = "Release details appear here once an event is selected.";
            ReleaseSummaryStatusBrush = NeutralBrush;
            ApprovedParticipantCount = 0;
            PresentParticipantCount = 0;
            PendingParticipantCount = 0;
            ManualAttendanceCount = 0;
        }

        private async Task LoadHistorySnapshotAsync(int? eventId)
        {
            var requestVersion = ++_historyRequestVersion;
            if (!eventId.HasValue)
            {
                ClearHistorySnapshot();
                return;
            }

            try
            {
                var snapshot = await _reportsService.BuildCashForWorkAttendanceSheetSnapshotAsync(eventId.Value);
                if (requestVersion != _historyRequestVersion)
                {
                    return;
                }

                _historySnapshot = snapshot;
                ApplyHistorySnapshot(snapshot);
            }
            catch (Exception ex)
            {
                if (requestVersion != _historyRequestVersion)
                {
                    return;
                }

                ClearHistorySnapshot();
                SetErrorStatus($"Unable to load the printable attendance sheet: {ex.Message}");
            }
        }

        private void ApplyHistorySnapshot(ReportsSnapshot snapshot)
        {
            HistoryTitle = snapshot.Title;
            HistorySubtitle = snapshot.Subtitle;
            HistoryRangeSummary = snapshot.RangeLabel;
            HistoryProgramSummary = snapshot.ProgramLabel;
            HistoryLayoutSummary = $"Suggested layout: A4 {snapshot.SuggestedOrientation}";
            HistoryExportHint = string.Equals(snapshot.SuggestedOrientation, "Landscape", StringComparison.OrdinalIgnoreCase)
                ? "Use landscape when printing or choosing Microsoft Print to PDF."
                : "Portrait layout is suitable for printing or saving through Microsoft Print to PDF.";

            HistoryMetrics.Clear();
            foreach (var metric in snapshot.Metrics)
            {
                HistoryMetrics.Add(metric);
            }

            HistoryHighlights.Clear();
            foreach (var highlight in snapshot.Highlights)
            {
                HistoryHighlights.Add(highlight);
            }

            HistoryPreviewRows = snapshot.Table.DefaultView;
            _printAttendanceSheetCommand.RaiseCanExecuteChanged();
        }

        private void ClearHistorySnapshot()
        {
            _historySnapshot = null;
            HistoryTitle = "Attendance History";
            HistorySubtitle = "Select an event to load the printable attendance sheet.";
            HistoryRangeSummary = "--";
            HistoryProgramSummary = "--";
            HistoryLayoutSummary = "Suggested layout: A4 Portrait";
            HistoryExportHint = "Print and choose Microsoft Print to PDF when a PDF file is needed.";
            HistoryMetrics.Clear();
            HistoryHighlights.Clear();
            HistoryPreviewRows = null;
            _printAttendanceSheetCommand.RaiseCanExecuteChanged();
        }

        private Task RefreshWorkspaceAsync()
        {
            if (IsBusy)
            {
                return Task.CompletedTask;
            }

            IsBusy = true;
            SetNeutralStatus("Refreshing the cash-for-work workspace...");

            try
            {
                var selectedEventId = SelectedEvent?.Id;
                LoadAyudaPrograms();
                LoadEligibleBeneficiaries();
                LoadAnnouncements();
                LoadEvents(selectedEventId);
                SetSuccessStatus("Cash-for-work workspace refreshed.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to refresh the cash-for-work workspace: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }

            return Task.CompletedTask;
        }

        private void OpenEventEditorPanel(CashForWorkEventKind eventKind)
        {
            _eventEditorKind = eventKind;
            EventTitle = string.Empty;
            EventLocation = string.Empty;
            EventDate = DateTime.Today;
            EventStartTime = "07:00";
            EventEndTime = "12:00";
            EventNotes = string.Empty;

            OnPropertyChanged(nameof(EventEditorKindLabel));
            OnPropertyChanged(nameof(EventEditorSubmitLabel));

            var title = _eventEditorKind == CashForWorkEventKind.Seminar
                ? "Create Seminar"
                : "Create Event";
            var subtitle = _eventEditorKind == CashForWorkEventKind.Seminar
                ? "Create a seminar using the same attendance and payout workflow."
                : "Create a cash-for-work event before assigning beneficiaries.";

            OpenPanel(CashForWorkWorkspacePanel.EventEditor, title, subtitle);
        }

        private void OpenBeneficiariesPanel()
        {
            OpenPanel(
                CashForWorkWorkspacePanel.Beneficiaries,
                "Add Beneficiaries",
                HasSelectedEvent
                    ? $"Assign approved beneficiaries to {SelectedEvent!.Title}."
                    : "Select an event from the dropdown first.");
        }

        private void OpenScanAttendancePanel()
        {
            OpenPanel(
                CashForWorkWorkspacePanel.ScanAttendance,
                "Scan Attendance",
                HasSelectedEvent
                    ? $"Create a scanner session or save manual attendance for {SelectedEvent!.Title}."
                    : "Select an event from the dropdown first.");
        }

        private void OpenPayoutPanel()
        {
            OpenPanel(
                CashForWorkWorkspacePanel.Payout,
                "Release Budget",
                HasSelectedEvent
                    ? $"Review the release-ready summary and record the budget release for {SelectedEvent!.Title}."
                    : "Select an event from the dropdown first.");
        }

        private void OpenAnnouncementsPanel()
        {
            if (!HasOpenAnnouncements)
            {
                return;
            }

            OpenPanel(
                CashForWorkWorkspacePanel.Announcements,
                "Announcements",
                $"{OpenAnnouncements.Count:N0} ongoing cash-for-work event(s) are currently open.");
        }

        private void OpenPanel(CashForWorkWorkspacePanel panel, string title, string subtitle)
        {
            DrawerTitle = title;
            DrawerSubtitle = subtitle;
            ActivePanel = panel;
        }

        private void ClosePanel()
        {
            ActivePanel = CashForWorkWorkspacePanel.None;
            DrawerTitle = "Workspace";
            DrawerSubtitle = "Select an action from the left rail.";
        }

        private void RefreshDrawerCopy()
        {
            switch (ActivePanel)
            {
                case CashForWorkWorkspacePanel.Beneficiaries:
                    DrawerSubtitle = HasSelectedEvent
                        ? $"Assign approved beneficiaries to {SelectedEvent!.Title}."
                        : "Select an event from the dropdown first.";
                    break;
                case CashForWorkWorkspacePanel.ScanAttendance:
                    DrawerSubtitle = HasSelectedEvent
                        ? $"Create a scanner session or save manual attendance for {SelectedEvent!.Title}."
                        : "Select an event from the dropdown first.";
                    break;
                case CashForWorkWorkspacePanel.Payout:
                    DrawerSubtitle = HasSelectedEvent
                        ? $"Review the release-ready summary and record the budget release for {SelectedEvent!.Title}."
                        : "Select an event from the dropdown first.";
                    break;
                default:
                    break;
            }
        }

        private void ExecuteSaveEvent()
        {
            if (string.IsNullOrWhiteSpace(EventTitle) || string.IsNullOrWhiteSpace(EventLocation))
            {
                MessageBox.Show("Provide the event title and location first.", "Missing Event Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParse(EventStartTime, out var startTime) ||
                !TimeSpan.TryParse(EventEndTime, out var endTime))
            {
                MessageBox.Show("Use HH:mm format for start and end time.", "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var savedEvent = _cashForWorkService.CreateEvent(
                    EventTitle,
                    EventLocation,
                    EventDate.Date,
                    startTime,
                    endTime,
                    EventNotes,
                    _currentUser.Id,
                    _eventEditorKind);

                LoadAnnouncements();
                LoadEvents(savedEvent.Id);
                ClosePanel();
                SetSuccessStatus($"{(_eventEditorKind == CashForWorkEventKind.Seminar ? "Seminar" : "Event")} created successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Save Event", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
        }

        private void ExecuteAddParticipant()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedEligibleBeneficiary == null)
            {
                MessageBox.Show("Select an approved beneficiary to add.", "No Beneficiary Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _cashForWorkService.AddParticipant(SelectedEvent.Id, SelectedEligibleBeneficiary.BeneficiaryStagingId, _currentUser.Id);
                LoadParticipants();
                LoadSavedAttendance();
                _ = LoadHistorySnapshotAsync(SelectedEvent.Id);
                SetSuccessStatus($"Added {SelectedEligibleBeneficiary.FullName} to {SelectedEvent.Title}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Add Beneficiary", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
        }

        private void ExecuteSaveManualAttendance()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedParticipantIds = Participants
                .Where(participant => participant.IsMarkedPresent)
                .Select(participant => participant.ParticipantId)
                .ToList();

            if (selectedParticipantIds.Count == 0)
            {
                MessageBox.Show("Mark at least one beneficiary before saving manual attendance.", "No Attendance Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var savedCount = _cashForWorkService.SaveManualAttendance(SelectedEvent.Id, _currentUser.Id, selectedParticipantIds);
            LoadSavedAttendance();
            LoadReleaseSummary();
            _ = LoadHistorySnapshotAsync(SelectedEvent.Id);
            SetSuccessStatus($"Saved {savedCount} manual attendance record(s) for {SelectedEvent.Title}.");
        }

        private async Task ExecuteCreateAttendanceScannerSessionAsync()
        {
            var blockReason = GetAttendanceScannerSessionBlockReason(SelectedEvent, Participants.Count);
            if (!string.IsNullOrWhiteSpace(blockReason))
            {
                MessageBox.Show(blockReason, "Scanner Session Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(blockReason);
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Preparing the attendance scanner session...");

            try
            {
                await EnsureAttendanceScannerDigitalIdsReadyAsync();

                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                await using var context = new AppDbContext();
                var sessionService = new ScannerSessionService(context);
                var session = await sessionService.CreateAttendanceSessionAsync(SelectedEvent.Id, _currentUser.Id, TimeSpan.FromMinutes(15));
                var sessionUrl = $"{baseUrl}/scanner?session={Uri.EscapeDataString(session.SessionToken)}";

                AttendanceScannerSessionUrl = sessionUrl;
                AttendanceScannerSessionPin = session.Pin;
                AttendanceScannerSessionExpiresAtText = $"Expires {session.ExpiresAt:MMMM dd, yyyy hh:mm tt}";
                AttendanceScannerQrImage = QrCodeToolkitService.GenerateQrImage(sessionUrl, 8);

                var copiedToClipboard = TryCopyTextToClipboard(sessionUrl);
                var openedInBrowser = TryOpenUrl(sessionUrl);

                SetSuccessStatus("Attendance scanner session is ready.");
                MessageBox.Show(
                    BuildAttendanceScannerReadyMessage(sessionUrl, session.Pin, copiedToClipboard, openedInBrowser),
                    "Attendance Scanner Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Scanner Session Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                SetErrorStatus($"Unable to start the attendance scanner session: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteReleaseBudgetAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedAyudaProgram == null)
            {
                MessageBox.Show("Select an ayuda program before releasing funds.", "No Program Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseAmount(ReleaseAmountText, out var releaseAmount))
            {
                MessageBox.Show("Enter a valid release amount greater than zero.", "Invalid Release Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Releasing event budget...");

            try
            {
                var result = await _cashForWorkService.ReleaseEventAsync(
                    SelectedEvent.Id,
                    SelectedAyudaProgram.Id,
                    releaseAmount,
                    _currentUser.Id,
                    string.IsNullOrWhiteSpace(SelectedEvent.Notes) ? SelectedEvent.Title : SelectedEvent.Notes);

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Message, "Unable to Release Budget", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetErrorStatus(result.Message);
                    return;
                }

                LoadAnnouncements();
                LoadEvents(SelectedEvent.Id);
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Release Budget", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus($"Unable to release the budget: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void PrintAttendanceSheet()
        {
            if (_historySnapshot == null)
            {
                return;
            }

            var document = _documentService.BuildDocument(_historySnapshot, new ReportDocumentOptions
            {
                PreparedBy = string.IsNullOrWhiteSpace(_currentUser.Username) ? _currentUser.Email : _currentUser.Username,
                IncludeLogo = true
            });

            var previewWindow = new ReportPrintPreviewWindow(document, _historySnapshot.Title)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            };

            previewWindow.ShowDialog();
        }

        private void ResetScannerSession()
        {
            AttendanceScannerSessionUrl = string.Empty;
            AttendanceScannerSessionPin = string.Empty;
            AttendanceScannerSessionExpiresAtText = string.Empty;
            AttendanceScannerQrImage = null;
        }

        private async Task EnsureAttendanceScannerDigitalIdsReadyAsync()
        {
            var beneficiaryStagingIds = Participants
                .Select(participant => participant.BeneficiaryStagingId)
                .Where(stagingId => stagingId > 0)
                .Distinct()
                .ToList();

            if (beneficiaryStagingIds.Count == 0)
            {
                throw new InvalidOperationException("The selected event does not contain scanner-ready beneficiaries.");
            }

            await using var context = new AppDbContext();
            var digitalIdService = new BeneficiaryDigitalIdService(context);

            foreach (var stagingId in beneficiaryStagingIds)
            {
                await digitalIdService.EnsureIssuedAsync(stagingId, _currentUser.Id);
            }
        }

        private static string BuildAttendanceScannerReadyMessage(string sessionUrl, string pin, bool copiedToClipboard, bool openedInBrowser)
        {
            var clipboardLine = copiedToClipboard
                ? "The scanner URL was copied to your clipboard."
                : "Copy the scanner URL below for the staff phone.";
            var browserLine = openedInBrowser
                ? "The scanner page was also opened in your browser."
                : "Open the scanner URL in a browser on the same Wi-Fi network.";

            return string.Join(
                Environment.NewLine,
                [
                    "Attendance scanner session is ready.",
                    string.Empty,
                    $"PIN: {pin}",
                    $"URL: {sessionUrl}",
                    string.Empty,
                    clipboardLine,
                    browserLine
                ]);
        }

        private static bool TryCopyTextToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryOpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshSelectedEventFlags()
        {
            OnPropertyChanged(nameof(HasSelectedEvent));
            OnPropertyChanged(nameof(SelectedEventVisibility));
            OnPropertyChanged(nameof(NoSelectedEventVisibility));
            OnPropertyChanged(nameof(SelectedEventLabel));
        }

        private void RefreshDrawerVisibilityFlags()
        {
            OnPropertyChanged(nameof(IsDrawerOpen));
            OnPropertyChanged(nameof(DrawerVisibility));
            OnPropertyChanged(nameof(EventEditorVisibility));
            OnPropertyChanged(nameof(BeneficiariesVisibility));
            OnPropertyChanged(nameof(ScanAttendanceVisibility));
            OnPropertyChanged(nameof(PayoutVisibility));
            OnPropertyChanged(nameof(AnnouncementsVisibility));
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = NeutralBrush;
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

        private static bool TryParseAmount(string text, out decimal amount)
        {
            amount = 0m;

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                return amount > 0;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return amount > 0;
            }

            return false;
        }

        private static ReleaseSummaryPresentation BuildReleaseSummaryPresentation(CashForWorkEvent selectedEvent, CashForWorkReleaseReadySummary summary)
        {
            if (selectedEvent.BudgetLedgerEntryId.HasValue && selectedEvent.ReleaseAmount.HasValue)
            {
                return new ReleaseSummaryPresentation(
                    "Released",
                    $"{summary.ReleaseReadyParticipantCount} participant(s) were included in the payout. Total release amount: {selectedEvent.ReleaseAmount.Value:N2}.",
                    SuccessBrush);
            }

            if (summary.ReleaseReadyParticipantCount == 0)
            {
                return new ReleaseSummaryPresentation(
                    "No payout-ready attendance yet",
                    "Create a scanner session or save manual attendance before releasing the budget.",
                    NeutralBrush);
            }

            if (summary.PendingParticipantCount == 0)
            {
                return new ReleaseSummaryPresentation(
                    "Ready for payout",
                    $"{summary.ReleaseReadyParticipantCount} participant(s) are included in the release-ready summary.",
                    SuccessBrush);
            }

            return new ReleaseSummaryPresentation(
                "Partial payout ready",
                $"{summary.ReleaseReadyParticipantCount} participant(s) can be released now. {summary.PendingParticipantCount} assigned participant(s) still have no attendance record and will be excluded from the release until attendance is saved.",
                WarningBrush);
        }

        private static string? GetAttendanceScannerSessionBlockReason(CashForWorkEvent? cashForWorkEvent, int participantCount)
        {
            if (cashForWorkEvent == null)
            {
                return "Select an event before opening the phone scanner.";
            }

            if (cashForWorkEvent.BudgetLedgerEntryId.HasValue || cashForWorkEvent.Status == CashForWorkEventStatus.Completed)
            {
                return "Released cash-for-work events can no longer open attendance scanner sessions.";
            }

            if (cashForWorkEvent.EventDate.Date > DateTime.Today)
            {
                return "Attendance scanner sessions can only be opened on or after the event date.";
            }

            if (participantCount <= 0)
            {
                return "Add beneficiaries to the selected event before opening the phone scanner.";
            }

            return null;
        }

        private static string BuildParticipantName(CashForWorkParticipant participant)
        {
            if (participant.Beneficiary != null)
            {
                return BuildDisplayName(
                    participant.Beneficiary.FullName,
                    participant.Beneficiary.FirstName,
                    participant.Beneficiary.MiddleName,
                    participant.Beneficiary.LastName);
            }

            return $"Beneficiary #{participant.BeneficiaryStagingId?.ToString(CultureInfo.InvariantCulture) ?? "legacy"}";
        }

        private sealed record ReleaseSummaryPresentation(
            string StatusText,
            string Detail,
            Brush StatusBrush);

        private static string BuildDisplayName(string? fullName, string? firstName, string? middleName, string? lastName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Trim();
            }

            return string.Join(
                " ",
                new[] { firstName, middleName, lastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static SolidColorBrush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }
    }

    public enum CashForWorkWorkspacePanel
    {
        None,
        EventEditor,
        Beneficiaries,
        ScanAttendance,
        Payout,
        Announcements
    }

    public sealed class CashForWorkParticipantListItem : ObservableObject
    {
        private bool _isMarkedPresent;

        public int ParticipantId { get; set; }
        public int BeneficiaryStagingId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string BeneficiaryId { get; set; } = string.Empty;
        public string CivilRegistryId { get; set; } = string.Empty;

        public bool IsMarkedPresent
        {
            get => _isMarkedPresent;
            set => SetProperty(ref _isMarkedPresent, value);
        }
    }

    public sealed class CashForWorkSavedAttendanceRow
    {
        public int AttendanceId { get; set; }
        public int ParticipantId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string BeneficiaryId { get; set; } = string.Empty;
        public string CivilRegistryId { get; set; } = string.Empty;
        public CashForWorkAttendanceStatus StatusValue { get; set; }
        public string Status { get; set; } = string.Empty;
        public AttendanceCaptureSource SourceValue { get; set; }
        public string Source { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
    }

    public sealed class CashForWorkEligibleBeneficiaryOption
    {
        public int BeneficiaryStagingId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string BeneficiaryId { get; init; } = string.Empty;
        public string CivilRegistryId { get; init; } = string.Empty;

        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(BeneficiaryId))
                {
                    return $"{FullName} [{BeneficiaryId}]";
                }

                if (!string.IsNullOrWhiteSpace(CivilRegistryId))
                {
                    return $"{FullName} [CR: {CivilRegistryId}]";
                }

                return FullName;
            }
        }

        public static CashForWorkEligibleBeneficiaryOption FromServiceModel(CashForWorkEligibleBeneficiary beneficiary)
        {
            return new CashForWorkEligibleBeneficiaryOption
            {
                BeneficiaryStagingId = beneficiary.BeneficiaryStagingId,
                FullName = beneficiary.FullName,
                BeneficiaryId = beneficiary.BeneficiaryId ?? string.Empty,
                CivilRegistryId = beneficiary.CivilRegistryId ?? string.Empty
            };
        }
    }

    public sealed class CashForWorkAnnouncementItem
    {
        public int EventId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string DateLabel { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
    }
}

using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
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
        private readonly ReportsService _reportsService;
        private readonly ReportDocumentService _documentService;
        private readonly ReportPdfExportService _pdfExportService;
        private readonly RelayCommand _saveEventCommand;
        private readonly RelayCommand _addParticipantCommand;
        private readonly RelayCommand _saveManualAttendanceCommand;
        private readonly RelayCommand _releaseBudgetCommand;
        private readonly RelayCommand _createAttendanceScannerSessionCommand;
        private readonly RelayCommand _openCreateEventPanelCommand;
        private readonly RelayCommand _openEditEventPanelCommand;
        private readonly RelayCommand _deleteEventCommand;
        private readonly RelayCommand _openAddBeneficiariesPanelCommand;
        private readonly RelayCommand _openScanAttendancePanelCommand;
        private readonly RelayCommand _openPayoutPanelCommand;
        private readonly RelayCommand _openAnnouncementsPanelCommand;
        private readonly RelayCommand _editAttendanceCommand;
        private readonly RelayCommand _deleteAttendanceCommand;
        private readonly RelayCommand _selectAnnouncementEventCommand;
        private readonly RelayCommand _closePanelCommand;
        private readonly RelayCommand _refreshWorkspaceCommand;
        private readonly RelayCommand _saveAttendanceSheetPdfCommand;
        private readonly RelayCommand _printAttendanceSheetCommand;
        private readonly RelayCommand _previousAttendancePageCommand;
        private readonly RelayCommand _nextAttendancePageCommand;
        private readonly RelayCommand _previousParticipantPageCommand;
        private readonly RelayCommand _nextParticipantPageCommand;
        private readonly RelayCommand _navigatePreviousCommand;
        private readonly RelayCommand _navigateNextCommand;

        private CashForWorkEvent? _selectedEvent;
        private CashForWorkSavedAttendanceRow? _selectedAttendanceRow;
        private CashForWorkEligibleBeneficiaryOption? _selectedEligibleBeneficiary;
        private CashForWorkWorkspacePanel _activePanel;
        private CashForWorkEventKind _eventEditorKind = CashForWorkEventKind.CashForWork;
        private int? _editingEventId;
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
        
        private int _attendancePageSize = 20;
        private int _attendanceCurrentPage = 1;
        private int _attendanceTotalItems;
        private List<CashForWorkSavedAttendanceRow> _allSavedAttendanceRows = new();

        private int _participantPageSize = 20;
        private int _participantCurrentPage = 1;
        private int _participantTotalItems;
        private List<CashForWorkParticipantListItem> _allParticipants = new();

        private string _releaseAmountText = string.Empty;
        private string _releaseSummaryEventLabel = "No event selected.";
        private string _releaseSummaryStatusText = "Select an event to build a payout summary.";
        private string _releaseSummaryDetail = "Release details appear here once an event is selected.";
        private Brush _releaseSummaryStatusBrush = NeutralBrush;
        private int _approvedParticipantCount;
        private int _presentParticipantCount;
        private int _pendingParticipantCount;
        private int _manualAttendanceCount;
        private decimal _globalCfwBudgetCapRemaining;
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

        private string _eventSearchText = string.Empty;
        private ICollectionView? _eventsView;
        private int _currentIndex = -1;

        public CashForWorkOcrViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _reportsService = new ReportsService();
            _documentService = new ReportDocumentService();
            _pdfExportService = new ReportPdfExportService();

            Events = new ObservableCollection<CashForWorkEvent>();
            EligibleBeneficiaries = new ObservableCollection<CashForWorkEligibleBeneficiaryOption>();
            Participants = new ObservableCollection<CashForWorkParticipantListItem>();
            SavedAttendanceRows = new ObservableCollection<CashForWorkSavedAttendanceRow>();
            OpenAnnouncements = new ObservableCollection<CashForWorkAnnouncementItem>();
            HistoryMetrics = new ObservableCollection<ReportsMetricItem>();
            HistoryHighlights = new ObservableCollection<string>();

            _saveEventCommand = new RelayCommand(async _ => await ExecuteSaveEventAsync(), _ => !IsBusy);
            _addParticipantCommand = new RelayCommand(async _ => await ExecuteAddParticipantAsync(), _ => !IsBusy);
            _saveManualAttendanceCommand = new RelayCommand(async _ => await ExecuteSaveManualAttendanceAsync(), _ => !IsBusy);
            _releaseBudgetCommand = new RelayCommand(async _ => await ExecuteReleaseBudgetAsync(), _ => !IsBusy);
            _createAttendanceScannerSessionCommand = new RelayCommand(async _ => await ExecuteCreateAttendanceScannerSessionAsync(), _ => !IsBusy);
            _openCreateEventPanelCommand = new RelayCommand(_ => OpenEventEditorPanel(CashForWorkEventKind.CashForWork), _ => !IsBusy);
            _openEditEventPanelCommand = new RelayCommand(_ => OpenEditEventPanel(), _ => !IsBusy && HasSelectedEvent);
            _deleteEventCommand = new RelayCommand(async _ => await ExecuteDeleteEventAsync(), _ => !IsBusy && HasSelectedEvent);
            _openAddBeneficiariesPanelCommand = new RelayCommand(_ => OpenBeneficiariesPanel(), _ => !IsBusy);
            _openScanAttendancePanelCommand = new RelayCommand(_ => OpenScanAttendancePanel(), _ => !IsBusy);
            _openPayoutPanelCommand = new RelayCommand(_ => OpenPayoutPanel(), _ => !IsBusy);
            _openAnnouncementsPanelCommand = new RelayCommand(_ => OpenAnnouncementsPanel(), _ => !IsBusy);
            _editAttendanceCommand = new RelayCommand(async _ => await ExecuteEditAttendanceAsync(), _ => !IsBusy && SelectedAttendanceRow != null);
            _deleteAttendanceCommand = new RelayCommand(async _ => await ExecuteDeleteAttendanceAsync(), _ => !IsBusy && SelectedAttendanceRow != null);
            _selectAnnouncementEventCommand = new RelayCommand(async parameter => await ExecuteSelectAnnouncementEventAsync(parameter), _ => !IsBusy);
            _closePanelCommand = new RelayCommand(_ => ClosePanel());
            _refreshWorkspaceCommand = new RelayCommand(async _ => await RefreshWorkspaceAsync(), _ => !IsBusy);
            _saveAttendanceSheetPdfCommand = new RelayCommand(_ => SaveAttendanceSheetPdf(), _ => !IsBusy && _historySnapshot != null);
            _printAttendanceSheetCommand = new RelayCommand(_ => PrintAttendanceSheet(), _ => !IsBusy && _historySnapshot != null);
            _previousAttendancePageCommand = new RelayCommand(_ => { AttendanceCurrentPage--; ApplyAttendancePagination(); }, _ => AttendanceCurrentPage > 1);
            _nextAttendancePageCommand = new RelayCommand(_ => { AttendanceCurrentPage++; ApplyAttendancePagination(); }, _ => AttendanceCurrentPage < AttendanceTotalPages);
            _previousParticipantPageCommand = new RelayCommand(_ => { ParticipantCurrentPage--; ApplyParticipantPagination(); }, _ => ParticipantCurrentPage > 1);
            _nextParticipantPageCommand = new RelayCommand(_ => { ParticipantCurrentPage++; ApplyParticipantPagination(); }, _ => ParticipantCurrentPage < ParticipantTotalPages);
            _navigatePreviousCommand = new RelayCommand(_ => NavigatePrevious(), _ => _currentIndex > 0);
            _navigateNextCommand = new RelayCommand(_ => NavigateNext(), _ => _currentIndex >= 0 && _currentIndex < Events.Count - 1);

            _eventsView = CollectionViewSource.GetDefaultView(Events);
            _eventsView.Filter = FilterEvents;

            _ = LoadWorkspaceAsync();
            ClearHistorySnapshot();
        }

        public ObservableCollection<CashForWorkEvent> Events { get; }
        public ObservableCollection<CashForWorkEligibleBeneficiaryOption> EligibleBeneficiaries { get; }
        public ObservableCollection<CashForWorkParticipantListItem> Participants { get; }
        public ObservableCollection<CashForWorkSavedAttendanceRow> SavedAttendanceRows { get; }
        public ObservableCollection<CashForWorkAnnouncementItem> OpenAnnouncements { get; }
        public ObservableCollection<ReportsMetricItem> HistoryMetrics { get; }
        public ObservableCollection<string> HistoryHighlights { get; }

        public ICommand SaveEventCommand => _saveEventCommand;
        public ICommand AddParticipantCommand => _addParticipantCommand;
        public ICommand SaveManualAttendanceCommand => _saveManualAttendanceCommand;
        public ICommand ReleaseBudgetCommand => _releaseBudgetCommand;
        public ICommand CreateAttendanceScannerSessionCommand => _createAttendanceScannerSessionCommand;
        public ICommand OpenCreateEventPanelCommand => _openCreateEventPanelCommand;
        public ICommand OpenEditEventPanelCommand => _openEditEventPanelCommand;
        public ICommand DeleteEventCommand => _deleteEventCommand;
        public ICommand OpenAddBeneficiariesPanelCommand => _openAddBeneficiariesPanelCommand;
        public ICommand OpenScanAttendancePanelCommand => _openScanAttendancePanelCommand;
        public ICommand OpenPayoutPanelCommand => _openPayoutPanelCommand;
        public ICommand OpenAnnouncementsPanelCommand => _openAnnouncementsPanelCommand;
        public ICommand EditAttendanceCommand => _editAttendanceCommand;
        public ICommand DeleteAttendanceCommand => _deleteAttendanceCommand;
        public ICommand SelectAnnouncementEventCommand => _selectAnnouncementEventCommand;
        public ICommand ClosePanelCommand => _closePanelCommand;
        public ICommand RefreshWorkspaceCommand => _refreshWorkspaceCommand;
        public ICommand SaveAttendanceSheetPdfCommand => _saveAttendanceSheetPdfCommand;
        public ICommand PrintAttendanceSheetCommand => _printAttendanceSheetCommand;
        public ICommand PreviousAttendancePageCommand => _previousAttendancePageCommand;
        public ICommand NextAttendancePageCommand => _nextAttendancePageCommand;
        public ICommand PreviousParticipantPageCommand => _previousParticipantPageCommand;
        public ICommand NextParticipantPageCommand => _nextParticipantPageCommand;
        public ICommand NavigatePreviousCommand => _navigatePreviousCommand;
        public ICommand NavigateNextCommand => _navigateNextCommand;

        public string EventSearchText
        {
            get => _eventSearchText;
            set
            {
                if (SetProperty(ref _eventSearchText, value))
                {
                    _eventsView?.Refresh();
                }
            }
        }

        public ICollectionView? EventsView => _eventsView;

        public string CurrentPosition
        {
            get
            {
                if (SelectedEvent == null || Events.Count == 0) return "0 / 0";
                return $"{_currentIndex + 1} / {Events.Count}";
            }
        }

        public int AttendancePageSize
        {
            get => _attendancePageSize;
            set
            {
                if (SetProperty(ref _attendancePageSize, value))
                {
                    AttendanceCurrentPage = 1;
                    ApplyAttendancePagination();
                }
            }
        }

        public int AttendanceCurrentPage
        {
            get => _attendanceCurrentPage;
            set
            {
                if (SetProperty(ref _attendanceCurrentPage, value))
                {
                    _previousAttendancePageCommand.RaiseCanExecuteChanged();
                    _nextAttendancePageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int AttendanceTotalItems
        {
            get => _attendanceTotalItems;
            private set
            {
                if (SetProperty(ref _attendanceTotalItems, value))
                {
                    OnPropertyChanged(nameof(AttendanceTotalPages));
                }
            }
        }

        public int AttendanceTotalPages => (int)Math.Ceiling((double)AttendanceTotalItems / AttendancePageSize);

        public string AttendancePageSummary
        {
            get
            {
                if (AttendanceTotalItems == 0) return "No records";
                var start = ((AttendanceCurrentPage - 1) * AttendancePageSize) + 1;
                var end = Math.Min(AttendanceCurrentPage * AttendancePageSize, AttendanceTotalItems);
                return $"Showing {start}-{end} of {AttendanceTotalItems}";
            }
        }

        public string AttendancePageIndicator => $"Page {AttendanceCurrentPage} of {Math.Max(1, AttendanceTotalPages)}";

        public int ParticipantPageSize
        {
            get => _participantPageSize;
            set
            {
                if (SetProperty(ref _participantPageSize, value))
                {
                    ParticipantCurrentPage = 1;
                    ApplyParticipantPagination();
                }
            }
        }

        public int ParticipantCurrentPage
        {
            get => _participantCurrentPage;
            set
            {
                if (SetProperty(ref _participantCurrentPage, value))
                {
                    _previousParticipantPageCommand.RaiseCanExecuteChanged();
                    _nextParticipantPageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int ParticipantTotalItems
        {
            get => _participantTotalItems;
            private set
            {
                if (SetProperty(ref _participantTotalItems, value))
                {
                    OnPropertyChanged(nameof(ParticipantTotalPages));
                }
            }
        }

        public int ParticipantTotalPages => (int)Math.Ceiling((double)ParticipantTotalItems / ParticipantPageSize);

        public string ParticipantPageSummary
        {
            get
            {
                if (ParticipantTotalItems == 0) return "No records";
                var start = ((ParticipantCurrentPage - 1) * ParticipantPageSize) + 1;
                var end = Math.Min(ParticipantCurrentPage * ParticipantPageSize, ParticipantTotalItems);
                return $"Showing {start}-{end} of {ParticipantTotalItems}";
            }
        }

        public string ParticipantPageIndicator => $"Page {ParticipantCurrentPage} of {Math.Max(1, ParticipantTotalPages)}";

        public CashForWorkEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (!SetProperty(ref _selectedEvent, value))
                {
                    return;
                }

                _currentIndex = value == null ? -1 : Events.IndexOf(value);
                OnPropertyChanged(nameof(CurrentPosition));
                _navigatePreviousCommand.RaiseCanExecuteChanged();
                _navigateNextCommand.RaiseCanExecuteChanged();

                SelectedAttendanceRow = null;

                // Only trigger details load if we are not already in a bulk loading operation
                if (!IsBusy)
                {
                    _ = LoadEventDetailsAsync(value?.Id);
                }

                ResetScannerSession();
                RefreshSelectedEventFlags();
                NormalizePanelAccessForSelectedEvent();
                RefreshDrawerCopy();
                _openEditEventPanelCommand.RaiseCanExecuteChanged();
                _deleteEventCommand.RaiseCanExecuteChanged();

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

        private void NavigatePrevious()
        {
            if (_currentIndex > 0)
            {
                SelectedEvent = Events[_currentIndex - 1];
            }
        }

        private void NavigateNext()
        {
            if (_currentIndex < Events.Count - 1)
            {
                SelectedEvent = Events[_currentIndex + 1];
            }
        }

        private bool FilterEvents(object obj)
        {
            if (obj is not CashForWorkEvent ev) return false;
            if (string.IsNullOrWhiteSpace(EventSearchText)) return true;

            return ev.Title.Contains(EventSearchText, StringComparison.OrdinalIgnoreCase) ||
                   ev.Location.Contains(EventSearchText, StringComparison.OrdinalIgnoreCase) ||
                   ev.EventDate.ToString("MMM dd, yyyy").Contains(EventSearchText, StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadEventDetailsAsync(int? eventId)
        {
            if (!eventId.HasValue)
            {
                Participants.Clear();
                SavedAttendanceRows.Clear();
                ResetReleaseSummary();
                ClearHistorySnapshot();
                return;
            }

            try
            {
                await LoadParticipantsAsync();
                await LoadSavedAttendanceAsync();
                await LoadReleaseSummaryAsync();
                await LoadHistorySnapshotAsync(eventId.Value);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Error loading event details: {ex.Message}");
            }
        }

        public decimal GlobalCfwBudgetCapRemaining
        {
            get => _globalCfwBudgetCapRemaining;
            private set => SetProperty(ref _globalCfwBudgetCapRemaining, value);
        }

        public CashForWorkSavedAttendanceRow? SelectedAttendanceRow
        {
            get => _selectedAttendanceRow;
            set
            {
                if (!SetProperty(ref _selectedAttendanceRow, value))
                {
                    return;
                }

                _editAttendanceCommand.RaiseCanExecuteChanged();
                _deleteAttendanceCommand.RaiseCanExecuteChanged();
            }
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

        public IEnumerable<CashForWorkEventKind> EventKinds => Enum.GetValues(typeof(CashForWorkEventKind)).Cast<CashForWorkEventKind>();

        public CashForWorkEventKind EventEditorKind
        {
            get => _eventEditorKind;
            set
            {
                if (SetProperty(ref _eventEditorKind, value))
                {
                    OnPropertyChanged(nameof(EventEditorKindLabel));
                    OnPropertyChanged(nameof(EventEditorSubmitLabel));
                    
                    if (!_editingEventId.HasValue)
                    {
                        var title = _eventEditorKind == CashForWorkEventKind.Seminar
                            ? "Create Seminar"
                            : "Create Event";
                        var subtitle = _eventEditorKind == CashForWorkEventKind.Seminar
                            ? "Create a seminar using the same attendance and payout workflow."
                            : "Create a cash-for-work event before assigning beneficiaries.";
                        
                        DrawerTitle = title;
                        DrawerSubtitle = subtitle;
                    }
                }
            }
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
                _openEditEventPanelCommand.RaiseCanExecuteChanged();
                _deleteEventCommand.RaiseCanExecuteChanged();
                _openAddBeneficiariesPanelCommand.RaiseCanExecuteChanged();
                _openScanAttendancePanelCommand.RaiseCanExecuteChanged();
                _openPayoutPanelCommand.RaiseCanExecuteChanged();
                _openAnnouncementsPanelCommand.RaiseCanExecuteChanged();
                _editAttendanceCommand.RaiseCanExecuteChanged();
                _deleteAttendanceCommand.RaiseCanExecuteChanged();
                _selectAnnouncementEventCommand.RaiseCanExecuteChanged();
                _refreshWorkspaceCommand.RaiseCanExecuteChanged();
                _saveAttendanceSheetPdfCommand.RaiseCanExecuteChanged();
                _printAttendanceSheetCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasSelectedEvent => SelectedEvent != null;
        public bool IsSelectedSeminarEvent => SelectedEvent?.EventKind == CashForWorkEventKind.Seminar;
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
        public Visibility AddBeneficiariesRailVisibility => IsSelectedSeminarEvent ? Visibility.Collapsed : Visibility.Visible;
        public Visibility PayoutRailVisibility => IsSelectedSeminarEvent ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ManualAttendanceVisibility => IsSelectedSeminarEvent ? Visibility.Collapsed : Visibility.Visible;
        public Visibility SeminarScannerHintVisibility => IsSelectedSeminarEvent ? Visibility.Visible : Visibility.Collapsed;
        public string SelectedEventLabel => SelectedEvent?.WorkspaceLabel ?? "No event selected";
        public string EventEditorKindLabel => _eventEditorKind == CashForWorkEventKind.Seminar ? "Seminar" : "Cash-for-Work";
        public string EventEditorSubmitLabel => _editingEventId.HasValue
            ? _eventEditorKind == CashForWorkEventKind.Seminar
                ? "UPDATE SEMINAR"
                : "UPDATE EVENT"
            : _eventEditorKind == CashForWorkEventKind.Seminar
                ? "CREATE SEMINAR"
                : "CREATE EVENT";

        private async Task LoadWorkspaceAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            SetNeutralStatus("Loading cash-for-work workspace...");

            try
            {
                await LoadGlobalCfwBudgetCapAsync();
                await LoadEligibleBeneficiariesAsync();
                await LoadAnnouncementsAsync();
                await LoadEventsAsync();
                SetSuccessStatus("Workspace loaded.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Error loading workspace: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadGlobalCfwBudgetCapAsync()
        {
            try
            {
                await using var context = new AppDbContext();
                var budgetService = new BudgetManagementService(context);
                var globalBudget = await budgetService.GetGlobalCashForWorkBudgetAsync();
                GlobalCfwBudgetCapRemaining = globalBudget?.BudgetCap ?? 0m;
            }
            catch
            {
                GlobalCfwBudgetCapRemaining = 0m;
            }
        }

        private async Task LoadEventsAsync(int? preferredEventId = null)
        {
            var selectedEventId = preferredEventId ?? SelectedEvent?.Id;
            Events.Clear();

            await using var context = new AppDbContext();
            var cfwService = new CashForWorkService(context);
            foreach (var cashForWorkEvent in cfwService.GetEvents())
            {
                Events.Add(cashForWorkEvent);
            }

            SelectedEvent = selectedEventId.HasValue
                ? Events.FirstOrDefault(item => item.Id == selectedEventId.Value)
                : null;

            _currentIndex = SelectedEvent == null ? -1 : Events.IndexOf(SelectedEvent);
            OnPropertyChanged(nameof(CurrentPosition));
            _navigatePreviousCommand.RaiseCanExecuteChanged();
            _navigateNextCommand.RaiseCanExecuteChanged();
        }

        private async Task LoadEligibleBeneficiariesAsync()
        {
            EligibleBeneficiaries.Clear();
            await using var context = new AppDbContext();
            var cfwService = new CashForWorkService(context);
            foreach (var beneficiary in cfwService.GetEligibleBeneficiaries())
            {
                EligibleBeneficiaries.Add(CashForWorkEligibleBeneficiaryOption.FromServiceModel(beneficiary));
            }
        }

        private async Task LoadAnnouncementsAsync()
        {
            OpenAnnouncements.Clear();
            await using var context = new AppDbContext();
            var cfwService = new CashForWorkService(context);

            foreach (var cashForWorkEvent in cfwService.GetOpenEvents())
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

            if (ActivePanel == CashForWorkWorkspacePanel.Announcements)
            {
                RefreshDrawerCopy();
            }
        }

        private async Task LoadParticipantsAsync()
        {
            _allParticipants.Clear();
            Participants.Clear();
            if (SelectedEvent == null)
            {
                AttendanceSummary = "Attendance records will appear after you select an event.";
                return;
            }

            await using var context = new AppDbContext();
            var cfwService = new CashForWorkService(context);
            foreach (var participant in cfwService.GetParticipants(SelectedEvent.Id))
            {
                _allParticipants.Add(new CashForWorkParticipantListItem
                {
                    ParticipantId = participant.Id,
                    BeneficiaryStagingId = participant.BeneficiaryStagingId ?? 0,
                    FullName = BuildParticipantName(participant),
                    BeneficiaryId = NormalizeNullable(participant.Beneficiary?.BeneficiaryId) ?? "--",
                    CivilRegistryId = NormalizeNullable(participant.Beneficiary?.CivilRegistryId) ?? "--"
                });
            }

            ParticipantTotalItems = _allParticipants.Count;
            ParticipantCurrentPage = 1;
            ApplyParticipantPagination();
        }

        private async Task LoadSavedAttendanceAsync(int? preferredAttendanceId = null)
        {
            var selectedAttendanceId = preferredAttendanceId ?? SelectedAttendanceRow?.AttendanceId;
            _allSavedAttendanceRows.Clear();
            SavedAttendanceRows.Clear();
            if (SelectedEvent == null)
            {
                SelectedAttendanceRow = null;
                AttendanceSummary = "Attendance records will appear after you select an event.";
                return;
            }

            await using var context = new AppDbContext();
            var cfwService = new CashForWorkService(context);
            var attendanceRecords = cfwService.GetAttendanceRecords(SelectedEvent.Id);
            var presentParticipantIds = attendanceRecords
                .Where(record =>
                    record.AttendanceDate.Date == SelectedEvent.EventDate.Date &&
                    record.Status == CashForWorkAttendanceStatus.Present)
                .Select(record => record.ParticipantId)
                .ToHashSet();

            foreach (var participant in _allParticipants)
            {
                participant.IsMarkedPresent = presentParticipantIds.Contains(participant.ParticipantId);
            }

            foreach (var record in attendanceRecords)
            {
                _allSavedAttendanceRows.Add(new CashForWorkSavedAttendanceRow
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

            AttendanceTotalItems = _allSavedAttendanceRows.Count;
            AttendanceCurrentPage = 1;
            ApplyAttendancePagination();

            SelectedAttendanceRow = selectedAttendanceId.HasValue
                ? _allSavedAttendanceRows.FirstOrDefault(row => row.AttendanceId == selectedAttendanceId.Value)
                : null;
            AttendanceSummary = $"{_allSavedAttendanceRows.Count} attendance record(s) captured for {SelectedEvent.Title}.";
        }

        private void ApplyAttendancePagination()
        {
            SavedAttendanceRows.Clear();
            var paged = _allSavedAttendanceRows
                .Skip((AttendanceCurrentPage - 1) * AttendancePageSize)
                .Take(AttendancePageSize);

            foreach (var row in paged)
            {
                SavedAttendanceRows.Add(row);
            }

            OnPropertyChanged(nameof(AttendancePageSummary));
            OnPropertyChanged(nameof(AttendancePageIndicator));
            _previousAttendancePageCommand.RaiseCanExecuteChanged();
            _nextAttendancePageCommand.RaiseCanExecuteChanged();
        }

        private void ApplyParticipantPagination()
        {
            Participants.Clear();
            var paged = _allParticipants
                .Skip((ParticipantCurrentPage - 1) * ParticipantPageSize)
                .Take(ParticipantPageSize);

            foreach (var row in paged)
            {
                Participants.Add(row);
            }

            OnPropertyChanged(nameof(ParticipantPageSummary));
            OnPropertyChanged(nameof(ParticipantPageIndicator));
            _previousParticipantPageCommand.RaiseCanExecuteChanged();
            _nextParticipantPageCommand.RaiseCanExecuteChanged();
        }

        private async Task LoadReleaseSummaryAsync()
        {
            if (SelectedEvent == null)
            {
                ResetReleaseSummary();
                return;
            }

            await using var context = new AppDbContext();
            var cfwService = new CashForWorkService(context);
            var summary = cfwService.GetReleaseReadySummary(SelectedEvent.Id);
            ReleaseSummaryEventLabel = $"{summary.EventTitle} | {summary.EventDate:MMM dd, yyyy} | {summary.Location}";
            ApprovedParticipantCount = summary.ApprovedParticipantCount;
            PresentParticipantCount = summary.PresentParticipantCount;
            PendingParticipantCount = summary.PendingParticipantCount;
            ManualAttendanceCount = summary.ManualAttendanceCount;

            var presentation = BuildReleaseSummaryPresentation(SelectedEvent, summary);
            ReleaseSummaryStatusText = presentation.StatusText;
            ReleaseSummaryDetail = presentation.Detail;
            ReleaseSummaryStatusBrush = presentation.StatusBrush;

            ReleaseAmountText = SelectedEvent.ReleaseAmount?.ToString("N2", CultureInfo.CurrentCulture) 
                ?? (summary.ProposedAmount > 0 ? summary.ProposedAmount.ToString("N2", CultureInfo.CurrentCulture) : string.Empty);
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
            ReleaseAmountText = string.Empty;
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
                ? "Save PDF creates a landscape attendance sheet. Print Preview stays available for paper copies."
                : "Save PDF creates a portrait attendance sheet. Print Preview stays available for paper copies.";

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
            _saveAttendanceSheetPdfCommand.RaiseCanExecuteChanged();
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
            HistoryExportHint = "Use Save PDF to write an attendance sheet file, or Print Preview for paper output.";
            HistoryMetrics.Clear();
            HistoryHighlights.Clear();
            HistoryPreviewRows = null;
            _saveAttendanceSheetPdfCommand.RaiseCanExecuteChanged();
            _printAttendanceSheetCommand.RaiseCanExecuteChanged();
        }

        private async Task RefreshWorkspaceAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Refreshing the cash-for-work workspace...");

            try
            {
                var selectedEventId = SelectedEvent?.Id;
                await LoadGlobalCfwBudgetCapAsync();
                await LoadEligibleBeneficiariesAsync();
                await LoadAnnouncementsAsync();
                await LoadEventsAsync(selectedEventId);
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
        }

        private void OpenEventEditorPanel(CashForWorkEventKind eventKind)
        {
            _editingEventId = null;
            EventEditorKind = eventKind;
            EventTitle = string.Empty;
            EventLocation = string.Empty;
            EventDate = DateTime.Today;
            EventStartTime = "07:00";
            EventEndTime = "12:00";
            EventNotes = string.Empty;

            OnPropertyChanged(nameof(EventEditorKindLabel));
            OnPropertyChanged(nameof(EventEditorSubmitLabel));

            var title = eventKind == CashForWorkEventKind.Seminar ? "New Seminar" : "New Event";
            var subtitle = eventKind == CashForWorkEventKind.Seminar ? "Create a new seminar." : "Create a new cash-for-work event.";

            OpenPanel(CashForWorkWorkspacePanel.EventEditor, title, subtitle);
        }

        private void OpenEditEventPanel()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingEventId = SelectedEvent.Id;
            EventEditorKind = SelectedEvent.EventKind;
            EventTitle = SelectedEvent.Title;
            EventLocation = SelectedEvent.Location;
            EventDate = SelectedEvent.EventDate.Date;
            EventStartTime = SelectedEvent.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            EventEndTime = SelectedEvent.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            EventNotes = SelectedEvent.Notes ?? string.Empty;

            OnPropertyChanged(nameof(EventEditorKindLabel));
            OnPropertyChanged(nameof(EventEditorSubmitLabel));

            var title = _eventEditorKind == CashForWorkEventKind.Seminar
                ? "Edit Seminar"
                : "Edit Event";
            var subtitle = _eventEditorKind == CashForWorkEventKind.Seminar
                ? $"Update the seminar details for {SelectedEvent.Title}."
                : $"Update the cash-for-work event details for {SelectedEvent.Title}.";

            OpenPanel(CashForWorkWorkspacePanel.EventEditor, title, subtitle);
        }

        private void OpenBeneficiariesPanel()
        {
            if (IsSelectedSeminarEvent)
            {
                OpenScanAttendancePanel();
                SetNeutralStatus("Seminar attendance is scan-based. Beneficiary assignment is not used for seminar events.");
                return;
            }

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
                    ? IsSelectedSeminarEvent
                        ? $"Open scanner attendance for {SelectedEvent!.Title}. Seminar attendees register as they scan."
                        : $"Create a scanner session or save manual attendance for {SelectedEvent!.Title}."
                    : "Select an event from the dropdown first.");
        }

        private void OpenPayoutPanel()
        {
            if (IsSelectedSeminarEvent)
            {
                OpenScanAttendancePanel();
                SetNeutralStatus("Seminar events are attendance-only and do not use the payout workflow.");
                return;
            }

            OpenPanel(
                CashForWorkWorkspacePanel.Payout,
                "Release Budget",
                HasSelectedEvent
                    ? $"Review the release-ready summary and record the budget release for {SelectedEvent!.Title}."
                    : "Select an event from the dropdown first.");
        }

        private void OpenAnnouncementsPanel()
        {
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
            _editingEventId = null;
            OnPropertyChanged(nameof(EventEditorSubmitLabel));
            ActivePanel = CashForWorkWorkspacePanel.None;
            DrawerTitle = "Workspace";
            DrawerSubtitle = "Select an action from the left rail.";
        }

        private void RefreshDrawerCopy()
        {
            switch (ActivePanel)
            {
                case CashForWorkWorkspacePanel.EventEditor:
                    if (_editingEventId.HasValue && HasSelectedEvent)
                    {
                        DrawerTitle = SelectedEvent!.EventKind == CashForWorkEventKind.Seminar ? "Edit Seminar" : "Edit Event";
                        DrawerSubtitle = SelectedEvent.EventKind == CashForWorkEventKind.Seminar
                            ? $"Update the seminar details for {SelectedEvent.Title}."
                            : $"Update the cash-for-work event details for {SelectedEvent.Title}.";
                    }
                    else
                    {
                        DrawerTitle = _eventEditorKind == CashForWorkEventKind.Seminar ? "Create Seminar" : "Create Event";
                        DrawerSubtitle = _eventEditorKind == CashForWorkEventKind.Seminar
                            ? "Create a seminar using the same attendance and payout workflow."
                            : "Create a cash-for-work event before assigning beneficiaries.";
                    }
                    break;
                case CashForWorkWorkspacePanel.Beneficiaries:
                    DrawerSubtitle = IsSelectedSeminarEvent
                        ? "Seminar attendance is open scan-based and does not use beneficiary assignment."
                        : HasSelectedEvent
                        ? $"Assign approved beneficiaries to {SelectedEvent!.Title}."
                        : "Select an event from the dropdown first.";
                    break;
                case CashForWorkWorkspacePanel.ScanAttendance:
                    DrawerSubtitle = HasSelectedEvent
                        ? IsSelectedSeminarEvent
                            ? $"Open scanner attendance for {SelectedEvent!.Title}. Seminar attendees register as they scan."
                            : $"Create a scanner session or save manual attendance for {SelectedEvent!.Title}."
                        : "Select an event from the dropdown first.";
                    break;
                case CashForWorkWorkspacePanel.Payout:
                    DrawerSubtitle = IsSelectedSeminarEvent
                        ? "Seminar events are attendance-only."
                        : HasSelectedEvent
                        ? $"Review the release-ready summary and record the budget release for {SelectedEvent!.Title}."
                        : "Select an event from the dropdown first.";
                    break;
                case CashForWorkWorkspacePanel.Announcements:
                    DrawerSubtitle = $"{OpenAnnouncements.Count:N0} ongoing cash-for-work event(s) are currently open.";
                    break;
                default:
                    break;
            }
        }

        private async Task ExecuteSaveEventAsync()
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

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                var isEditing = _editingEventId.HasValue;
                CashForWorkEvent savedEvent;
                if (_editingEventId is int eventIdToUpdate)
                {
                    savedEvent = await cfwService.UpdateEventAsync(
                        eventIdToUpdate,
                        EventTitle,
                        EventLocation,
                        EventDate.Date,
                        startTime,
                        endTime,
                        EventNotes,
                        _currentUser.Id);
                }
                else
                {
                    savedEvent = await cfwService.CreateEventAsync(
                        EventTitle,
                        EventLocation,
                        EventDate.Date,
                        startTime,
                        endTime,
                        EventNotes,
                        _currentUser.Id,
                        _eventEditorKind);
                }

                await LoadAnnouncementsAsync();
                await LoadEventsAsync(savedEvent.Id);
                ClosePanel();
                SetSuccessStatus(
                    isEditing
                        ? $"{(_eventEditorKind == CashForWorkEventKind.Seminar ? "Seminar" : "Event")} updated successfully."
                        : $"{(_eventEditorKind == CashForWorkEventKind.Seminar ? "Seminar" : "Event")} created successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Save Event", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteDeleteEventAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var deletedEvent = SelectedEvent;
            var confirmDelete = MessageBox.Show(
                $"Delete the event '{deletedEvent.Title}' and all of its attendance records?",
                "Delete Event",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmDelete != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                cfwService.DeleteEvent(deletedEvent.Id, _currentUser.Id);
                await LoadAnnouncementsAsync();
                await LoadEventsAsync();
                ClosePanel();
                SetSuccessStatus($"Deleted event: {deletedEvent.Title}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Delete Event", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteAddParticipantAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                MessageBox.Show("Seminar attendance is scan-based and does not use beneficiary assignment.", "Seminar Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedEligibleBeneficiary == null)
            {
                MessageBox.Show("Select an approved beneficiary to add.", "No Beneficiary Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                cfwService.AddParticipant(SelectedEvent.Id, SelectedEligibleBeneficiary.BeneficiaryStagingId, _currentUser.Id);
                await LoadParticipantsAsync();
                await LoadSavedAttendanceAsync();
                await LoadHistorySnapshotAsync(SelectedEvent.Id);
                SetSuccessStatus($"Added {SelectedEligibleBeneficiary.FullName} to {SelectedEvent.Title}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Add Beneficiary", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteSaveManualAttendanceAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                MessageBox.Show("Seminar attendance is scan-based only. Use the scanner session to record attendance.", "Seminar Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
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

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                var savedCount = cfwService.SaveManualAttendance(SelectedEvent.Id, _currentUser.Id, selectedParticipantIds);
                await LoadSavedAttendanceAsync();
                await LoadReleaseSummaryAsync();
                await LoadHistorySnapshotAsync(SelectedEvent.Id);
                SetSuccessStatus($"Saved {savedCount} manual attendance record(s) for {SelectedEvent.Title}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Save Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteEditAttendanceAsync()
        {
            if (SelectedAttendanceRow == null)
            {
                MessageBox.Show("Select an attendance record first.", "No Attendance Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedAttendance = SelectedAttendanceRow;
            var dialog = new CashForWorkAttendanceWindow(
                selectedAttendance.FullName,
                selectedAttendance.BeneficiaryId,
                selectedAttendance.CivilRegistryId,
                selectedAttendance.AttendanceDate,
                selectedAttendance.StatusValue,
                selectedAttendance.SourceValue)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                var updatedAttendance = cfwService.UpdateAttendance(
                    selectedAttendance.AttendanceId,
                    dialog.AttendanceDate.Date,
                    dialog.Status,
                    dialog.Source,
                    _currentUser.Id);

                await LoadSavedAttendanceAsync(updatedAttendance.Id);
                await LoadReleaseSummaryAsync();
                await LoadHistorySnapshotAsync(SelectedEvent?.Id);
                SetSuccessStatus($"Updated attendance for {selectedAttendance.FullName}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Update Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteDeleteAttendanceAsync()
        {
            if (SelectedAttendanceRow == null)
            {
                MessageBox.Show("Select an attendance record first.", "No Attendance Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedAttendance = SelectedAttendanceRow;
            var confirmDelete = MessageBox.Show(
                $"Delete the attendance record for '{selectedAttendance.FullName}'?",
                "Delete Attendance",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmDelete != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                cfwService.DeleteAttendance(selectedAttendance.AttendanceId, _currentUser.Id);
                await LoadSavedAttendanceAsync();
                await LoadReleaseSummaryAsync();
                await LoadHistorySnapshotAsync(SelectedEvent?.Id);
                SetSuccessStatus($"Deleted attendance for {selectedAttendance.FullName}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Delete Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteSelectAnnouncementEventAsync(object? parameter)
        {
            var eventId = parameter switch
            {
                int directId => directId,
                string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) => parsedId,
                _ => 0
            };

            if (eventId <= 0)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var selectedEvent = Events.FirstOrDefault(item => item.Id == eventId);
                if (selectedEvent == null)
                {
                    await LoadEventsAsync(eventId);
                    selectedEvent = SelectedEvent;
                }
                else
                {
                    SelectedEvent = selectedEvent;
                }

                if (selectedEvent == null)
                {
                    SetErrorStatus("The selected announcement event could not be loaded.");
                    return;
                }

                ClosePanel();
                SetSuccessStatus($"Loaded {selectedEvent.Title} from announcements.");
            }
            finally
            {
                IsBusy = false;
            }
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
                var selectedEvent = SelectedEvent
                    ?? throw new InvalidOperationException("Select an event before opening the phone scanner.");
                var session = await sessionService.CreateAttendanceSessionAsync(selectedEvent.Id, _currentUser.Id, TimeSpan.FromMinutes(15));
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

            if (SelectedEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                MessageBox.Show("Seminar events are attendance-only and do not use the payout workflow.", "Seminar Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
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
                await using var context = new AppDbContext();
                var cfwService = new CashForWorkService(context);
                var result = await cfwService.ReleaseEventAsync(
                    SelectedEvent.Id,
                    releaseAmount,
                    _currentUser.Id,
                    string.IsNullOrWhiteSpace(SelectedEvent.Notes) ? SelectedEvent.Title : SelectedEvent.Notes);

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Message, "Unable to Release Budget", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadAnnouncementsAsync();
                await LoadEventsAsync(SelectedEvent.Id);
                await LoadGlobalCfwBudgetCapAsync();
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

        private void SaveAttendanceSheetPdf()
        {
            if (_historySnapshot == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"{_historySnapshot.ExportFilePrefix}-{DateTime.Now:yyyyMMdd-HHmm}.pdf"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _pdfExportService.Save(_historySnapshot, dialog.FileName, new ReportPdfExportOptions
                {
                    PreparedBy = string.IsNullOrWhiteSpace(_currentUser.Username) ? _currentUser.Email : _currentUser.Username
                });

                SetSuccessStatus($"Saved attendance sheet PDF to {Path.GetFileName(dialog.FileName)}.");
            }
            catch (IOException)
            {
                SetErrorStatus($"Unable to save {Path.GetFileName(dialog.FileName)} because it is open in another application. Close the PDF viewer and try again.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to save the attendance sheet PDF: {ex.Message}");
            }
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
            if (IsSelectedSeminarEvent)
            {
                return;
            }

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
            OnPropertyChanged(nameof(IsSelectedSeminarEvent));
            OnPropertyChanged(nameof(SelectedEventVisibility));
            OnPropertyChanged(nameof(NoSelectedEventVisibility));
            OnPropertyChanged(nameof(SelectedEventLabel));
            OnPropertyChanged(nameof(AddBeneficiariesRailVisibility));
            OnPropertyChanged(nameof(PayoutRailVisibility));
            OnPropertyChanged(nameof(ManualAttendanceVisibility));
            OnPropertyChanged(nameof(SeminarScannerHintVisibility));
        }

        private void NormalizePanelAccessForSelectedEvent()
        {
            if (!IsSelectedSeminarEvent)
            {
                return;
            }

            if (ActivePanel is CashForWorkWorkspacePanel.Beneficiaries or CashForWorkWorkspacePanel.Payout)
            {
                ActivePanel = CashForWorkWorkspacePanel.ScanAttendance;
            }
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
            if (selectedEvent.EventKind == CashForWorkEventKind.Seminar)
            {
                return new ReleaseSummaryPresentation(
                    "Attendance only",
                    $"{summary.ReleaseReadyParticipantCount} seminar attendee(s) already have attendance records for this event.",
                    NeutralBrush);
            }

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

            if (cashForWorkEvent.EventKind != CashForWorkEventKind.Seminar && participantCount <= 0)
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

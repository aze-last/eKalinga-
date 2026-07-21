using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
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
    public sealed class SeminarAttendanceViewModel : ObservableObject
    {
        private static readonly Brush NeutralBrush = CreateBrush("#64748B");
        private static readonly Brush SuccessBrush = CreateBrush("#15803D");
        private static readonly Brush ErrorBrush = CreateBrush("#BE123C");

        private readonly User _currentUser;
        private readonly ReportsService _reportsService;
        private readonly ReportDocumentService _documentService;
        private readonly ReportPdfExportService _pdfExportService;
        private readonly RelayCommand _saveSeminarCommand;
        private readonly RelayCommand _createAttendanceScannerSessionCommand;
        private readonly RelayCommand _openCreateSeminarPanelCommand;
        private readonly RelayCommand _openEditSeminarPanelCommand;
        private readonly RelayCommand _deleteSeminarCommand;
        private readonly RelayCommand _openScanAttendancePanelCommand;
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
        private readonly RelayCommand _navigatePreviousCommand;
        private readonly RelayCommand _navigateNextCommand;
        private readonly RelayCommand _openPcScannerCommand;
        private readonly RelayCommand _processPcScanCommand;
        private readonly RelayCommand _confirmScannedClaimCommand;
        private readonly RelayCommand _cancelScannedClaimCommand;
        private readonly RelayCommand _toggleSidebarCommand;

        private CashForWorkEvent? _selectedEvent;
        private CashForWorkSavedAttendanceRow? _selectedAttendanceRow;
        private SeminarWorkspacePanel _activePanel;
        private int? _editingEventId;
        private bool _isBusy;
        private int _historyRequestVersion;
        private string _drawerTitle = "Workspace";
        private string _drawerSubtitle = "Select an action from the left rail.";
        private string _eventTitle = string.Empty;
        private string _eventLocation = string.Empty;
        private string _eventNotes = string.Empty;
        private DateTime _eventDate = DateTime.Today;
        private DateTime? _eventStartTime = DateTime.Today.AddHours(7);
        private DateTime? _eventEndTime = DateTime.Today.AddHours(12);
        private DateTime? _finishDate = DateTime.Today;
        private int? _selectedBudgetId;
        private CashForWorkBenefitType _selectedBenefitType = CashForWorkBenefitType.None;
        private string _unitAmountText = string.Empty;
        private string _benefitDescriptionText = string.Empty;
        private string _statusMessage = "Select a seminar from the dropdown or open the Create Seminar panel.";
        private Brush _statusBrush = NeutralBrush;
        private string _attendanceSummary = "Attendance records will appear after you select a seminar.";

        private int _attendancePageSize = 20;
        private int _attendanceCurrentPage = 1;
        private int _attendanceTotalItems;
        private List<CashForWorkSavedAttendanceRow> _allSavedAttendanceRows = new();

        private string _attendanceScannerSessionUrl = string.Empty;
        private string _attendanceScannerSessionPin = string.Empty;
        private string _attendanceScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _attendanceScannerQrImage;
        private string _historyTitle = "Attendance History";
        private string _historySubtitle = "Select a seminar to load the printable attendance sheet.";
        private string _historyRangeSummary = "--";
        private string _historyProgramSummary = "--";
        private string _historyLayoutSummary = "Suggested layout: A4 Portrait";
        private string _historyExportHint = "Print and choose Microsoft Print to PDF when a PDF file is needed.";
        private DataView? _historyPreviewRows;
        private ReportsSnapshot? _historySnapshot;

        private bool _isPcScannerOpen;
        private bool _isSidebarCollapsed;
        private GridLength _sidebarWidth = new GridLength(320);
        private CashForWorkParticipantListItem? _scannedBeneficiary;
        private string? _scannedBeneficiaryStatus;
        private BitmapSource? _scannedBeneficiaryPhoto;
        private bool _isScannedResultVisible;
        private string? _lastScannedPayload;

        private string _eventSearchText = string.Empty;
        private ICollectionView? _eventsView;
        private int _currentIndex = -1;

        public SeminarAttendanceViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _reportsService = new ReportsService();
            _documentService = new ReportDocumentService();
            _pdfExportService = new ReportPdfExportService();

            Events = new ObservableCollection<CashForWorkEvent>();
            SavedAttendanceRows = new ObservableCollection<CashForWorkSavedAttendanceRow>();
            OpenAnnouncements = new ObservableCollection<CashForWorkAnnouncementItem>();
            HistoryMetrics = new ObservableCollection<ReportsMetricItem>();
            HistoryHighlights = new ObservableCollection<string>();

            _saveSeminarCommand = new RelayCommand(async _ => await ExecuteSaveSeminarAsync(), _ => !IsBusy);
            _createAttendanceScannerSessionCommand = new RelayCommand(async _ => await ExecuteCreateAttendanceScannerSessionAsync(), _ => !IsBusy);
            _openCreateSeminarPanelCommand = new RelayCommand(_ => OpenCreateSeminarPanel(), _ => !IsBusy);
            _openEditSeminarPanelCommand = new RelayCommand(_ => OpenEditSeminarPanel(), _ => !IsBusy && HasSelectedEvent);
            _deleteSeminarCommand = new RelayCommand(async _ => await ExecuteDeleteSeminarAsync(), _ => !IsBusy && HasSelectedEvent);
            _openScanAttendancePanelCommand = new RelayCommand(_ => OpenScanAttendancePanel(), _ => !IsBusy);
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
            _navigatePreviousCommand = new RelayCommand(_ => NavigatePrevious(), _ => _currentIndex > 0);
            _navigateNextCommand = new RelayCommand(_ => NavigateNext(), _ => _currentIndex >= 0 && _currentIndex < Events.Count - 1);
            _openPcScannerCommand = new RelayCommand(_ => IsPcScannerOpen = true, _ => !IsBusy && HasSelectedEvent);
            _processPcScanCommand = new RelayCommand(payload => _ = ExecuteProcessPcScan(payload as string));
            _toggleSidebarCommand = new RelayCommand(_ => ToggleSidebar());
            _confirmScannedClaimCommand = new RelayCommand(async _ => await ExecuteConfirmScannedAttendanceAsync(), _ => !IsBusy && ScannedBeneficiary != null);
            _cancelScannedClaimCommand = new RelayCommand(_ => ResetScannedResult());

            _eventsView = CollectionViewSource.GetDefaultView(Events);
            _eventsView.Filter = FilterEvents;

            _ = LoadWorkspaceAsync();
            ClearHistorySnapshot();
        }

        public ObservableCollection<CashForWorkEvent> Events { get; }
        public ObservableCollection<CashForWorkSavedAttendanceRow> SavedAttendanceRows { get; }
        public ObservableCollection<CashForWorkAnnouncementItem> OpenAnnouncements { get; }
        public ObservableCollection<ReportsMetricItem> HistoryMetrics { get; }
        public ObservableCollection<string> HistoryHighlights { get; }

        public ICommand SaveSeminarCommand => _saveSeminarCommand;
        public ICommand CreateAttendanceScannerSessionCommand => _createAttendanceScannerSessionCommand;
        public ICommand OpenCreateSeminarPanelCommand => _openCreateSeminarPanelCommand;
        public ICommand OpenEditSeminarPanelCommand => _openEditSeminarPanelCommand;
        public ICommand DeleteSeminarCommand => _deleteSeminarCommand;
        public ICommand OpenScanAttendancePanelCommand => _openScanAttendancePanelCommand;
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
        public ICommand NavigatePreviousCommand => _navigatePreviousCommand;
        public ICommand NavigateNextCommand => _navigateNextCommand;
        public ICommand OpenPcScannerCommand => _openPcScannerCommand;
        public ICommand ProcessPcScanCommand => _processPcScanCommand;
        public ICommand ToggleSidebarCommand => _toggleSidebarCommand;
        public ICommand ConfirmScannedClaimCommand => _confirmScannedClaimCommand;
        public ICommand CancelScannedClaimCommand => _cancelScannedClaimCommand;

        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    SidebarWidth = value ? new GridLength(0) : new GridLength(320);
                }
            }
        }

        public GridLength SidebarWidth
        {
            get => _sidebarWidth;
            private set => SetProperty(ref _sidebarWidth, value);
        }

        public ObservableCollection<CashForWorkBudget> AvailableBudgets { get; } = new();

        public int? SelectedBudgetId
        {
            get => _selectedBudgetId;
            set => SetProperty(ref _selectedBudgetId, value);
        }

        public CashForWorkBenefitType SelectedBenefitType
        {
            get => _selectedBenefitType;
            set
            {
                if (SetProperty(ref _selectedBenefitType, value))
                {
                    OnPropertyChanged(nameof(IsBenefitEarmarked));
                    OnPropertyChanged(nameof(IsCashBenefit));
                    OnPropertyChanged(nameof(IsGoodsBenefit));
                }
            }
        }

        public bool IsBenefitEarmarked => SelectedBenefitType != CashForWorkBenefitType.None;
        public bool IsCashBenefit => SelectedBenefitType == CashForWorkBenefitType.Cash;
        public bool IsGoodsBenefit => SelectedBenefitType == CashForWorkBenefitType.Goods;

        public string UnitAmountText
        {
            get => _unitAmountText;
            set => SetProperty(ref _unitAmountText, value);
        }

        public string BenefitDescriptionText
        {
            get => _benefitDescriptionText;
            set => SetProperty(ref _benefitDescriptionText, value);
        }

        public System.Collections.IEnumerable BenefitTypes => System.Enum.GetValues(typeof(CashForWorkBenefitType));

        private void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

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
                _openPcScannerCommand.RaiseCanExecuteChanged();

                SelectedAttendanceRow = null;

                // Only trigger details load if we are not already in a bulk loading operation
                if (!IsBusy)
                {
                    _ = LoadEventDetailsAsync(value?.Id);
                }

                ResetScannerSession();
                RefreshSelectedEventFlags();
                RefreshDrawerCopy();
                _openEditSeminarPanelCommand.RaiseCanExecuteChanged();
                _deleteSeminarCommand.RaiseCanExecuteChanged();

                if (value == null)
                {
                    SetNeutralStatus("Select a seminar from the dropdown or open the Create Seminar panel.");
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
                SavedAttendanceRows.Clear();
                ClearHistorySnapshot();
                return;
            }

            try
            {
                await LoadSavedAttendanceAsync();
                await LoadHistorySnapshotAsync(eventId.Value);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Error loading seminar details: {ex.Message}");
            }
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

        public SeminarWorkspacePanel ActivePanel
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
            set
            {
                var oldDate = _eventDate;
                if (SetProperty(ref _eventDate, value))
                {
                    // If FinishDate was the same as the old EventDate or null, update it to the new one
                    if (!FinishDate.HasValue || FinishDate.Value.Date == oldDate.Date)
                    {
                        FinishDate = value;
                    }
                }
            }
        }

        public DateTime? EventStartTime
        {
            get => _eventStartTime;
            set => SetProperty(ref _eventStartTime, value);
        }

        public DateTime? EventEndTime
        {
            get => _eventEndTime;
            set => SetProperty(ref _eventEndTime, value);
        }

        public DateTime? FinishDate
        {
            get => _finishDate;
            set => SetProperty(ref _finishDate, value);
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

                _saveSeminarCommand.RaiseCanExecuteChanged();
                _createAttendanceScannerSessionCommand.RaiseCanExecuteChanged();
                _openCreateSeminarPanelCommand.RaiseCanExecuteChanged();
                _openEditSeminarPanelCommand.RaiseCanExecuteChanged();
                _deleteSeminarCommand.RaiseCanExecuteChanged();
                _openScanAttendancePanelCommand.RaiseCanExecuteChanged();
                _openAnnouncementsPanelCommand.RaiseCanExecuteChanged();
                _editAttendanceCommand.RaiseCanExecuteChanged();
                _deleteAttendanceCommand.RaiseCanExecuteChanged();
                _selectAnnouncementEventCommand.RaiseCanExecuteChanged();
                _refreshWorkspaceCommand.RaiseCanExecuteChanged();
                _saveAttendanceSheetPdfCommand.RaiseCanExecuteChanged();
                _printAttendanceSheetCommand.RaiseCanExecuteChanged();
                _openPcScannerCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasSelectedEvent => SelectedEvent != null;
        public bool HasOpenAnnouncements => OpenAnnouncements.Count > 0;
        public Visibility SelectedEventVisibility => HasSelectedEvent ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NoSelectedEventVisibility => HasSelectedEvent ? Visibility.Collapsed : Visibility.Visible;
        public bool IsDrawerOpen => ActivePanel != SeminarWorkspacePanel.None;
        public bool IsPcScannerOpen
        {
            get => _isPcScannerOpen;
            set
            {
                if (SetProperty(ref _isPcScannerOpen, value))
                {
                    OnPropertyChanged(nameof(IsAnyOverlayOpen));
                }
            }
        }
        public bool IsAnyOverlayOpen => IsDrawerOpen || IsPcScannerOpen;
        public Visibility DrawerVisibility => IsDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SeminarEditorVisibility => ActivePanel == SeminarWorkspacePanel.SeminarEditor ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ScanAttendanceVisibility => ActivePanel == SeminarWorkspacePanel.ScanAttendance ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AnnouncementsVisibility => ActivePanel == SeminarWorkspacePanel.Announcements ? Visibility.Visible : Visibility.Collapsed;
        public string SelectedEventLabel => SelectedEvent?.WorkspaceLabel ?? "No seminar selected";

        public string SeminarEditorSubmitLabel => _editingEventId.HasValue ? "UPDATE SEMINAR" : "CREATE SEMINAR";

        public CashForWorkParticipantListItem? ScannedBeneficiary
        {
            get => _scannedBeneficiary;
            private set
            {
                if (SetProperty(ref _scannedBeneficiary, value))
                {
                    _confirmScannedClaimCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? ScannedBeneficiaryStatus
        {
            get => _scannedBeneficiaryStatus;
            private set => SetProperty(ref _scannedBeneficiaryStatus, value);
        }

        public BitmapSource? ScannedBeneficiaryPhoto
        {
            get => _scannedBeneficiaryPhoto;
            private set => SetProperty(ref _scannedBeneficiaryPhoto, value);
        }

        public bool IsScannedResultVisible
        {
            get => _isScannedResultVisible;
            private set => SetProperty(ref _isScannedResultVisible, value);
        }

        public string ScannerHeader => "Seminar Attendance Capture";
        public string ScannerDescription => "Scan ID to register the attendee for this seminar.";

        private async Task LoadWorkspaceAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            SetNeutralStatus("Loading seminar attendance workspace...");

            try
            {
                await LoadBudgetsAsync();
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

        private async Task LoadBudgetsAsync()
        {
            AvailableBudgets.Clear();
            await using var context = new LocalDbContext();
            var budgetService = new BudgetManagementService(context);
            var budgets = await budgetService.GetCashForWorkBudgetsAsync();
            foreach (var budget in budgets.Where(b => b.BudgetCode != "GLOBAL_CFW_BUDGET"))
            {
                AvailableBudgets.Add(budget);
            }
        }

        private async Task LoadEventsAsync(int? preferredEventId = null)
        {
            var selectedEventId = preferredEventId ?? SelectedEvent?.Id;
            Events.Clear();

            await using var context = new LocalDbContext();
            var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
            foreach (var seminarEvent in cfwService.GetEvents(CashForWorkEventKind.Seminar))
            {
                Events.Add(seminarEvent);
            }

            SelectedEvent = selectedEventId.HasValue
                ? Events.FirstOrDefault(item => item.Id == selectedEventId.Value)
                : null;

            _currentIndex = SelectedEvent == null ? -1 : Events.IndexOf(SelectedEvent);
            OnPropertyChanged(nameof(CurrentPosition));
            _navigatePreviousCommand.RaiseCanExecuteChanged();
            _navigateNextCommand.RaiseCanExecuteChanged();
        }

        private async Task LoadAnnouncementsAsync()
        {
            OpenAnnouncements.Clear();
            await using var context = new LocalDbContext();
            var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());

            foreach (var seminarEvent in cfwService.GetOpenEvents(CashForWorkEventKind.Seminar))
            {
                OpenAnnouncements.Add(new CashForWorkAnnouncementItem
                {
                    EventId = seminarEvent.Id,
                    Title = seminarEvent.Title,
                    Kind = "Seminar",
                    Location = seminarEvent.Location,
                    DateLabel = seminarEvent.EventDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                    Status = seminarEvent.Status.ToString(),
                    Summary = seminarEvent.WorkspaceAnnouncementLabel
                });
            }

            OnPropertyChanged(nameof(HasOpenAnnouncements));

            if (ActivePanel == SeminarWorkspacePanel.Announcements)
            {
                RefreshDrawerCopy();
            }
        }

        private async Task LoadSavedAttendanceAsync(int? preferredAttendanceId = null)
        {
            var selectedAttendanceId = preferredAttendanceId ?? SelectedAttendanceRow?.AttendanceId;
            _allSavedAttendanceRows.Clear();
            SavedAttendanceRows.Clear();
            if (SelectedEvent == null)
            {
                SelectedAttendanceRow = null;
                AttendanceSummary = "Attendance records will appear after you select a seminar.";
                return;
            }

            await using var context = new LocalDbContext();
            var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
            var attendanceRecords = cfwService.GetAttendanceRecords(SelectedEvent.Id);

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
            HistorySubtitle = "Select a seminar to load the printable attendance sheet.";
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
            SetNeutralStatus("Refreshing the seminar attendance workspace...");

            try
            {
                var selectedEventId = SelectedEvent?.Id;
                await LoadAnnouncementsAsync();
                await LoadEventsAsync(selectedEventId);
                SetSuccessStatus("Seminar attendance workspace refreshed.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to refresh the seminar attendance workspace: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenCreateSeminarPanel()
        {
            _editingEventId = null;
            EventTitle = string.Empty;
            EventLocation = string.Empty;
            EventDate = DateTime.Today;
            FinishDate = DateTime.Today;
            EventStartTime = DateTime.Today.AddHours(7);
            EventEndTime = DateTime.Today.AddHours(12);
            EventNotes = string.Empty;
            SelectedBudgetId = null;
            SelectedBenefitType = CashForWorkBenefitType.None;
            UnitAmountText = string.Empty;
            BenefitDescriptionText = string.Empty;

            OnPropertyChanged(nameof(SeminarEditorSubmitLabel));

            OpenPanel(SeminarWorkspacePanel.SeminarEditor, "New Seminar", "Create a new seminar. Attendees register themselves as they scan.");
        }

        private void OpenEditSeminarPanel()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select a seminar first.", "No Seminar Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingEventId = SelectedEvent.Id;
            EventTitle = SelectedEvent.Title;
            EventLocation = SelectedEvent.Location;
            EventDate = SelectedEvent.EventDate.Date;
            FinishDate = SelectedEvent.FinishDate?.Date ?? SelectedEvent.EventDate.Date;
            EventStartTime = DateTime.Today.Date.Add(SelectedEvent.StartTime);
            EventEndTime = DateTime.Today.Date.Add(SelectedEvent.EndTime);
            EventNotes = SelectedEvent.Notes ?? string.Empty;
            SelectedBudgetId = SelectedEvent.CashForWorkBudgetId;
            SelectedBenefitType = SelectedEvent.BenefitType;
            UnitAmountText = SelectedEvent.UnitAmount.ToString("F2");
            BenefitDescriptionText = SelectedEvent.BenefitDescription ?? string.Empty;

            OnPropertyChanged(nameof(SeminarEditorSubmitLabel));

            OpenPanel(SeminarWorkspacePanel.SeminarEditor, "Edit Seminar", $"Update the seminar details for {SelectedEvent.Title}.");
        }

        private void OpenScanAttendancePanel()
        {
            OpenPanel(
                SeminarWorkspacePanel.ScanAttendance,
                "Scan Attendance",
                HasSelectedEvent
                    ? $"Open scanner attendance for {SelectedEvent!.Title}. Seminar attendees register as they scan."
                    : "Select a seminar from the dropdown first.");
        }

        private void OpenAnnouncementsPanel()
        {
            OpenPanel(
                SeminarWorkspacePanel.Announcements,
                "Announcements",
                $"{OpenAnnouncements.Count:N0} ongoing seminar(s) are currently open.");
        }

        private void OpenPanel(SeminarWorkspacePanel panel, string title, string subtitle)
        {
            DrawerTitle = title;
            DrawerSubtitle = subtitle;
            ActivePanel = panel;
        }

        private void ClosePanel()
        {
            _editingEventId = null;
            OnPropertyChanged(nameof(SeminarEditorSubmitLabel));
            ActivePanel = SeminarWorkspacePanel.None;
            DrawerTitle = "Workspace";
            DrawerSubtitle = "Select an action from the left rail.";
        }

        private void RefreshDrawerCopy()
        {
            switch (ActivePanel)
            {
                case SeminarWorkspacePanel.SeminarEditor:
                    if (_editingEventId.HasValue && HasSelectedEvent)
                    {
                        DrawerTitle = "Edit Seminar";
                        DrawerSubtitle = $"Update the seminar details for {SelectedEvent!.Title}.";
                    }
                    else
                    {
                        DrawerTitle = "Create Seminar";
                        DrawerSubtitle = "Create a new seminar. Attendees register themselves as they scan.";
                    }
                    break;
                case SeminarWorkspacePanel.ScanAttendance:
                    DrawerSubtitle = HasSelectedEvent
                        ? $"Open scanner attendance for {SelectedEvent!.Title}. Seminar attendees register as they scan."
                        : "Select a seminar from the dropdown first.";
                    break;
                case SeminarWorkspacePanel.Announcements:
                    DrawerSubtitle = $"{OpenAnnouncements.Count:N0} ongoing seminar(s) are currently open.";
                    break;
                default:
                    break;
            }
        }

        private async Task ExecuteSaveSeminarAsync()
        {
            if (string.IsNullOrWhiteSpace(EventTitle) || string.IsNullOrWhiteSpace(EventLocation))
            {
                MessageBox.Show("Provide the seminar title and location first.", "Missing Seminar Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!EventStartTime.HasValue || !EventEndTime.HasValue)
            {
                MessageBox.Show("Provide both start and end times.", "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal unitAmount = 0m;
            if (SelectedBenefitType == CashForWorkBenefitType.Cash)
            {
                if (!decimal.TryParse(UnitAmountText, out var parsedAmt) || parsedAmt <= 0)
                {
                    MessageBox.Show("Provide a valid unit amount for the Cash benefit.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                unitAmount = parsedAmt;
            }

            if (SelectedBenefitType != CashForWorkBenefitType.None && !SelectedBudgetId.HasValue)
            {
                MessageBox.Show("Please select an earmarked budget to pull funds from.", "Missing Budget", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            try
            {
                await using var context = new LocalDbContext();
                var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var isEditing = _editingEventId.HasValue;

                var startTime = EventStartTime.Value.TimeOfDay;
                var endTime = EventEndTime.Value.TimeOfDay;

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
                        _currentUser.Id,
                        unitAmount,
                        CashForWorkEventKind.Seminar,
                        FinishDate?.Date,
                        SelectedBenefitType,
                        SelectedBenefitType == CashForWorkBenefitType.Goods ? BenefitDescriptionText : null,
                        SelectedBudgetId);
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
                        SelectedBudgetId,
                        unitAmount,
                        CashForWorkEventKind.Seminar,
                        FinishDate?.Date,
                        SelectedBenefitType,
                        SelectedBenefitType == CashForWorkBenefitType.Goods ? BenefitDescriptionText : null);
                }

                await LoadAnnouncementsAsync();
                await LoadEventsAsync(savedEvent.Id);
                ClosePanel();
                SetSuccessStatus(isEditing ? "Seminar updated successfully." : "Seminar created successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Save Seminar", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetErrorStatus(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteDeleteSeminarAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select a seminar first.", "No Seminar Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var deletedEvent = SelectedEvent;
            var confirmDelete = MessageBox.Show(
                $"Delete the seminar '{deletedEvent.Title}' and all of its attendance records?",
                "Delete Seminar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmDelete != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await using var context = new LocalDbContext();
                var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                cfwService.DeleteEvent(deletedEvent.Id, _currentUser.Id);
                await LoadAnnouncementsAsync();
                await LoadEventsAsync();
                ClosePanel();
                SetSuccessStatus($"Deleted seminar: {deletedEvent.Title}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Delete Seminar", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                await using var context = new LocalDbContext();
                var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                var updatedAttendance = cfwService.UpdateAttendance(
                    selectedAttendance.AttendanceId,
                    dialog.AttendanceDate.Date,
                    dialog.Status,
                    dialog.Source,
                    _currentUser.Id);

                await LoadSavedAttendanceAsync(updatedAttendance.Id);
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
                await using var context = new LocalDbContext();
                var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());
                cfwService.DeleteAttendance(selectedAttendance.AttendanceId, _currentUser.Id);
                await LoadSavedAttendanceAsync();
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
                    SetErrorStatus("The selected announcement seminar could not be loaded.");
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
            var blockReason = GetAttendanceScannerSessionBlockReason(SelectedEvent);
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
                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                await using var context = new LocalDbContext();
                var sessionService = new ScannerSessionService(context);
                var selectedEvent = SelectedEvent
                    ?? throw new InvalidOperationException("Select a seminar before opening the phone scanner.");
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

        private async Task ExecuteProcessPcScan(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || SelectedEvent == null) return;

            IsBusy = true;
            SetNeutralStatus("Analyzing ID card...");

            try
            {
                await using var context = new LocalDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);
                var lookup = await digitalIdService.LookupByQrPayloadAsync(payload);

                if (lookup == null)
                {
                    SetErrorStatus("Invalid QR code or beneficiary not found.");
                    return;
                }

                ScannedBeneficiary = new CashForWorkParticipantListItem
                {
                    FullName = lookup.FullName,
                    BeneficiaryId = lookup.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = lookup.CivilRegistryId ?? string.Empty,
                    BeneficiaryStagingId = lookup.BeneficiaryStagingId
                };

                ScannedBeneficiaryStatus = "Confirm attendance for this attendee.";
                ScannedBeneficiaryPhoto = string.IsNullOrWhiteSpace(lookup.PhotoPath) ? null : LocalImageLoader.Load(lookup.PhotoPath) as BitmapSource;

                _lastScannedPayload = payload;
                IsScannedResultVisible = true;
                SetNeutralStatus($"ID analyzed: {lookup.FullName}. Please review and confirm attendance.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Scan analysis error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteConfirmScannedAttendanceAsync()
        {
            if (SelectedEvent == null || ScannedBeneficiary == null || string.IsNullOrWhiteSpace(_lastScannedPayload))
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Recording attendance for {ScannedBeneficiary.FullName}...");

            try
            {
                await using var context = new LocalDbContext();
                var cfwService = new CashForWorkService(context, ggmsConsolidatedTransactionService: new GgmsConsolidatedTransactionService());

                var success = await cfwService.SaveScannerAttendanceAsync(
                    SelectedEvent.Id,
                    _currentUser.Id,
                    null,
                    _lastScannedPayload,
                    AttendanceCaptureSource.DesktopCamera);

                if (success)
                {
                    SetSuccessStatus($"Attendance recorded successfully for {ScannedBeneficiary.FullName}.");
                    await LoadSavedAttendanceAsync();
                    ResetScannedResult();
                    IsPcScannerOpen = false;
                }
                else
                {
                    SetErrorStatus("Unable to record attendance. Check if the beneficiary is approved or already scanned.");
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to record attendance: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetScannedResult()
        {
            ScannedBeneficiary = null;
            ScannedBeneficiaryStatus = null;
            ScannedBeneficiaryPhoto = null;
            _lastScannedPayload = null;
            IsScannedResultVisible = false;
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
            OnPropertyChanged(nameof(IsAnyOverlayOpen));
            OnPropertyChanged(nameof(DrawerVisibility));
            OnPropertyChanged(nameof(SeminarEditorVisibility));
            OnPropertyChanged(nameof(ScanAttendanceVisibility));
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

        private static string? GetAttendanceScannerSessionBlockReason(CashForWorkEvent? seminarEvent)
        {
            if (seminarEvent == null)
            {
                return "Select a seminar before opening the phone scanner.";
            }

            if (seminarEvent.Status == CashForWorkEventStatus.Completed)
            {
                return "Completed seminars can no longer open attendance scanner sessions.";
            }

            if (seminarEvent.EventDate.Date > DateTime.Today)
            {
                return "Attendance scanner sessions can only be opened on or after the seminar date.";
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

    public enum SeminarWorkspacePanel
    {
        None,
        SeminarEditor,
        ScanAttendance,
        Announcements
    }
}

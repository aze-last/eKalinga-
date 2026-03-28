using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class CashForWorkOcrViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly CashForWorkService _cashForWorkService;

        private CashForWorkEvent? _selectedEvent;
        private AyudaProgram? _selectedAyudaProgram;
        private HouseholdMember? _selectedEligibleMember;
        private string _eventTitle = "Barangay Clean-Up Drive";
        private string _eventLocation = "Barangay Hall Grounds";
        private string _eventNotes = string.Empty;
        private DateTime _eventDate = DateTime.Today;
        private string _eventStartTime = "07:00";
        private string _eventEndTime = "12:00";
        private string _statusMessage = "Create or select a cash-for-work event to begin.";
        private string _attendanceSummary = "No event selected.";
        private string _releaseAmountText = string.Empty;
        private string _releaseSummaryEventLabel = "No event selected.";
        private string _releaseSummaryStatusText = "Select an event to build a release-ready summary.";
        private string _releaseSummaryDetail = "Attendance totals will appear here once an event is selected.";
        private Brush _releaseSummaryStatusBrush = Brushes.DimGray;
        private int _approvedParticipantCount;
        private int _presentParticipantCount;
        private int _pendingParticipantCount;
        private int _manualAttendanceCount;
        private string _attendanceScannerSessionUrl = string.Empty;
        private string _attendanceScannerSessionPin = string.Empty;
        private string _attendanceScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _attendanceScannerQrImage;

        public CashForWorkOcrViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _context = new AppDbContext();
            _auditService = new AuditService(_context);
            _cashForWorkService = new CashForWorkService(_context, _auditService);

            CreateEventCommand = new RelayCommand(_ => ExecuteCreateEvent());
            AddParticipantCommand = new RelayCommand(_ => ExecuteAddParticipant());
            SaveManualAttendanceCommand = new RelayCommand(_ => ExecuteSaveManualAttendance());
            ReleaseBudgetCommand = new RelayCommand(async _ => await ExecuteReleaseBudgetAsync());
            CreateAttendanceScannerSessionCommand = new RelayCommand(async _ => await ExecuteCreateAttendanceScannerSessionAsync());

            LoadAyudaPrograms();
            LoadEvents();
            LoadEligibleMembers();
        }

        public ObservableCollection<AyudaProgram> AyudaPrograms { get; } = new();
        public ObservableCollection<CashForWorkEvent> Events { get; } = new();
        public ObservableCollection<HouseholdMember> EligibleMembers { get; } = new();
        public ObservableCollection<CashForWorkParticipantListItem> Participants { get; } = new();
        public ObservableCollection<CashForWorkSavedAttendanceRow> SavedAttendanceRows { get; } = new();

        public ICommand CreateEventCommand { get; }
        public ICommand AddParticipantCommand { get; }
        public ICommand SaveManualAttendanceCommand { get; }
        public ICommand ReleaseBudgetCommand { get; }
        public ICommand CreateAttendanceScannerSessionCommand { get; }

        public CashForWorkEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (SetProperty(ref _selectedEvent, value))
                {
                    LoadParticipants();
                    LoadSavedAttendance();
                    SelectedAyudaProgram = value?.AyudaProgramId.HasValue == true
                        ? AyudaPrograms.FirstOrDefault(program => program.Id == value.AyudaProgramId.Value)
                        : null;
                    ReleaseAmountText = value?.ReleaseAmount?.ToString("N2") ?? string.Empty;
                    AttendanceScannerSessionUrl = string.Empty;
                    AttendanceScannerSessionPin = string.Empty;
                    AttendanceScannerSessionExpiresAtText = string.Empty;
                    AttendanceScannerQrImage = null;
                    StatusMessage = value == null
                        ? "Create or select a cash-for-work event to begin."
                        : $"Loaded event: {value.Title}";
                }
            }
        }

        public AyudaProgram? SelectedAyudaProgram
        {
            get => _selectedAyudaProgram;
            set => SetProperty(ref _selectedAyudaProgram, value);
        }

        public HouseholdMember? SelectedEligibleMember
        {
            get => _selectedEligibleMember;
            set => SetProperty(ref _selectedEligibleMember, value);
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
            set => SetProperty(ref _statusMessage, value);
        }

        public string AttendanceSummary
        {
            get => _attendanceSummary;
            set => SetProperty(ref _attendanceSummary, value);
        }

        public string ReleaseAmountText
        {
            get => _releaseAmountText;
            set => SetProperty(ref _releaseAmountText, value);
        }

        public string ReleaseSummaryEventLabel
        {
            get => _releaseSummaryEventLabel;
            set => SetProperty(ref _releaseSummaryEventLabel, value);
        }

        public string ReleaseSummaryStatusText
        {
            get => _releaseSummaryStatusText;
            set => SetProperty(ref _releaseSummaryStatusText, value);
        }

        public string ReleaseSummaryDetail
        {
            get => _releaseSummaryDetail;
            set => SetProperty(ref _releaseSummaryDetail, value);
        }

        public Brush ReleaseSummaryStatusBrush
        {
            get => _releaseSummaryStatusBrush;
            set => SetProperty(ref _releaseSummaryStatusBrush, value);
        }

        public int ApprovedParticipantCount
        {
            get => _approvedParticipantCount;
            set => SetProperty(ref _approvedParticipantCount, value);
        }

        public int PresentParticipantCount
        {
            get => _presentParticipantCount;
            set => SetProperty(ref _presentParticipantCount, value);
        }

        public int PendingParticipantCount
        {
            get => _pendingParticipantCount;
            set => SetProperty(ref _pendingParticipantCount, value);
        }

        public int ManualAttendanceCount
        {
            get => _manualAttendanceCount;
            set => SetProperty(ref _manualAttendanceCount, value);
        }

        public string AttendanceScannerSessionUrl
        {
            get => _attendanceScannerSessionUrl;
            set => SetProperty(ref _attendanceScannerSessionUrl, value);
        }

        public string AttendanceScannerSessionPin
        {
            get => _attendanceScannerSessionPin;
            set => SetProperty(ref _attendanceScannerSessionPin, value);
        }

        public string AttendanceScannerSessionExpiresAtText
        {
            get => _attendanceScannerSessionExpiresAtText;
            set => SetProperty(ref _attendanceScannerSessionExpiresAtText, value);
        }

        public BitmapSource? AttendanceScannerQrImage
        {
            get => _attendanceScannerQrImage;
            set => SetProperty(ref _attendanceScannerQrImage, value);
        }

        private void LoadEvents()
        {
            var selectedEventId = SelectedEvent?.Id;
            Events.Clear();
            foreach (var cashForWorkEvent in _cashForWorkService.GetEvents())
            {
                Events.Add(cashForWorkEvent);
            }

            SelectedEvent = Events.FirstOrDefault(item => item.Id == selectedEventId)
                ?? Events.FirstOrDefault();
        }

        private void LoadAyudaPrograms()
        {
            AyudaPrograms.Clear();
            foreach (var program in _context.AyudaPrograms
                         .AsNoTracking()
                         .Where(item => item.IsActive)
                         .OrderBy(item => item.ProgramName))
            {
                AyudaPrograms.Add(program);
            }
        }

        private void LoadEligibleMembers()
        {
            EligibleMembers.Clear();
            foreach (var member in _cashForWorkService.GetEligibleMembers())
            {
                EligibleMembers.Add(member);
            }
        }

        private void LoadParticipants()
        {
            Participants.Clear();
            if (SelectedEvent == null)
            {
                AttendanceSummary = "No event selected.";
                return;
            }

            foreach (var participant in _cashForWorkService.GetParticipants(SelectedEvent.Id))
            {
                Participants.Add(new CashForWorkParticipantListItem
                {
                    ParticipantId = participant.Id,
                    HouseholdMemberId = participant.HouseholdMemberId,
                    FullName = participant.HouseholdMember.FullName,
                    HouseholdCode = participant.HouseholdMember.Household.HouseholdCode,
                    Purok = participant.HouseholdMember.Household.Purok
                });
            }

            UpdateAttendanceSummary();
        }

        private void LoadSavedAttendance()
        {
            SavedAttendanceRows.Clear();
            if (SelectedEvent == null)
            {
                ResetReleaseSummary();
                return;
            }

            var attendanceRecords = _cashForWorkService.GetAttendanceRecords(SelectedEvent.Id);
            var savedParticipantIds = attendanceRecords
                .Select(record => record.ParticipantId)
                .ToHashSet();

            foreach (var participant in Participants)
            {
                participant.IsMarkedPresent = savedParticipantIds.Contains(participant.ParticipantId);
            }

            foreach (var record in attendanceRecords)
            {
                SavedAttendanceRows.Add(new CashForWorkSavedAttendanceRow
                {
                    FullName = record.Participant.HouseholdMember.FullName,
                    HouseholdCode = record.Participant.HouseholdMember.Household.HouseholdCode,
                    Purok = record.Participant.HouseholdMember.Household.Purok,
                    Status = record.Status.ToString(),
                    Source = record.Source.ToString(),
                    RecordedAt = record.RecordedAt
                });
            }

            UpdateAttendanceSummary();
            LoadReleaseSummary();
        }

        private void UpdateAttendanceSummary()
        {
            if (SelectedEvent == null)
            {
                AttendanceSummary = "No event selected.";
                return;
            }

            AttendanceSummary =
                $"{SavedAttendanceRows.Count} attendance record(s) saved out of {Participants.Count} approved participant(s).";
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

            if (SelectedEvent.BudgetLedgerEntryId.HasValue && SelectedEvent.ReleaseAmount.HasValue)
            {
                ReleaseSummaryStatusText = "Released";
                ReleaseSummaryDetail = $"{summary.ReleaseReadyParticipantCount} participant(s) were released under this event. Total release amount: {SelectedEvent.ReleaseAmount.Value:N2}.";
                ReleaseSummaryStatusBrush = Brushes.ForestGreen;
                return;
            }

            if (summary.ReleaseReadyParticipantCount == 0)
            {
                ReleaseSummaryStatusText = "No release-ready attendance yet";
                ReleaseSummaryDetail = "Save manual attendance or capture attendance through the phone scanner first.";
                ReleaseSummaryStatusBrush = Brushes.DarkGoldenrod;
                return;
            }

            if (summary.PendingParticipantCount == 0)
            {
                ReleaseSummaryStatusText = "Release-ready";
                ReleaseSummaryDetail = $"{summary.ReleaseReadyParticipantCount} participant(s) are included in the release-ready cash-for-work summary.";
                ReleaseSummaryStatusBrush = Brushes.ForestGreen;
                return;
            }

            ReleaseSummaryStatusText = "Attendance still incomplete";
            ReleaseSummaryDetail = $"{summary.PendingParticipantCount} approved participant(s) still have no attendance record for this event date.";
            ReleaseSummaryStatusBrush = Brushes.DarkGoldenrod;
        }

        private void ResetReleaseSummary()
        {
            ReleaseSummaryEventLabel = "No event selected.";
            ReleaseSummaryStatusText = "Select an event to build a release-ready summary.";
            ReleaseSummaryDetail = "Attendance totals will appear here once an event is selected.";
            ReleaseSummaryStatusBrush = Brushes.DimGray;
            ApprovedParticipantCount = 0;
            PresentParticipantCount = 0;
            PendingParticipantCount = 0;
            ManualAttendanceCount = 0;
        }

        private void ExecuteCreateEvent()
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

            var cashForWorkEvent = _cashForWorkService.CreateEvent(
                EventTitle,
                EventLocation,
                EventDate,
                startTime,
                endTime,
                EventNotes,
                _currentUser.Id);

            LoadEvents();
            SelectedEvent = Events.FirstOrDefault(item => item.Id == cashForWorkEvent.Id);
            StatusMessage = $"Created event: {cashForWorkEvent.Title}";
        }

        private void ExecuteAddParticipant()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedEligibleMember == null)
            {
                MessageBox.Show("Select a household member to add.", "No Participant Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _cashForWorkService.AddParticipant(SelectedEvent.Id, SelectedEligibleMember.Id, _currentUser.Id);
                LoadParticipants();
                LoadSavedAttendance();
                StatusMessage = $"Added participant: {SelectedEligibleMember.FullName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Add Participant", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteSaveManualAttendance()
        {
            if (SelectedEvent == null)
            {
                return;
            }

            var selectedParticipantIds = Participants
                .Where(participant => participant.IsMarkedPresent)
                .Select(participant => participant.ParticipantId)
                .ToList();

            if (selectedParticipantIds.Count == 0)
            {
                MessageBox.Show("Mark at least one approved participant before saving manual attendance.", "No Participants Marked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var savedCount = _cashForWorkService.SaveManualAttendance(SelectedEvent.Id, _currentUser.Id, selectedParticipantIds);
            LoadSavedAttendance();
            StatusMessage = $"Saved {savedCount} manual attendance record(s) for {SelectedEvent.Title}.";
            MessageBox.Show(StatusMessage, "Manual Attendance Saved", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var result = await _cashForWorkService.ReleaseEventAsync(
                SelectedEvent.Id,
                SelectedAyudaProgram.Id,
                releaseAmount,
                _currentUser.Id,
                string.IsNullOrWhiteSpace(EventNotes) ? SelectedEvent.Title : EventNotes);

            if (!result.IsSuccess)
            {
                MessageBox.Show(result.Message, "Unable to Release Budget", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = result.Message;
                return;
            }

            LoadEvents();
            LoadSavedAttendance();
            StatusMessage = result.Message;
            MessageBox.Show(result.Message, "Cash-for-Work Released", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ExecuteCreateAttendanceScannerSessionAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event before opening the phone scanner.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                var sessionService = new ScannerSessionService(_context);
                var session = await sessionService.CreateAttendanceSessionAsync(SelectedEvent.Id, _currentUser.Id, TimeSpan.FromMinutes(15));
                var sessionUrl = $"{baseUrl}/scanner?session={Uri.EscapeDataString(session.SessionToken)}";

                AttendanceScannerSessionUrl = sessionUrl;
                AttendanceScannerSessionPin = session.Pin;
                AttendanceScannerSessionExpiresAtText = $"Expires {session.ExpiresAt:MMMM dd, yyyy hh:mm tt}";
                AttendanceScannerQrImage = QrCodeToolkitService.GenerateQrImage(sessionUrl, 8);
                StatusMessage = "Attendance scanner session is ready for the employee phone.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Unable to start the attendance scanner session: {ex.Message}";
                MessageBox.Show(ex.Message, "Scanner Session Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }

    public sealed class CashForWorkParticipantListItem : ObservableObject
    {
        private bool _isMarkedPresent;

        public int ParticipantId { get; set; }
        public int HouseholdMemberId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string HouseholdCode { get; set; } = string.Empty;
        public string Purok { get; set; } = string.Empty;

        public bool IsMarkedPresent
        {
            get => _isMarkedPresent;
            set => SetProperty(ref _isMarkedPresent, value);
        }
    }

    public sealed class CashForWorkSavedAttendanceRow
    {
        public string FullName { get; set; } = string.Empty;
        public string HouseholdCode { get; set; } = string.Empty;
        public string Purok { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
    }
}

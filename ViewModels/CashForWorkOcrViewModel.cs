using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class CashForWorkOcrViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly OcrRuntimeOptions _ocrOptions;
        private readonly CashForWorkService _cashForWorkService;

        private CashForWorkEvent? _selectedEvent;
        private HouseholdMember? _selectedEligibleMember;
        private OcrProfile? _selectedOcrProfile;
        private string _eventTitle = "Barangay Clean-Up Drive";
        private string _eventLocation = "Barangay Hall Grounds";
        private string _eventNotes = string.Empty;
        private DateTime _eventDate = DateTime.Today;
        private string _eventStartTime = "07:00";
        private string _eventEndTime = "12:00";
        private string _selectedImagePath = string.Empty;
        private string _statusMessage = "Create or select a cash-for-work event to begin.";
        private string _ocrStatusText = "Checking OCR service...";
        private Brush _ocrStatusBrush = Brushes.DarkGoldenrod;
        private string _attendanceSummary = "No event selected.";
        private string _releaseSummaryEventLabel = "No event selected.";
        private string _releaseSummaryStatusText = "Select an event to build a release-ready summary.";
        private string _releaseSummaryDetail = "Attendance totals will appear here once an event is selected.";
        private Brush _releaseSummaryStatusBrush = Brushes.DimGray;
        private int _approvedParticipantCount;
        private int _presentParticipantCount;
        private int _pendingParticipantCount;
        private int _manualAttendanceCount;
        private int _ocrAttendanceCount;
        private bool _isBusy;

        public CashForWorkOcrViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _context = new AppDbContext();
            _auditService = new AuditService(_context);
            _ocrOptions = OcrRuntimeOptions.Load();
            _cashForWorkService = new CashForWorkService(_context, _auditService);

            BrowseImageCommand = new RelayCommand(_ => ExecuteBrowseImage());
            RefreshOcrStatusCommand = new RelayCommand(async _ => await ExecuteRefreshOcrStatusAsync());
            CreateEventCommand = new RelayCommand(_ => ExecuteCreateEvent());
            AddParticipantCommand = new RelayCommand(_ => ExecuteAddParticipant());
            ProcessImageCommand = new RelayCommand(async _ => await ExecuteProcessImageAsync());
            SaveAttendanceCommand = new RelayCommand(_ => ExecuteSaveAttendance());
            SaveManualAttendanceCommand = new RelayCommand(_ => ExecuteSaveManualAttendance());

            LoadOcrProfiles();
            LoadEvents();
            LoadEligibleMembers();
            _ = ExecuteRefreshOcrStatusAsync();
        }

        public ObservableCollection<OcrProfile> OcrProfiles { get; } = new();
        public ObservableCollection<CashForWorkEvent> Events { get; } = new();
        public ObservableCollection<HouseholdMember> EligibleMembers { get; } = new();
        public ObservableCollection<CashForWorkParticipantListItem> Participants { get; } = new();
        public ObservableCollection<CashForWorkAttendanceReviewRow> ReviewRows { get; } = new();
        public ObservableCollection<CashForWorkSavedAttendanceRow> SavedAttendanceRows { get; } = new();

        public ICommand BrowseImageCommand { get; }
        public ICommand RefreshOcrStatusCommand { get; }
        public ICommand CreateEventCommand { get; }
        public ICommand AddParticipantCommand { get; }
        public ICommand ProcessImageCommand { get; }
        public ICommand SaveAttendanceCommand { get; }
        public ICommand SaveManualAttendanceCommand { get; }

        public CashForWorkEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (SetProperty(ref _selectedEvent, value))
                {
                    LoadParticipants();
                    LoadSavedAttendance();
                    ReviewRows.Clear();
                    StatusMessage = value == null
                        ? "Create or select a cash-for-work event to begin."
                        : $"Loaded event: {value.Title}";
                }
            }
        }

        public HouseholdMember? SelectedEligibleMember
        {
            get => _selectedEligibleMember;
            set => SetProperty(ref _selectedEligibleMember, value);
        }

        public OcrProfile? SelectedOcrProfile
        {
            get => _selectedOcrProfile;
            set
            {
                if (SetProperty(ref _selectedOcrProfile, value))
                {
                    ReviewRows.Clear();
                    StatusMessage = value == null
                        ? "Select an OCR engine to process attendance."
                        : $"OCR engine ready to use: {value.Name}";
                    _ = ExecuteRefreshOcrStatusAsync();
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

        public string SelectedImagePath
        {
            get => _selectedImagePath;
            set => SetProperty(ref _selectedImagePath, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string OcrStatusText
        {
            get => _ocrStatusText;
            set => SetProperty(ref _ocrStatusText, value);
        }

        public Brush OcrStatusBrush
        {
            get => _ocrStatusBrush;
            set => SetProperty(ref _ocrStatusBrush, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string AttendanceSummary
        {
            get => _attendanceSummary;
            set => SetProperty(ref _attendanceSummary, value);
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

        public int OcrAttendanceCount
        {
            get => _ocrAttendanceCount;
            set => SetProperty(ref _ocrAttendanceCount, value);
        }

        private void LoadOcrProfiles()
        {
            OcrProfiles.Clear();
            foreach (var profile in _ocrOptions.Profiles)
            {
                OcrProfiles.Add(profile);
            }

            SelectedOcrProfile = OcrProfiles.FirstOrDefault(profile =>
                profile.Provider.Equals(_ocrOptions.Provider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.Model ?? string.Empty, _ocrOptions.Model ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? OcrProfiles.FirstOrDefault(profile => profile.Provider.Equals(_ocrOptions.Provider, StringComparison.OrdinalIgnoreCase))
                ?? OcrProfiles.FirstOrDefault();
        }

        private void LoadEvents()
        {
            Events.Clear();
            foreach (var cashForWorkEvent in _cashForWorkService.GetEvents())
            {
                Events.Add(cashForWorkEvent);
            }

            SelectedEvent ??= Events.FirstOrDefault();
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
            OcrAttendanceCount = summary.OcrAttendanceCount;

            if (summary.ReleaseReadyParticipantCount == 0)
            {
                ReleaseSummaryStatusText = "No release-ready attendance yet";
                ReleaseSummaryDetail = "Save manual attendance first, then optionally use OCR review-assist for additional matches.";
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
            OcrAttendanceCount = 0;
        }

        private async Task ExecuteRefreshOcrStatusAsync()
        {
            if (SelectedOcrProfile == null)
            {
                OcrStatusText = "No OCR engine selected.";
                OcrStatusBrush = Brushes.Firebrick;
                return;
            }

            var health = await CreateSelectedOcrService().GetHealthAsync();
            OcrStatusText = health.IsAvailable
                ? $"{SelectedOcrProfile.Name}: {health.Detail}"
                : $"{SelectedOcrProfile.Name}: {health.Detail}";
            OcrStatusBrush = health.IsAvailable ? Brushes.ForestGreen : Brushes.Firebrick;
        }

        private void ExecuteBrowseImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                Title = "Select Attendance Logbook Image"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedImagePath = dialog.FileName;
                StatusMessage = $"Selected image: {Path.GetFileName(dialog.FileName)}";
            }
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

        private async Task ExecuteProcessImageAsync()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedImagePath) || !File.Exists(SelectedImagePath))
            {
                MessageBox.Show("Choose an attendance image to upload first.", "No Image Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            var selectedProfileName = SelectedOcrProfile?.Name ?? "OCR";
            StatusMessage = $"Processing uploaded logbook image through {selectedProfileName}...";

            try
            {
                var reviewItems = await _cashForWorkService.ReviewAttendanceFromImageAsync(
                    SelectedEvent.Id,
                    SelectedImagePath,
                    CreateSelectedOcrService());
                ReviewRows.Clear();
                foreach (var reviewItem in reviewItems)
                {
                    ReviewRows.Add(new CashForWorkAttendanceReviewRow
                    {
                        ExtractedName = reviewItem.ExtractedName,
                        MatchStatus = reviewItem.MatchStatus,
                        SuggestedParticipantId = reviewItem.SuggestedParticipantId,
                        SuggestedParticipantName = reviewItem.SuggestedParticipantName,
                        SimilarityPercent = $"{reviewItem.SimilarityScore:P0}",
                        IsSelected = reviewItem.IsSelected
                    });
                }

                StatusMessage = $"{selectedProfileName} finished. {ReviewRows.Count} extracted names ready for review.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "OCR Processing Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "OCR processing failed.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteSaveAttendance()
        {
            if (SelectedEvent == null)
            {
                return;
            }

            var selections = ReviewRows
                .Select(row => new CashForWorkAttendanceReviewItem
                {
                    ExtractedName = row.ExtractedName,
                    MatchStatus = row.MatchStatus,
                    SuggestedParticipantId = row.SuggestedParticipantId,
                    SuggestedParticipantName = row.SuggestedParticipantName,
                    IsSelected = row.IsSelected
                })
                .ToList();

            var savedCount = _cashForWorkService.SaveAttendanceSelections(SelectedEvent.Id, _currentUser.Id, selections);
            LoadSavedAttendance();
            StatusMessage = $"Saved {savedCount} attendance record(s) for {SelectedEvent.Title}.";
            MessageBox.Show(StatusMessage, "Attendance Saved", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private IOcrService CreateSelectedOcrService()
        {
            return OcrServiceFactory.Create(_ocrOptions, SelectedOcrProfile);
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

    public sealed class CashForWorkAttendanceReviewRow : ObservableObject
    {
        private bool _isSelected;

        public string ExtractedName { get; set; } = string.Empty;
        public AttendanceMatchStatus MatchStatus { get; set; }
        public int? SuggestedParticipantId { get; set; }
        public string SuggestedParticipantName { get; set; } = string.Empty;
        public string SimilarityPercent { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
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

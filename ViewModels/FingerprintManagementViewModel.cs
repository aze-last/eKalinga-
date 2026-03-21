using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public class FingerprintManagementViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;
        private readonly User _targetUser;
        private readonly FingerprintService _fingerprintService;

        private FingerprintTemplate? _selectedTemplate;
        private int _selectedFingerIndex = 1;
        private string _captureStatus = "No capture yet.";
        private bool _isScannerConnected;
        private string _scannerStatusText = "Checking scanner status...";
        private string _scannerStatusDetail = string.Empty;
        private Brush _scannerStatusBrush = Brushes.Red;
        private bool _isCaptureSessionActive;
        private Visibility _captureGuideVisibility = Visibility.Collapsed;
        private string _captureGuideTitle = "Enrollment Guide";
        private string _captureGuideMessage = string.Empty;
        private int _requiredEnrollmentScanCount = 4;

        private readonly List<byte[]> _enrollmentSamples = new();
        private byte[]? _capturedTemplateBytes;
        private int? _capturedQualityScore;

        public ObservableCollection<FingerprintTemplate> Templates { get; } = new();
        public ObservableCollection<FingerIndexOption> FingerOptions { get; } = new();

        public FingerprintTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set => SetProperty(ref _selectedTemplate, value);
        }

        public int SelectedFingerIndex
        {
            get => _selectedFingerIndex;
            set => SetProperty(ref _selectedFingerIndex, value);
        }

        public string CaptureStatus
        {
            get => _captureStatus;
            set => SetProperty(ref _captureStatus, value);
        }

        public bool IsScannerConnected
        {
            get => _isScannerConnected;
            set => SetProperty(ref _isScannerConnected, value);
        }

        public string ScannerStatusText
        {
            get => _scannerStatusText;
            set => SetProperty(ref _scannerStatusText, value);
        }

        public string ScannerStatusDetail
        {
            get => _scannerStatusDetail;
            set => SetProperty(ref _scannerStatusDetail, value);
        }

        public Brush ScannerStatusBrush
        {
            get => _scannerStatusBrush;
            set => SetProperty(ref _scannerStatusBrush, value);
        }

        public Visibility CaptureGuideVisibility
        {
            get => _captureGuideVisibility;
            set => SetProperty(ref _captureGuideVisibility, value);
        }

        public string CaptureGuideTitle
        {
            get => _captureGuideTitle;
            set => SetProperty(ref _captureGuideTitle, value);
        }

        public string CaptureGuideMessage
        {
            get => _captureGuideMessage;
            set => SetProperty(ref _captureGuideMessage, value);
        }

        public string HeaderText => $"Fingerprint Management - {_targetUser.Username} ({_targetUser.Role})";

        public ICommand CaptureCommand { get; }
        public ICommand EnrollOrUpdateCommand { get; }
        public ICommand VerifySelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand RefreshCommand { get; }

        public FingerprintManagementViewModel(User currentUser, User targetUser)
        {
            _currentUser = currentUser;
            _targetUser = targetUser;
            _context = new AppDbContext();
            _fingerprintService = new FingerprintService(_context);

            if (_currentUser.Role != UserRole.Admin)
            {
                throw new UnauthorizedAccessException("Only Admin can configure fingerprint templates.");
            }

            try
            {
                _requiredEnrollmentScanCount = _fingerprintService.GetEnrollmentRequiredSampleCount(_currentUser.Id);
            }
            catch
            {
                _requiredEnrollmentScanCount = 4;
            }

            for (int i = 0; i <= 9; i++)
            {
                FingerOptions.Add(new FingerIndexOption
                {
                    Index = i,
                    Label = i switch
                    {
                        0 => "0 - Right Thumb",
                        1 => "1 - Right Index",
                        2 => "2 - Right Middle",
                        3 => "3 - Right Ring",
                        4 => "4 - Right Little",
                        5 => "5 - Left Thumb",
                        6 => "6 - Left Index",
                        7 => "7 - Left Middle",
                        8 => "8 - Left Ring",
                        9 => "9 - Left Little",
                        _ => i.ToString()
                    }
                });
            }

            CaptureCommand = new RelayCommand(
                async _ => await ExecuteCaptureAsync(),
                _ => !_isCaptureSessionActive);
            EnrollOrUpdateCommand = new RelayCommand(
                _ => ExecuteEnrollOrUpdate(),
                _ => !_isCaptureSessionActive && _capturedTemplateBytes != null && _capturedTemplateBytes.Length > 0);
            VerifySelectedCommand = new RelayCommand(_ => ExecuteVerifySelected(), _ => !_isCaptureSessionActive);
            DeleteSelectedCommand = new RelayCommand(_ => ExecuteDeleteSelected(), _ => !_isCaptureSessionActive);
            RefreshCommand = new RelayCommand(_ => ExecuteRefresh(), _ => !_isCaptureSessionActive);

            ExecuteRefresh();
        }

        private void LoadTemplates()
        {
            Templates.Clear();
            var templates = _fingerprintService.GetTemplatesForUser(_currentUser.Id, _targetUser.Id);
            foreach (var template in templates)
            {
                Templates.Add(template);
            }
        }

        private void ExecuteRefresh()
        {
            LoadTemplates();
            ResetEnrollmentSession("No capture yet.");
            RefreshScannerStatus();
        }

        private void RefreshScannerStatus()
        {
            try
            {
                var deviceStatus = _fingerprintService.GetDeviceStatus(_currentUser.Id);
                IsScannerConnected = deviceStatus.IsConnected;
                ScannerStatusText = deviceStatus.IsConnected ? "Scanner Connected" : "Scanner Not Ready";
                ScannerStatusDetail = deviceStatus.Message;
                ScannerStatusBrush = deviceStatus.IsConnected ? Brushes.LimeGreen : Brushes.Red;
            }
            catch (Exception ex)
            {
                IsScannerConnected = false;
                ScannerStatusText = "Scanner Not Ready";
                ScannerStatusDetail = ex.Message;
                ScannerStatusBrush = Brushes.Red;
            }
        }

        private async Task ExecuteCaptureAsync()
        {
            if (_isCaptureSessionActive)
            {
                ShowCaptureGuide(
                    "Enrollment in progress",
                    $"Place the same finger on the scanner until all {_requiredEnrollmentScanCount} accepted scans are collected.");
                return;
            }

            _isCaptureSessionActive = true;
            _capturedTemplateBytes = null;
            _capturedQualityScore = null;
            _enrollmentSamples.Clear();
            RefreshCommandStates();

            try
            {
                int consecutiveTimeouts = 0;

                ShowCaptureGuide(
                    "Enrollment started",
                    $"Place the same finger on the scanner. The app will capture {_requiredEnrollmentScanCount} accepted scans automatically. Do not press the button again.");

                while (true)
                {
                    var remainingBeforeScan = Math.Max(_requiredEnrollmentScanCount - _enrollmentSamples.Count, 1);
                    ShowCaptureGuide(
                        "Scan finger",
                        $"Press the same finger on the scanner now. Remaining accepted scans: {remainingBeforeScan} of {_requiredEnrollmentScanCount}.");

                    FingerprintEnrollmentSampleResult sample;
                    try
                    {
                        sample = await Task.Run(() => ExecuteWorkerServiceCall(
                            service => service.CaptureEnrollmentSampleFromDevice(_currentUser.Id)));
                        consecutiveTimeouts = 0;
                    }
                    catch (TimeoutException)
                    {
                        consecutiveTimeouts++;

                        if (consecutiveTimeouts < 2)
                        {
                            CaptureStatus = "Capture timed out while waiting for the finger.";
                            ShowCaptureGuide(
                                "Waiting for finger",
                                "Scanner timed out. Place the same finger on the scanner now. The session is still active.");
                            RefreshScannerStatus();
                            continue;
                        }

                        throw new TimeoutException("Enrollment session stopped because the scanner timed out twice. Click Capture Enrollment Sample to start again.");
                    }

                    _capturedQualityScore = sample.QualityScore ?? _capturedQualityScore;

                    if (!sample.IsUsableForEnrollment || sample.SampleData.Length == 0)
                    {
                        _capturedTemplateBytes = null;
                        CaptureStatus = $"Capture rejected: {sample.Feedback}";
                        ShowCaptureGuide(
                            "Scan rejected",
                            $"{sample.Feedback} Keep using the same finger. Accepted scans: {_enrollmentSamples.Count} of {_requiredEnrollmentScanCount}.");
                        RefreshScannerStatus();
                        continue;
                    }

                    _enrollmentSamples.Add(sample.SampleData);

                    var buildResult = await Task.Run(() => ExecuteWorkerServiceCall(
                        service => service.BuildEnrollmentTemplate(_currentUser.Id, _enrollmentSamples.ToList())));

                    _capturedTemplateBytes = buildResult.IsReady ? buildResult.TemplateData : null;

                    CaptureStatus = $"Sample {_enrollmentSamples.Count} captured from {sample.ReaderDescription}. {buildResult.StatusMessage}";
                    RefreshScannerStatus();

                    if (buildResult.IsReady)
                    {
                        ShowCaptureGuide(
                            "Template ready",
                            "Fingerprint template is ready. Click Save Ready Template to store it.");
                        MessageBox.Show("Fingerprint template is ready to save.", "Capture",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }

                    ShowCaptureGuide(
                        "Enrollment in progress",
                        $"Accepted scans: {_enrollmentSamples.Count} of {_requiredEnrollmentScanCount}. {buildResult.StatusMessage}");
                }
            }
            catch (Exception ex)
            {
                CaptureStatus = $"Capture failed: {ex.Message}";
                ShowCaptureGuide("Capture stopped", ex.Message);
                RefreshScannerStatus();
                MessageBox.Show($"Capture failed: {ex.Message}", "Capture Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isCaptureSessionActive = false;
                RefreshCommandStates();
            }
        }

        private void ExecuteEnrollOrUpdate()
        {
            try
            {
                if (_capturedTemplateBytes == null || _capturedTemplateBytes.Length == 0)
                {
                    MessageBox.Show("Capture a fingerprint first before enrolling/updating.", "Missing Capture",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _fingerprintService.EnrollOrUpdateTemplate(
                    _currentUser.Id,
                    _targetUser.Id,
                    SelectedFingerIndex,
                    _capturedTemplateBytes,
                    _capturedQualityScore);

                LoadTemplates();
                ResetEnrollmentSession("Template saved. Capture again to enroll another finger.");
                MessageBox.Show("Fingerprint template saved.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Enroll/Update failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteVerifySelected()
        {
            try
            {
                if (SelectedTemplate == null)
                {
                    MessageBox.Show("Select a template row first.", "No Template",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var verify = _fingerprintService.VerifyAgainstTemplate(_currentUser.Id, SelectedTemplate.Id);
                LoadTemplates();

                var statusText = verify.IsMatch ? "MATCH" : "NO MATCH";
                MessageBox.Show(
                    $"Verification result: {statusText}\nScore: {verify.Score}\nThreshold: {verify.Threshold}\nQuality: {verify.CapturedQualityScore?.ToString() ?? "n/a"}",
                    "Verify",
                    MessageBoxButton.OK,
                    verify.IsMatch ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verify failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteDeleteSelected()
        {
            try
            {
                if (SelectedTemplate == null)
                {
                    MessageBox.Show("Select a template row first.", "No Template",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show(
                    $"Delete template for finger index {SelectedTemplate.FingerIndex}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }

                _fingerprintService.DeleteTemplateById(_currentUser.Id, SelectedTemplate.Id);
                LoadTemplates();

                MessageBox.Show("Template deleted (deactivated).", "Deleted",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetEnrollmentSession(string statusMessage)
        {
            _enrollmentSamples.Clear();
            _capturedTemplateBytes = null;
            _capturedQualityScore = null;
            CaptureStatus = statusMessage;
            CaptureGuideVisibility = Visibility.Collapsed;
            CaptureGuideTitle = "Enrollment Guide";
            CaptureGuideMessage = string.Empty;
            RefreshCommandStates();
        }

        private void ShowCaptureGuide(string title, string message)
        {
            CaptureGuideTitle = title;
            CaptureGuideMessage = message;
            CaptureGuideVisibility = Visibility.Visible;
        }

        private void RefreshCommandStates()
        {
            (CaptureCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EnrollOrUpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (VerifySelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private static T ExecuteWorkerServiceCall<T>(Func<FingerprintService, T> action)
        {
            using var context = new AppDbContext();
            var service = new FingerprintService(context);
            return action(service);
        }
    }

    public class FingerIndexOption
    {
        public int Index { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}

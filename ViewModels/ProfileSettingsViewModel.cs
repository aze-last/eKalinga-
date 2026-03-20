using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ProfileSettingsViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly int _userId;
        private UserProfile _profile = new UserProfile();
        private UserPreference _preferences = new UserPreference();
        private User? _user;
        private Employee? _employee;
        private readonly RelayCommand _testImportConnectionCommand;
        private readonly RelayCommand _saveImportConnectionCommand;
        private readonly RelayCommand _importBeneficiariesCommand;
        private readonly RelayCommand _refreshStagingPreviewCommand;
        private readonly RelayCommand _createBackupCommand;
        private readonly RelayCommand _restoreBackupCommand;
        private readonly RelayCommand _loadRemoteTablesCommand;
        private readonly RelayCommand _reviewSelectedTableCommand;
        private readonly RelayCommand _syncSelectedTableToLocalCommand;

        public bool IsManager { get; }
        public bool CanManageSystemImport { get; }
        public bool CanEditContactInfo => IsManager;

        public ObservableCollection<SelectablePosition> Positions { get; } = new();
        public ObservableCollection<string> ShiftBlocks { get; } = new() { "Any", "AM", "PM", "Mid", "Night" };
        public ObservableCollection<string> DefaultViews { get; } = new() { "Dashboard", "Weekly Schedule", "Shift Planning", "Attendance Logs", "Employee List" };
        public ObservableCollection<string> ReportFormats { get; } = new() { "CSV", "PDF" };
        public ObservableCollection<string> RemoteTables { get; } = new();

        private ImageSource? _photoImage;
        public ImageSource? PhotoImage
        {
            get => _photoImage;
            set => SetProperty(ref _photoImage, value);
        }

        private string _photoPath = string.Empty;
        public string PhotoPath
        {
            get => _photoPath;
            set => SetProperty(ref _photoPath, value);
        }

        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        private string _nickname = string.Empty;
        public string Nickname
        {
            get => _nickname;
            set => SetProperty(ref _nickname, value);
        }

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        private string _phone = string.Empty;
        public string Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        private string _address = string.Empty;
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        private string _emergencyContactName = string.Empty;
        public string EmergencyContactName
        {
            get => _emergencyContactName;
            set => SetProperty(ref _emergencyContactName, value);
        }

        private string _emergencyContactPhone = string.Empty;
        public string EmergencyContactPhone
        {
            get => _emergencyContactPhone;
            set => SetProperty(ref _emergencyContactPhone, value);
        }

        private string _selectedShiftBlock = "Any";
        public string SelectedShiftBlock
        {
            get => _selectedShiftBlock;
            set => SetProperty(ref _selectedShiftBlock, value);
        }

        public bool IsMondayOff { get => _isMondayOff; set => SetProperty(ref _isMondayOff, value); }
        public bool IsTuesdayOff { get => _isTuesdayOff; set => SetProperty(ref _isTuesdayOff, value); }
        public bool IsWednesdayOff { get => _isWednesdayOff; set => SetProperty(ref _isWednesdayOff, value); }
        public bool IsThursdayOff { get => _isThursdayOff; set => SetProperty(ref _isThursdayOff, value); }
        public bool IsFridayOff { get => _isFridayOff; set => SetProperty(ref _isFridayOff, value); }
        public bool IsSaturdayOff { get => _isSaturdayOff; set => SetProperty(ref _isSaturdayOff, value); }
        public bool IsSundayOff { get => _isSundayOff; set => SetProperty(ref _isSundayOff, value); }

        private bool _isMondayOff;
        private bool _isTuesdayOff;
        private bool _isWednesdayOff;
        private bool _isThursdayOff;
        private bool _isFridayOff;
        private bool _isSaturdayOff;
        private bool _isSundayOff;

        private bool _notifyLeave = true;
        public bool NotifyLeave { get => _notifyLeave; set => SetProperty(ref _notifyLeave, value); }
        private bool _notifyShift = true;
        public bool NotifyShift { get => _notifyShift; set => SetProperty(ref _notifyShift, value); }
        private bool _notifyAnnouncement = true;
        public bool NotifyAnnouncement { get => _notifyAnnouncement; set => SetProperty(ref _notifyAnnouncement, value); }

        private bool _notifyInApp = true;
        public bool NotifyInApp { get => _notifyInApp; set => SetProperty(ref _notifyInApp, value); }
        private bool _notifyEmail;
        public bool NotifyEmail { get => _notifyEmail; set => SetProperty(ref _notifyEmail, value); }

        private string _defaultView = "Dashboard";
        public string DefaultView
        {
            get => _defaultView;
            set => SetProperty(ref _defaultView, value);
        }

        private string _reportFormat = "CSV";
        public string ReportFormat
        {
            get => _reportFormat;
            set => SetProperty(ref _reportFormat, value);
        }

        private string _approvalSignature = string.Empty;
        public string ApprovalSignature
        {
            get => _approvalSignature;
            set => SetProperty(ref _approvalSignature, value);
        }

        private bool _autoNotifyOnApproval = true;
        public bool AutoNotifyOnApproval
        {
            get => _autoNotifyOnApproval;
            set => SetProperty(ref _autoNotifyOnApproval, value);
        }

        private string _currentPassword = string.Empty;
        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        private string _importSourceServer = string.Empty;
        public string ImportSourceServer
        {
            get => _importSourceServer;
            set => SetProperty(ref _importSourceServer, value);
        }

        private string _importSourcePortText = "3306";
        public string ImportSourcePortText
        {
            get => _importSourcePortText;
            set => SetProperty(ref _importSourcePortText, value);
        }

        private string _importSourceDatabase = string.Empty;
        public string ImportSourceDatabase
        {
            get => _importSourceDatabase;
            set => SetProperty(ref _importSourceDatabase, value);
        }

        private string _importSourceUsername = string.Empty;
        public string ImportSourceUsername
        {
            get => _importSourceUsername;
            set => SetProperty(ref _importSourceUsername, value);
        }

        private string _importSourcePassword = string.Empty;
        public string ImportSourcePassword
        {
            get => _importSourcePassword;
            set => SetProperty(ref _importSourcePassword, value);
        }

        private string _localImportTargetSummary = string.Empty;
        public string LocalImportTargetSummary
        {
            get => _localImportTargetSummary;
            private set => SetProperty(ref _localImportTargetSummary, value);
        }

        private string _backupTargetSummary = string.Empty;
        public string BackupTargetSummary
        {
            get => _backupTargetSummary;
            private set => SetProperty(ref _backupTargetSummary, value);
        }

        private string _selectedRemoteTable = string.Empty;
        public string SelectedRemoteTable
        {
            get => _selectedRemoteTable;
            set
            {
                if (SetProperty(ref _selectedRemoteTable, value))
                {
                    _reviewSelectedTableCommand.RaiseCanExecuteChanged();
                    _syncSelectedTableToLocalCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _tableReviewSummary = "Load remote tables to start reviewing.";
        public string TableReviewSummary
        {
            get => _tableReviewSummary;
            private set => SetProperty(ref _tableReviewSummary, value);
        }

        private string _selectedTableColumnsSummary = string.Empty;
        public string SelectedTableColumnsSummary
        {
            get => _selectedTableColumnsSummary;
            private set => SetProperty(ref _selectedTableColumnsSummary, value);
        }

        private DataView? _tablePreviewView;
        public DataView? TablePreviewView
        {
            get => _tablePreviewView;
            private set => SetProperty(ref _tablePreviewView, value);
        }

        private string _importStatusMessage = "Configure the CRS source connection, then import beneficiaries into local staging.";
        public string ImportStatusMessage
        {
            get => _importStatusMessage;
            private set => SetProperty(ref _importStatusMessage, value);
        }

        private Brush _importStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        public Brush ImportStatusBrush
        {
            get => _importStatusBrush;
            private set => SetProperty(ref _importStatusBrush, value);
        }

        private string _backupStatusMessage = "Create a backup of the current app database before running restore.";
        public string BackupStatusMessage
        {
            get => _backupStatusMessage;
            private set => SetProperty(ref _backupStatusMessage, value);
        }

        private Brush _backupStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        public Brush BackupStatusBrush
        {
            get => _backupStatusBrush;
            private set => SetProperty(ref _backupStatusBrush, value);
        }

        private string _lastBackupSummary = "No backup activity yet in this session.";
        public string LastBackupSummary
        {
            get => _lastBackupSummary;
            private set => SetProperty(ref _lastBackupSummary, value);
        }

        private string _lastBackupFilePath = string.Empty;
        public string LastBackupFilePath
        {
            get => _lastBackupFilePath;
            private set => SetProperty(ref _lastBackupFilePath, value);
        }

        private bool _isImportBusy;
        public bool IsImportBusy
        {
            get => _isImportBusy;
            private set
            {
                if (SetProperty(ref _isImportBusy, value))
                {
                    _testImportConnectionCommand.RaiseCanExecuteChanged();
                    _saveImportConnectionCommand.RaiseCanExecuteChanged();
                    _importBeneficiariesCommand.RaiseCanExecuteChanged();
                    _refreshStagingPreviewCommand.RaiseCanExecuteChanged();
                    _createBackupCommand.RaiseCanExecuteChanged();
                    _restoreBackupCommand.RaiseCanExecuteChanged();
                    _loadRemoteTablesCommand.RaiseCanExecuteChanged();
                    _reviewSelectedTableCommand.RaiseCanExecuteChanged();
                    _syncSelectedTableToLocalCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isBackupBusy;
        public bool IsBackupBusy
        {
            get => _isBackupBusy;
            private set
            {
                if (SetProperty(ref _isBackupBusy, value))
                {
                    _testImportConnectionCommand.RaiseCanExecuteChanged();
                    _saveImportConnectionCommand.RaiseCanExecuteChanged();
                    _importBeneficiariesCommand.RaiseCanExecuteChanged();
                    _refreshStagingPreviewCommand.RaiseCanExecuteChanged();
                    _createBackupCommand.RaiseCanExecuteChanged();
                    _restoreBackupCommand.RaiseCanExecuteChanged();
                    _loadRemoteTablesCommand.RaiseCanExecuteChanged();
                    _reviewSelectedTableCommand.RaiseCanExecuteChanged();
                    _syncSelectedTableToLocalCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand BrowsePhotoCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand TestImportConnectionCommand => _testImportConnectionCommand;
        public ICommand SaveImportConnectionCommand => _saveImportConnectionCommand;
        public ICommand ImportBeneficiariesCommand => _importBeneficiariesCommand;
        public ICommand RefreshStagingPreviewCommand => _refreshStagingPreviewCommand;
        public ICommand CreateBackupCommand => _createBackupCommand;
        public ICommand RestoreBackupCommand => _restoreBackupCommand;
        public ICommand LoadRemoteTablesCommand => _loadRemoteTablesCommand;
        public ICommand ReviewSelectedTableCommand => _reviewSelectedTableCommand;
        public ICommand SyncSelectedTableToLocalCommand => _syncSelectedTableToLocalCommand;
        public event Action? ProfileUpdated;

        public ProfileSettingsViewModel(User user)
        {
            _context = new AppDbContext();
            _userId = user.Id;
            IsManager = user.Role == UserRole.Manager || user.Role == UserRole.ShiftManager;
            CanManageSystemImport = user.Role == UserRole.Admin || user.Role == UserRole.HRStaff;

            BrowsePhotoCommand = new RelayCommand(_ => BrowsePhoto());
            SaveCommand = new RelayCommand(_ => SaveProfile());
            CancelCommand = new RelayCommand(_ => LoadProfile());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
            _testImportConnectionCommand = new RelayCommand(
                async _ => await ExecuteTestImportConnectionAsync(),
                _ => CanManageSystemImport && !IsImportBusy);
            _saveImportConnectionCommand = new RelayCommand(
                _ => ExecuteSaveImportConnection(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy);
            _importBeneficiariesCommand = new RelayCommand(
                async _ => await ExecuteImportBeneficiariesAsync(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy);
            _refreshStagingPreviewCommand = new RelayCommand(
                _ => LoadStagingPreview(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy);
            _createBackupCommand = new RelayCommand(
                async _ => await ExecuteCreateBackupAsync(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy);
            _restoreBackupCommand = new RelayCommand(
                async _ => await ExecuteRestoreBackupAsync(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy);
            _loadRemoteTablesCommand = new RelayCommand(
                async _ => await LoadRemoteTablesAsync(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy);
            _reviewSelectedTableCommand = new RelayCommand(
                async _ => await ReviewSelectedTableAsync(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy && !string.IsNullOrWhiteSpace(SelectedRemoteTable));
            _syncSelectedTableToLocalCommand = new RelayCommand(
                async _ => await SyncSelectedTableToLocalAsync(),
                _ => CanManageSystemImport && !IsImportBusy && !IsBackupBusy && !string.IsNullOrWhiteSpace(SelectedRemoteTable));

            LoadProfile();
            LoadImportConfiguration();
            LoadBackupConfiguration();
            LoadStagingPreview();
        }

        private void LoadProfile()
        {
            _user = _context.Users.Include(u => u.Employee).FirstOrDefault(u => u.Id == _userId);
            _employee = _user?.Employee;

            _profile = _context.UserProfiles.FirstOrDefault(p => p.UserId == _userId) ?? new UserProfile
            {
                UserId = _userId,
                FullName = _employee?.FullName ?? string.Empty,
                Phone = string.Empty,
                Address = string.Empty,
                Nickname = string.Empty
            };

            _preferences = _context.UserPreferences.FirstOrDefault(p => p.UserId == _userId) ?? new UserPreference
            {
                UserId = _userId
            };

            Positions.Clear();
            foreach (var pos in _context.Positions.OrderBy(p => p.Name))
            {
                Positions.Add(new SelectablePosition(pos.Id, pos.Name));
            }

            FullName = string.IsNullOrWhiteSpace(_profile.FullName) ? _employee?.FullName ?? string.Empty : _profile.FullName;
            Nickname = _profile.Nickname;
            Email = _user?.Email ?? string.Empty;
            Phone = _profile.Phone;
            Address = _profile.Address;
            EmergencyContactName = _profile.EmergencyContactName;
            EmergencyContactPhone = _profile.EmergencyContactPhone;

            PhotoPath = _profile.PhotoPath;
            PhotoImage = BuildImage(PhotoPath);

            SelectedShiftBlock = _preferences.PreferredShiftBlock;
            ParseDaysOff(_preferences.PreferredDaysOff);
            ApplySelectedPositions(_preferences.PreferredPositions);
            ApplyNotificationPrefs(_preferences.NotificationTypes, _preferences.NotificationChannels);

            DefaultView = _preferences.DefaultView;
            ReportFormat = _preferences.ReportFormat;
            ApprovalSignature = _preferences.ApprovalSignature;
            AutoNotifyOnApproval = _preferences.AutoNotifyOnApproval;

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }

        private void SaveProfile()
        {
            if (_user == null)
            {
                MessageBox.Show("Unable to load profile.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _profile.UserId = _userId;
            _profile.FullName = FullName.Trim();
            _profile.Nickname = Nickname.Trim();
            _profile.Phone = Phone.Trim();
            _profile.Address = Address.Trim();
            _profile.EmergencyContactName = EmergencyContactName.Trim();
            _profile.EmergencyContactPhone = EmergencyContactPhone.Trim();
            _profile.PhotoPath = PhotoPath.Trim();
            _profile.UpdatedAt = DateTime.Now;

            if (_profile.Id == 0)
            {
                _context.UserProfiles.Add(_profile);
            }
            else
            {
                _context.UserProfiles.Update(_profile);
            }

            if (IsManager)
            {
                _user.Email = Email.Trim();
                _context.Users.Update(_user);
            }

            if (_employee != null && !string.IsNullOrWhiteSpace(FullName))
            {
                _employee.FullName = FullName.Trim();
                _context.Employees.Update(_employee);
            }

            _preferences.UserId = _userId;
            _preferences.PreferredShiftBlock = SelectedShiftBlock;
            _preferences.PreferredDaysOff = BuildDaysOff();
            _preferences.PreferredPositions = BuildPositions();
            _preferences.NotificationTypes = BuildNotificationTypes();
            _preferences.NotificationChannels = BuildNotificationChannels();
            _preferences.DefaultView = DefaultView;
            _preferences.ReportFormat = ReportFormat;
            _preferences.ApprovalSignature = ApprovalSignature.Trim();
            _preferences.AutoNotifyOnApproval = AutoNotifyOnApproval;
            _preferences.UpdatedAt = DateTime.Now;

            if (_preferences.Id == 0)
            {
                _context.UserPreferences.Add(_preferences);
            }
            else
            {
                _context.UserPreferences.Update(_preferences);
            }

            _context.SaveChanges();
            MessageBox.Show("Profile updated successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            ProfileUpdated?.Invoke();
        }

        private void ChangePassword()
        {
            if (_user == null)
            {
                MessageBox.Show("Unable to load account.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("Please fill out all password fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("New password and confirmation do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, _user.PasswordHash))
            {
                MessageBox.Show("Current password is incorrect.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            _user.UpdatedAt = DateTime.Now;
            _context.Users.Update(_user);
            _context.SaveChanges();

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            MessageBox.Show("Password updated successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowsePhoto()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Select Profile Photo"
            };

            if (dialog.ShowDialog() != true)
                return;

            var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Profiles");
            Directory.CreateDirectory(targetDir);

            var fileName = $"profile_{_userId}{Path.GetExtension(dialog.FileName)}";
            var targetPath = Path.Combine(targetDir, fileName);

            File.Copy(dialog.FileName, targetPath, true);
            PhotoPath = targetPath;
            PhotoImage = BuildImage(targetPath);
        }

        private ImageSource? BuildImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void ParseDaysOff(string days)
        {
            var tokens = (days ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            IsMondayOff = tokens.Contains("Mon");
            IsTuesdayOff = tokens.Contains("Tue");
            IsWednesdayOff = tokens.Contains("Wed");
            IsThursdayOff = tokens.Contains("Thu");
            IsFridayOff = tokens.Contains("Fri");
            IsSaturdayOff = tokens.Contains("Sat");
            IsSundayOff = tokens.Contains("Sun");
        }

        private string BuildDaysOff()
        {
            var list = new[]
            {
                (IsMondayOff, "Mon"),
                (IsTuesdayOff, "Tue"),
                (IsWednesdayOff, "Wed"),
                (IsThursdayOff, "Thu"),
                (IsFridayOff, "Fri"),
                (IsSaturdayOff, "Sat"),
                (IsSundayOff, "Sun")
            }.Where(x => x.Item1).Select(x => x.Item2);

            return string.Join(",", list);
        }

        private void ApplySelectedPositions(string data)
        {
            var ids = (data ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pos in Positions)
            {
                pos.IsSelected = ids.Contains(pos.Id.ToString());
            }
        }

        private string BuildPositions()
        {
            var ids = Positions.Where(p => p.IsSelected).Select(p => p.Id.ToString());
            return string.Join(",", ids);
        }

        private void ApplyNotificationPrefs(string types, string channels)
        {
            var typeTokens = (types ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var channelTokens = (channels ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            NotifyLeave = typeTokens.Contains("Leave");
            NotifyShift = typeTokens.Contains("Shift");
            NotifyAnnouncement = typeTokens.Contains("Announcement");

            NotifyInApp = channelTokens.Contains("InApp");
            NotifyEmail = channelTokens.Contains("Email");
        }

        private string BuildNotificationTypes()
        {
            var list = new[]
            {
                (NotifyLeave, "Leave"),
                (NotifyShift, "Shift"),
                (NotifyAnnouncement, "Announcement")
            }.Where(x => x.Item1).Select(x => x.Item2);

            return string.Join(",", list);
        }

        private string BuildNotificationChannels()
        {
            var list = new[]
            {
                (NotifyInApp, "InApp"),
                (NotifyEmail, "Email")
            }.Where(x => x.Item1).Select(x => x.Item2);

            return string.Join(",", list);
        }

        private void LoadImportConfiguration()
        {
            var importPreset = MunicipalityImportConnectionSettingsService.Load();
            ImportSourceServer = importPreset.Server;
            ImportSourcePortText = importPreset.Port.ToString();
            ImportSourceDatabase = importPreset.Database;
            ImportSourceUsername = importPreset.Username;
            ImportSourcePassword = importPreset.Password;

            var settings = ConnectionSettingsService.Load();
            var localPreset = settings.GetPreset("Local");
            LocalImportTargetSummary = $"{localPreset.DisplayName}: {localPreset.Server}:{localPreset.Port} / {localPreset.Database} -> BeneficiaryStaging";
            TableReviewSummary = "No staged beneficiaries loaded yet.";
            SelectedTableColumnsSummary = string.Empty;
            TablePreviewView = null;
            SetImportNeutralStatus("Use the CRS connection details, test them, then import beneficiaries into local staging.");
        }

        private void LoadBackupConfiguration()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);

            BackupTargetSummary = $"{preset.DisplayName}: {preset.Server}:{preset.Port} / {preset.Database} (selected preset: {settings.SelectedPreset})";
            SetBackupNeutralStatus("Create a database backup of the current app preset or restore an existing archive into it.");
        }

        private async Task ExecuteTestImportConnectionAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy)
            {
                return;
            }

            if (!TryBuildImportSourcePreset(out var preset, out var validationMessage))
            {
                SetImportErrorStatus(validationMessage);
                return;
            }

            IsImportBusy = true;
            SetImportNeutralStatus("Testing CRS source connection...");

            try
            {
                var result = await ConnectionSettingsService.TestConnectionAsync(preset);
                if (result.IsSuccess)
                {
                    SetImportSuccessStatus(result.Message);
                }
                else
                {
                    SetImportErrorStatus(result.Message);
                }
            }
            finally
            {
                IsImportBusy = false;
            }
        }

        private void ExecuteSaveImportConnection()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy)
            {
                return;
            }

            if (!TryBuildImportSourcePreset(out var preset, out var validationMessage))
            {
                SetImportErrorStatus(validationMessage);
                return;
            }

            MunicipalityImportConnectionSettingsService.Save(preset);
            SetImportSuccessStatus("CRS import connection saved.");
        }

        private async Task ExecuteImportBeneficiariesAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy)
            {
                return;
            }

            if (!TryBuildImportSourcePreset(out var preset, out var validationMessage))
            {
                SetImportErrorStatus(validationMessage);
                return;
            }

            var confirm = MessageBox.Show(
                "Import beneficiaries from the Civil Registry System into local staging?\n\n" +
                "New rows will be inserted into BeneficiaryStaging as Pending.\n" +
                "Existing Civil Registry IDs will be skipped.",
                "Import CRS Beneficiaries",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsImportBusy = true;
            SetImportNeutralStatus("Importing beneficiaries from CRS...");

            try
            {
                var result = await CrsBeneficiaryImportService.ImportPendingAsync(preset);
                if (result.IsSuccess)
                {
                    MunicipalityImportConnectionSettingsService.Save(preset);
                    LoadStagingPreview();
                    SetImportSuccessStatus(result.Message);
                }
                else
                {
                    SetImportErrorStatus(result.Message);
                }
            }
            finally
            {
                IsImportBusy = false;
            }
        }

        private async Task ExecuteCreateBackupAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy)
            {
                return;
            }

            LoadBackupConfiguration();
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);

            var dialog = new SaveFileDialog
            {
                Filter = "ASM Backup Archive (*.asmbak)|*.asmbak",
                Title = "Create Database Backup",
                FileName = $"{settings.SelectedPreset.ToLowerInvariant()}-{preset.Database}-{DateTime.Now:yyyyMMdd-HHmmss}.asmbak"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            IsBackupBusy = true;
            SetBackupNeutralStatus($"Creating a backup from `{preset.Database}`...");

            try
            {
                var result = await LocalBackupService.CreateBackupAsync(dialog.FileName);
                if (result.IsSuccess && result.Manifest != null)
                {
                    LastBackupFilePath = dialog.FileName;
                    LastBackupSummary = BuildBackupSummary("Created", result.Manifest);
                    SetBackupSuccessStatus(result.Message);
                }
                else
                {
                    SetBackupErrorStatus(result.Message);
                }
            }
            finally
            {
                IsBackupBusy = false;
            }
        }

        private async Task ExecuteRestoreBackupAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy)
            {
                return;
            }

            LoadBackupConfiguration();

            var dialog = new OpenFileDialog
            {
                Filter = "ASM Backup Archive (*.asmbak)|*.asmbak",
                Title = "Restore Database Backup",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var manifest = await LocalBackupService.ReadManifestAsync(dialog.FileName);
            if (manifest == null)
            {
                SetBackupErrorStatus("The selected file is not a valid backup archive.");
                return;
            }

            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            var confirm = MessageBox.Show(
                "Restore this backup into the current app database?\n\n" +
                $"Target preset: {settings.SelectedPreset}\n" +
                $"Target database: {preset.Database}\n" +
                $"Backup database: {manifest.Database}\n" +
                $"Created: {manifest.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                $"Tables: {manifest.IncludedTables.Count}\n" +
                $"Rows: {manifest.TotalRows}\n\n" +
                "This replaces current application rows in the selected preset.\n" +
                "Fingerprint templates, local config files, and image files are not restored by this archive.",
                "Restore Database Backup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBackupBusy = true;
            SetBackupNeutralStatus($"Restoring backup into `{preset.Database}`...");

            try
            {
                var result = await LocalBackupService.RestoreBackupAsync(dialog.FileName);
                if (result.IsSuccess && result.Manifest != null)
                {
                    _context.ChangeTracker.Clear();
                    LoadProfile();
                    LoadStagingPreview();
                    LastBackupFilePath = dialog.FileName;
                    LastBackupSummary = BuildBackupSummary("Restored", result.Manifest);
                    SetBackupSuccessStatus(result.Message);
                    ProfileUpdated?.Invoke();
                }
                else
                {
                    SetBackupErrorStatus(result.Message);
                }
            }
            finally
            {
                IsBackupBusy = false;
            }
        }

        private void LoadStagingPreview()
        {
            using var previewContext = new AppDbContext();

            var rows = previewContext.BeneficiaryStaging
                .AsNoTracking()
                .OrderByDescending(row => row.ImportedAt)
                .Take(50)
                .ToList();

            var totalCount = previewContext.BeneficiaryStaging.Count();
            var pendingCount = previewContext.BeneficiaryStaging.Count(row => row.VerificationStatus == VerificationStatus.Pending);
            var approvedCount = previewContext.BeneficiaryStaging.Count(row => row.VerificationStatus == VerificationStatus.Approved);
            var rejectedCount = previewContext.BeneficiaryStaging.Count(row => row.VerificationStatus == VerificationStatus.Rejected);

            TableReviewSummary = totalCount == 0
                ? "BeneficiaryStaging is empty."
                : $"Local staging records: {totalCount} | Pending: {pendingCount} | Approved: {approvedCount} | Rejected: {rejectedCount}";
            SelectedTableColumnsSummary = totalCount == 0
                ? "Run the CRS import to populate local staging."
                : "Showing the 50 most recently imported staging records.";

            var table = new DataTable();
            table.Columns.Add("Full Name");
            table.Columns.Add("Civil Registry ID");
            table.Columns.Add("Address");
            table.Columns.Add("Sex");
            table.Columns.Add("Age");
            table.Columns.Add("PWD");
            table.Columns.Add("Senior");
            table.Columns.Add("Status");
            table.Columns.Add("Imported At");

            foreach (var row in rows)
            {
                table.Rows.Add(
                    row.FullName ?? string.Empty,
                    row.CivilRegistryId ?? string.Empty,
                    row.Address ?? string.Empty,
                    row.Sex ?? string.Empty,
                    row.Age ?? string.Empty,
                    row.IsPwd ? "Yes" : "No",
                    row.IsSenior ? "Yes" : "No",
                    row.VerificationStatus.ToString(),
                    row.ImportedAt.ToString("yyyy-MM-dd HH:mm"));
            }

            TablePreviewView = table.DefaultView;
        }

        private async Task LoadRemoteTablesAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy)
            {
                return;
            }

            if (!TryBuildImportSourcePreset(out var preset, out var validationMessage))
            {
                SetImportErrorStatus(validationMessage);
                return;
            }

            IsImportBusy = true;
            SetImportNeutralStatus("Loading remote tables...");

            try
            {
                var tables = await DatabaseTableReviewService.ListTablesAsync(preset);
                RemoteTables.Clear();
                foreach (var table in tables)
                {
                    RemoteTables.Add(table);
                }

                if (!RemoteTables.Contains(SelectedRemoteTable))
                {
                    SelectedRemoteTable = RemoteTables.FirstOrDefault() ?? string.Empty;
                }

                TableReviewSummary = RemoteTables.Count == 0
                    ? "No tables were found in the selected remote schema."
                    : $"Loaded {RemoteTables.Count} remote table(s).";
                SetImportSuccessStatus($"Loaded {RemoteTables.Count} remote table(s).");
            }
            catch (Exception ex)
            {
                SetImportErrorStatus($"Unable to load remote tables: {ex.Message}");
            }
            finally
            {
                IsImportBusy = false;
            }
        }

        private async Task ReviewSelectedTableAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy || string.IsNullOrWhiteSpace(SelectedRemoteTable))
            {
                return;
            }

            if (!TryBuildImportSourcePreset(out var sourcePreset, out var validationMessage))
            {
                SetImportErrorStatus(validationMessage);
                return;
            }

            var localPreset = ConnectionSettingsService.Load().GetPreset("Local");
            IsImportBusy = true;
            SetImportNeutralStatus($"Reviewing `{SelectedRemoteTable}`...");

            try
            {
                var review = await DatabaseTableReviewService.ReviewTableAsync(sourcePreset, localPreset, SelectedRemoteTable);

                if (!review.ExistsInRemote)
                {
                    TableReviewSummary = $"Remote table `{SelectedRemoteTable}` was not found.";
                    SelectedTableColumnsSummary = string.Empty;
                    TablePreviewView = null;
                    SetImportErrorStatus(TableReviewSummary);
                    return;
                }

                TableReviewSummary = review.ExistsInLocal
                    ? $"Remote rows: {review.RemoteRowCount} | Local rows: {review.LocalRowCount} | Local table exists."
                    : $"Remote rows: {review.RemoteRowCount} | Local table missing. Sync will create a local copy.";
                SelectedTableColumnsSummary = review.ColumnNames.Count == 0
                    ? string.Empty
                    : $"Columns: {string.Join(", ", review.ColumnNames)}";
                TablePreviewView = review.PreviewRows.DefaultView;
                SetImportSuccessStatus($"Reviewed remote table `{SelectedRemoteTable}`.");
            }
            finally
            {
                IsImportBusy = false;
            }
        }

        private bool TryBuildImportSourcePreset(out DatabaseConnectionPreset preset, out string validationMessage)
        {
            preset = new DatabaseConnectionPreset();
            validationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(ImportSourceServer))
            {
                validationMessage = "Enter the Hostinger/MySQL server name used in Workbench.";
                return false;
            }

            if (!int.TryParse(ImportSourcePortText, out var port) || port <= 0 || port > 65535)
            {
                validationMessage = "Enter a valid MySQL port between 1 and 65535.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ImportSourceDatabase))
            {
                validationMessage = "Enter the CRS source database name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ImportSourceUsername))
            {
                validationMessage = "Enter the source username or email.";
                return false;
            }

            preset = new DatabaseConnectionPreset
            {
                DisplayName = "CRS Import Source",
                Server = ImportSourceServer.Trim(),
                Port = port,
                Database = ImportSourceDatabase.Trim(),
                Username = ImportSourceUsername.Trim(),
                Password = ImportSourcePassword
            };

            return true;
        }

        private async Task SyncSelectedTableToLocalAsync()
        {
            if (!CanManageSystemImport || IsImportBusy || IsBackupBusy || string.IsNullOrWhiteSpace(SelectedRemoteTable))
            {
                return;
            }

            if (!TryBuildImportSourcePreset(out var sourcePreset, out var validationMessage))
            {
                SetImportErrorStatus(validationMessage);
                return;
            }

            var settings = ConnectionSettingsService.Load();
            var localPreset = settings.GetPreset("Local");
            var tableName = SelectedRemoteTable;

            IsImportBusy = true;
            SetImportNeutralStatus($"Syncing `{tableName}` into Local...");

            try
            {
                var result = await DatabaseTableReviewService.SyncTableToLocalAsync(sourcePreset, localPreset, tableName);
                if (result.IsSuccess)
                {
                    SetImportSuccessStatus(result.Message);
                }
                else
                {
                    SetImportErrorStatus(result.Message);
                }
            }
            finally
            {
                IsImportBusy = false;
            }

            await ReviewSelectedTableAsync();
        }

        private void SetImportNeutralStatus(string message)
        {
            ImportStatusMessage = message;
            ImportStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void SetImportSuccessStatus(string message)
        {
            ImportStatusMessage = message;
            ImportStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7A4A"));
        }

        private void SetImportErrorStatus(string message)
        {
            ImportStatusMessage = message;
            ImportStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
        }

        private string BuildBackupSummary(string action, BackupManifest manifest)
        {
            var excludedSummary = manifest.ExcludedTables.Count == 0
                ? "No excluded tables."
                : $"Excluded: {string.Join(", ", manifest.ExcludedTables)}";

            return $"{action} {manifest.CreatedAt:yyyy-MM-dd HH:mm} | Database: {manifest.Database} | Tables: {manifest.IncludedTables.Count} | Rows: {manifest.TotalRows} | {excludedSummary}";
        }

        private void SetBackupNeutralStatus(string message)
        {
            BackupStatusMessage = message;
            BackupStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void SetBackupSuccessStatus(string message)
        {
            BackupStatusMessage = message;
            BackupStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7A4A"));
        }

        private void SetBackupErrorStatus(string message)
        {
            BackupStatusMessage = message;
            BackupStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
        }
    }

    public class SelectablePosition : ObservableObject
    {
        public int Id { get; }
        public string Name { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SelectablePosition(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

}

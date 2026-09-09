using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Net.Mail;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class LanDatabaseItemViewModel : ObservableObject
    {
        private string _displayName = string.Empty;
        private string _server = string.Empty;
        private int _port = 3306;
        private string _database = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _isEnabled = true;
        private bool _isSelected;

        public string Key { get; init; } = string.Empty;
        public bool IsDefault => string.Equals(Key, ConnectionSettingsService.LanPresetKey, StringComparison.OrdinalIgnoreCase);

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string Server
        {
            get => _server;
            set
            {
                if (SetProperty(ref _server, value))
                {
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                if (SetProperty(ref _port, value))
                {
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public string Database
        {
            get => _database;
            set
            {
                if (SetProperty(ref _database, value))
                {
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    OnPropertyChanged(nameof(StatusLabel));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Summary => string.IsNullOrWhiteSpace(Server)
            ? "Not configured yet"
            : $"{Server}:{Port} / {Database}";

        public string StatusLabel => IsEnabled ? "Active (Running)" : "Inactive (Disabled)";

        public Brush StatusBrush => IsEnabled
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));

        public Visibility DeleteVisibility => IsDefault ? Visibility.Collapsed : Visibility.Visible;

        private string _connectionStatusText = "Untested";
        private Brush _connectionStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        private bool _isTesting;

        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set => SetProperty(ref _connectionStatusText, value);
        }

        public Brush ConnectionStatusBrush
        {
            get => _connectionStatusBrush;
            set => SetProperty(ref _connectionStatusBrush, value);
        }

        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }

        public void SetTestingStatus()
        {
            IsTesting = true;
            ConnectionStatusText = "Testing...";
            ConnectionStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        }

        public void SetSuccessStatus(string message)
        {
            IsTesting = false;
            ConnectionStatusText = message;
            ConnectionStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
        }

        public void SetFailedStatus(string message)
        {
            IsTesting = false;
            ConnectionStatusText = message;
            ConnectionStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BE123C"));
        }

        public void ResetStatus()
        {
            IsTesting = false;
            ConnectionStatusText = "Untested";
            ConnectionStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        }
    }

    public sealed class ConnectionSettingsViewModel : ObservableObject
    {
        private const string LocalPresetKey = "Local";
        private const string LanPresetKey = "Lan";
        private const string RemotePresetKey = "Remote";
        private const string AppDatabaseSavePurposeLabel = "app database settings save";
        private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(45);
        private const int OtpMaxAttempts = 3;

        private readonly bool _selectionOnly;
        private readonly bool _requireOtpOnSave;
        private readonly ConnectionSettingsModel _settings;
        private readonly ConnectionSettingsModel _originalSettings;
        private readonly RelayCommand _testConnectionCommand;
        private readonly RelayCommand _saveCommand;
        private readonly RelayCommand _sendSaveOtpCommand;
        private readonly RelayCommand _verifySaveOtpCommand;
        private readonly RelayCommand _resendSaveOtpCommand;
        private readonly Action<ConnectionSettingsModel> _saveSettings;
        private readonly Func<string, string, string, TimeSpan, Task<OtpEmailSendResult>> _sendOtpAsync;
        private readonly string _officialEmail;

        private string _selectedPresetKey = string.Empty;
        private string _server = string.Empty;
        private string _portText = "3306";
        private string _database = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _statusMessage = string.Empty;
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private bool _isBusy;
        private bool _showSaveOtpPanel;
        private string _saveOtpCode = string.Empty;
        private string _saveOtpStatusMessage = "OTP is only required when saving app database changes.";
        private Brush _saveOtpStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private OtpChallengeSession? _saveOtpSession;
        private bool _hasSaveOtpAuthorization;
        private bool _savePendingAfterOtp;

        public event Action<bool?>? CloseRequested;

        public string SelectedPresetKey
        {
            get => _selectedPresetKey;
            private set
            {
                if (SetProperty(ref _selectedPresetKey, value))
                {
                    OnPropertyChanged(nameof(IsLocalSelected));
                    OnPropertyChanged(nameof(IsLanSelected));
                    OnPropertyChanged(nameof(IsRemoteSelected));
                    OnPropertyChanged(nameof(ShowCredentialEditor));
                    OnPropertyChanged(nameof(CanEditSelectedPresetCredentials));
                    OnPropertyChanged(nameof(IsPresetCredentialsReadOnly));
                    OnPropertyChanged(nameof(ShowLanCredentialEditor));
                    OnPropertyChanged(nameof(ShowFixedPresetNotice));
                    OnPropertyChanged(nameof(CurrentPresetDisplayName));
                    OnPropertyChanged(nameof(PresetHelpText));
                }
            }
        }

        public string Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }

        public string PortText
        {
            get => _portText;
            set => SetProperty(ref _portText, value);
        }

        public string Database
        {
            get => _database;
            set => SetProperty(ref _database, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    OnPropertyChanged(nameof(PasswordStatusText));
                }
            }
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

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _testConnectionCommand.RaiseCanExecuteChanged();
                    _saveCommand.RaiseCanExecuteChanged();
                    RaiseSaveOtpCommandStates();
                }
            }
        }

        public bool IsSelectionOnly => _selectionOnly;
        public bool RequiresOtpOnSave => !_selectionOnly && _requireOtpOnSave;
        public bool IsLocalSelected => string.Equals(SelectedPresetKey, LocalPresetKey, StringComparison.OrdinalIgnoreCase);
        public bool IsLanSelected => ConnectionSettingsService.IsLanPresetKey(SelectedPresetKey);
        public bool IsRemoteSelected => string.Equals(SelectedPresetKey, RemotePresetKey, StringComparison.OrdinalIgnoreCase);
        public bool CanEditSelectedPresetCredentials => IsSelectionOnly
            ? IsLanSelected
            : IsLanSelected || IsRemoteSelected;
        public bool ShowCredentialEditor => CanEditSelectedPresetCredentials;
        public bool ShowLanCredentialEditor => IsLanSelected;
        public bool ShowFixedPresetNotice => !CanEditSelectedPresetCredentials;
        public bool IsPresetCredentialsReadOnly => !CanEditSelectedPresetCredentials;
        public string CurrentPresetDisplayName => string.IsNullOrWhiteSpace(SelectedPresetKey)
            ? string.Empty
            : _settings.GetPreset(SelectedPresetKey).DisplayName;
        public string HeaderDescription => IsSelectionOnly
            ? "Choose which app database is active for login, startup, dashboard data, and snapshots. Network (LAN) can be configured here before sign-in so users can point the app to an available LAN server anytime."
            : "Select the active app database here. Local stays fixed by the shipped app configuration, while Network (LAN) and Remote can be updated on this screen.";
        public string FooterDescription => IsSelectionOnly
            ? "Use Test Connection to verify the selected preset. If Network (LAN) is selected, update the host and database fields here, then save to return to the login form."
            : "Save to apply the active app database. Network (LAN) and Remote credentials are stored in your Windows user profile, while Local stays fixed.";
        public string SaveButtonText => IsSelectionOnly ? "SAVE AND CONTINUE" : "SAVE SETTINGS";
        public string PasswordStatusText => string.IsNullOrWhiteSpace(Password) ? "Not configured" : "Configured";
        public string PresetHelpText => IsSelectionOnly
            ? IsLanSelected
                ? "Network (LAN) is editable before sign-in. Enter the LAN server details here, test the connection, then save and continue."
                : $"{CurrentPresetDisplayName} is selection-only before sign-in. Switch to Network (LAN) if you need to configure connection details here."
            : CanEditSelectedPresetCredentials
                ? $"Update the {CurrentPresetDisplayName} host and database credentials here. This preset is stored in your Windows user profile."
                : $"{CurrentPresetDisplayName} is fixed by the shipped app configuration and is read-only here. Select Network (LAN) or Remote if you need editable connection details.";

        public bool ShowSaveOtpPanel
        {
            get => _showSaveOtpPanel;
            private set
            {
                if (SetProperty(ref _showSaveOtpPanel, value))
                {
                    RaiseSaveOtpCommandStates();
                }
            }
        }

        public string SaveOtpCode
        {
            get => _saveOtpCode;
            set
            {
                if (SetProperty(ref _saveOtpCode, value))
                {
                    _verifySaveOtpCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SaveOtpStatusMessage
        {
            get => _saveOtpStatusMessage;
            private set => SetProperty(ref _saveOtpStatusMessage, value);
        }

        public Brush SaveOtpStatusBrush
        {
            get => _saveOtpStatusBrush;
            private set => SetProperty(ref _saveOtpStatusBrush, value);
        }

        public string SaveOtpPromptText => HasValidOfficialEmailForOtp
            ? $"OTP is only required if you save changes. Send the code to {OfficialEmailMask}, then verify it before the save continues."
            : "Save a valid Official Email in System Profile first before saving app database changes that require OTP.";

        public string SaveOtpResendButtonText => BuildResendButtonText(_saveOtpSession);

        public bool CanSendSaveOtp =>
            !IsBusy
            && RequiresOtpOnSave
            && ShowSaveOtpPanel
            && HasValidOfficialEmailForOtp
            && _saveOtpSession == null;

        public bool CanResendSaveOtp =>
            !IsBusy
            && RequiresOtpOnSave
            && ShowSaveOtpPanel
            && HasValidOfficialEmailForOtp
            && _saveOtpSession != null
            && OtpChallengeService.CanResend(_saveOtpSession, DateTimeOffset.UtcNow);

        public bool CanVerifySaveOtp =>
            !IsBusy
            && RequiresOtpOnSave
            && ShowSaveOtpPanel
            && _saveOtpSession != null
            && !string.IsNullOrWhiteSpace(SaveOtpCode);

        public bool HasValidOfficialEmailForOtp => IsValidEmail(_officialEmail);

        public string OfficialEmailMask => HasValidOfficialEmailForOtp
            ? MaskEmailAddress(_officialEmail)
            : "Official Email not configured";

        public ICommand SelectLocalPresetCommand { get; }
        public ICommand SelectLanPresetCommand { get; }
        public ICommand SelectRemotePresetCommand { get; }
        public ICommand AddLanDatabaseCommand { get; }
        public ICommand RemoveLanDatabaseCommand { get; }
        public ICommand SelectSpecificLanDatabaseCommand { get; }
        public ICommand TestSpecificLanDatabaseCommand { get; }
        public ICommand TestConnectionCommand => _testConnectionCommand;
        public ICommand SaveCommand => _saveCommand;
        public ICommand CancelCommand { get; }
        public ICommand SendSaveOtpCommand => _sendSaveOtpCommand;
        public ICommand VerifySaveOtpCommand => _verifySaveOtpCommand;
        public ICommand ResendSaveOtpCommand => _resendSaveOtpCommand;
        public ObservableCollection<LanDatabaseItemViewModel> LanDatabases { get; } = new();

        public ConnectionSettingsViewModel(
            bool selectionOnly = true,
            bool requireOtpOnSave = false,
            ConnectionSettingsModel? initialSettings = null,
            string? officialEmail = null,
            Action<ConnectionSettingsModel>? saveSettings = null,
            Func<string, string, string, TimeSpan, Task<OtpEmailSendResult>>? sendOtpAsync = null)
        {
            _selectionOnly = selectionOnly;
            _requireOtpOnSave = requireOtpOnSave;

            var loadedSettings = CloneSettings(initialSettings ?? ConnectionSettingsService.Load());
            _settings = CloneSettings(loadedSettings);
            _originalSettings = CloneSettings(loadedSettings);
            _officialEmail = officialEmail ?? SystemProfileSettingsService.Load().Email;
            _saveSettings = saveSettings ?? ConnectionSettingsService.Save;
            _sendOtpAsync = sendOtpAsync ?? OtpEmailService.SendOtpAsync;

            SelectLocalPresetCommand = new RelayCommand(_ => SelectPreset(LocalPresetKey));
            SelectLanPresetCommand = new RelayCommand(_ => SelectPreset(LanPresetKey));
            SelectRemotePresetCommand = new RelayCommand(_ => SelectPreset(RemotePresetKey));
            AddLanDatabaseCommand = new RelayCommand(_ => AddLanDatabase());
            RemoveLanDatabaseCommand = new RelayCommand(p => RemoveLanDatabase(p as LanDatabaseItemViewModel));
            SelectSpecificLanDatabaseCommand = new RelayCommand(p => SelectSpecificLanDatabase(p as LanDatabaseItemViewModel));
            TestSpecificLanDatabaseCommand = new RelayCommand(async p => await ExecuteTestSpecificLanDatabaseAsync(p as LanDatabaseItemViewModel), _ => !IsBusy);
            _testConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync(), _ => !IsBusy);
            _saveCommand = new RelayCommand(_ => ExecuteSave(), _ => !IsBusy);
            _sendSaveOtpCommand = new RelayCommand(async _ => await SendSaveOtpAsync(isResend: false), _ => CanSendSaveOtp);
            _verifySaveOtpCommand = new RelayCommand(_ => VerifySaveOtp(), _ => CanVerifySaveOtp);
            _resendSaveOtpCommand = new RelayCommand(async _ => await SendSaveOtpAsync(isResend: true), _ => CanResendSaveOtp);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

            InitializeLanDatabases();
            LoadPreset(_settings.SelectedPreset);
            SetNeutralStatus(BuildPresetLoadedMessage());
        }

        private void SelectPreset(string presetKey)
        {
            if (IsBusy || string.Equals(SelectedPresetKey, presetKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (CanEditSelectedPresetCredentials)
            {
                TryUpdateSelectedPreset(showValidationErrors: false);
            }

            _settings.SelectedPreset = presetKey;
            LoadPreset(presetKey);
            SetNeutralStatus(BuildPresetLoadedMessage());
        }

        private async Task ExecuteTestSpecificLanDatabaseAsync(LanDatabaseItemViewModel? item)
        {
            if (IsBusy || item == null)
            {
                return;
            }

            if (string.Equals(SelectedPresetKey, item.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryUpdateSelectedPreset(showValidationErrors: true))
                {
                    return;
                }
            }

            var preset = new DatabaseConnectionPreset
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                Server = item.Server,
                Port = item.Port,
                Database = item.Database,
                Username = item.Username,
                Password = item.Password
            };

            if (string.IsNullOrWhiteSpace(preset.Server))
            {
                item.SetFailedStatus("Missing server host");
                SetErrorStatus($"{item.DisplayName} is missing a server or host name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(preset.Database))
            {
                item.SetFailedStatus("Missing database name");
                SetErrorStatus($"{item.DisplayName} is missing a database name.");
                return;
            }

            IsBusy = true;
            item.SetTestingStatus();
            SetNeutralStatus($"Testing connection to {item.DisplayName} ({preset.Server}:{preset.Port})...");

            try
            {
                var result = await ConnectionSettingsService.TestConnectionAsync(preset, timeoutSeconds: 5);
                if (result.IsSuccess)
                {
                    item.SetSuccessStatus("Connected (OK)");
                    SetSuccessStatus($"[{item.DisplayName}] {result.Message}");
                }
                else
                {
                    item.SetFailedStatus(result.Message);
                    SetErrorStatus($"[{item.DisplayName}] {result.Message}");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteTestConnectionAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (IsLanSelected)
            {
                if (CanEditSelectedPresetCredentials)
                {
                    TryUpdateSelectedPreset(showValidationErrors: false);
                }

                var itemsToTest = LanDatabases.ToList();
                if (itemsToTest.Count == 0)
                {
                    SetErrorStatus("No Network LAN databases configured.");
                    return;
                }

                IsBusy = true;
                var successCount = 0;
                var failCount = 0;

                try
                {
                    for (int i = 0; i < itemsToTest.Count; i++)
                    {
                        var item = itemsToTest[i];
                        item.SetTestingStatus();
                        SetNeutralStatus($"Testing LAN database {i + 1} of {itemsToTest.Count}: {item.DisplayName} ({item.Server}:{item.Port})...");

                        var preset = new DatabaseConnectionPreset
                        {
                            Key = item.Key,
                            DisplayName = item.DisplayName,
                            Server = item.Server,
                            Port = item.Port,
                            Database = item.Database,
                            Username = item.Username,
                            Password = item.Password
                        };

                        if (string.IsNullOrWhiteSpace(preset.Server) || string.IsNullOrWhiteSpace(preset.Database))
                        {
                            item.SetFailedStatus("Unconfigured host/database");
                            failCount++;
                            continue;
                        }

                        var result = await ConnectionSettingsService.TestConnectionAsync(preset, timeoutSeconds: 5);
                        if (result.IsSuccess)
                        {
                            item.SetSuccessStatus("Connected (OK)");
                            successCount++;
                        }
                        else
                        {
                            item.SetFailedStatus(result.Message);
                            failCount++;
                        }
                    }

                    if (itemsToTest.Count == 1)
                    {
                        var singleItem = itemsToTest[0];
                        if (singleItem.ConnectionStatusText.Contains("Connected", StringComparison.OrdinalIgnoreCase))
                        {
                            SetSuccessStatus($"[{singleItem.DisplayName}] Connected successfully to {singleItem.Database} on {singleItem.Server}:{singleItem.Port}.");
                        }
                        else
                        {
                            SetErrorStatus($"[{singleItem.DisplayName}] {singleItem.ConnectionStatusText}");
                        }
                    }
                    else if (failCount == 0)
                    {
                        SetSuccessStatus($"All {itemsToTest.Count} LAN databases connected successfully.");
                    }
                    else
                    {
                        SetErrorStatus($"LAN test complete: {successCount} reachable, {failCount} failed/timed out. Check details above.");
                    }
                }
                finally
                {
                    IsBusy = false;
                }

                return;
            }

            if (!TryBuildPresetForTesting(out var singlePreset, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Testing connection to {CurrentPresetDisplayName}...");

            try
            {
                var result = await ConnectionSettingsService.TestConnectionAsync(singlePreset, timeoutSeconds: 5);
                if (result.IsSuccess)
                {
                    SetSuccessStatus(result.Message);
                }
                else
                {
                    SetErrorStatus(result.Message);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteSave()
        {
            if (IsBusy)
            {
                return;
            }

            if (!TryBuildModelForSave(out var modelToSave, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            if (RequiresOtpOnSave && SettingsDiffer(_originalSettings, modelToSave) && !_hasSaveOtpAuthorization)
            {
                _savePendingAfterOtp = true;
                ShowSaveOtpPanel = true;
                SetSaveOtpNeutral("OTP is required only because you changed the app database settings. Send and verify the code before saving.");
                SetNeutralStatus("OTP verification is required before saving the updated app database settings.");
                return;
            }

            ShowSaveOtpPanel = false;
            _savePendingAfterOtp = false;
            _saveSettings(CloneSettings(modelToSave));
            CloseRequested?.Invoke(true);
        }

        private async Task SendSaveOtpAsync(bool isResend)
        {
            ShowSaveOtpPanel = true;

            if (!HasValidOfficialEmailForOtp)
            {
                var message = "Save a valid Official Email in System Profile first before requesting OTP access.";
                SetSaveOtpError(message);
                SetErrorStatus(message);
                return;
            }

            if (isResend && _saveOtpSession != null && !OtpChallengeService.CanResend(_saveOtpSession, DateTimeOffset.UtcNow))
            {
                SetSaveOtpError($"Wait {GetCountdownSeconds(_saveOtpSession.ResendAvailableAtUtc)} second(s) before requesting a new code.");
                return;
            }

            IsBusy = true;

            try
            {
                var issuedCode = OtpChallengeService.IssueCode(
                    AppDatabaseSavePurposeLabel,
                    _officialEmail.Trim(),
                    DateTimeOffset.UtcNow,
                    OtpExpiry,
                    OtpResendCooldown,
                    OtpMaxAttempts);

                var sendResult = await _sendOtpAsync(
                    _officialEmail.Trim(),
                    issuedCode.Code,
                    AppDatabaseSavePurposeLabel,
                    OtpExpiry);

                if (!sendResult.IsSuccess)
                {
                    SetSaveOtpError(sendResult.Message);
                    return;
                }

                _saveOtpSession = issuedCode.Session;
                SaveOtpCode = string.Empty;
                SetSaveOtpSuccess($"OTP sent to {OfficialEmailMask}. The code expires in 3 minutes.");
                RaiseSaveOtpPropertyChanges();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void VerifySaveOtp()
        {
            var result = OtpChallengeService.VerifyCode(_saveOtpSession, SaveOtpCode, DateTimeOffset.UtcNow);
            if (!result.IsSuccess)
            {
                if (result.RequiresNewCode)
                {
                    _saveOtpSession = null;
                }

                SaveOtpCode = string.Empty;
                SetSaveOtpError(result.Message);
                RaiseSaveOtpPropertyChanges();
                return;
            }

            _saveOtpSession = null;
            SaveOtpCode = string.Empty;
            _hasSaveOtpAuthorization = true;
            ShowSaveOtpPanel = false;
            SetSaveOtpSuccess("OTP verified. Saving can continue for this window session.");
            RaiseSaveOtpPropertyChanges();

            if (_savePendingAfterOtp)
            {
                ExecuteSave();
            }
        }

        private void InitializeLanDatabases()
        {
            LanDatabases.Clear();
            foreach (var pair in _settings.GetLanPresets())
            {
                var item = new LanDatabaseItemViewModel
                {
                    Key = pair.Key,
                    DisplayName = string.IsNullOrWhiteSpace(pair.Value.DisplayName) ? GetDefaultDisplayName(pair.Key) : pair.Value.DisplayName,
                    Server = pair.Value.Server,
                    Port = pair.Value.Port,
                    Database = pair.Value.Database,
                    Username = pair.Value.Username,
                    Password = pair.Value.Password,
                    IsEnabled = pair.Value.IsEnabled,
                    IsSelected = string.Equals(SelectedPresetKey, pair.Key, StringComparison.OrdinalIgnoreCase)
                };
                LanDatabases.Add(item);
            }
        }

        private void AddLanDatabase()
        {
            if (IsBusy)
            {
                return;
            }

            if (CanEditSelectedPresetCredentials)
            {
                TryUpdateSelectedPreset(showValidationErrors: false);
            }

            var nextIndex = LanDatabases.Count + 1;
            var key = $"Lan_{Guid.NewGuid():N}";
            var newPreset = new DatabaseConnectionPreset
            {
                Key = key,
                DisplayName = $"Network LAN ({nextIndex})",
                Server = "127.0.0.1",
                Port = 3306,
                Database = "attendance_shifting_db",
                Username = "root",
                Password = string.Empty,
                IsEnabled = true
            };

            _settings.Presets[key] = newPreset;
            var item = new LanDatabaseItemViewModel
            {
                Key = key,
                DisplayName = newPreset.DisplayName,
                Server = newPreset.Server,
                Port = newPreset.Port,
                Database = newPreset.Database,
                Username = newPreset.Username,
                Password = newPreset.Password,
                IsEnabled = newPreset.IsEnabled,
                IsSelected = false
            };
            LanDatabases.Add(item);

            SelectPreset(key);
        }

        private void RemoveLanDatabase(LanDatabaseItemViewModel? item)
        {
            if (IsBusy || item == null || item.IsDefault)
            {
                return;
            }

            _settings.Presets.Remove(item.Key);
            LanDatabases.Remove(item);

            if (string.Equals(SelectedPresetKey, item.Key, StringComparison.OrdinalIgnoreCase))
            {
                SelectPreset(LanPresetKey);
            }
        }

        private void SelectSpecificLanDatabase(LanDatabaseItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            SelectPreset(item.Key);
        }

        private void LoadPreset(string presetKey)
        {
            var preset = _settings.GetPreset(presetKey);
            if (string.IsNullOrWhiteSpace(preset.DisplayName))
            {
                preset.DisplayName = GetDefaultDisplayName(presetKey);
            }

            SelectedPresetKey = presetKey;
            Server = preset.Server;
            PortText = preset.Port.ToString();
            Database = preset.Database;
            Username = preset.Username;
            Password = preset.Password;

            foreach (var lanItem in LanDatabases)
            {
                lanItem.IsSelected = string.Equals(lanItem.Key, presetKey, StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool TryUpdateSelectedPreset(bool showValidationErrors)
        {
            if (string.IsNullOrWhiteSpace(SelectedPresetKey))
            {
                return false;
            }

            if (!TryBuildWorkingPreset(out var preset, out var validationMessage))
            {
                if (showValidationErrors)
                {
                    SetErrorStatus(validationMessage);
                }

                return false;
            }

            var selectedPreset = _settings.GetPreset(SelectedPresetKey);
            selectedPreset.DisplayName = string.IsNullOrWhiteSpace(selectedPreset.DisplayName)
                ? GetDefaultDisplayName(SelectedPresetKey)
                : selectedPreset.DisplayName;
            selectedPreset.Server = preset.Server;
            selectedPreset.Port = preset.Port;
            selectedPreset.Database = preset.Database;
            selectedPreset.Username = preset.Username;
            selectedPreset.Password = preset.Password;

            var matchingLanItem = LanDatabases.FirstOrDefault(l => string.Equals(l.Key, SelectedPresetKey, StringComparison.OrdinalIgnoreCase));
            if (matchingLanItem != null)
            {
                matchingLanItem.Server = preset.Server;
                matchingLanItem.Port = preset.Port;
                matchingLanItem.Database = preset.Database;
                matchingLanItem.Username = preset.Username;
                matchingLanItem.Password = preset.Password;
            }

            return true;
        }

        private bool TryBuildWorkingPreset(out DatabaseConnectionPreset preset, out string validationMessage)
        {
            preset = new DatabaseConnectionPreset();
            validationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Server))
            {
                validationMessage = "Enter a server or host name.";
                return false;
            }

            if (!int.TryParse(PortText, out var port) || port <= 0 || port > 65535)
            {
                validationMessage = "Enter a valid MySQL port between 1 and 65535.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                validationMessage = "Enter a database name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                validationMessage = "Enter a database username.";
                return false;
            }

            preset = new DatabaseConnectionPreset
            {
                DisplayName = GetDefaultDisplayName(SelectedPresetKey),
                Server = Server.Trim(),
                Port = port,
                Database = Database.Trim(),
                Username = Username.Trim(),
                Password = Password
            };

            return true;
        }

        private bool TryBuildModelForSave(out ConnectionSettingsModel model, out string validationMessage)
        {
            model = CloneSettings(_settings);
            validationMessage = string.Empty;

            if (CanEditSelectedPresetCredentials)
            {
                if (!TryBuildWorkingPreset(out var preset, out validationMessage))
                {
                    return false;
                }

                var selectedPreset = model.GetPreset(SelectedPresetKey);
                selectedPreset.DisplayName = string.IsNullOrWhiteSpace(selectedPreset.DisplayName)
                    ? GetDefaultDisplayName(SelectedPresetKey)
                    : selectedPreset.DisplayName;
                selectedPreset.Server = preset.Server;
                selectedPreset.Port = preset.Port;
                selectedPreset.Database = preset.Database;
                selectedPreset.Username = preset.Username;
                selectedPreset.Password = preset.Password;
            }

            foreach (var lanItem in LanDatabases)
            {
                var target = model.GetPreset(lanItem.Key);
                target.Key = lanItem.Key;
                target.DisplayName = lanItem.DisplayName;
                target.IsEnabled = lanItem.IsEnabled;
                if (!string.Equals(lanItem.Key, SelectedPresetKey, StringComparison.OrdinalIgnoreCase))
                {
                    target.Server = lanItem.Server;
                    target.Port = lanItem.Port;
                    target.Database = lanItem.Database;
                    target.Username = lanItem.Username;
                    target.Password = lanItem.Password;
                }
            }

            model.SelectedPreset = SelectedPresetKey;
            return true;
        }

        private bool TryBuildPresetForTesting(out DatabaseConnectionPreset preset, out string validationMessage)
        {
            if (CanEditSelectedPresetCredentials)
            {
                return TryBuildWorkingPreset(out preset, out validationMessage);
            }

            var selectedPreset = _settings.GetPreset(SelectedPresetKey);
            if (string.IsNullOrWhiteSpace(selectedPreset.Server))
            {
                preset = new DatabaseConnectionPreset();
                validationMessage = $"{CurrentPresetDisplayName} is missing a server or host name.";
                return false;
            }

            if (selectedPreset.Port <= 0 || selectedPreset.Port > 65535)
            {
                preset = new DatabaseConnectionPreset();
                validationMessage = $"{CurrentPresetDisplayName} has an invalid MySQL port.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedPreset.Database))
            {
                preset = new DatabaseConnectionPreset();
                validationMessage = $"{CurrentPresetDisplayName} is missing a database name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedPreset.Username))
            {
                preset = new DatabaseConnectionPreset();
                validationMessage = $"{CurrentPresetDisplayName} is missing a database username.";
                return false;
            }

            preset = new DatabaseConnectionPreset
            {
                DisplayName = selectedPreset.DisplayName,
                Server = selectedPreset.Server,
                Port = selectedPreset.Port,
                Database = selectedPreset.Database,
                Username = selectedPreset.Username,
                Password = selectedPreset.Password
            };

            validationMessage = string.Empty;
            return true;
        }

        private static ConnectionSettingsModel CloneSettings(ConnectionSettingsModel source)
        {
            return new ConnectionSettingsModel
            {
                SelectedPreset = source.SelectedPreset,
                Presets = source.Presets.ToDictionary(
                    pair => pair.Key,
                    pair => ClonePreset(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
            };
        }

        private static DatabaseConnectionPreset ClonePreset(DatabaseConnectionPreset source)
        {
            return new DatabaseConnectionPreset
            {
                Key = source.Key,
                DisplayName = source.DisplayName,
                Server = source.Server,
                Port = source.Port,
                Database = source.Database,
                Username = source.Username,
                Password = source.Password,
                IsEnabled = source.IsEnabled
            };
        }

        private static bool SettingsDiffer(ConnectionSettingsModel left, ConnectionSettingsModel right)
        {
            if (!string.Equals(left.SelectedPreset, right.SelectedPreset, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (left.Presets.Count != right.Presets.Count)
            {
                return true;
            }

            foreach (var pair in left.Presets)
            {
                if (!right.Presets.TryGetValue(pair.Key, out var rightPreset))
                {
                    return true;
                }

                if (!PresetsMatch(pair.Value, rightPreset))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PresetsMatch(DatabaseConnectionPreset left, DatabaseConnectionPreset right)
        {
            return string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
                && string.Equals(left.Server, right.Server, StringComparison.Ordinal)
                && left.Port == right.Port
                && string.Equals(left.Database, right.Database, StringComparison.Ordinal)
                && string.Equals(left.Username, right.Username, StringComparison.Ordinal)
                && string.Equals(left.Password, right.Password, StringComparison.Ordinal)
                && left.IsEnabled == right.IsEnabled;
        }

        private static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                _ = new MailAddress(email.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string MaskEmailAddress(string email)
        {
            var trimmed = email.Trim();
            var separatorIndex = trimmed.IndexOf('@');
            if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
            {
                return trimmed;
            }

            var localPart = trimmed[..separatorIndex];
            var domain = trimmed[(separatorIndex + 1)..];
            var visibleCharacters = Math.Min(1, localPart.Length);
            var maskedCharacters = Math.Max(0, localPart.Length - visibleCharacters);
            return $"{localPart[..visibleCharacters]}{new string('*', Math.Max(3, maskedCharacters))}@{domain}";
        }

        private static string BuildResendButtonText(OtpChallengeSession? session)
        {
            if (session == null)
            {
                return "RESEND OTP";
            }

            var remainingSeconds = GetCountdownSeconds(session.ResendAvailableAtUtc);
            return remainingSeconds > 0
                ? $"RESEND OTP ({remainingSeconds}s)"
                : "RESEND OTP";
        }

        private static int GetCountdownSeconds(DateTimeOffset targetUtc)
        {
            return Math.Max(0, (int)Math.Ceiling((targetUtc - DateTimeOffset.UtcNow).TotalSeconds));
        }

        private void RaiseSaveOtpPropertyChanges()
        {
            OnPropertyChanged(nameof(HasValidOfficialEmailForOtp));
            OnPropertyChanged(nameof(OfficialEmailMask));
            OnPropertyChanged(nameof(SaveOtpPromptText));
            OnPropertyChanged(nameof(SaveOtpResendButtonText));
            RaiseSaveOtpCommandStates();
        }

        private void RaiseSaveOtpCommandStates()
        {
            _sendSaveOtpCommand?.RaiseCanExecuteChanged();
            _verifySaveOtpCommand?.RaiseCanExecuteChanged();
            _resendSaveOtpCommand?.RaiseCanExecuteChanged();
        }

        private static string GetDefaultDisplayName(string presetKey)
        {
            if (string.Equals(presetKey, LocalPresetKey, StringComparison.OrdinalIgnoreCase))
            {
                return "Local";
            }

            if (string.Equals(presetKey, LanPresetKey, StringComparison.OrdinalIgnoreCase) || presetKey.StartsWith("Lan_", StringComparison.OrdinalIgnoreCase))
            {
                return "Network (LAN)";
            }

            return "Remote";
        }

        private string BuildPresetLoadedMessage()
        {
            if (IsSelectionOnly)
            {
                return IsLanSelected
                    ? "Network (LAN) app database selected. Update the LAN server details here, test the connection, then save and continue."
                    : $"{CurrentPresetDisplayName} app database selected. Switch to Network (LAN) if you need to edit connection details before sign-in.";
            }

            return CanEditSelectedPresetCredentials
                ? $"{CurrentPresetDisplayName} is selected. You can update its host and database credentials here."
                : $"{CurrentPresetDisplayName} is selected. This preset is fixed by app settings; use Network (LAN) or Remote for editable connection details.";
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7A4A"));
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
        }

        private void SetSaveOtpNeutral(string message)
        {
            SaveOtpStatusMessage = message;
            SaveOtpStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void SetSaveOtpSuccess(string message)
        {
            SaveOtpStatusMessage = message;
            SaveOtpStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7A4A"));
        }

        private void SetSaveOtpError(string message)
        {
            SaveOtpStatusMessage = message;
            SaveOtpStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
        }
    }
}

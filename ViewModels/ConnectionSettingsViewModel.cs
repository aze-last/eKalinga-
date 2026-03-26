using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class ConnectionSettingsViewModel : ObservableObject
    {
        private const string LocalPresetKey = "Local";
        private const string LanPresetKey = "Lan";
        private const string RemotePresetKey = "Remote";

        private readonly bool _selectionOnly;
        private readonly ConnectionSettingsModel _settings;
        private readonly RelayCommand _testConnectionCommand;
        private readonly RelayCommand _saveCommand;

        private string _selectedPresetKey = string.Empty;
        private string _server = string.Empty;
        private string _portText = "3306";
        private string _database = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _statusMessage = string.Empty;
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private bool _isBusy;

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
                }
            }
        }

        public bool IsSelectionOnly => _selectionOnly;
        public bool ShowCredentialEditor => !IsSelectionOnly;
        public bool ShowLanCredentialEditor => !IsSelectionOnly && IsLanSelected;
        public bool ShowFixedPresetNotice => !IsSelectionOnly && !IsLanSelected;
        public bool IsLocalSelected => string.Equals(SelectedPresetKey, LocalPresetKey, StringComparison.OrdinalIgnoreCase);
        public bool IsLanSelected => string.Equals(SelectedPresetKey, LanPresetKey, StringComparison.OrdinalIgnoreCase);
        public bool IsRemoteSelected => string.Equals(SelectedPresetKey, RemotePresetKey, StringComparison.OrdinalIgnoreCase);
        public bool CanEditSelectedPresetCredentials => ShowCredentialEditor && IsLanSelected;
        public bool IsPresetCredentialsReadOnly => !CanEditSelectedPresetCredentials;
        public string CurrentPresetDisplayName => string.IsNullOrWhiteSpace(SelectedPresetKey)
            ? string.Empty
            : _settings.GetPreset(SelectedPresetKey).DisplayName;
        public string HeaderDescription => IsSelectionOnly
            ? "Choose which app database is active for login, startup, dashboard data, and snapshots. You can test the selected preset here, then edit Network (LAN) credentials later from System Settings."
            : "Select the active app database here. Local and Hostinger are fixed presets; only Network (LAN) credentials can be updated from this screen.";
        public string FooterDescription => IsSelectionOnly
            ? "Use Test Connection to verify the selected preset, then save to apply it and return to the login form. Database credentials are managed from System Settings."
            : "Save to apply the active app database. Only Network (LAN) credentials are saved from this screen; Local and Hostinger remain fixed.";
        public string SaveButtonText => IsSelectionOnly ? "SAVE AND CONTINUE" : "SAVE SETTINGS";
        public string PasswordStatusText => string.IsNullOrWhiteSpace(Password) ? "Not configured" : "Configured";
        public string PresetHelpText => IsSelectionOnly
            ? "Use System Settings after login if you need to update Network (LAN) credentials. Local and Hostinger stay fixed."
            : CanEditSelectedPresetCredentials
                ? "Update the Network (LAN) host and database credentials here. This preset is stored in your Windows user profile."
                : $"{CurrentPresetDisplayName} is fixed by app settings and is read-only here. Switch to Network (LAN) if you need editable connection details.";

        public ICommand SelectLocalPresetCommand { get; }
        public ICommand SelectLanPresetCommand { get; }
        public ICommand SelectRemotePresetCommand { get; }
        public ICommand TestConnectionCommand => _testConnectionCommand;
        public ICommand SaveCommand => _saveCommand;
        public ICommand CancelCommand { get; }

        public ConnectionSettingsViewModel(bool selectionOnly = true)
        {
            _selectionOnly = selectionOnly;
            _settings = ConnectionSettingsService.Load();

            SelectLocalPresetCommand = new RelayCommand(_ => SelectPreset(LocalPresetKey));
            SelectLanPresetCommand = new RelayCommand(_ => SelectPreset(LanPresetKey));
            SelectRemotePresetCommand = new RelayCommand(_ => SelectPreset(RemotePresetKey));
            _testConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync(), _ => !IsBusy);
            _saveCommand = new RelayCommand(_ => ExecuteSave(), _ => !IsBusy);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

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

        private async Task ExecuteTestConnectionAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (!TryBuildPresetForTesting(out var preset, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Testing connection...");

            try
            {
                var result = await ConnectionSettingsService.TestConnectionAsync(preset);
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

            if (CanEditSelectedPresetCredentials && !TryUpdateSelectedPreset(showValidationErrors: true))
            {
                return;
            }

            _settings.SelectedPreset = SelectedPresetKey;
            ConnectionSettingsService.Save(_settings);
            CloseRequested?.Invoke(true);
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
            selectedPreset.DisplayName = GetDefaultDisplayName(SelectedPresetKey);
            selectedPreset.Server = preset.Server;
            selectedPreset.Port = preset.Port;
            selectedPreset.Database = preset.Database;
            selectedPreset.Username = preset.Username;
            selectedPreset.Password = preset.Password;
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

        private static string GetDefaultDisplayName(string presetKey)
        {
            return presetKey switch
            {
                LocalPresetKey => "Local",
                LanPresetKey => "Network (LAN)",
                _ => "Remote (Hostinger)"
            };
        }

        private string BuildPresetLoadedMessage()
        {
            if (IsSelectionOnly)
            {
                return $"{CurrentPresetDisplayName} app database selected. Use System Settings to edit Network (LAN) credentials.";
            }

            return CanEditSelectedPresetCredentials
                ? "Network (LAN) is selected. You can update its host and database credentials here."
                : $"{CurrentPresetDisplayName} is selected. This preset is fixed by app settings; only Network (LAN) can be edited here.";
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
    }
}

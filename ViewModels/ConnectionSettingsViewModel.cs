using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class ConnectionSettingsViewModel : ObservableObject
    {
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
                    OnPropertyChanged(nameof(IsRemoteSelected));
                    OnPropertyChanged(nameof(CurrentPresetDisplayName));
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
            set => SetProperty(ref _password, value);
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

        public bool IsLocalSelected => string.Equals(SelectedPresetKey, "Local", StringComparison.OrdinalIgnoreCase);
        public bool IsRemoteSelected => string.Equals(SelectedPresetKey, "Remote", StringComparison.OrdinalIgnoreCase);
        public string CurrentPresetDisplayName => string.IsNullOrWhiteSpace(SelectedPresetKey)
            ? string.Empty
            : _settings.GetPreset(SelectedPresetKey).DisplayName;

        public ICommand SelectLocalPresetCommand { get; }
        public ICommand SelectRemotePresetCommand { get; }
        public ICommand TestConnectionCommand => _testConnectionCommand;
        public ICommand SaveCommand => _saveCommand;
        public ICommand CancelCommand { get; }

        public ConnectionSettingsViewModel()
        {
            _settings = ConnectionSettingsService.Load();

            SelectLocalPresetCommand = new RelayCommand(_ => SelectPreset("Local"));
            SelectRemotePresetCommand = new RelayCommand(_ => SelectPreset("Remote"));
            _testConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync(), _ => !IsBusy);
            _saveCommand = new RelayCommand(_ => ExecuteSave(), _ => !IsBusy);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

            LoadPreset(_settings.SelectedPreset);
            SetNeutralStatus($"{CurrentPresetDisplayName} app database loaded. Use Settings > Advanced Load Tables for external source databases.");
        }

        private void SelectPreset(string presetKey)
        {
            if (IsBusy || string.Equals(SelectedPresetKey, presetKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TryUpdateSelectedPreset(showValidationErrors: false);
            _settings.SelectedPreset = presetKey;
            LoadPreset(presetKey);
            SetNeutralStatus($"{CurrentPresetDisplayName} app database loaded. Use Settings > Advanced Load Tables for external source databases.");
        }

        private async Task ExecuteTestConnectionAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (!TryBuildWorkingPreset(out var preset, out var validationMessage))
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

            if (!TryUpdateSelectedPreset(showValidationErrors: true))
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

        private static string GetDefaultDisplayName(string presetKey)
        {
            return presetKey switch
            {
                "Local" => "Local",
                _ => "Remote (Hostinger)"
            };
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

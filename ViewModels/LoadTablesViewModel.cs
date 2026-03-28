using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class LoadTablesViewModel : ObservableObject
    {
        private readonly RelayCommand _testConnectionCommand;
        private readonly RelayCommand _saveSourceConnectionCommand;
        private readonly RelayCommand _loadTablesCommand;
        private readonly RelayCommand _syncSelectedTableCommand;

        private string _server = string.Empty;
        private string _portText = "3306";
        private string _database = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _targetSummary = string.Empty;
        private string _statusMessage = "Enter the source database connection, then load available tables into the active app database.";
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private bool _isBusy;
        private string _selectedTable = string.Empty;
        private bool _existsInRemote;
        private bool _existsInLocal;
        private int _remoteRowCount;
        private int _localRowCount;
        private DataView? _previewRowsView;

        public LoadTablesViewModel()
        {
            _testConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync(), _ => !IsBusy);
            _saveSourceConnectionCommand = new RelayCommand(_ => ExecuteSaveSourceConnection(), _ => !IsBusy);
            _loadTablesCommand = new RelayCommand(async _ => await ExecuteLoadTablesAsync(), _ => !IsBusy);
            _syncSelectedTableCommand = new RelayCommand(async _ => await ExecuteSyncSelectedTableAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(SelectedTable));

            LoadSourcePreset();
            RefreshTargetSummary();
        }

        public ObservableCollection<string> Tables { get; } = new();

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

        public string TargetSummary
        {
            get => _targetSummary;
            private set => SetProperty(ref _targetSummary, value);
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
                    _saveSourceConnectionCommand.RaiseCanExecuteChanged();
                    _loadTablesCommand.RaiseCanExecuteChanged();
                    _syncSelectedTableCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    _syncSelectedTableCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(ExistenceSummary));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _ = LoadSelectedTablePreviewAsync();
                    }
                }
            }
        }

        public bool ExistsInRemote
        {
            get => _existsInRemote;
            private set
            {
                if (SetProperty(ref _existsInRemote, value))
                {
                    OnPropertyChanged(nameof(ExistenceSummary));
                }
            }
        }

        public bool ExistsInLocal
        {
            get => _existsInLocal;
            private set
            {
                if (SetProperty(ref _existsInLocal, value))
                {
                    OnPropertyChanged(nameof(ExistenceSummary));
                }
            }
        }

        public int RemoteRowCount
        {
            get => _remoteRowCount;
            private set => SetProperty(ref _remoteRowCount, value);
        }

        public int LocalRowCount
        {
            get => _localRowCount;
            private set => SetProperty(ref _localRowCount, value);
        }

        public DataView? PreviewRowsView
        {
            get => _previewRowsView;
            private set => SetProperty(ref _previewRowsView, value);
        }

        public string ExistenceSummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SelectedTable))
                {
                    return "Select a source table to inspect whether it already exists in the local app database.";
                }

                if (!ExistsInRemote)
                {
                    return $"'{SelectedTable}' was not found in the source database.";
                }

                if (ExistsInLocal)
                {
                    return $"'{SelectedTable}' already exists in the active app database. Loading it again will replace the current copy with a fresh snapshot from the source database.";
                }

                return $"'{SelectedTable}' does not exist yet in the active app database. LOAD SELECTED TABLE will create the target table from the source schema and copy the available rows.";
            }
        }

        public ICommand TestConnectionCommand => _testConnectionCommand;
        public ICommand SaveSourceConnectionCommand => _saveSourceConnectionCommand;
        public ICommand LoadTablesCommand => _loadTablesCommand;
        public ICommand SyncSelectedTableCommand => _syncSelectedTableCommand;

        public void RefreshTargetSummary()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            TargetSummary = $"{preset.DisplayName}: {preset.Server}:{preset.Port} / {preset.Database}";
        }

        private void LoadSourcePreset()
        {
            var preset = MunicipalityImportConnectionSettingsService.Load();
            Server = preset.Server;
            PortText = preset.Port.ToString();
            Database = preset.Database;
            Username = preset.Username;
            Password = preset.Password;
        }

        private async Task ExecuteTestConnectionAsync()
        {
            if (!TryBuildSourcePreset(out var preset, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Testing municipality source connection...");

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

        private void ExecuteSaveSourceConnection()
        {
            if (!TryBuildSourcePreset(out var preset, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            MunicipalityImportConnectionSettingsService.Save(preset);
            SetSuccessStatus("Municipality source connection saved.");
        }

        private async Task ExecuteLoadTablesAsync()
        {
            if (!TryBuildSourcePreset(out var sourcePreset, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading source tables...");

            try
            {
                Tables.Clear();
                var tables = await DatabaseTableReviewService.ListTablesAsync(sourcePreset);
                foreach (var table in tables)
                {
                    Tables.Add(table);
                }

                if (Tables.Count == 0)
                {
                    SetErrorStatus("No tables were found in the configured source database.");
                    return;
                }

                SelectedTable = Tables[0];
                SetSuccessStatus($"Loaded {Tables.Count} table(s) from the source database.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load tables: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSelectedTablePreviewAsync(bool manageBusyState = true)
        {
            if (string.IsNullOrWhiteSpace(SelectedTable))
            {
                return;
            }

            if (!TryBuildSourcePreset(out var sourcePreset, out _))
            {
                return;
            }

            var targetPreset = GetTargetPreset();
            if (manageBusyState)
            {
                IsBusy = true;
            }

            try
            {
                var result = await DatabaseTableReviewService.ReviewTableAsync(sourcePreset, targetPreset, SelectedTable);
                ExistsInRemote = result.ExistsInRemote;
                ExistsInLocal = result.ExistsInLocal;
                RemoteRowCount = result.RemoteRowCount;
                LocalRowCount = result.LocalRowCount;
                PreviewRowsView = result.PreviewRows.DefaultView;
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to review `{SelectedTable}`: {ex.Message}");
            }
            finally
            {
                if (manageBusyState)
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ExecuteSyncSelectedTableAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedTable))
            {
                SetErrorStatus("Select a table first.");
                return;
            }

            if (!TryBuildSourcePreset(out var sourcePreset, out var validationMessage))
            {
                SetErrorStatus(validationMessage);
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Loading a snapshot of '{SelectedTable}' into the active app database...");

            try
            {
                var targetPreset = GetTargetPreset();
                var result = await DatabaseTableReviewService.SyncTableToLocalAsync(sourcePreset, targetPreset, SelectedTable);
                if (result.IsSuccess)
                {
                    SetSuccessStatus(result.Message);
                    await LoadSelectedTablePreviewAsync(manageBusyState: false);
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

        private DatabaseConnectionPreset GetTargetPreset()
        {
            var settings = ConnectionSettingsService.Load();
            return settings.GetPreset(settings.SelectedPreset);
        }

        private bool TryBuildSourcePreset(out DatabaseConnectionPreset preset, out string validationMessage)
        {
            preset = new DatabaseConnectionPreset();
            validationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Server))
            {
                validationMessage = "Enter a source server or host.";
                return false;
            }

            if (!int.TryParse(PortText, out var port) || port <= 0 || port > 65535)
            {
                validationMessage = "Enter a valid MySQL port.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                validationMessage = "Enter a source database name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                validationMessage = "Enter a source username.";
                return false;
            }

            preset = new DatabaseConnectionPreset
            {
                DisplayName = "Municipality Import Source",
                Server = Server.Trim(),
                Port = port,
                Database = Database.Trim(),
                Username = Username.Trim(),
                Password = Password
            };

            return true;
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

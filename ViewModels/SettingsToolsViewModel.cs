using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Win32;
using MySqlConnector;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class SettingsToolsViewModel : ObservableObject
    {
        private const string MasterListTableName = "val_beneficiaries";
        private const string StagingTableName = "BeneficiaryStaging";
        private const string MasterListPreviewLabel = "Masterlist Snapshot";
        private const string StagingPreviewLabel = "Beneficiary Staging";

        private readonly RelayCommand _testConnectionCommand;
        private readonly RelayCommand _saveImportConnectionCommand;
        private readonly RelayCommand _snapshotMasterListCommand;
        private readonly RelayCommand _importToStagingCommand;
        private readonly RelayCommand _refreshPreviewCommand;
        private readonly RelayCommand _openAdvancedLoadTablesCommand;
        private readonly RelayCommand _createBackupCommand;
        private readonly RelayCommand _importBackupCommand;
        private readonly RelayCommand _saveFeatureRulesCommand;

        private string _server = string.Empty;
        private string _portText = "3306";
        private string _database = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _activeTargetSummary = string.Empty;
        private string _backupTargetSummary = string.Empty;
        private string _selectedPreviewLabel = MasterListPreviewLabel;
        private DataView? _previewRowsView;
        private string _previewSummary = "Loading local preview...";
        private string _snapshotStatusMessage = "Configure the municipality source, then snapshot `val_beneficiaries` or import pending rows into staging.";
        private Brush _snapshotStatusBrush = CreateBrush("#6B7280");
        private string _backupStatusMessage = "Create or import a full database backup for the currently selected app preset.";
        private Brush _backupStatusBrush = CreateBrush("#6B7280");
        private string _lastBackupActivity = "No backup activity yet in this session.";
        private string _largeAssistanceWarningThresholdText = string.Empty;
        private string _featureRulesStatusMessage = "Set the warning-only threshold used when a beneficiary has already received significant assistance.";
        private Brush _featureRulesStatusBrush = CreateBrush("#6B7280");
        private int _masterListRowCount;
        private int _stagingRowCount;
        private bool _isBusy;

        public SettingsToolsViewModel()
        {
            PreviewOptions = new ObservableCollection<string>
            {
                MasterListPreviewLabel,
                StagingPreviewLabel
            };

            _testConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync(), _ => !IsBusy);
            _saveImportConnectionCommand = new RelayCommand(_ => ExecuteSaveImportConnection(), _ => !IsBusy);
            _snapshotMasterListCommand = new RelayCommand(async _ => await ExecuteSnapshotMasterListAsync(), _ => !IsBusy);
            _importToStagingCommand = new RelayCommand(async _ => await ExecuteImportToStagingAsync(), _ => !IsBusy);
            _refreshPreviewCommand = new RelayCommand(async _ => await RefreshPreviewAsync(), _ => !IsBusy);
            _openAdvancedLoadTablesCommand = new RelayCommand(_ => AdvancedLoadTablesRequested?.Invoke(), _ => !IsBusy);
            _createBackupCommand = new RelayCommand(async _ => await ExecuteCreateBackupAsync(), _ => !IsBusy);
            _importBackupCommand = new RelayCommand(async _ => await ExecuteImportBackupAsync(), _ => !IsBusy);
            _saveFeatureRulesCommand = new RelayCommand(_ => ExecuteSaveFeatureRules(), _ => !IsBusy);

            LoadImportConnection();
            LoadFeatureRules();
            RefreshTargetSummaries();
            _ = RefreshPreviewAsync();
        }

        public event Action? AdvancedLoadTablesRequested;

        public ObservableCollection<string> PreviewOptions { get; }

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

        public string ActiveTargetSummary
        {
            get => _activeTargetSummary;
            private set => SetProperty(ref _activeTargetSummary, value);
        }

        public string BackupTargetSummary
        {
            get => _backupTargetSummary;
            private set => SetProperty(ref _backupTargetSummary, value);
        }

        public string SelectedPreviewLabel
        {
            get => _selectedPreviewLabel;
            set
            {
                if (SetProperty(ref _selectedPreviewLabel, value))
                {
                    OnPropertyChanged(nameof(PreviewCardTitle));
                    _ = RefreshPreviewAsync();
                }
            }
        }

        public string PreviewCardTitle =>
            SelectedPreviewLabel == StagingPreviewLabel
                ? "Local Staging Preview"
                : "Local Masterlist Preview";

        public DataView? PreviewRowsView
        {
            get => _previewRowsView;
            private set => SetProperty(ref _previewRowsView, value);
        }

        public string PreviewSummary
        {
            get => _previewSummary;
            private set => SetProperty(ref _previewSummary, value);
        }

        public string SnapshotStatusMessage
        {
            get => _snapshotStatusMessage;
            private set => SetProperty(ref _snapshotStatusMessage, value);
        }

        public Brush SnapshotStatusBrush
        {
            get => _snapshotStatusBrush;
            private set => SetProperty(ref _snapshotStatusBrush, value);
        }

        public string BackupStatusMessage
        {
            get => _backupStatusMessage;
            private set => SetProperty(ref _backupStatusMessage, value);
        }

        public Brush BackupStatusBrush
        {
            get => _backupStatusBrush;
            private set => SetProperty(ref _backupStatusBrush, value);
        }

        public string LastBackupActivity
        {
            get => _lastBackupActivity;
            private set => SetProperty(ref _lastBackupActivity, value);
        }

        public string LargeAssistanceWarningThresholdText
        {
            get => _largeAssistanceWarningThresholdText;
            set => SetProperty(ref _largeAssistanceWarningThresholdText, value);
        }

        public string FeatureRulesStatusMessage
        {
            get => _featureRulesStatusMessage;
            private set => SetProperty(ref _featureRulesStatusMessage, value);
        }

        public Brush FeatureRulesStatusBrush
        {
            get => _featureRulesStatusBrush;
            private set => SetProperty(ref _featureRulesStatusBrush, value);
        }

        public int MasterListRowCount
        {
            get => _masterListRowCount;
            private set => SetProperty(ref _masterListRowCount, value);
        }

        public int StagingRowCount
        {
            get => _stagingRowCount;
            private set => SetProperty(ref _stagingRowCount, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _testConnectionCommand.RaiseCanExecuteChanged();
                    _saveImportConnectionCommand.RaiseCanExecuteChanged();
                    _snapshotMasterListCommand.RaiseCanExecuteChanged();
                    _importToStagingCommand.RaiseCanExecuteChanged();
                    _refreshPreviewCommand.RaiseCanExecuteChanged();
                    _openAdvancedLoadTablesCommand.RaiseCanExecuteChanged();
                    _createBackupCommand.RaiseCanExecuteChanged();
                    _importBackupCommand.RaiseCanExecuteChanged();
                    _saveFeatureRulesCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand TestConnectionCommand => _testConnectionCommand;
        public ICommand SaveImportConnectionCommand => _saveImportConnectionCommand;
        public ICommand SnapshotMasterListCommand => _snapshotMasterListCommand;
        public ICommand ImportToStagingCommand => _importToStagingCommand;
        public ICommand RefreshPreviewCommand => _refreshPreviewCommand;
        public ICommand OpenAdvancedLoadTablesCommand => _openAdvancedLoadTablesCommand;
        public ICommand CreateBackupCommand => _createBackupCommand;
        public ICommand ImportBackupCommand => _importBackupCommand;
        public ICommand SaveFeatureRulesCommand => _saveFeatureRulesCommand;

        private void LoadImportConnection()
        {
            var preset = MunicipalityImportConnectionSettingsService.Load();
            Server = preset.Server;
            PortText = preset.Port.ToString(CultureInfo.InvariantCulture);
            Database = preset.Database;
            Username = preset.Username;
            Password = preset.Password;
        }

        private void LoadFeatureRules()
        {
            var settings = FeatureSettingsService.Load();
            LargeAssistanceWarningThresholdText = settings.LargeAssistanceWarningThreshold.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void RefreshTargetSummaries()
        {
            var preset = GetTargetPreset();
            var summary = $"{preset.DisplayName}: {preset.Server}:{preset.Port} / {preset.Database}";
            ActiveTargetSummary = summary;
            BackupTargetSummary = summary;
        }

        private async Task ExecuteTestConnectionAsync()
        {
            if (!TryBuildSourcePreset(out var preset, out var validationMessage))
            {
                SetSnapshotError(validationMessage);
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    SetSnapshotNeutral("Testing municipality source connection...");
                    var result = await ConnectionSettingsService.TestConnectionAsync(preset);
                    if (result.IsSuccess)
                    {
                        SetSnapshotSuccess(result.Message);
                    }
                    else
                    {
                        SetSnapshotError(result.Message);
                    }
                });
        }

        private void ExecuteSaveImportConnection()
        {
            if (!TryBuildSourcePreset(out var preset, out var validationMessage))
            {
                SetSnapshotError(validationMessage);
                return;
            }

            MunicipalityImportConnectionSettingsService.Save(preset);
            SetSnapshotSuccess("Municipality import connection saved.");
        }

        private async Task ExecuteSnapshotMasterListAsync()
        {
            if (!TryBuildSourcePreset(out var sourcePreset, out var validationMessage))
            {
                SetSnapshotError(validationMessage);
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    RefreshTargetSummaries();
                    SetSnapshotNeutral("Snapshotting `val_beneficiaries` into the active database...");

                    var result = await DatabaseTableReviewService.SyncTableToLocalAsync(
                        sourcePreset,
                        GetTargetPreset(),
                        MasterListTableName);

                    if (result.IsSuccess)
                    {
                        SetSnapshotSuccess(result.Message);
                        await RefreshPreviewCoreAsync();
                        return;
                    }

                    SetSnapshotError(result.Message);
                });
        }

        private async Task ExecuteImportToStagingAsync()
        {
            if (!TryBuildSourcePreset(out var sourcePreset, out var validationMessage))
            {
                SetSnapshotError(validationMessage);
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    RefreshTargetSummaries();
                    SetSnapshotNeutral("Importing remote beneficiaries into local staging...");

                    var result = await CrsBeneficiaryImportService.ImportPendingAsync(sourcePreset);
                    if (result.IsSuccess)
                    {
                        SetSnapshotSuccess(result.Message);
                        await RefreshPreviewCoreAsync();
                        return;
                    }

                    SetSnapshotError(result.Message);
                });
        }

        private async Task RefreshPreviewAsync()
        {
            await ExecuteBusyAsync(RefreshPreviewCoreAsync);
        }

        private async Task RefreshPreviewCoreAsync()
        {
            RefreshTargetSummaries();

            var targetPreset = GetTargetPreset();
            await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(targetPreset));
            await connection.OpenAsync();

            MasterListRowCount = await GetRowCountAsync(connection, targetPreset.Database, MasterListTableName);
            StagingRowCount = await GetRowCountAsync(connection, targetPreset.Database, StagingTableName);

            var previewTableName = ResolvePreviewTableName();
            var previewExists = await TableExistsAsync(connection, targetPreset.Database, previewTableName);
            if (!previewExists)
            {
                PreviewRowsView = new DataTable().DefaultView;
                PreviewSummary = previewTableName == MasterListTableName
                    ? "`val_beneficiaries` is not in the active database yet. Run Snapshot Masterlist first."
                    : "`BeneficiaryStaging` is not in the active database yet.";
                return;
            }

            var preview = await LoadPreviewRowsAsync(connection, previewTableName, 200);
            PreviewRowsView = preview.DefaultView;

            var previewRowCount = preview.Rows.Count;
            if (previewRowCount == 0)
            {
                PreviewSummary = previewTableName == MasterListTableName
                    ? "`val_beneficiaries` is empty in the active database."
                    : "`BeneficiaryStaging` is empty. Run Import to Staging to populate it.";
                return;
            }

            PreviewSummary = $"Showing {previewRowCount:N0} row(s) from `{previewTableName}` in `{targetPreset.Database}`.";
        }

        private async Task ExecuteCreateBackupAsync()
        {
            RefreshTargetSummaries();

            var targetPreset = GetTargetPreset();
            var dialog = new SaveFileDialog
            {
                Filter = "Barangay Backup (*.zip)|*.zip",
                AddExtension = true,
                DefaultExt = ".zip",
                FileName = $"{targetPreset.Database}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    SetBackupNeutral("Creating database backup...");
                    var result = await LocalBackupService.CreateBackupAsync(dialog.FileName);
                    if (result.IsSuccess)
                    {
                        SetBackupSuccess(result.Message);
                        LastBackupActivity = $"Last backup: {dialog.FileName}";
                        return;
                    }

                    SetBackupError(result.Message);
                    LastBackupActivity = "Backup failed in this session.";
                });
        }

        private async Task ExecuteImportBackupAsync()
        {
            RefreshTargetSummaries();

            var dialog = new OpenFileDialog
            {
                Filter = "Barangay Backup (*.zip)|*.zip",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    SetBackupNeutral("Importing database backup...");
                    var result = await LocalBackupService.RestoreBackupAsync(dialog.FileName);
                    if (result.IsSuccess)
                    {
                        SetBackupSuccess(result.Message);
                        LastBackupActivity = $"Last imported backup: {dialog.FileName}";
                        await RefreshPreviewCoreAsync();
                        return;
                    }

                    SetBackupError(result.Message);
                    LastBackupActivity = "Backup import failed in this session.";
                });
        }

        private void ExecuteSaveFeatureRules()
        {
            if (!TryParseWarningThreshold(out var threshold))
            {
                SetFeatureRulesError("Enter a valid warning threshold amount greater than or equal to zero.");
                return;
            }

            FeatureSettingsService.Save(new FeatureSettingsModel
            {
                LargeAssistanceWarningThreshold = threshold
            });

            LargeAssistanceWarningThresholdText = threshold.ToString("0.##", CultureInfo.InvariantCulture);
            SetFeatureRulesSuccess("Saved the large-assistance warning threshold.");
        }

        private static Brush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }

        private void SetSnapshotNeutral(string message)
        {
            SnapshotStatusMessage = message;
            SnapshotStatusBrush = CreateBrush("#6B7280");
        }

        private void SetSnapshotSuccess(string message)
        {
            SnapshotStatusMessage = message;
            SnapshotStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetSnapshotError(string message)
        {
            SnapshotStatusMessage = message;
            SnapshotStatusBrush = CreateBrush("#991B1B");
        }

        private void SetBackupNeutral(string message)
        {
            BackupStatusMessage = message;
            BackupStatusBrush = CreateBrush("#6B7280");
        }

        private void SetBackupSuccess(string message)
        {
            BackupStatusMessage = message;
            BackupStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetBackupError(string message)
        {
            BackupStatusMessage = message;
            BackupStatusBrush = CreateBrush("#991B1B");
        }

        private void SetFeatureRulesSuccess(string message)
        {
            FeatureRulesStatusMessage = message;
            FeatureRulesStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetFeatureRulesError(string message)
        {
            FeatureRulesStatusMessage = message;
            FeatureRulesStatusBrush = CreateBrush("#991B1B");
        }

        private async Task ExecuteBusyAsync(Func<Task> action)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await action();
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
                validationMessage = "Enter the remote source server or host.";
                return false;
            }

            if (!int.TryParse(PortText, out var port) || port <= 0 || port > 65535)
            {
                validationMessage = "Enter a valid MySQL port between 1 and 65535.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                validationMessage = "Enter the remote source database name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                validationMessage = "Enter the remote source username.";
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

        private string ResolvePreviewTableName()
        {
            return SelectedPreviewLabel == StagingPreviewLabel
                ? StagingTableName
                : MasterListTableName;
        }

        private static async Task<bool> TableExistsAsync(MySqlConnection connection, string databaseName, string tableName)
        {
            const string sql =
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = @databaseName
                  AND table_name = @tableName
                  AND table_type = 'BASE TABLE';
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@databaseName", databaseName);
            command.Parameters.AddWithValue("@tableName", tableName);

            var count = await command.ExecuteScalarAsync();
            return Convert.ToInt32(count, CultureInfo.InvariantCulture) > 0;
        }

        private static async Task<int> GetRowCountAsync(MySqlConnection connection, string databaseName, string tableName)
        {
            if (!await TableExistsAsync(connection, databaseName, tableName))
            {
                return 0;
            }

            await using var command = new MySqlCommand($"SELECT COUNT(*) FROM `{tableName}`;", connection);
            var count = await command.ExecuteScalarAsync();
            return Convert.ToInt32(count, CultureInfo.InvariantCulture);
        }

        private static async Task<DataTable> LoadPreviewRowsAsync(MySqlConnection connection, string tableName, int limit)
        {
            var preview = new DataTable();
            var safeLimit = Math.Clamp(limit, 1, 250);

            await using var command = new MySqlCommand($"SELECT * FROM `{tableName}` LIMIT {safeLimit};", connection);
            await using var reader = await command.ExecuteReaderAsync();

            for (var index = 0; index < reader.FieldCount; index++)
            {
                preview.Columns.Add(reader.GetName(index), typeof(string));
            }

            while (await reader.ReadAsync())
            {
                var row = preview.NewRow();
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[index] = reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
                }

                preview.Rows.Add(row);
            }

            return preview;
        }

        private bool TryParseWarningThreshold(out decimal threshold)
        {
            threshold = 0m;

            return decimal.TryParse(LargeAssistanceWarningThresholdText, NumberStyles.Number, CultureInfo.InvariantCulture, out threshold)
                || decimal.TryParse(LargeAssistanceWarningThresholdText, NumberStyles.Number, CultureInfo.CurrentCulture, out threshold);
        }
    }
}

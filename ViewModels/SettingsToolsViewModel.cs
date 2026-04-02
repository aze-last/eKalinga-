using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Data;
using Microsoft.Win32;
using MySqlConnector;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed partial class SettingsToolsViewModel : ObservableObject, IDisposable
    {
        private const string MasterListTableName = "val_beneficiaries";
        private const string StagingTableName = "BeneficiaryStaging";
        private const string MasterListPreviewLabel = "Validated Beneficiaries Snapshot";
        private const string StagingPreviewLabel = "Beneficiary Staging";

        private readonly RelayCommand _testConnectionCommand;
        private readonly RelayCommand _saveImportConnectionCommand;
        private readonly RelayCommand _snapshotMasterListCommand;
        private readonly RelayCommand _importToStagingCommand;
        private readonly RelayCommand _refreshPreviewCommand;
        private readonly RelayCommand _openAdvancedLoadTablesCommand;
        private readonly RelayCommand _createBackupCommand;
        private readonly RelayCommand _createIncrementalBackupCommand;
        private readonly RelayCommand _createDifferentialBackupCommand;
        private readonly RelayCommand _restoreBackupCommand;
        private readonly RelayCommand _migrateLocalAndRemoteCommand;
        private readonly RelayCommand _saveFeatureRulesCommand;
        private readonly RelayCommand _testGgmsConnectionCommand;
        private readonly RelayCommand _saveGgmsSettingsCommand;
        private readonly RelayCommand _checkForUpdatesCommand;
        private readonly RelayCommand _downloadUpdateCommand;
        private readonly RelayCommand _installPendingUpdateCommand;
        private readonly RelayCommand _remindMeLaterCommand;
        private readonly RelayCommand _saveUpdatePreferencesCommand;
        private readonly RelayCommand _openUpdateDownloadPageCommand;
        private readonly RelayCommand _saveSystemProfileCommand;
        private readonly RelayCommand _browseSystemLogoCommand;
        private readonly RelayCommand _removeSystemLogoCommand;
        private readonly RelayCommand _browseSystemLoginBackgroundCommand;
        private readonly RelayCommand _removeSystemLoginBackgroundCommand;
        private readonly RelayCommand _saveAccountCommand;
        private readonly RelayCommand _changePasswordCommand;
        private readonly User? _currentUser;
        private readonly Func<AppDbContext> _dbContextFactory;

        private string _systemName = string.Empty;
        private string _systemOwner = string.Empty;
        private string _systemCompanyAddress = string.Empty;
        private string _systemEmail = string.Empty;
        private string _systemContactNumber = string.Empty;
        private string _systemLogoPath = string.Empty;
        private string _savedSystemLogoPath = string.Empty;
        private string _systemLoginBackgroundPath = string.Empty;
        private string _savedSystemLoginBackgroundPath = string.Empty;
        private string _systemInstallSerial = string.Empty;
        private ImageSource? _systemLogoImage;
        private ImageSource? _systemLoginBackgroundImage;
        private string _systemProfileStatusMessage = "Set the app-wide system identity and keep the company serial number matched to the active database.";
        private Brush _systemProfileStatusBrush = CreateBrush("#6B7280");
        private string _accountFullName = string.Empty;
        private string _accountUsername = string.Empty;
        private string _accountEmail = string.Empty;
        private string _accountContactNumber = string.Empty;
        private string _accountStatusMessage = "Update the currently signed-in account details used by this app.";
        private Brush _accountStatusBrush = CreateBrush("#6B7280");
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _securityStatusMessage = "Change the current account password here. The current password is required.";
        private Brush _securityStatusBrush = CreateBrush("#6B7280");
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
        private string _backupPresetName = string.Empty;
        private string _backupStatusMessage = "Create a full backup first for the active preset, then use incremental or differential backups as needed.";
        private Brush _backupStatusBrush = CreateBrush("#6B7280");
        private string _lastBackupActivity = "No backup activity yet in this session.";
        private string _lastFullBackupDisplay = "No full backup recorded for this preset yet.";
        private string _lastIncrementalBackupDisplay = "No incremental backup recorded for this preset yet.";
        private string _lastDifferentialBackupDisplay = "No differential backup recorded for this preset yet.";
        private string _backupGuidanceMessage = "Incremental and differential backups require an existing full backup for the active preset.";
        private bool _canCreateDeltaBackups;
        private string _appDatabaseStatusMessage = "Open App Database Settings to switch presets. Only Network (LAN) is editable there; Local and Remote stay fixed from the shipped app configuration.";
        private Brush _appDatabaseStatusBrush = CreateBrush("#6B7280");
        private string _ggmsOfficeCode = string.Empty;
        private string _ggmsOfficeTable = "tbl_offices";
        private string _ggmsAllocationTable = "officeallocations";
        private string _ggmsServer = string.Empty;
        private string _ggmsPortText = "3306";
        private string _ggmsDatabase = string.Empty;
        private string _ggmsUsername = string.Empty;
        private string _ggmsPassword = string.Empty;
        private string _ggmsStatusMessage = "Configure the GGMS budget source here. Keep the default table names unless your GGMS schema differs.";
        private Brush _ggmsStatusBrush = CreateBrush("#6B7280");
        private string _largeAssistanceWarningThresholdText = string.Empty;
        private string _featureRulesStatusMessage = "Set the warning-only threshold used when a beneficiary has already received significant assistance.";
        private Brush _featureRulesStatusBrush = CreateBrush("#6B7280");
        private string _currentAppVersion = AppVersionService.GetCurrentVersion();
        private string _latestAvailableVersion = "Not checked yet";
        private string _updatePublishedAt = string.Empty;
        private string _updateReleaseNotes = "No update check has run yet.";
        private string _updateManifestUrl = string.Empty;
        private string _updateStatusMessage = "Set the public update manifest URL, then check for updates from this screen.";
        private Brush _updateStatusBrush = CreateBrush("#6B7280");
        private string _updateDownloadPageUrl = string.Empty;
        private string _updateInstallerUrl = string.Empty;
        private string _updateSha256 = string.Empty;
        private PendingAppUpdate? _pendingAppUpdate;
        private string _downloadedInstallerLabel = "No downloaded installer is waiting yet.";
        private double _updateDownloadProgressPercent;
        private bool _isUpdateDownloadInProgress;
        private bool _checkForUpdatesOnStartup = true;
        private bool _isUpdateAvailable;
        private int _masterListRowCount;
        private int _stagingRowCount;
        private bool _isBusy;
        private bool _lastPasswordChangeSucceeded;

        public SettingsToolsViewModel(User? currentUser = null, Func<AppDbContext>? dbContextFactory = null)
        {
            _currentUser = currentUser;
            _dbContextFactory = dbContextFactory ?? (() => new AppDbContext());

            PreviewOptions = new ObservableCollection<string>
            {
                MasterListPreviewLabel,
                StagingPreviewLabel
            };

            _saveSystemProfileCommand = new RelayCommand(_ => ExecuteSaveSystemProfile(), _ => !IsBusy);
            _browseSystemLogoCommand = new RelayCommand(_ => ExecuteBrowseSystemLogo(), _ => !IsBusy);
            _removeSystemLogoCommand = new RelayCommand(_ => ExecuteRemoveSystemLogo(), _ => !IsBusy && CanRemoveSystemLogo);
            _browseSystemLoginBackgroundCommand = new RelayCommand(_ => ExecuteBrowseSystemLoginBackground(), _ => !IsBusy);
            _removeSystemLoginBackgroundCommand = new RelayCommand(_ => ExecuteRemoveSystemLoginBackground(), _ => !IsBusy && CanRemoveSystemLoginBackground);
            _saveAccountCommand = new RelayCommand(_ => ExecuteSaveAccount(), _ => !IsBusy && HasCurrentUser);
            _changePasswordCommand = new RelayCommand(async _ => await HandleChangePasswordAsync(), _ => !IsBusy && HasCurrentUser);
            _testConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync(), _ => !IsBusy);
            _saveImportConnectionCommand = new RelayCommand(_ => ExecuteSaveImportConnection(), _ => !IsBusy);
            _snapshotMasterListCommand = new RelayCommand(async _ => await ExecuteSnapshotMasterListAsync(), _ => !IsBusy);
            _importToStagingCommand = new RelayCommand(async _ => await ExecuteImportToStagingAsync(), _ => !IsBusy);
            _refreshPreviewCommand = new RelayCommand(async _ => await RefreshPreviewAsync(), _ => !IsBusy);
            _openAdvancedLoadTablesCommand = new RelayCommand(_ => AdvancedLoadTablesRequested?.Invoke(), _ => !IsBusy);
            _createBackupCommand = new RelayCommand(async _ => await ExecuteCreateBackupAsync(BackupTypes.Full), _ => !IsBusy);
            _createIncrementalBackupCommand = new RelayCommand(async _ => await ExecuteCreateBackupAsync(BackupTypes.Incremental), _ => !IsBusy && CanCreateDeltaBackups);
            _createDifferentialBackupCommand = new RelayCommand(async _ => await ExecuteCreateBackupAsync(BackupTypes.Differential), _ => !IsBusy && CanCreateDeltaBackups);
            _restoreBackupCommand = new RelayCommand(async _ => await ExecuteRestoreBackupAsync(), _ => !IsBusy);
            _migrateLocalAndRemoteCommand = new RelayCommand(async _ => await ExecuteMigrateLocalAndRemoteAsync(), _ => !IsBusy);
            _saveFeatureRulesCommand = new RelayCommand(_ => ExecuteSaveFeatureRules(), _ => !IsBusy);
            _testGgmsConnectionCommand = new RelayCommand(async _ => await ExecuteTestGgmsConnectionAsync(), _ => !IsBusy);
            _saveGgmsSettingsCommand = new RelayCommand(_ => ExecuteSaveGgmsSettings(), _ => !IsBusy);
            _checkForUpdatesCommand = new RelayCommand(async _ => await ExecuteCheckForUpdatesAsync(), _ => !IsBusy);
            _downloadUpdateCommand = new RelayCommand(async _ => await ExecuteDownloadUpdateAsync(), _ => !IsBusy && CanDownloadUpdate);
            _installPendingUpdateCommand = new RelayCommand(_ => ExecuteInstallPendingUpdate(), _ => !IsBusy && CanInstallPendingUpdate);
            _remindMeLaterCommand = new RelayCommand(_ => ExecuteRemindMeLater(), _ => !IsBusy && HasPendingUpdate);
            _saveUpdatePreferencesCommand = new RelayCommand(_ => ExecuteSaveUpdatePreferences(), _ => !IsBusy);
            _openUpdateDownloadPageCommand = new RelayCommand(_ => ExecuteOpenUpdateDownloadPage(), _ => !IsBusy && CanOpenUpdateDownloadPage);
            InitializeOtpState();

            LoadSystemProfile();
            LoadCurrentUserAccount();
            LoadImportConnection();
            LoadFeatureRules();
            LoadGgmsSettings();
            LoadUpdatePreferences();
            RefreshTargetSummaries();
            _ = RefreshPreviewAsync();
        }

        public event Action? AdvancedLoadTablesRequested;

        public ObservableCollection<string> PreviewOptions { get; }

        public bool HasCurrentUser => _currentUser != null;

        public string SystemName
        {
            get => _systemName;
            set => SetProperty(ref _systemName, value);
        }

        public string SystemOwner
        {
            get => _systemOwner;
            set => SetProperty(ref _systemOwner, value);
        }

        public string SystemCompanyAddress
        {
            get => _systemCompanyAddress;
            set => SetProperty(ref _systemCompanyAddress, value);
        }

        public string SystemEmail
        {
            get => _systemEmail;
            set
            {
                if (SetProperty(ref _systemEmail, value))
                {
                    HandleSystemEmailChanged();
                }
            }
        }

        public string SystemContactNumber
        {
            get => _systemContactNumber;
            set => SetProperty(ref _systemContactNumber, value);
        }

        public string SystemLogoPath
        {
            get => _systemLogoPath;
            private set
            {
                if (SetProperty(ref _systemLogoPath, value))
                {
                    OnPropertyChanged(nameof(SystemLogoFileLabel));
                    OnPropertyChanged(nameof(CanRemoveSystemLogo));
                    _removeSystemLogoCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SystemInstallSerial
        {
            get => _systemInstallSerial;
            private set => SetProperty(ref _systemInstallSerial, value);
        }

        public ImageSource? SystemLogoImage
        {
            get => _systemLogoImage;
            private set
            {
                if (SetProperty(ref _systemLogoImage, value))
                {
                    OnPropertyChanged(nameof(HasSystemLogo));
                }
            }
        }

        public bool HasSystemLogo => SystemLogoImage != null;
        public bool CanRemoveSystemLogo => !string.IsNullOrWhiteSpace(SystemLogoPath);
        public string SystemLogoFileLabel => string.IsNullOrWhiteSpace(SystemLogoPath)
            ? "Using the default login logo."
            : Path.GetFileName(SystemLogoPath);

        public string SystemLoginBackgroundPath
        {
            get => _systemLoginBackgroundPath;
            private set
            {
                if (SetProperty(ref _systemLoginBackgroundPath, value))
                {
                    OnPropertyChanged(nameof(SystemLoginBackgroundFileLabel));
                    OnPropertyChanged(nameof(CanRemoveSystemLoginBackground));
                    _removeSystemLoginBackgroundCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public ImageSource? SystemLoginBackgroundImage
        {
            get => _systemLoginBackgroundImage;
            private set
            {
                if (SetProperty(ref _systemLoginBackgroundImage, value))
                {
                    OnPropertyChanged(nameof(HasSystemLoginBackground));
                }
            }
        }

        public bool HasSystemLoginBackground => SystemLoginBackgroundImage != null;
        public bool CanRemoveSystemLoginBackground => !string.IsNullOrWhiteSpace(SystemLoginBackgroundPath);
        public string SystemLoginBackgroundFileLabel => string.IsNullOrWhiteSpace(SystemLoginBackgroundPath)
            ? "Using the default login background."
            : Path.GetFileName(SystemLoginBackgroundPath);

        public string SystemProfileStatusMessage
        {
            get => _systemProfileStatusMessage;
            private set => SetProperty(ref _systemProfileStatusMessage, value);
        }

        public Brush SystemProfileStatusBrush
        {
            get => _systemProfileStatusBrush;
            private set => SetProperty(ref _systemProfileStatusBrush, value);
        }

        public string AccountFullName
        {
            get => _accountFullName;
            set => SetProperty(ref _accountFullName, value);
        }

        public string AccountUsername
        {
            get => _accountUsername;
            set => SetProperty(ref _accountUsername, value);
        }

        public string AccountEmail
        {
            get => _accountEmail;
            set => SetProperty(ref _accountEmail, value);
        }

        public string AccountContactNumber
        {
            get => _accountContactNumber;
            set => SetProperty(ref _accountContactNumber, value);
        }

        public string AccountStatusMessage
        {
            get => _accountStatusMessage;
            private set => SetProperty(ref _accountStatusMessage, value);
        }

        public Brush AccountStatusBrush
        {
            get => _accountStatusBrush;
            private set => SetProperty(ref _accountStatusBrush, value);
        }

        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string SecurityStatusMessage
        {
            get => _securityStatusMessage;
            private set => SetProperty(ref _securityStatusMessage, value);
        }

        public Brush SecurityStatusBrush
        {
            get => _securityStatusBrush;
            private set => SetProperty(ref _securityStatusBrush, value);
        }

        public bool LastPasswordChangeSucceeded
        {
            get => _lastPasswordChangeSucceeded;
            private set => SetProperty(ref _lastPasswordChangeSucceeded, value);
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
                : "Local Validated Beneficiaries Preview";

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

        public string BackupPresetName
        {
            get => _backupPresetName;
            private set => SetProperty(ref _backupPresetName, value);
        }

        public string LastFullBackupDisplay
        {
            get => _lastFullBackupDisplay;
            private set => SetProperty(ref _lastFullBackupDisplay, value);
        }

        public string LastIncrementalBackupDisplay
        {
            get => _lastIncrementalBackupDisplay;
            private set => SetProperty(ref _lastIncrementalBackupDisplay, value);
        }

        public string LastDifferentialBackupDisplay
        {
            get => _lastDifferentialBackupDisplay;
            private set => SetProperty(ref _lastDifferentialBackupDisplay, value);
        }

        public string BackupGuidanceMessage
        {
            get => _backupGuidanceMessage;
            private set => SetProperty(ref _backupGuidanceMessage, value);
        }

        public bool CanCreateDeltaBackups
        {
            get => _canCreateDeltaBackups;
            private set
            {
                if (SetProperty(ref _canCreateDeltaBackups, value))
                {
                    _createIncrementalBackupCommand?.RaiseCanExecuteChanged();
                    _createDifferentialBackupCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public string AppDatabaseStatusMessage
        {
            get => _appDatabaseStatusMessage;
            private set => SetProperty(ref _appDatabaseStatusMessage, value);
        }

        public Brush AppDatabaseStatusBrush
        {
            get => _appDatabaseStatusBrush;
            private set => SetProperty(ref _appDatabaseStatusBrush, value);
        }

        public string GgmsOfficeCode
        {
            get => _ggmsOfficeCode;
            set => SetProperty(ref _ggmsOfficeCode, value);
        }

        public string GgmsOfficeTable
        {
            get => _ggmsOfficeTable;
            set => SetProperty(ref _ggmsOfficeTable, value);
        }

        public string GgmsAllocationTable
        {
            get => _ggmsAllocationTable;
            set => SetProperty(ref _ggmsAllocationTable, value);
        }

        public string GgmsServer
        {
            get => _ggmsServer;
            set => SetProperty(ref _ggmsServer, value);
        }

        public string GgmsPortText
        {
            get => _ggmsPortText;
            set => SetProperty(ref _ggmsPortText, value);
        }

        public string GgmsDatabase
        {
            get => _ggmsDatabase;
            set => SetProperty(ref _ggmsDatabase, value);
        }

        public string GgmsUsername
        {
            get => _ggmsUsername;
            set => SetProperty(ref _ggmsUsername, value);
        }

        public string GgmsPassword
        {
            get => _ggmsPassword;
            set => SetProperty(ref _ggmsPassword, value);
        }

        public string GgmsStatusMessage
        {
            get => _ggmsStatusMessage;
            private set => SetProperty(ref _ggmsStatusMessage, value);
        }

        public Brush GgmsStatusBrush
        {
            get => _ggmsStatusBrush;
            private set => SetProperty(ref _ggmsStatusBrush, value);
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

        public string CurrentAppVersion
        {
            get => _currentAppVersion;
            private set => SetProperty(ref _currentAppVersion, value);
        }

        public string LatestAvailableVersion
        {
            get => _latestAvailableVersion;
            private set => SetProperty(ref _latestAvailableVersion, value);
        }

        public string UpdatePublishedAt
        {
            get => _updatePublishedAt;
            private set => SetProperty(ref _updatePublishedAt, value);
        }

        public string UpdateReleaseNotes
        {
            get => _updateReleaseNotes;
            private set => SetProperty(ref _updateReleaseNotes, value);
        }

        public string UpdateManifestUrl
        {
            get => _updateManifestUrl;
            set => SetProperty(ref _updateManifestUrl, value);
        }

        public string UpdateStatusMessage
        {
            get => _updateStatusMessage;
            private set => SetProperty(ref _updateStatusMessage, value);
        }

        public Brush UpdateStatusBrush
        {
            get => _updateStatusBrush;
            private set => SetProperty(ref _updateStatusBrush, value);
        }

        public string DownloadedInstallerLabel
        {
            get => _downloadedInstallerLabel;
            private set => SetProperty(ref _downloadedInstallerLabel, value);
        }

        public double UpdateDownloadProgressPercent
        {
            get => _updateDownloadProgressPercent;
            private set => SetProperty(ref _updateDownloadProgressPercent, value);
        }

        public bool IsUpdateDownloadInProgress
        {
            get => _isUpdateDownloadInProgress;
            private set
            {
                if (SetProperty(ref _isUpdateDownloadInProgress, value))
                {
                    OnPropertyChanged(nameof(ShowUpdateDownloadProgress));
                }
            }
        }

        public bool CheckForUpdatesOnStartup
        {
            get => _checkForUpdatesOnStartup;
            set => SetProperty(ref _checkForUpdatesOnStartup, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set
            {
                if (SetProperty(ref _isUpdateAvailable, value))
                {
                    RaiseUpdateUiStateChanged();
                }
            }
        }

        public bool HasPendingUpdate => _pendingAppUpdate != null;
        public bool ShowUpdateBanner => HasPendingUpdate || IsUpdateAvailable;
        public string UpdateBannerTitle => HasPendingUpdate
            ? "Update Ready to Install"
            : IsUpdateAvailable
                ? "Update Available"
                : string.Empty;
        public string UpdateBannerMessage => HasPendingUpdate
            ? $"Version {_pendingAppUpdate!.Version} is downloaded and ready to install."
            : IsUpdateAvailable
                ? CanDownloadUpdate
                    ? $"Version {LatestAvailableVersion} is available to download from Settings."
                    : $"Version {LatestAvailableVersion} is available. Open the download page until installer metadata is published."
                : string.Empty;
        public bool ShowDownloadUpdateActions => IsUpdateAvailable && !HasPendingUpdate;
        public bool ShowPendingUpdateActions => HasPendingUpdate;
        public bool ShowUpdateDownloadProgress => IsUpdateDownloadInProgress;
        public bool CanDownloadUpdate =>
            !IsBusy
            && IsUpdateAvailable
            && !HasPendingUpdate
            && !string.IsNullOrWhiteSpace(_updateInstallerUrl)
            && !string.IsNullOrWhiteSpace(_updateSha256);
        public bool CanInstallPendingUpdate => !IsBusy && HasPendingUpdate;
        public bool CanOpenUpdateDownloadPage => !string.IsNullOrWhiteSpace(_updateDownloadPageUrl);

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
                    _saveSystemProfileCommand.RaiseCanExecuteChanged();
                    _browseSystemLogoCommand.RaiseCanExecuteChanged();
                    _removeSystemLogoCommand.RaiseCanExecuteChanged();
                    _browseSystemLoginBackgroundCommand.RaiseCanExecuteChanged();
                    _removeSystemLoginBackgroundCommand.RaiseCanExecuteChanged();
                    _saveAccountCommand.RaiseCanExecuteChanged();
                    _changePasswordCommand.RaiseCanExecuteChanged();
                    _testConnectionCommand.RaiseCanExecuteChanged();
                    _saveImportConnectionCommand.RaiseCanExecuteChanged();
                    _snapshotMasterListCommand.RaiseCanExecuteChanged();
                    _importToStagingCommand.RaiseCanExecuteChanged();
                    _refreshPreviewCommand.RaiseCanExecuteChanged();
                    _openAdvancedLoadTablesCommand.RaiseCanExecuteChanged();
                    _createBackupCommand.RaiseCanExecuteChanged();
                    _createIncrementalBackupCommand.RaiseCanExecuteChanged();
                    _createDifferentialBackupCommand.RaiseCanExecuteChanged();
                    _restoreBackupCommand.RaiseCanExecuteChanged();
                    _migrateLocalAndRemoteCommand.RaiseCanExecuteChanged();
                    _saveFeatureRulesCommand.RaiseCanExecuteChanged();
                    _testGgmsConnectionCommand.RaiseCanExecuteChanged();
                    _saveGgmsSettingsCommand.RaiseCanExecuteChanged();
                    _checkForUpdatesCommand.RaiseCanExecuteChanged();
                    _downloadUpdateCommand.RaiseCanExecuteChanged();
                    _installPendingUpdateCommand.RaiseCanExecuteChanged();
                    _remindMeLaterCommand.RaiseCanExecuteChanged();
                    _saveUpdatePreferencesCommand.RaiseCanExecuteChanged();
                    _openUpdateDownloadPageCommand.RaiseCanExecuteChanged();
                    OnBusyStateChangedForOtp();
                }
            }
        }

        public ICommand SaveSystemProfileCommand => _saveSystemProfileCommand;
        public ICommand BrowseSystemLogoCommand => _browseSystemLogoCommand;
        public ICommand RemoveSystemLogoCommand => _removeSystemLogoCommand;
        public ICommand BrowseSystemLoginBackgroundCommand => _browseSystemLoginBackgroundCommand;
        public ICommand RemoveSystemLoginBackgroundCommand => _removeSystemLoginBackgroundCommand;
        public ICommand SaveAccountCommand => _saveAccountCommand;
        public ICommand ChangePasswordCommand => _changePasswordCommand;
        public ICommand TestConnectionCommand => _testConnectionCommand;
        public ICommand SaveImportConnectionCommand => _saveImportConnectionCommand;
        public ICommand SnapshotMasterListCommand => _snapshotMasterListCommand;
        public ICommand ImportToStagingCommand => _importToStagingCommand;
        public ICommand RefreshPreviewCommand => _refreshPreviewCommand;
        public ICommand OpenAdvancedLoadTablesCommand => _openAdvancedLoadTablesCommand;
        public ICommand CreateBackupCommand => _createBackupCommand;
        public ICommand CreateIncrementalBackupCommand => _createIncrementalBackupCommand;
        public ICommand CreateDifferentialBackupCommand => _createDifferentialBackupCommand;
        public ICommand RestoreBackupCommand => _restoreBackupCommand;
        public ICommand MigrateLocalAndRemoteCommand => _migrateLocalAndRemoteCommand;
        public ICommand SaveFeatureRulesCommand => _saveFeatureRulesCommand;
        public ICommand TestGgmsConnectionCommand => _testGgmsConnectionCommand;
        public ICommand SaveGgmsSettingsCommand => _saveGgmsSettingsCommand;
        public ICommand CheckForUpdatesCommand => _checkForUpdatesCommand;
        public ICommand DownloadUpdateCommand => _downloadUpdateCommand;
        public ICommand InstallPendingUpdateCommand => _installPendingUpdateCommand;
        public ICommand RemindMeLaterCommand => _remindMeLaterCommand;
        public ICommand SaveUpdatePreferencesCommand => _saveUpdatePreferencesCommand;
        public ICommand OpenUpdateDownloadPageCommand => _openUpdateDownloadPageCommand;

        private void LoadSystemProfile()
        {
            var settings = SystemProfileSettingsService.Load();
            SystemName = settings.SystemName;
            SystemOwner = settings.Owner;
            SystemCompanyAddress = settings.CompanyAddress;
            SystemEmail = settings.Email;
            SystemContactNumber = settings.ContactNumber;
            SystemLogoPath = settings.LogoPath;
            _savedSystemLogoPath = settings.LogoPath;
            SystemLoginBackgroundPath = settings.LoginBackgroundPath;
            _savedSystemLoginBackgroundPath = settings.LoginBackgroundPath;
            SystemInstallSerial = settings.InstallSerial;
            RefreshSystemLogoPreview();
            RefreshSystemLoginBackgroundPreview();
        }

        private void LoadCurrentUserAccount()
        {
            if (_currentUser == null)
            {
                AccountStatusMessage = "Sign in first before editing account details.";
                SecurityStatusMessage = "Sign in first before changing a password.";
                return;
            }

            using var context = _dbContextFactory();
            var snapshot = UserAccountSettingsService.Load(context, _currentUser.Id);
            AccountFullName = snapshot.FullName;
            AccountUsername = snapshot.Username;
            AccountEmail = snapshot.Email;
            AccountContactNumber = snapshot.ContactNumber;
        }

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

        private void LoadGgmsSettings()
        {
            var settings = BudgetRuntimeOptions.Load();
            GgmsOfficeCode = settings.AyudaOfficeCode;
            GgmsOfficeTable = settings.GgmsOfficeTable;
            GgmsAllocationTable = settings.GgmsAllocationTable;
            GgmsServer = settings.GgmsConnection.Server;
            GgmsPortText = settings.GgmsConnection.Port.ToString(CultureInfo.InvariantCulture);
            GgmsDatabase = settings.GgmsConnection.Database;
            GgmsUsername = settings.GgmsConnection.Username;
            GgmsPassword = settings.GgmsConnection.Password;
        }

        private void LoadUpdatePreferences()
        {
            var preferences = AppPreferencesService.Load();
            CheckForUpdatesOnStartup = preferences.CheckForUpdatesOnStartup;
            UpdateManifestUrl = preferences.UpdateManifestUrl;
            LoadPendingUpdate();
            ApplyUpdateResult(AppUpdateCoordinator.LatestResult, preserveNotCheckedMessage: true);

            if (AppUpdateCoordinator.LatestResult.Status == UpdateCheckStatus.NotChecked
                && CheckForUpdatesOnStartup
                && !string.IsNullOrWhiteSpace(UpdateManifestUrl))
            {
                _ = ExecuteCheckForUpdatesAsync(backgroundCheckOnly: true);
            }
        }

        private void LoadPendingUpdate()
        {
            var pending = AppUpdatePackageService.LoadPendingUpdate();
            if (pending != null && !File.Exists(pending.InstallerPath))
            {
                AppUpdatePackageService.ClearPendingUpdate();
                pending = null;
            }

            _pendingAppUpdate = pending;
            DownloadedInstallerLabel = pending == null
                ? "No downloaded installer is waiting yet."
                : $"{pending.InstallerFileName} ({pending.Version})";

            if (pending != null)
            {
                SetUpdateSuccess($"Version {pending.Version} is downloaded and ready to install.");
            }

            RaiseUpdateUiStateChanged();
        }

        private void ExecuteSaveSystemProfile()
        {
            var systemName = SystemName.Trim();
            var owner = SystemOwner.Trim();
            var companyAddress = SystemCompanyAddress.Trim();
            var email = SystemEmail.Trim();
            var contactNumber = SystemContactNumber.Trim();
            var logoPath = SystemLogoPath.Trim();
            var loginBackgroundPath = SystemLoginBackgroundPath.Trim();

            if (string.IsNullOrWhiteSpace(systemName))
            {
                SetSystemProfileError("Enter a system name.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            {
                SetSystemProfileError("Enter a valid system email address.");
                return;
            }

            SystemProfileSettingsService.Save(new SystemProfileSettingsModel
            {
                SystemName = systemName,
                Owner = owner,
                CompanyAddress = companyAddress,
                Email = email,
                ContactNumber = contactNumber,
                LogoPath = logoPath,
                LoginBackgroundPath = loginBackgroundPath,
                InstallSerial = SystemInstallSerial
            });

            if (!string.Equals(_savedSystemLogoPath, logoPath, StringComparison.OrdinalIgnoreCase))
            {
                SystemProfileSettingsService.RemoveStoredLogo(_savedSystemLogoPath);
                _savedSystemLogoPath = logoPath;
            }

            if (!string.Equals(_savedSystemLoginBackgroundPath, loginBackgroundPath, StringComparison.OrdinalIgnoreCase))
            {
                SystemProfileSettingsService.RemoveStoredLogo(_savedSystemLoginBackgroundPath);
                _savedSystemLoginBackgroundPath = loginBackgroundPath;
            }

            SystemName = systemName;
            SystemOwner = owner;
            SystemCompanyAddress = companyAddress;
            SystemEmail = email;
            SystemContactNumber = contactNumber;
            SetSystemProfileSuccess("System profile saved.");
        }

        private void ExecuteBrowseSystemLogo()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var disposableWorkingLogoPath = GetDisposableWorkingLogoPath();
                SystemLogoPath = SystemProfileSettingsService.CopyLogoToBrandingFolder(dialog.FileName, disposableWorkingLogoPath);
                RefreshSystemLogoPreview();
                SetSystemProfileSuccess("Logo selected. Save system profile to apply the updated branding.");
            }
            catch (Exception ex)
            {
                SetSystemProfileError($"Unable to use the selected logo. {ex.Message}");
            }
        }

        private void ExecuteRemoveSystemLogo()
        {
            var disposableWorkingLogoPath = GetDisposableWorkingLogoPath();
            if (!string.IsNullOrWhiteSpace(disposableWorkingLogoPath))
            {
                SystemProfileSettingsService.RemoveStoredLogo(disposableWorkingLogoPath);
            }

            SystemLogoPath = string.Empty;
            RefreshSystemLogoPreview();
            SetSystemProfileSuccess("Custom logo removed. Save system profile to restore the default logo.");
        }

        private void ExecuteBrowseSystemLoginBackground()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var disposableWorkingBackgroundPath = GetDisposableWorkingLoginBackgroundPath();
                SystemLoginBackgroundPath = SystemProfileSettingsService.CopyBackgroundToBrandingFolder(dialog.FileName, disposableWorkingBackgroundPath);
                RefreshSystemLoginBackgroundPreview();
                SetSystemProfileSuccess("Background selected. Save system profile to apply the updated branding.");
            }
            catch (Exception ex)
            {
                SetSystemProfileError($"Unable to use the selected background. {ex.Message}");
            }
        }

        private void ExecuteRemoveSystemLoginBackground()
        {
            var disposableWorkingBackgroundPath = GetDisposableWorkingLoginBackgroundPath();
            if (!string.IsNullOrWhiteSpace(disposableWorkingBackgroundPath))
            {
                SystemProfileSettingsService.RemoveStoredLogo(disposableWorkingBackgroundPath);
            }

            SystemLoginBackgroundPath = string.Empty;
            RefreshSystemLoginBackgroundPreview();
            SetSystemProfileSuccess("Custom background removed. Save system profile to restore the default background.");
        }

        private void ExecuteSaveAccount()
        {
            if (_currentUser == null)
            {
                SetAccountError("No signed-in user is available for account updates.");
                return;
            }

            using var context = _dbContextFactory();
            var result = UserAccountSettingsService.SaveAccount(
                context,
                _currentUser,
                new AccountSettingsUpdateRequest(
                    AccountFullName,
                    AccountUsername,
                    AccountEmail,
                    AccountContactNumber));

            if (!result.IsSuccess)
            {
                SetAccountError(result.Message);
                return;
            }

            LoadCurrentUserAccount();
            SetAccountSuccess(result.Message);
        }

        private void ExecuteChangePassword()
        {
            LastPasswordChangeSucceeded = false;

            if (_currentUser == null)
            {
                SetSecurityError("No signed-in user is available for password changes.");
                return;
            }

            using var context = _dbContextFactory();
            var result = UserAccountSettingsService.ChangePassword(
                context,
                _currentUser,
                new PasswordChangeRequest(CurrentPassword, NewPassword, ConfirmPassword));

            if (!result.IsSuccess)
            {
                SetSecurityError(result.Message);
                return;
            }

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            LastPasswordChangeSucceeded = true;
            SetSecuritySuccess(result.Message);
        }

        private void RefreshTargetSummaries()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            var summary = ConnectionSettingsService.FormatPresetSummary(preset);
            ActiveTargetSummary = summary;
            BackupTargetSummary = summary;
            BackupPresetName = preset.DisplayName;
            RefreshBackupStatus(settings.SelectedPreset);
        }

        private void RefreshBackupStatus(string presetKey)
        {
            var status = BackupCatalogService.GetPresetStatus(presetKey);
            LastFullBackupDisplay = FormatBackupTimestamp(status.LastFullBackupAt, "No full backup recorded for this preset yet.");
            LastIncrementalBackupDisplay = FormatBackupTimestamp(status.LastIncrementalBackupAt, "No incremental backup recorded for this preset yet.");
            LastDifferentialBackupDisplay = FormatBackupTimestamp(status.LastDifferentialBackupAt, "No differential backup recorded for this preset yet.");
            CanCreateDeltaBackups = status.CanCreateDeltaBackups;
            BackupGuidanceMessage = status.CanCreateDeltaBackups
                ? $"Full, incremental, and differential backups are available for {BackupPresetName}. Incremental compares against the latest incremental chain for the latest full backup. Differential compares against the latest full backup only."
                : $"Create a full backup first for {BackupPresetName} before incremental or differential backups are allowed.";
        }

        private static string FormatBackupTimestamp(DateTime? timestamp, string emptyMessage)
        {
            return timestamp.HasValue
                ? timestamp.Value.ToString("MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)
                : emptyMessage;
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
                    ? "`val_beneficiaries` is not in the active database yet. Run Snapshot Validated Beneficiaries first."
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

        private async Task ExecuteCreateBackupAsync(string backupType)
        {
            RefreshTargetSummaries();

            var targetPreset = GetTargetPreset();
            var normalizedType = BackupChainService.NormalizeBackupType(backupType);
            if (!string.Equals(normalizedType, BackupTypes.Full, StringComparison.OrdinalIgnoreCase) && !CanCreateDeltaBackups)
            {
                SetBackupError("Create a full backup first for the active preset before using incremental or differential backups.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Barangay Backup (*.zip)|*.zip",
                AddExtension = true,
                DefaultExt = ".zip",
                FileName = $"{targetPreset.Database}_{normalizedType.ToLowerInvariant()}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    SetBackupNeutral($"Creating {normalizedType.ToLowerInvariant()} backup...");
                    var result = await LocalBackupService.CreateBackupAsync(dialog.FileName, normalizedType);
                    if (result.IsSuccess)
                    {
                        SetBackupSuccess(result.Message);
                        LastBackupActivity = $"Last {normalizedType.ToLowerInvariant()} backup: {dialog.FileName}";
                        RefreshTargetSummaries();
                        return;
                    }

                    SetBackupError(result.Message);
                    LastBackupActivity = "Backup failed in this session.";
                });
        }

        private async Task ExecuteRestoreBackupAsync()
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
                    SetBackupNeutral("Restoring database backup chain...");
                    var result = await LocalBackupService.RestoreBackupAsync(dialog.FileName);
                    if (result.IsSuccess)
                    {
                        SetBackupSuccess(result.Message);
                        LastBackupActivity = $"Last restored backup target: {dialog.FileName}";
                        RefreshTargetSummaries();
                        await RefreshPreviewCoreAsync();
                        return;
                    }

                    SetBackupError(result.Message);
                    LastBackupActivity = "Backup restore failed in this session.";
                });
        }

        private async Task ExecuteMigrateLocalAndRemoteAsync()
        {
            await ExecuteBusyAsync(
                async () =>
                {
                    SetAppDatabaseNeutral("Migrating Local and Remote app databases...");

                    var result = await AppDatabaseMigrationService.MigrateLocalAndRemoteAsync();
                    RefreshTargetSummaries();

                    var statusMessage = string.Join(" ", result.PresetResults.Select(item => item.Message));
                    if (result.IsSuccess)
                    {
                        SetAppDatabaseSuccess(statusMessage);
                        return;
                    }

                    SetAppDatabaseError(statusMessage);
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

        private async Task ExecuteTestGgmsConnectionAsync()
        {
            if (!TryBuildGgmsSettings(requireConnectionDetails: true, out var settings, out var validationMessage))
            {
                SetGgmsError(validationMessage);
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    SetGgmsNeutral("Testing GGMS budget source connection...");
                    var result = await ConnectionSettingsService.TestConnectionAsync(settings.GgmsConnection);
                    if (result.IsSuccess)
                    {
                        SetGgmsSuccess(result.Message);
                    }
                    else
                    {
                        SetGgmsError(result.Message);
                    }
                });
        }

        private void ExecuteSaveGgmsSettings()
        {
            if (!TryBuildGgmsSettings(requireConnectionDetails: false, out var settings, out var validationMessage))
            {
                SetGgmsError(validationMessage);
                return;
            }

            BudgetRuntimeOptions.Save(settings);
            SetGgmsSuccess("GGMS budget source settings saved.");
        }

        private async Task ExecuteCheckForUpdatesAsync(bool backgroundCheckOnly = false)
        {
            if (backgroundCheckOnly)
            {
                var result = await AppUpdateCoordinator.CheckNowAsync(UpdateManifestUrl);
                ApplyUpdateResult(result, preserveNotCheckedMessage: false);
                return;
            }

            await ExecuteBusyAsync(
                async () =>
                {
                    SetUpdateNeutral("Checking for updates...");
                    var result = await AppUpdateCoordinator.CheckNowAsync(UpdateManifestUrl);
                    ApplyUpdateResult(result, preserveNotCheckedMessage: false);
                });
        }

        private async Task ExecuteDownloadUpdateAsync()
        {
            if (!AppUpdateCoordinator.LatestResult.CanDownloadInstaller)
            {
                SetUpdateError("The latest update result does not include a downloadable installer yet.");
                return;
            }

            PendingAppUpdate? downloadedUpdate = null;

            await ExecuteBusyAsync(
                async () =>
                {
                    IsUpdateDownloadInProgress = true;
                    UpdateDownloadProgressPercent = 0;

                    try
                    {
                        var progress = new Progress<UpdateDownloadProgress>(update =>
                        {
                            UpdateDownloadProgressPercent = update.PercentComplete;
                            if (update.TotalBytes.HasValue && update.TotalBytes.Value > 0)
                            {
                                SetUpdateNeutral($"Downloading version {AppUpdateCoordinator.LatestResult.LatestVersion}... {update.PercentComplete:0.#}%");
                            }
                            else
                            {
                                SetUpdateNeutral($"Downloading version {AppUpdateCoordinator.LatestResult.LatestVersion}...");
                            }
                        });

                        downloadedUpdate = await AppUpdatePackageService.DownloadUpdateAsync(
                            AppUpdateCoordinator.LatestResult,
                            progress);

                        _pendingAppUpdate = downloadedUpdate;
                        DownloadedInstallerLabel = $"{downloadedUpdate.InstallerFileName} ({downloadedUpdate.Version})";
                        SetUpdateSuccess($"Downloaded version {downloadedUpdate.Version} and verified the installer.");
                        RaiseUpdateUiStateChanged();
                    }
                    catch (Exception ex)
                    {
                        SetUpdateError($"Unable to download the installer. {ex.Message}");
                    }
                    finally
                    {
                        IsUpdateDownloadInProgress = false;
                    }
                });

            if (downloadedUpdate == null)
            {
                return;
            }

            var choice = MessageBox.Show(
                $"Version {downloadedUpdate.Version} is ready to install. Install it now?",
                "Update Ready",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Yes)
            {
                ExecuteInstallPendingUpdate(skipConfirmation: true);
            }
            else
            {
                ExecuteRemindMeLater();
            }
        }

        private void ExecuteInstallPendingUpdate(bool skipConfirmation = false)
        {
            if (_pendingAppUpdate == null)
            {
                SetUpdateError("There is no downloaded installer ready to run.");
                return;
            }

            if (!File.Exists(_pendingAppUpdate.InstallerPath))
            {
                AppUpdatePackageService.ClearPendingUpdate();
                _pendingAppUpdate = null;
                DownloadedInstallerLabel = "No downloaded installer is waiting yet.";
                RaiseUpdateUiStateChanged();
                SetUpdateError("The downloaded installer is missing. Download the update again.");
                return;
            }

            if (!skipConfirmation)
            {
                var confirm = MessageBox.Show(
                    $"Install version {_pendingAppUpdate.Version} now? The app will close while setup runs and reopen after the upgrade completes.",
                    "Install Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            try
            {
                AppUpdatePackageService.LaunchInstaller(_pendingAppUpdate);
                Application.Current?.Shutdown();
            }
            catch (Exception ex)
            {
                SetUpdateError($"Unable to start the installer. {ex.Message}");
            }
        }

        private void ExecuteRemindMeLater()
        {
            if (_pendingAppUpdate == null)
            {
                SetUpdateNeutral("No downloaded installer is waiting right now.");
                return;
            }

            SetUpdateNeutral($"Version {_pendingAppUpdate.Version} is downloaded. Install it later from Settings > Updates.");
        }

        private void ExecuteSaveUpdatePreferences()
        {
            AppPreferencesService.Save(new AppPreferencesModel
            {
                CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
                UpdateManifestUrl = UpdateManifestUrl
            });

            SetUpdateSuccess("Update preferences saved.");
        }

        private void ExecuteOpenUpdateDownloadPage()
        {
            if (string.IsNullOrWhiteSpace(_updateDownloadPageUrl))
            {
                SetUpdateError("No release page is available for the current update result.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateDownloadPageUrl,
                    UseShellExecute = true
                });

                SetUpdateSuccess("Opened the update download page in your browser.");
            }
            catch (Exception ex)
            {
                SetUpdateError($"Unable to open the update download page. {ex.Message}");
            }
        }

        private static Brush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }

        private void RefreshSystemLogoPreview()
        {
            SystemLogoImage = LocalImageLoader.Load(SystemLogoPath);
        }

        private string? GetDisposableWorkingLogoPath()
        {
            if (string.IsNullOrWhiteSpace(SystemLogoPath))
            {
                return null;
            }

            return string.Equals(SystemLogoPath, _savedSystemLogoPath, StringComparison.OrdinalIgnoreCase)
                ? null
                : SystemLogoPath;
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

        private void SetSystemProfileSuccess(string message)
        {
            SystemProfileStatusMessage = message;
            SystemProfileStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetSystemProfileError(string message)
        {
            SystemProfileStatusMessage = message;
            SystemProfileStatusBrush = CreateBrush("#991B1B");
        }

        private void SetAccountSuccess(string message)
        {
            AccountStatusMessage = message;
            AccountStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetAccountError(string message)
        {
            AccountStatusMessage = message;
            AccountStatusBrush = CreateBrush("#991B1B");
        }

        private void SetSecuritySuccess(string message)
        {
            SecurityStatusMessage = message;
            SecurityStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetSecurityNeutral(string message)
        {
            SecurityStatusMessage = message;
            SecurityStatusBrush = CreateBrush("#6B7280");
        }

        private void SetSecurityError(string message)
        {
            SecurityStatusMessage = message;
            SecurityStatusBrush = CreateBrush("#991B1B");
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

        private void SetAppDatabaseNeutral(string message)
        {
            AppDatabaseStatusMessage = message;
            AppDatabaseStatusBrush = CreateBrush("#6B7280");
        }

        private void SetAppDatabaseSuccess(string message)
        {
            AppDatabaseStatusMessage = message;
            AppDatabaseStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetAppDatabaseError(string message)
        {
            AppDatabaseStatusMessage = message;
            AppDatabaseStatusBrush = CreateBrush("#991B1B");
        }

        private void SetGgmsNeutral(string message)
        {
            GgmsStatusMessage = message;
            GgmsStatusBrush = CreateBrush("#6B7280");
        }

        private void SetGgmsSuccess(string message)
        {
            GgmsStatusMessage = message;
            GgmsStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetGgmsError(string message)
        {
            GgmsStatusMessage = message;
            GgmsStatusBrush = CreateBrush("#991B1B");
        }

        private void SetUpdateNeutral(string message)
        {
            UpdateStatusMessage = message;
            UpdateStatusBrush = CreateBrush("#6B7280");
        }

        private void SetUpdateSuccess(string message)
        {
            UpdateStatusMessage = message;
            UpdateStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetUpdateError(string message)
        {
            UpdateStatusMessage = message;
            UpdateStatusBrush = CreateBrush("#991B1B");
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

        private bool TryBuildGgmsSettings(bool requireConnectionDetails, out BudgetRuntimeOptions settings, out string validationMessage)
        {
            settings = new BudgetRuntimeOptions();
            validationMessage = string.Empty;

            if (!int.TryParse(GgmsPortText, out var port) || port <= 0 || port > 65535)
            {
                validationMessage = "Enter a valid GGMS MySQL port between 1 and 65535.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(GgmsOfficeTable))
            {
                validationMessage = "Enter the GGMS office table name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(GgmsAllocationTable))
            {
                validationMessage = "Enter the GGMS allocation table name.";
                return false;
            }

            if (requireConnectionDetails)
            {
                if (string.IsNullOrWhiteSpace(GgmsServer))
                {
                    validationMessage = "Enter the GGMS server or host name.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(GgmsDatabase))
                {
                    validationMessage = "Enter the GGMS database name.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(GgmsUsername))
                {
                    validationMessage = "Enter the GGMS database username.";
                    return false;
                }
            }

            settings = new BudgetRuntimeOptions
            {
                AyudaOfficeCode = GgmsOfficeCode.Trim(),
                GgmsOfficeTable = GgmsOfficeTable.Trim(),
                GgmsAllocationTable = GgmsAllocationTable.Trim(),
                GgmsConnection = new DatabaseConnectionPreset
                {
                    DisplayName = "GGMS Budget Source",
                    Server = GgmsServer.Trim(),
                    Port = port,
                    Database = GgmsDatabase.Trim(),
                    Username = GgmsUsername.Trim(),
                    Password = GgmsPassword
                }
            };

            return true;
        }

        private void ApplyUpdateResult(UpdateCheckResult result, bool preserveNotCheckedMessage)
        {
            CurrentAppVersion = string.IsNullOrWhiteSpace(result.CurrentVersion)
                ? AppVersionService.GetCurrentVersion()
                : result.CurrentVersion;

            if (result.Status == UpdateCheckStatus.NotChecked && preserveNotCheckedMessage)
            {
                IsUpdateAvailable = false;
                return;
            }

            LatestAvailableVersion = string.IsNullOrWhiteSpace(result.LatestVersion)
                ? "Not checked yet"
                : result.LatestVersion;
            UpdatePublishedAt = string.IsNullOrWhiteSpace(result.PublishedAt)
                ? "Not published yet"
                : result.PublishedAt;
            UpdateReleaseNotes = result.Notes.Count == 0
                ? "No release notes were provided."
                : string.Join(Environment.NewLine, result.Notes.Select(note => $"- {note}"));
            _updateDownloadPageUrl = result.ReleasePageUrl ?? string.Empty;
            _updateInstallerUrl = result.InstallerUrl ?? string.Empty;
            _updateSha256 = result.Sha256 ?? string.Empty;
            OnPropertyChanged(nameof(CanOpenUpdateDownloadPage));
            _openUpdateDownloadPageCommand.RaiseCanExecuteChanged();

            if (_pendingAppUpdate != null
                && result.Status == UpdateCheckStatus.UpdateAvailable
                && AppVersionService.TryParseVersion(result.LatestVersion, out var latestParsed)
                && AppVersionService.TryParseVersion(_pendingAppUpdate.Version, out var pendingParsed)
                && latestParsed > pendingParsed)
            {
                AppUpdatePackageService.ClearPendingUpdate();
                _pendingAppUpdate = null;
                DownloadedInstallerLabel = "No downloaded installer is waiting yet.";
            }

            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    IsUpdateAvailable = true;
                    SetUpdateSuccess(result.Message);
                    break;
                case UpdateCheckStatus.UpToDate:
                    IsUpdateAvailable = false;
                    SetUpdateSuccess(result.Message);
                    break;
                case UpdateCheckStatus.NotConfigured:
                    IsUpdateAvailable = false;
                    SetUpdateNeutral(result.Message);
                    break;
                case UpdateCheckStatus.Failed:
                    IsUpdateAvailable = false;
                    SetUpdateError(result.Message);
                    break;
                default:
                    IsUpdateAvailable = false;
                    if (!preserveNotCheckedMessage)
                    {
                        SetUpdateNeutral(result.Message);
                    }

                    break;
            }

            if (_pendingAppUpdate != null)
            {
                SetUpdateSuccess($"Version {_pendingAppUpdate.Version} is downloaded and ready to install.");
            }

            RaiseUpdateUiStateChanged();
        }

        private void RaiseUpdateUiStateChanged()
        {
            OnPropertyChanged(nameof(HasPendingUpdate));
            OnPropertyChanged(nameof(ShowUpdateBanner));
            OnPropertyChanged(nameof(UpdateBannerTitle));
            OnPropertyChanged(nameof(UpdateBannerMessage));
            OnPropertyChanged(nameof(ShowDownloadUpdateActions));
            OnPropertyChanged(nameof(ShowPendingUpdateActions));
            OnPropertyChanged(nameof(CanDownloadUpdate));
            OnPropertyChanged(nameof(CanInstallPendingUpdate));
            OnPropertyChanged(nameof(ShowUpdateDownloadProgress));
            _downloadUpdateCommand.RaiseCanExecuteChanged();
            _installPendingUpdateCommand.RaiseCanExecuteChanged();
            _remindMeLaterCommand.RaiseCanExecuteChanged();
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

        private static bool IsValidEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshSystemLoginBackgroundPreview()
        {
            SystemLoginBackgroundImage = LocalImageLoader.Load(SystemLoginBackgroundPath);
        }

        private string? GetDisposableWorkingLoginBackgroundPath()
        {
            if (string.IsNullOrWhiteSpace(SystemLoginBackgroundPath))
            {
                return null;
            }

            return string.Equals(SystemLoginBackgroundPath, _savedSystemLoginBackgroundPath, StringComparison.OrdinalIgnoreCase)
                ? null
                : SystemLoginBackgroundPath;
        }
    }
}

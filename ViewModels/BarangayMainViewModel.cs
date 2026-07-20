using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BarangayMainViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private string _currentSection = "Dashboard";
        private object _currentView;
        private string _currentSectionTitle = "Dashboard";
        private string _currentSectionSubtitle = "Monitor validated beneficiaries, pending approvals, aid requests, budgets, and cash-for-work at a glance.";
        private string _connectionSummary = string.Empty;
        private ImageSource? _officeLogoImage;
        private ImageSource? _userPhotoImage;
        private string _userDisplayName = "Barangay Administrator";
        private string _officeName = "Local Government Unit";
        private string _softwareTitle = "eKalinga+ Ayuda Management System";
        private string _softwareSubtitle = "Centralized barangay assistance operations";
        private string _connectionStatusText = "Connecting...";
        private Brush _connectionStatusBrush = Brushes.Gray;
        private string _ggmsStatusText = "GGMS: Checking...";

        public BarangayMainViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _currentView = BuildView(_currentSection);

            ShowDashboardCommand = new RelayCommand(_ => SwitchSection("Dashboard"));
            ShowCashForWorkCommand = new RelayCommand(_ => SwitchSection("CashForWork"));
            ShowCashForWorkPayoutCommand = new RelayCommand(_ => SwitchSection("CashForWorkPayout"));
            ShowBudgetCommand = new RelayCommand(_ => SwitchSection("Budget"));
            ShowDistributionCommand = new RelayCommand(_ => SwitchSection("Distribution"));
            ShowMasterListCommand = new RelayCommand(_ => SwitchSection("MasterList"));
            ShowAssistanceCasesCommand = new RelayCommand(_ => SwitchSection("AssistanceCases"));
            ShowBorrowingCommand = new RelayCommand(_ => SwitchSection("Borrowing"));
            ShowGgmsTransactionsCommand = new RelayCommand(_ => SwitchSection("GgmsTransactions"));
            ShowReportsCommand = new RelayCommand(_ => SwitchSection("Reports"));
            ShowScanningPortalCommand = new RelayCommand(_ => SwitchSection("ScanningPortal"));
            RefreshBranding();
            RefreshConnectionSummary();
            LoadUserSummary();

            // Subscribe to connectivity and sync events
            ConnectivityService.Instance.ConnectivityChanged += OnConnectivityChanged;
            SyncService.Instance.SyncStatusChanged += OnSyncStatusChanged;
            SyncService.Instance.LastSyncUpdated += OnLastSyncUpdated;

            // Trigger initial state
            UpdateConnectivityStatus(ConnectivityService.Instance.IsOnline, ConnectivityService.Instance.IsGgmsAvailable);
        }

        public object CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        public User CurrentUser => _currentUser;

        public string CurrentSectionTitle
        {
            get => _currentSectionTitle;
            private set => SetProperty(ref _currentSectionTitle, value);
        }

        public string CurrentSectionSubtitle
        {
            get => _currentSectionSubtitle;
            private set => SetProperty(ref _currentSectionSubtitle, value);
        }

        public string ConnectionSummary
        {
            get => _connectionSummary;
            private set => SetProperty(ref _connectionSummary, value);
        }

        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            private set => SetProperty(ref _connectionStatusText, value);
        }

        public Brush ConnectionStatusBrush
        {
            get => _connectionStatusBrush;
            private set => SetProperty(ref _connectionStatusBrush, value);
        }

        public string GgmsStatusText
        {
            get => _ggmsStatusText;
            private set => SetProperty(ref _ggmsStatusText, value);
        }

        public string OfficeName
        {
            get => _officeName;
            private set
            {
                if (SetProperty(ref _officeName, value))
                {
                    OnPropertyChanged(nameof(OfficeInitials));
                }
            }
        }

        public string OfficeProfileLabel => "Office Profile";

        public string SoftwareTitle
        {
            get => _softwareTitle;
            private set => SetProperty(ref _softwareTitle, value);
        }

        public string SoftwareSubtitle
        {
            get => _softwareSubtitle;
            private set => SetProperty(ref _softwareSubtitle, value);
        }

        public ImageSource? OfficeLogoImage
        {
            get => _officeLogoImage;
            private set
            {
                if (SetProperty(ref _officeLogoImage, value))
                {
                    OnPropertyChanged(nameof(HasOfficeLogo));
                }
            }
        }

        public bool HasOfficeLogo => OfficeLogoImage != null;

        public string OfficeInitials => BuildInitials(OfficeName);

        public ImageSource? UserPhotoImage
        {
            get => _userPhotoImage;
            private set => SetProperty(ref _userPhotoImage, value);
        }

        public string UserDisplayName
        {
            get => _userDisplayName;
            private set => SetProperty(ref _userDisplayName, value);
        }

        public bool IsDashboardSelected => _currentSection == "Dashboard";
        public bool IsCashForWorkSelected => _currentSection == "CashForWork";
        public bool IsBudgetSelected => _currentSection == "Budget";
        public bool IsDistributionSelected => _currentSection == "Distribution";
        public bool IsMasterListSelected => _currentSection == "MasterList";
        public bool IsAssistanceCasesSelected => _currentSection == "AssistanceCases";
        public bool IsGgmsTransactionsSelected => _currentSection == "GgmsTransactions";
        public bool IsReportsSelected => _currentSection == "Reports";
        public bool IsSecondarySectionVisible => _currentSection != "Dashboard";

        public Visibility DashboardVisibility => UserPermissionService.CanAccessDashboard ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MasterListVisibility => UserPermissionService.CanAccessMasterList ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AssistanceCasesVisibility => UserPermissionService.CanAccessAssistanceCases ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BudgetVisibility => UserPermissionService.CanAccessBudget ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DistributionVisibility => UserPermissionService.CanAccessDistribution ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CashForWorkVisibility => UserPermissionService.CanAccessCashForWork ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BorrowingVisibility => Visibility.Collapsed; // Permanently hidden as per GEMINI.md
        public Visibility ReportsVisibility => UserPermissionService.CanAccessReports ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GgmsTransactionsVisibility => UserPermissionService.CanAccessGgmsTransactions ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UserManagementVisibility => UserPermissionService.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ScanningPortalVisibility => Visibility.Collapsed; // Launch pad tile hidden per request

        public RelayCommand ShowDashboardCommand { get; }
        public RelayCommand ShowCashForWorkCommand { get; }
        public RelayCommand ShowCashForWorkPayoutCommand { get; }
        public RelayCommand ShowBudgetCommand { get; }
        public RelayCommand ShowDistributionCommand { get; }
        public RelayCommand ShowMasterListCommand { get; }
        public RelayCommand ShowScanningPortalCommand { get; }
        public ICommand ShowAssistanceCasesCommand { get; }
        public ICommand ShowBorrowingCommand { get; }
        public ICommand ShowGgmsTransactionsCommand { get; }
        public ICommand ShowReportsCommand { get; }

        public void RefreshConnectionSummary()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            ConnectionSummary = $"{preset.DisplayName} | {preset.Server}:{preset.Port} / {preset.Database}";
        }

        public void RefreshBranding()
        {
            var branding = SystemProfileSettingsService.BuildLoginBranding(SystemProfileSettingsService.Load());
            OfficeName = branding.Title;
            SoftwareTitle = BuildSoftwareTitle(branding.Subtitle);
            SoftwareSubtitle = "Centralized barangay assistance operations";
            OfficeLogoImage = LocalImageLoader.Load(branding.LogoPath);
        }

        public void ReloadCurrentView()
        {
            CurrentView = BuildView(_currentSection);
        }

        public void RefreshUserSummary()
        {
            LoadUserSummary();
        }

        private void SwitchSection(string section)
        {
            if (_currentSection == section)
            {
                ReloadCurrentView();
                return;
            }

            _currentSection = section;
            OnPropertyChanged(nameof(IsDashboardSelected));
            OnPropertyChanged(nameof(IsCashForWorkSelected));
            OnPropertyChanged(nameof(IsBudgetSelected));
            OnPropertyChanged(nameof(IsDistributionSelected));
            OnPropertyChanged(nameof(IsMasterListSelected));
            OnPropertyChanged(nameof(IsAssistanceCasesSelected));
            OnPropertyChanged(nameof(IsGgmsTransactionsSelected));
            OnPropertyChanged(nameof(IsReportsSelected));
            OnPropertyChanged(nameof(IsSecondarySectionVisible));
            CurrentView = BuildView(section);
        }

        private object BuildView(string section)
        {
            switch (section)
            {
                case "Dashboard":
                    CurrentSectionTitle = "Dashboard";
                    CurrentSectionSubtitle = "Monitor validated beneficiaries, pending approvals, aid requests, budgets, and cash-for-work at a glance.";
                    return new BarangayDashboardPage();
                case "Budget":
                    CurrentSectionTitle = "Budget";
                    CurrentSectionSubtitle = "Sync GGMS allocation, record private donations, and audit the shared Ayuda release ledger.";
                    return new BudgetPage(_currentUser);
                case "Distribution":
                    CurrentSectionTitle = "Project Distribution";
                    CurrentSectionSubtitle = "Select a project first, attach approved beneficiaries, and use the phone scanner to mark one claim per beneficiary.";
                    return new ProjectDistributionPage(_currentUser);
                case "MasterList":
                    CurrentSectionTitle = "Validated Beneficiaries";
                    CurrentSectionSubtitle = "Browse the full registry of validated beneficiaries, search by name or ID, and view individual profiles.";
                    return new MasterListPage(_currentUser);
                case "AssistanceCases":
                    CurrentSectionTitle = "Aid Request";
                    CurrentSectionSubtitle = "Create requests, choose a validated beneficiary or household, and release approved aid against budget.";
                    return new AssistanceCaseManagementPage(_currentUser);
                case "GgmsTransactions":
                    CurrentSectionTitle = "GGMS Consolidated Transactions";
                    CurrentSectionSubtitle = "View and filter all eKalinga+ transactions synced to the Global Government Management System.";
                    return new GgmsConsolidatedTransactionPage(_currentUser);
                case "Borrowing":
                    CurrentSectionTitle = "Equipment Borrowing";
                    CurrentSectionSubtitle = "Track barangay assets (tents, chairs, projectors), manage borrower records, and monitor overdue returns.";
                    return new BorrowingPage(_currentUser);
                case "Reports":
                    CurrentSectionTitle = "Reports";
                    CurrentSectionSubtitle = "Generate centralized summaries, export CSV tables, and print polished reports for PDF output.";
                    return new ReportsPage(_currentUser);
                case "ScanningPortal":
                    CurrentSectionTitle = "Scanning Portal";
                    CurrentSectionSubtitle = "Use the hardware scanner gun to instantly pull up beneficiary profiles and perform actions.";
                    return new ScanningPortalPage(_currentUser);
                case "CashForWorkPayout":
                    CurrentSectionTitle = "Cash-for-Work Payout";
                    CurrentSectionSubtitle = "Scan workers for daily attendance, then release attendance-based payouts.";
                    return new CashForWorkPayoutPage(_currentUser);
                default:
                    CurrentSectionTitle = "Attendance & Payouts";
                    CurrentSectionSubtitle = "Create work events or seminars, assign participants, and save attendance to release payouts.";
                    return new CashForWorkOcrPage(_currentUser);
            }
        }

        private void LoadUserSummary()
        {
            using var context = new Data.LocalDbContext();
            var profile = context.UserProfiles.FirstOrDefault(item => item.UserId == _currentUser.Id);

            UserDisplayName = !string.IsNullOrWhiteSpace(profile?.FullName)
                ? profile.FullName
                : _currentUser.Email;

            UserPhotoImage = BuildImage(profile?.PhotoPath);
        }

        private static ImageSource? BuildImage(string? path)
        {
            return LocalImageLoader.Load(path);
        }

        private static string BuildSoftwareTitle(string? systemName)
        {
            var trimmed = systemName?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "eKalinga+ Ayuda Management System";
            }

            if (trimmed.Contains("system", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.Contains("ayuda", StringComparison.OrdinalIgnoreCase))
            {
                return $"{trimmed} Management System";
            }

            return $"{trimmed} Ayuda Management System";
        }

        private static string BuildInitials(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "LGU";
            }

            var parts = text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(3)
                .Select(part => char.ToUpperInvariant(part[0]));

            var initials = new string(parts.ToArray());
            return string.IsNullOrWhiteSpace(initials) ? "LGU" : initials;
        }

        private void OnConnectivityChanged(object? sender, ConnectivityStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => UpdateConnectivityStatus(e.IsOnline, e.IsGgmsAvailable));
        }

        private void OnSyncStatusChanged(object? sender, string status)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ConnectivityService.Instance.IsOnline)
                {
                    ConnectionStatusText = $"Online - {status}";
                }
            });
        }

        private void OnLastSyncUpdated(object? sender, DateTime lastSync)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ConnectivityService.Instance.IsOnline)
                {
                    var mins = (int)(DateTime.UtcNow - lastSync).TotalMinutes;
                    ConnectionStatusText = $"Online - Synced {mins} mins ago";
                }
            });
        }

        private void UpdateConnectivityStatus(bool isOnline, bool isGgmsAvailable)
        {
            if (isOnline)
            {
                ConnectionStatusBrush = Brushes.LimeGreen;
                ConnectionStatusText = "Online - Ready to sync";
            }
            else
            {
                ConnectionStatusBrush = Brushes.OrangeRed;
                ConnectionStatusText = "Offline - Changes saved locally";
            }

            if (isGgmsAvailable)
            {
                GgmsStatusText = "GGMS: Online";
            }
            else
            {
                GgmsStatusText = "GGMS: Offline";
            }
        }
    }
}

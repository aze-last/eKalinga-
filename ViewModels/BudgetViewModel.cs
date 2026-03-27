using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BudgetViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly AppDbContext _context;
        private readonly BudgetManagementService _budgetService;
        private readonly GgmsBudgetSyncService _ggmsBudgetSyncService;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _syncGovernmentBudgetCommand;
        private readonly RelayCommand _recordDonationCommand;
        private readonly RelayCommand _createProgramCommand;
        private readonly RelayCommand _openSeminarPanelCommand;
        private readonly RelayCommand _closeSeminarPanelCommand;
        private readonly RelayCommand _createSeminarCommand;
        private readonly RelayCommand _browseProofCommand;
        private string _statusMessage = "Loading budget controls...";
        private Brush _statusBrush = Brushes.DimGray;
        private decimal _combinedAvailable;
        private decimal _governmentAvailable;
        private decimal _privateAvailable;
        private decimal _releasedTotal;
        private decimal _governmentAllocated;
        private decimal _governmentSpentReference;
        private string _governmentOfficeCode = "OFF-2026-0006";
        private string _governmentOfficeName = "Ayuda";
        private string _latestGovernmentSyncLabel = "No government sync yet.";
        private PrivateDonationDonorType _selectedDonorType = PrivateDonationDonorType.Person;
        private DonationProofType _selectedProofType = DonationProofType.Cash;
        private string _donorName = string.Empty;
        private string _donationAmountText = string.Empty;
        private DateTime _donationDateReceived = DateTime.Today;
        private string _donationReferenceNumber = string.Empty;
        private string _donationRemarks = string.Empty;
        private string _proofReferenceNumber = string.Empty;
        private string _proofFilePath = string.Empty;
        private string _programCode = string.Empty;
        private string _programName = string.Empty;
        private AyudaProgramType _selectedProgramType = AyudaProgramType.AssistanceCase;
        private string _programDescription = string.Empty;
        private string _programAssistanceType = string.Empty;
        private string _programUnitAmountText = string.Empty;
        private string _programItemDescription = string.Empty;
        private DateTime? _programStartDate;
        private DateTime? _programEndDate;
        private string _programBudgetCapText = string.Empty;
        private AyudaProgramDistributionStatus _selectedProgramDistributionStatus = AyudaProgramDistributionStatus.Draft;
        private string _seminarCode = string.Empty;
        private string _seminarTitle = string.Empty;
        private string _seminarCredentials = string.Empty;
        private AssistanceReleaseKind _selectedSeminarSupportKind = AssistanceReleaseKind.Cash;
        private string _seminarAmountText = string.Empty;
        private string _seminarGoodsDescription = string.Empty;
        private string _seminarAgenda = string.Empty;
        private DateTime? _seminarDate = DateTime.Today;
        private bool _isSeminarPanelOpen;
        private bool _isBusy;

        public BudgetViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _context = new AppDbContext();
            _budgetService = new BudgetManagementService(_context);
            _ggmsBudgetSyncService = new GgmsBudgetSyncService();
            DonorTypes = new ObservableCollection<PrivateDonationDonorType>(Enum.GetValues<PrivateDonationDonorType>());
            ProofTypes = new ObservableCollection<DonationProofType>(Enum.GetValues<DonationProofType>());
            ProgramTypes = new ObservableCollection<AyudaProgramType>(Enum.GetValues<AyudaProgramType>());
            ProgramDistributionStatuses = new ObservableCollection<AyudaProgramDistributionStatus>(Enum.GetValues<AyudaProgramDistributionStatus>());
            SeminarSupportKinds = new ObservableCollection<AssistanceReleaseKind>(Enum.GetValues<AssistanceReleaseKind>());
            Programs = new ObservableCollection<AyudaProgram>();
            Donations = new ObservableCollection<PrivateDonation>();
            LedgerEntries = new ObservableCollection<BudgetLedgerEntryListItem>();
            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _syncGovernmentBudgetCommand = new RelayCommand(async _ => await SyncGovernmentBudgetAsync(), _ => !IsBusy);
            _recordDonationCommand = new RelayCommand(async _ => await RecordDonationAsync(), _ => !IsBusy);
            _createProgramCommand = new RelayCommand(async _ => await CreateProgramAsync(), _ => !IsBusy);
            _openSeminarPanelCommand = new RelayCommand(_ => OpenSeminarPanel(), _ => !IsBusy && !IsSeminarPanelOpen);
            _closeSeminarPanelCommand = new RelayCommand(_ => CloseSeminarPanel(), _ => IsSeminarPanelOpen);
            _createSeminarCommand = new RelayCommand(async _ => await CreateSeminarAsync(), _ => !IsBusy);
            _browseProofCommand = new RelayCommand(_ => BrowseProof());
            _ = LoadAsync();
        }

        public ObservableCollection<PrivateDonationDonorType> DonorTypes { get; }
        public ObservableCollection<DonationProofType> ProofTypes { get; }
        public ObservableCollection<AyudaProgramType> ProgramTypes { get; }
        public ObservableCollection<AyudaProgramDistributionStatus> ProgramDistributionStatuses { get; }
        public ObservableCollection<AssistanceReleaseKind> SeminarSupportKinds { get; }
        public ObservableCollection<AyudaProgram> Programs { get; }
        public ObservableCollection<PrivateDonation> Donations { get; }
        public ObservableCollection<BudgetLedgerEntryListItem> LedgerEntries { get; }

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand SyncGovernmentBudgetCommand => _syncGovernmentBudgetCommand;
        public ICommand RecordDonationCommand => _recordDonationCommand;
        public ICommand CreateProgramCommand => _createProgramCommand;
        public ICommand OpenSeminarPanelCommand => _openSeminarPanelCommand;
        public ICommand CloseSeminarPanelCommand => _closeSeminarPanelCommand;
        public ICommand CreateSeminarCommand => _createSeminarCommand;
        public ICommand BrowseProofCommand => _browseProofCommand;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _refreshCommand.RaiseCanExecuteChanged();
                    _syncGovernmentBudgetCommand.RaiseCanExecuteChanged();
                    _recordDonationCommand.RaiseCanExecuteChanged();
                    _createProgramCommand.RaiseCanExecuteChanged();
                    _openSeminarPanelCommand.RaiseCanExecuteChanged();
                    _closeSeminarPanelCommand.RaiseCanExecuteChanged();
                    _createSeminarCommand.RaiseCanExecuteChanged();
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

        public decimal CombinedAvailable
        {
            get => _combinedAvailable;
            private set => SetProperty(ref _combinedAvailable, value);
        }

        public decimal GovernmentAvailable
        {
            get => _governmentAvailable;
            private set => SetProperty(ref _governmentAvailable, value);
        }

        public decimal PrivateAvailable
        {
            get => _privateAvailable;
            private set => SetProperty(ref _privateAvailable, value);
        }

        public decimal ReleasedTotal
        {
            get => _releasedTotal;
            private set => SetProperty(ref _releasedTotal, value);
        }

        public decimal GovernmentAllocated
        {
            get => _governmentAllocated;
            private set => SetProperty(ref _governmentAllocated, value);
        }

        public decimal GovernmentSpentReference
        {
            get => _governmentSpentReference;
            private set => SetProperty(ref _governmentSpentReference, value);
        }

        public string GovernmentOfficeCode
        {
            get => _governmentOfficeCode;
            private set => SetProperty(ref _governmentOfficeCode, value);
        }

        public string GovernmentOfficeName
        {
            get => _governmentOfficeName;
            private set => SetProperty(ref _governmentOfficeName, value);
        }

        public string LatestGovernmentSyncLabel
        {
            get => _latestGovernmentSyncLabel;
            private set => SetProperty(ref _latestGovernmentSyncLabel, value);
        }

        public PrivateDonationDonorType SelectedDonorType
        {
            get => _selectedDonorType;
            set => SetProperty(ref _selectedDonorType, value);
        }

        public DonationProofType SelectedProofType
        {
            get => _selectedProofType;
            set => SetProperty(ref _selectedProofType, value);
        }

        public string DonorName
        {
            get => _donorName;
            set => SetProperty(ref _donorName, value);
        }

        public string DonationAmountText
        {
            get => _donationAmountText;
            set => SetProperty(ref _donationAmountText, value);
        }

        public DateTime DonationDateReceived
        {
            get => _donationDateReceived;
            set => SetProperty(ref _donationDateReceived, value);
        }

        public string DonationReferenceNumber
        {
            get => _donationReferenceNumber;
            set => SetProperty(ref _donationReferenceNumber, value);
        }

        public string DonationRemarks
        {
            get => _donationRemarks;
            set => SetProperty(ref _donationRemarks, value);
        }

        public string ProofReferenceNumber
        {
            get => _proofReferenceNumber;
            set => SetProperty(ref _proofReferenceNumber, value);
        }

        public string ProofFilePath
        {
            get => _proofFilePath;
            set => SetProperty(ref _proofFilePath, value);
        }

        public string ProgramCode
        {
            get => _programCode;
            set => SetProperty(ref _programCode, value);
        }

        public string ProgramName
        {
            get => _programName;
            set => SetProperty(ref _programName, value);
        }

        public AyudaProgramType SelectedProgramType
        {
            get => _selectedProgramType;
            set => SetProperty(ref _selectedProgramType, value);
        }

        public string ProgramDescription
        {
            get => _programDescription;
            set => SetProperty(ref _programDescription, value);
        }

        public string ProgramAssistanceType
        {
            get => _programAssistanceType;
            set => SetProperty(ref _programAssistanceType, value);
        }

        public string ProgramUnitAmountText
        {
            get => _programUnitAmountText;
            set => SetProperty(ref _programUnitAmountText, value);
        }

        public string ProgramItemDescription
        {
            get => _programItemDescription;
            set => SetProperty(ref _programItemDescription, value);
        }

        public DateTime? ProgramStartDate
        {
            get => _programStartDate;
            set => SetProperty(ref _programStartDate, value);
        }

        public DateTime? ProgramEndDate
        {
            get => _programEndDate;
            set => SetProperty(ref _programEndDate, value);
        }

        public string ProgramBudgetCapText
        {
            get => _programBudgetCapText;
            set => SetProperty(ref _programBudgetCapText, value);
        }

        public AyudaProgramDistributionStatus SelectedProgramDistributionStatus
        {
            get => _selectedProgramDistributionStatus;
            set => SetProperty(ref _selectedProgramDistributionStatus, value);
        }

        public string SeminarCode
        {
            get => _seminarCode;
            set => SetProperty(ref _seminarCode, value);
        }

        public string SeminarTitle
        {
            get => _seminarTitle;
            set => SetProperty(ref _seminarTitle, value);
        }

        public string SeminarCredentials
        {
            get => _seminarCredentials;
            set => SetProperty(ref _seminarCredentials, value);
        }

        public AssistanceReleaseKind SelectedSeminarSupportKind
        {
            get => _selectedSeminarSupportKind;
            set
            {
                if (SetProperty(ref _selectedSeminarSupportKind, value))
                {
                    OnPropertyChanged(nameof(SeminarAmountVisibility));
                    OnPropertyChanged(nameof(SeminarGoodsVisibility));
                }
            }
        }

        public string SeminarAmountText
        {
            get => _seminarAmountText;
            set => SetProperty(ref _seminarAmountText, value);
        }

        public string SeminarGoodsDescription
        {
            get => _seminarGoodsDescription;
            set => SetProperty(ref _seminarGoodsDescription, value);
        }

        public string SeminarAgenda
        {
            get => _seminarAgenda;
            set => SetProperty(ref _seminarAgenda, value);
        }

        public DateTime? SeminarDate
        {
            get => _seminarDate;
            set => SetProperty(ref _seminarDate, value);
        }

        public bool IsSeminarPanelOpen
        {
            get => _isSeminarPanelOpen;
            private set
            {
                if (SetProperty(ref _isSeminarPanelOpen, value))
                {
                    OnPropertyChanged(nameof(SeminarPanelVisibility));
                    _openSeminarPanelCommand.RaiseCanExecuteChanged();
                    _closeSeminarPanelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public Visibility SeminarPanelVisibility => IsSeminarPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SeminarAmountVisibility => SelectedSeminarSupportKind == AssistanceReleaseKind.Cash
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility SeminarGoodsVisibility => SelectedSeminarSupportKind == AssistanceReleaseKind.Goods
            ? Visibility.Visible
            : Visibility.Collapsed;

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading budget controls...");

            try
            {
                await LoadProgramsAsync();
                await LoadOverviewAsync();
                await LoadDonationsAsync();
                await LoadLedgerAsync();
                SetSuccessStatus("Budget controls refreshed.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load budget controls: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadProgramsAsync()
        {
            Programs.Clear();
            foreach (var program in await _budgetService.GetProgramsAsync())
            {
                Programs.Add(program);
            }
        }

        private async Task LoadOverviewAsync()
        {
            var overview = await _budgetService.GetOverviewAsync();
            CombinedAvailable = overview.CombinedAvailable;
            GovernmentAvailable = overview.GovernmentAvailable;
            PrivateAvailable = overview.PrivateAvailable;
            ReleasedTotal = overview.ReleasedTotal;
            GovernmentAllocated = overview.GovernmentAllocated;
            GovernmentSpentReference = overview.GovernmentSpentReference;
            GovernmentOfficeCode = overview.OfficeCode ?? "OFF-2026-0006";
            GovernmentOfficeName = overview.OfficeName ?? "Ayuda";
            LatestGovernmentSyncLabel = overview.LastGovernmentSyncAt.HasValue
                ? $"Last GGMS sync: {overview.LastGovernmentSyncAt:MMM dd, yyyy hh:mm tt}"
                : "No government sync yet.";
        }

        private async Task LoadDonationsAsync()
        {
            Donations.Clear();
            foreach (var donation in await _budgetService.GetPrivateDonationsAsync())
            {
                Donations.Add(donation);
            }
        }

        private async Task LoadLedgerAsync()
        {
            LedgerEntries.Clear();
            var programsById = Programs.ToDictionary(program => program.Id, program => program.ProgramName);

            foreach (var entry in await _budgetService.GetRecentLedgerEntriesAsync())
            {
                LedgerEntries.Add(new BudgetLedgerEntryListItem
                {
                    EntryDate = entry.EntryDate,
                    EntryType = entry.EntryType.ToString(),
                    FeatureSource = entry.FeatureSource.ToString(),
                    ReleaseKind = entry.ReleaseKind?.ToString() ?? "--",
                    ProgramName = entry.ProgramId.HasValue && programsById.TryGetValue(entry.ProgramId.Value, out var programName)
                        ? programName
                        : "--",
                    RecipientCount = entry.RecipientCount,
                    TotalAmount = entry.TotalAmount,
                    GovernmentPortion = entry.GovernmentPortion,
                    PrivatePortion = entry.PrivatePortion,
                    Remarks = entry.Remarks ?? string.Empty
                });
            }
        }

        private async Task SyncGovernmentBudgetAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Syncing government budget from GGMS...");

            try
            {
                var result = await _ggmsBudgetSyncService.SyncAyudaBudgetAsync(_context, _currentUser.Id);
                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                await LoadOverviewAsync();
                SetSuccessStatus("Government budget sync completed.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to sync GGMS budget: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RecordDonationAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (!TryParseAmount(DonationAmountText, out var amount))
            {
                SetErrorStatus("Enter a valid donation amount greater than zero.");
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Recording private donation...");

            try
            {
                var result = await _budgetService.RecordPrivateDonationAsync(
                    new PrivateDonationRequest(
                        SelectedDonorType,
                        DonorName,
                        amount,
                        DonationDateReceived,
                        NormalizeNullable(DonationReferenceNumber),
                        NormalizeNullable(DonationRemarks),
                        SelectedProofType,
                        NormalizeNullable(ProofReferenceNumber),
                        NormalizeNullable(ProofFilePath)),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                ResetDonationForm();
                await LoadOverviewAsync();
                await LoadDonationsAsync();
                await LoadLedgerAsync();
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to record donation: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateProgramAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (!TryParseOptionalAmount(ProgramUnitAmountText, out var unitAmount))
            {
                SetErrorStatus("Enter a valid unit amount or leave it blank.");
                return;
            }

            if (!TryParseOptionalAmount(ProgramBudgetCapText, out var budgetCap))
            {
                SetErrorStatus("Enter a valid project budget cap or leave it blank.");
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Creating ayuda program...");

            try
            {
                var result = await _budgetService.CreateProgramAsync(
                    new AyudaProgramRequest(
                        ProgramCode,
                        ProgramName,
                        SelectedProgramType,
                        NormalizeNullable(ProgramDescription),
                        NormalizeNullable(ProgramAssistanceType),
                        unitAmount,
                        NormalizeNullable(ProgramItemDescription),
                        ProgramStartDate,
                        ProgramEndDate,
                        budgetCap,
                        SelectedProgramDistributionStatus),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                ResetProgramForm();
                await LoadProgramsAsync();
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to create program: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateSeminarAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SeminarCode))
            {
                SetErrorStatus("Enter a seminar code.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SeminarTitle))
            {
                SetErrorStatus("Enter the seminar title.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SeminarAgenda))
            {
                SetErrorStatus("Enter the seminar agenda.");
                return;
            }

            decimal? unitAmount = null;
            if (SelectedSeminarSupportKind == AssistanceReleaseKind.Cash)
            {
                if (!TryParseAmount(SeminarAmountText, out var parsedAmount))
                {
                    SetErrorStatus("Enter a valid seminar amount.");
                    return;
                }

                unitAmount = parsedAmount;
            }
            else if (string.IsNullOrWhiteSpace(SeminarGoodsDescription))
            {
                SetErrorStatus("Describe the seminar goods or package.");
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Creating seminar setup...");

            try
            {
                var result = await _budgetService.CreateProgramAsync(
                    new AyudaProgramRequest(
                        SeminarCode,
                        SeminarTitle,
                        AyudaProgramType.Seminar,
                        BuildSeminarDescription(),
                        SelectedSeminarSupportKind == AssistanceReleaseKind.Cash ? "Seminar Amount" : "Seminar Goods",
                        unitAmount,
                        NormalizeNullable(SeminarGoodsDescription),
                        SeminarDate?.Date,
                        SeminarDate?.Date,
                        unitAmount,
                        AyudaProgramDistributionStatus.Draft),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                ResetSeminarForm();
                CloseSeminarPanel();
                await LoadProgramsAsync();
                SetSuccessStatus(result.Message);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to create seminar setup: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenSeminarPanel()
        {
            if (IsBusy)
            {
                return;
            }

            IsSeminarPanelOpen = true;
        }

        private void CloseSeminarPanel()
        {
            IsSeminarPanelOpen = false;
        }

        private void BrowseProof()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files|*.*",
                Title = "Select Donation Proof File"
            };

            if (dialog.ShowDialog() == true)
            {
                ProofFilePath = dialog.FileName;
            }
        }

        private void ResetDonationForm()
        {
            SelectedDonorType = PrivateDonationDonorType.Person;
            SelectedProofType = DonationProofType.Cash;
            DonorName = string.Empty;
            DonationAmountText = string.Empty;
            DonationDateReceived = DateTime.Today;
            DonationReferenceNumber = string.Empty;
            DonationRemarks = string.Empty;
            ProofReferenceNumber = string.Empty;
            ProofFilePath = string.Empty;
        }

        private void ResetProgramForm()
        {
            ProgramCode = string.Empty;
            ProgramName = string.Empty;
            SelectedProgramType = AyudaProgramType.AssistanceCase;
            ProgramDescription = string.Empty;
            ProgramAssistanceType = string.Empty;
            ProgramUnitAmountText = string.Empty;
            ProgramItemDescription = string.Empty;
            ProgramStartDate = null;
            ProgramEndDate = null;
            ProgramBudgetCapText = string.Empty;
            SelectedProgramDistributionStatus = AyudaProgramDistributionStatus.Draft;
        }

        private void ResetSeminarForm()
        {
            SeminarCode = string.Empty;
            SeminarTitle = string.Empty;
            SeminarCredentials = string.Empty;
            SelectedSeminarSupportKind = AssistanceReleaseKind.Cash;
            SeminarAmountText = string.Empty;
            SeminarGoodsDescription = string.Empty;
            SeminarAgenda = string.Empty;
            SeminarDate = DateTime.Today;
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = Brushes.DimGray;
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = Brushes.ForestGreen;
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = Brushes.Firebrick;
        }

        private static bool TryParseAmount(string text, out decimal amount)
        {
            amount = 0m;

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                return amount > 0;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return amount > 0;
            }

            return false;
        }

        private static bool TryParseOptionalAmount(string text, out decimal? amount)
        {
            amount = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (TryParseAmount(text, out var parsedAmount))
            {
                amount = parsedAmount;
                return true;
            }

            return false;
        }

        private string BuildSeminarDescription()
        {
            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(SeminarCredentials))
            {
                sections.Add($"Credentials: {SeminarCredentials.Trim()}");
            }

            sections.Add($"Agenda: {SeminarAgenda.Trim()}");

            return string.Join(Environment.NewLine, sections);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public sealed class BudgetLedgerEntryListItem
    {
        public DateTime EntryDate { get; init; }
        public string EntryType { get; init; } = string.Empty;
        public string FeatureSource { get; init; } = string.Empty;
        public string ReleaseKind { get; init; } = string.Empty;
        public string ProgramName { get; init; } = string.Empty;
        public int RecipientCount { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal GovernmentPortion { get; init; }
        public decimal PrivatePortion { get; init; }
        public string Remarks { get; init; } = string.Empty;
    }
}

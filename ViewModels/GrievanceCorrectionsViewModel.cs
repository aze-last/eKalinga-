using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class GrievanceCorrectionsViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private readonly ObservableCollection<GrievanceListItem> _grievances = new();
        private readonly ObservableCollection<BeneficiaryOption> _beneficiaries = new();
        private readonly ObservableCollection<LookupOption> _assistanceCases = new();
        private readonly ObservableCollection<LookupOption> _cashForWorkEvents = new();
        private readonly ObservableCollection<LedgerEntryListItem> _ledgerEntries = new();
        private readonly ObservableCollection<HistoryItem> _historyItems = new();
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _newGrievanceCommand;
        private readonly RelayCommand _saveCommand;
        private readonly RelayCommand _openReviewCommand;
        private readonly RelayCommand _resolveCommand;
        private readonly RelayCommand _rejectCommand;
        private readonly RelayCommand _recordManualAssistanceCommand;
        private ICollectionView _grievancesView;
        private GrievanceListItem? _selectedGrievance;
        private BeneficiaryOption? _selectedBeneficiary;
        private LookupOption? _selectedAssistanceCase;
        private LookupOption? _selectedCashForWorkEvent;
        private string _searchText = string.Empty;
        private string _selectedStatusFilter = "All";
        private bool _isBusy;
        private string _statusMessage = "Loading grievance records...";
        private Brush _statusBrush = CreateBrush("#6B7280");
        private int _totalGrievances;
        private int _openCount;
        private int _underReviewCount;
        private int _resolvedCount;
        private int _rejectedCount;
        private string _editableGrievanceNumber = "New grievance";
        private GrievanceType _selectedGrievanceType = GrievanceType.Duplicate;
        private string _editableTitle = string.Empty;
        private string _editableDescription = string.Empty;
        private string _editableActionRemarks = string.Empty;
        private string _manualAssistanceAmount = string.Empty;
        private DateTime _manualAssistanceReleaseDate = DateTime.Today;
        private string _manualAssistanceRemarks = string.Empty;
        private decimal _warningThreshold;
        private decimal _totalAssistanceReceived;
        private bool _isWarningVisible;

        public GrievanceCorrectionsViewModel(User currentUser)
        {
            _currentUser = currentUser;
            StatusFilters = new ObservableCollection<string>
            {
                "All",
                "Open",
                "Under review",
                "Resolved",
                "Rejected"
            };

            GrievanceTypes = new ObservableCollection<GrievanceType>(Enum.GetValues<GrievanceType>());
            _grievancesView = CollectionViewSource.GetDefaultView(_grievances);

            _refreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            _newGrievanceCommand = new RelayCommand(_ => BeginNewGrievance(), _ => !IsBusy);
            _saveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            _openReviewCommand = new RelayCommand(async _ => await ChangeStatusAsync(GrievanceStatus.UnderReview), _ => CanChangeStatus(GrievanceStatus.UnderReview));
            _resolveCommand = new RelayCommand(async _ => await ChangeStatusAsync(GrievanceStatus.Resolved), _ => CanChangeStatus(GrievanceStatus.Resolved));
            _rejectCommand = new RelayCommand(async _ => await ChangeStatusAsync(GrievanceStatus.Rejected), _ => CanChangeStatus(GrievanceStatus.Rejected));
            _recordManualAssistanceCommand = new RelayCommand(async _ => await RecordManualAssistanceAsync(), _ => CanRecordManualAssistance());

            ApplyFilter();
            _ = LoadAsync();
        }

        public ObservableCollection<string> StatusFilters { get; }

        public ObservableCollection<GrievanceType> GrievanceTypes { get; }

        public ObservableCollection<BeneficiaryOption> Beneficiaries => _beneficiaries;

        public ObservableCollection<LookupOption> AssistanceCases => _assistanceCases;

        public ObservableCollection<LookupOption> CashForWorkEvents => _cashForWorkEvents;

        public ObservableCollection<LedgerEntryListItem> LedgerEntries => _ledgerEntries;

        public ObservableCollection<HistoryItem> HistoryItems => _historyItems;

        public ICollectionView GrievancesView
        {
            get => _grievancesView;
            private set => SetProperty(ref _grievancesView, value);
        }

        public GrievanceListItem? SelectedGrievance
        {
            get => _selectedGrievance;
            set
            {
                if (SetProperty(ref _selectedGrievance, value))
                {
                    _ = SyncSelectionAsync();
                    RaiseCommandStates();
                }
            }
        }

        public BeneficiaryOption? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set
            {
                if (SetProperty(ref _selectedBeneficiary, value))
                {
                    OnPropertyChanged(nameof(SelectedBeneficiaryName));
                    OnPropertyChanged(nameof(SelectedBeneficiaryIdentity));
                    OnPropertyChanged(nameof(SelectedBeneficiaryAddress));
                    _ = RefreshSelectedBeneficiaryContextAsync();
                    RaiseCommandStates();
                }
            }
        }

        public LookupOption? SelectedAssistanceCase
        {
            get => _selectedAssistanceCase;
            set => SetProperty(ref _selectedAssistanceCase, value);
        }

        public LookupOption? SelectedCashForWorkEvent
        {
            get => _selectedCashForWorkEvent;
            set => SetProperty(ref _selectedCashForWorkEvent, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (SetProperty(ref _selectedStatusFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
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

        public int TotalGrievances
        {
            get => _totalGrievances;
            private set => SetProperty(ref _totalGrievances, value);
        }

        public int OpenCount
        {
            get => _openCount;
            private set => SetProperty(ref _openCount, value);
        }

        public int UnderReviewCount
        {
            get => _underReviewCount;
            private set => SetProperty(ref _underReviewCount, value);
        }

        public int ResolvedCount
        {
            get => _resolvedCount;
            private set => SetProperty(ref _resolvedCount, value);
        }

        public int RejectedCount
        {
            get => _rejectedCount;
            private set => SetProperty(ref _rejectedCount, value);
        }

        public string EditableGrievanceNumber
        {
            get => _editableGrievanceNumber;
            private set => SetProperty(ref _editableGrievanceNumber, value);
        }

        public GrievanceType SelectedGrievanceType
        {
            get => _selectedGrievanceType;
            set => SetProperty(ref _selectedGrievanceType, value);
        }

        public string EditableTitle
        {
            get => _editableTitle;
            set => SetProperty(ref _editableTitle, value);
        }

        public string EditableDescription
        {
            get => _editableDescription;
            set => SetProperty(ref _editableDescription, value);
        }

        public string EditableActionRemarks
        {
            get => _editableActionRemarks;
            set
            {
                if (SetProperty(ref _editableActionRemarks, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string ManualAssistanceAmount
        {
            get => _manualAssistanceAmount;
            set
            {
                if (SetProperty(ref _manualAssistanceAmount, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public DateTime ManualAssistanceReleaseDate
        {
            get => _manualAssistanceReleaseDate;
            set => SetProperty(ref _manualAssistanceReleaseDate, value);
        }

        public string ManualAssistanceRemarks
        {
            get => _manualAssistanceRemarks;
            set
            {
                if (SetProperty(ref _manualAssistanceRemarks, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public decimal WarningThreshold
        {
            get => _warningThreshold;
            private set
            {
                if (SetProperty(ref _warningThreshold, value))
                {
                    OnPropertyChanged(nameof(WarningThresholdText));
                    OnPropertyChanged(nameof(WarningBannerText));
                }
            }
        }

        public decimal TotalAssistanceReceived
        {
            get => _totalAssistanceReceived;
            private set
            {
                if (SetProperty(ref _totalAssistanceReceived, value))
                {
                    OnPropertyChanged(nameof(TotalAssistanceReceivedText));
                    OnPropertyChanged(nameof(WarningBannerText));
                }
            }
        }

        public bool IsWarningVisible
        {
            get => _isWarningVisible;
            private set
            {
                if (SetProperty(ref _isWarningVisible, value))
                {
                    OnPropertyChanged(nameof(WarningBannerText));
                }
            }
        }

        public string SelectedBeneficiaryName => SelectedBeneficiary?.FullName ?? "Select an imported beneficiary";

        public string SelectedBeneficiaryIdentity =>
            SelectedBeneficiary == null
                ? "No beneficiary linked yet."
                : $"Civil Registry ID: {SelectedBeneficiary.CivilRegistryId ?? "--"} | Beneficiary ID: {SelectedBeneficiary.BeneficiaryId ?? "--"}";

        public string SelectedBeneficiaryAddress =>
            string.IsNullOrWhiteSpace(SelectedBeneficiary?.Address)
                ? "No address available."
                : SelectedBeneficiary.Address!;

        public string TotalAssistanceReceivedText => $"{TotalAssistanceReceived:N2}";

        public string WarningThresholdText => $"{WarningThreshold:N2}";

        public string WarningBannerText =>
            IsWarningVisible
                ? $"Warning only: this beneficiary already has {TotalAssistanceReceived:N2} recorded assistance, which meets or exceeds the threshold of {WarningThreshold:N2}. Continue only with remarks."
                : "No large-assistance warning for the selected beneficiary.";

        public ICommand RefreshCommand => _refreshCommand;
        public ICommand NewGrievanceCommand => _newGrievanceCommand;
        public ICommand SaveCommand => _saveCommand;
        public ICommand OpenReviewCommand => _openReviewCommand;
        public ICommand ResolveCommand => _resolveCommand;
        public ICommand RejectCommand => _rejectCommand;
        public ICommand RecordManualAssistanceCommand => _recordManualAssistanceCommand;

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading grievance and beneficiary records...");

            try
            {
                var preferredGrievanceId = SelectedGrievance?.Id;

                await using var context = new AppDbContext();

                var grievances = await context.GrievanceRecords
                    .AsNoTracking()
                    .OrderByDescending(item => item.CreatedAt)
                    .ThenByDescending(item => item.Id)
                    .ToListAsync();

                var beneficiaries = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .OrderBy(item => item.FullName)
                    .ThenBy(item => item.LastName)
                    .ToListAsync();

                var assistanceCases = await context.AssistanceCases
                    .AsNoTracking()
                    .OrderByDescending(item => item.RequestedOn)
                    .ThenByDescending(item => item.Id)
                    .ToListAsync();

                var cashForWorkEvents = await context.CashForWorkEvents
                    .AsNoTracking()
                    .OrderByDescending(item => item.EventDate)
                    .ThenBy(item => item.Title)
                    .ToListAsync();

                _grievances.Clear();
                foreach (var grievance in grievances)
                {
                    _grievances.Add(GrievanceListItem.FromEntity(grievance));
                }

                _beneficiaries.Clear();
                foreach (var beneficiary in beneficiaries)
                {
                    _beneficiaries.Add(BeneficiaryOption.FromEntity(beneficiary));
                }

                _assistanceCases.Clear();
                _assistanceCases.Add(LookupOption.Empty("No linked assistance case"));
                foreach (var assistanceCase in assistanceCases)
                {
                    _assistanceCases.Add(new LookupOption(assistanceCase.Id, $"{assistanceCase.CaseNumber} - {assistanceCase.AssistanceType}"));
                }

                _cashForWorkEvents.Clear();
                _cashForWorkEvents.Add(LookupOption.Empty("No linked cash-for-work event"));
                foreach (var cashForWorkEvent in cashForWorkEvents)
                {
                    _cashForWorkEvents.Add(new LookupOption(cashForWorkEvent.Id, $"{cashForWorkEvent.EventDate:yyyy-MM-dd} - {cashForWorkEvent.Title}"));
                }

                UpdateCounts();

                GrievancesView = CollectionViewSource.GetDefaultView(_grievances);
                ApplyFilter();

                SelectedGrievance = _grievances.FirstOrDefault(item => item.Id == preferredGrievanceId)
                    ?? _grievances.FirstOrDefault();

                if (SelectedGrievance == null)
                {
                    BeginNewGrievance();
                }

                SetSuccessStatus($"Loaded {TotalGrievances:N0} grievance record(s).");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load grievance records: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BeginNewGrievance()
        {
            SelectedGrievance = null;
            EditableGrievanceNumber = "New grievance";
            SelectedGrievanceType = GrievanceType.Duplicate;
            EditableTitle = string.Empty;
            EditableDescription = string.Empty;
            EditableActionRemarks = string.Empty;
            SelectedAssistanceCase = _assistanceCases.FirstOrDefault();
            SelectedCashForWorkEvent = _cashForWorkEvents.FirstOrDefault();
            _historyItems.Clear();
            ManualAssistanceAmount = string.Empty;
            ManualAssistanceRemarks = string.Empty;
            ManualAssistanceReleaseDate = DateTime.Today;

            if (SelectedBeneficiary == null)
            {
                SelectedBeneficiary = _beneficiaries.FirstOrDefault();
            }
        }

        private async Task SyncSelectionAsync()
        {
            try
            {
                if (SelectedGrievance == null)
                {
                    EditableGrievanceNumber = "New grievance";
                    SelectedGrievanceType = GrievanceType.Duplicate;
                    EditableTitle = string.Empty;
                    EditableDescription = string.Empty;
                    EditableActionRemarks = string.Empty;
                    SelectedAssistanceCase = _assistanceCases.FirstOrDefault();
                    SelectedCashForWorkEvent = _cashForWorkEvents.FirstOrDefault();
                    _historyItems.Clear();
                    await RefreshSelectedBeneficiaryContextAsync();
                    return;
                }

                EditableGrievanceNumber = SelectedGrievance.GrievanceNumber;
                SelectedGrievanceType = SelectedGrievance.Type;
                EditableTitle = SelectedGrievance.Title;
                EditableDescription = SelectedGrievance.Description;
                EditableActionRemarks = SelectedGrievance.ResolutionRemarks ?? string.Empty;
                SelectedAssistanceCase = _assistanceCases.FirstOrDefault(item => item.Id == SelectedGrievance.AssistanceCaseId)
                    ?? _assistanceCases.FirstOrDefault();
                SelectedCashForWorkEvent = _cashForWorkEvents.FirstOrDefault(item => item.Id == SelectedGrievance.CashForWorkEventId)
                    ?? _cashForWorkEvents.FirstOrDefault();
                SelectedBeneficiary = _beneficiaries.FirstOrDefault(item => item.StagingId == SelectedGrievance.StagingId)
                    ?? _beneficiaries.FirstOrDefault(item =>
                        string.Equals(item.CivilRegistryId, SelectedGrievance.CivilRegistryId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.BeneficiaryId, SelectedGrievance.BeneficiaryId, StringComparison.OrdinalIgnoreCase));

                await LoadHistoryAsync(SelectedGrievance);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load the selected grievance detail: {ex.Message}");
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedBeneficiary == null)
            {
                SetErrorStatus("Select an imported beneficiary before saving the grievance.");
                return;
            }

            IsBusy = true;

            try
            {
                await using var context = new AppDbContext();
                var service = new GrievanceManagementService(context);

                GrievanceOperationResult result;
                if (SelectedGrievance == null)
                {
                    result = await service.CreateAsync(
                        new GrievanceCreateRequest(
                            SelectedBeneficiary.StagingId,
                            SelectedGrievanceType,
                            EditableTitle,
                            EditableDescription,
                            SelectedAssistanceCase?.Id,
                            SelectedCashForWorkEvent?.Id),
                        _currentUser.Id);
                }
                else
                {
                    result = await service.UpdateAsync(
                        SelectedGrievance.Id,
                        new GrievanceUpdateRequest(
                            SelectedGrievanceType,
                            EditableTitle,
                            EditableDescription,
                            SelectedAssistanceCase?.Id,
                            SelectedCashForWorkEvent?.Id,
                            null),
                        _currentUser.Id,
                        EditableActionRemarks);
                }

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                SetSuccessStatus(result.Message);
                await LoadAsync();
                SelectedGrievance = result.GrievanceId.HasValue
                    ? _grievances.FirstOrDefault(item => item.Id == result.GrievanceId.Value)
                    : SelectedGrievance;
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to save grievance: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ChangeStatusAsync(GrievanceStatus targetStatus)
        {
            if (SelectedGrievance == null)
            {
                return;
            }

            IsBusy = true;

            try
            {
                await using var context = new AppDbContext();
                var service = new GrievanceManagementService(context);
                var result = await service.ChangeStatusAsync(
                    SelectedGrievance.Id,
                    targetStatus,
                    _currentUser.Id,
                    EditableActionRemarks);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                SetSuccessStatus(result.Message);
                await LoadAsync();
                SelectedGrievance = result.GrievanceId.HasValue
                    ? _grievances.FirstOrDefault(item => item.Id == result.GrievanceId.Value)
                    : _grievances.FirstOrDefault();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to update grievance status: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RecordManualAssistanceAsync()
        {
            if (SelectedBeneficiary == null)
            {
                SetErrorStatus("Select an imported beneficiary before recording prior assistance.");
                return;
            }

            if (!TryParseManualAmount(out var amount))
            {
                SetErrorStatus("Enter a valid prior assistance amount greater than zero.");
                return;
            }

            IsBusy = true;

            try
            {
                await using var context = new AppDbContext();
                var service = new BeneficiaryAssistanceLedgerService(context);
                var result = await service.RecordManualEntryAsync(
                    new AssistanceLedgerManualEntryRequest(
                        SelectedBeneficiary.StagingId,
                        amount,
                        ManualAssistanceReleaseDate,
                        ManualAssistanceRemarks),
                    _currentUser.Id);

                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    return;
                }

                ManualAssistanceAmount = string.Empty;
                ManualAssistanceRemarks = string.Empty;
                ManualAssistanceReleaseDate = DateTime.Today;
                SetSuccessStatus(result.Message);
                await RefreshSelectedBeneficiaryContextAsync();
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to record prior assistance: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshSelectedBeneficiaryContextAsync()
        {
            if (SelectedBeneficiary == null)
            {
                WarningThreshold = FeatureSettingsService.Load().LargeAssistanceWarningThreshold;
                TotalAssistanceReceived = 0m;
                IsWarningVisible = false;
                _ledgerEntries.Clear();
                return;
            }

            try
            {
                var featureSettings = FeatureSettingsService.Load();
                WarningThreshold = featureSettings.LargeAssistanceWarningThreshold;

                await using var context = new AppDbContext();
                var service = new BeneficiaryAssistanceLedgerService(context);
                var summary = await service.GetWarningSummaryForStagingAsync(SelectedBeneficiary.StagingId, WarningThreshold);
                var entries = await service.GetEntriesForStagingAsync(SelectedBeneficiary.StagingId);

                TotalAssistanceReceived = summary.TotalAmountReceived;
                IsWarningVisible = summary.IsAboveThreshold;

                _ledgerEntries.Clear();
                foreach (var entry in entries)
                {
                    _ledgerEntries.Add(LedgerEntryListItem.FromEntity(entry));
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load beneficiary assistance totals: {ex.Message}");
            }
        }

        private async Task LoadHistoryAsync(GrievanceListItem grievance)
        {
            await using var context = new AppDbContext();

            var logs = await context.ActivityLogs
                .AsNoTracking()
                .Include(item => item.User)
                .Where(item => item.Entity == "GrievanceRecord" && item.EntityId == grievance.Id)
                .OrderByDescending(item => item.Timestamp)
                .ToListAsync();

            _historyItems.Clear();
            foreach (var log in logs)
            {
                _historyItems.Add(new HistoryItem(
                    log.Timestamp,
                    log.Action,
                    log.Details,
                    log.User?.Email ?? "System"));
            }

            if (grievance.AssistanceCaseId.HasValue)
            {
                var assistanceCase = await context.AssistanceCases
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == grievance.AssistanceCaseId.Value);

                if (assistanceCase != null)
                {
                    _historyItems.Add(new HistoryItem(
                        assistanceCase.UpdatedAt,
                        "LinkedAssistanceCase",
                        $"Linked to assistance case {assistanceCase.CaseNumber} ({assistanceCase.Status}).",
                        "Case registry"));
                }
            }

            if (grievance.CashForWorkEventId.HasValue)
            {
                var cashForWorkEvent = await context.CashForWorkEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == grievance.CashForWorkEventId.Value);

                if (cashForWorkEvent != null)
                {
                    _historyItems.Add(new HistoryItem(
                        cashForWorkEvent.UpdatedAt,
                        "LinkedCashForWorkEvent",
                        $"Linked to cash-for-work event '{cashForWorkEvent.Title}' on {cashForWorkEvent.EventDate:yyyy-MM-dd}.",
                        "Cash-for-work"));
                }
            }

            var ordered = _historyItems
                .OrderByDescending(item => item.Timestamp)
                .ToList();

            _historyItems.Clear();
            foreach (var item in ordered)
            {
                _historyItems.Add(item);
            }
        }

        private void UpdateCounts()
        {
            TotalGrievances = _grievances.Count;
            OpenCount = _grievances.Count(item => item.Status == GrievanceStatus.Open);
            UnderReviewCount = _grievances.Count(item => item.Status == GrievanceStatus.UnderReview);
            ResolvedCount = _grievances.Count(item => item.Status == GrievanceStatus.Resolved);
            RejectedCount = _grievances.Count(item => item.Status == GrievanceStatus.Rejected);
        }

        private void ApplyFilter()
        {
            GrievancesView.Filter = item =>
            {
                if (item is not GrievanceListItem grievance)
                {
                    return false;
                }

                if (SelectedStatusFilter != "All" &&
                    !string.Equals(grievance.StatusText, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                return Contains(grievance.GrievanceNumber, SearchText)
                    || Contains(grievance.Title, SearchText)
                    || Contains(grievance.BeneficiaryIdentity, SearchText)
                    || Contains(grievance.TypeText, SearchText);
            };

            GrievancesView.Refresh();
        }

        private bool CanSave()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && !string.IsNullOrWhiteSpace(EditableTitle)
                && !string.IsNullOrWhiteSpace(EditableDescription);
        }

        private bool CanChangeStatus(GrievanceStatus targetStatus)
        {
            return !IsBusy
                && SelectedGrievance != null
                && SelectedGrievance.Status != targetStatus;
        }

        private bool CanRecordManualAssistance()
        {
            return !IsBusy
                && SelectedBeneficiary != null
                && TryParseManualAmount(out _)
                && !string.IsNullOrWhiteSpace(ManualAssistanceRemarks);
        }

        private void RaiseCommandStates()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _newGrievanceCommand.RaiseCanExecuteChanged();
            _saveCommand.RaiseCanExecuteChanged();
            _openReviewCommand.RaiseCanExecuteChanged();
            _resolveCommand.RaiseCanExecuteChanged();
            _rejectCommand.RaiseCanExecuteChanged();
            _recordManualAssistanceCommand.RaiseCanExecuteChanged();
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#6B7280");
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = CreateBrush("#991B1B");
        }

        private bool TryParseManualAmount(out decimal amount)
        {
            amount = 0m;

            if (decimal.TryParse(ManualAssistanceAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return amount > 0;
            }

            if (decimal.TryParse(ManualAssistanceAmount, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                return amount > 0;
            }

            return false;
        }

        private static bool Contains(string? source, string value)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        private static Brush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }
    }

    public sealed record BeneficiaryOption(
        int StagingId,
        string FullName,
        string? CivilRegistryId,
        string? BeneficiaryId,
        string? Address,
        string DisplayLabel)
    {
        public static BeneficiaryOption FromEntity(BeneficiaryStaging row)
        {
            var fullName = string.IsNullOrWhiteSpace(row.FullName)
                ? $"{row.LastName}, {row.FirstName}".Trim(',', ' ')
                : row.FullName!;
            var identity = row.CivilRegistryId ?? row.BeneficiaryId ?? "No ID";
            return new BeneficiaryOption(
                row.StagingID,
                fullName,
                row.CivilRegistryId,
                row.BeneficiaryId,
                row.Address,
                $"{fullName} ({identity})");
        }
    }

    public sealed record LookupOption(int? Id, string DisplayLabel)
    {
        public static LookupOption Empty(string displayLabel) => new(null, displayLabel);
    }

    public sealed record LedgerEntryListItem(
        DateTime ReleaseDate,
        decimal Amount,
        string SourceModuleText,
        string Remarks)
    {
        public static LedgerEntryListItem FromEntity(BeneficiaryAssistanceLedgerEntry entry)
        {
            return new LedgerEntryListItem(
                entry.ReleaseDate,
                entry.Amount,
                entry.SourceModule.ToString(),
                entry.Remarks);
        }
    }

    public sealed record HistoryItem(
        DateTime Timestamp,
        string Action,
        string Details,
        string Actor)
    {
        public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm");
    }

    public sealed record GrievanceListItem(
        int Id,
        string GrievanceNumber,
        GrievanceType Type,
        string TypeText,
        GrievanceStatus Status,
        string StatusText,
        Brush StatusBrush,
        Brush StatusTextBrush,
        string Title,
        string Description,
        string? CivilRegistryId,
        string? BeneficiaryId,
        string BeneficiaryIdentity,
        int? StagingId,
        int? AssistanceCaseId,
        int? CashForWorkEventId,
        string? ResolutionRemarks,
        DateTime CreatedAt)
    {
        public static GrievanceListItem FromEntity(GrievanceRecord grievance)
        {
            var (background, foreground) = grievance.Status switch
            {
                GrievanceStatus.Open => (CreateBrush("#FEF3C7"), CreateBrush("#92400E")),
                GrievanceStatus.UnderReview => (CreateBrush("#DBEAFE"), CreateBrush("#1D4ED8")),
                GrievanceStatus.Resolved => (CreateBrush("#DCFCE7"), CreateBrush("#166534")),
                _ => (CreateBrush("#FEE2E2"), CreateBrush("#991B1B"))
            };

            return new GrievanceListItem(
                grievance.Id,
                grievance.GrievanceNumber,
                grievance.Type,
                ToTypeText(grievance.Type),
                grievance.Status,
                ToStatusText(grievance.Status),
                background,
                foreground,
                grievance.Title,
                grievance.Description,
                grievance.CivilRegistryId,
                grievance.BeneficiaryId,
                grievance.CivilRegistryId ?? grievance.BeneficiaryId ?? "No linked identity",
                grievance.StagingId,
                grievance.AssistanceCaseId,
                grievance.CashForWorkEventId,
                grievance.ResolutionRemarks,
                grievance.CreatedAt);
        }

        private static string ToTypeText(GrievanceType type)
        {
            return type switch
            {
                GrievanceType.WrongIdentity => "Wrong identity",
                GrievanceType.MissingBeneficiary => "Missing beneficiary",
                GrievanceType.WrongRelease => "Wrong release",
                _ => "Duplicate"
            };
        }

        private static string ToStatusText(GrievanceStatus status)
        {
            return status switch
            {
                GrievanceStatus.UnderReview => "Under review",
                _ => status.ToString()
            };
        }

        private static Brush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }
    }
}

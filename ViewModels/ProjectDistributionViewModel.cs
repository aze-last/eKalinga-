using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class ProjectDistributionViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private AyudaProgram? _selectedProgram;
        private BeneficiaryStaging? _selectedApprovedBeneficiary;
        private string _statusMessage = "Select a project/program first, then attach approved beneficiaries.";
        private Brush _statusBrush = Brushes.DimGray;
        private string _selectedProgramWarning = "Dates are optional. Add them later if the project still has no schedule.";
        private string _distributionScannerSessionUrl = string.Empty;
        private string _distributionScannerSessionPin = string.Empty;
        private string _distributionScannerSessionExpiresAtText = string.Empty;
        private BitmapSource? _distributionScannerQrImage;
        private bool _isBusy;

        public ProjectDistributionViewModel(User currentUser)
        {
            _currentUser = currentUser;

            RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            AddBeneficiaryToProjectCommand = new RelayCommand(async _ => await AddBeneficiaryAsync(), _ => !IsBusy);
            CreateDistributionScannerSessionCommand = new RelayCommand(async _ => await CreateDistributionScannerSessionAsync(), _ => !IsBusy);

            _ = LoadAsync();
        }

        public ObservableCollection<AyudaProgram> Programs { get; } = new();
        public ObservableCollection<BeneficiaryStaging> ApprovedBeneficiaries { get; } = new();
        public ObservableCollection<AyudaProjectBeneficiary> ProjectBeneficiaries { get; } = new();
        public ObservableCollection<AyudaProjectClaim> ProjectClaims { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand AddBeneficiaryToProjectCommand { get; }
        public ICommand CreateDistributionScannerSessionCommand { get; }

        public AyudaProgram? SelectedProgram
        {
            get => _selectedProgram;
            set
            {
                if (SetProperty(ref _selectedProgram, value))
                {
                    SelectedProgramWarning = BuildProgramWarning(value);
                    DistributionScannerSessionUrl = string.Empty;
                    DistributionScannerSessionPin = string.Empty;
                    DistributionScannerSessionExpiresAtText = string.Empty;
                    DistributionScannerQrImage = null;

                    if (!IsBusy)
                    {
                        _ = LoadProjectDetailsAsync();
                    }
                }
            }
        }

        public BeneficiaryStaging? SelectedApprovedBeneficiary
        {
            get => _selectedApprovedBeneficiary;
            set => SetProperty(ref _selectedApprovedBeneficiary, value);
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

        public string SelectedProgramWarning
        {
            get => _selectedProgramWarning;
            private set => SetProperty(ref _selectedProgramWarning, value);
        }

        public string DistributionScannerSessionUrl
        {
            get => _distributionScannerSessionUrl;
            private set => SetProperty(ref _distributionScannerSessionUrl, value);
        }

        public string DistributionScannerSessionPin
        {
            get => _distributionScannerSessionPin;
            private set => SetProperty(ref _distributionScannerSessionPin, value);
        }

        public string DistributionScannerSessionExpiresAtText
        {
            get => _distributionScannerSessionExpiresAtText;
            private set => SetProperty(ref _distributionScannerSessionExpiresAtText, value);
        }

        public BitmapSource? DistributionScannerQrImage
        {
            get => _distributionScannerQrImage;
            private set => SetProperty(ref _distributionScannerQrImage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    if (RefreshCommand is RelayCommand refresh)
                    {
                        refresh.RaiseCanExecuteChanged();
                    }

                    if (AddBeneficiaryToProjectCommand is RelayCommand add)
                    {
                        add.RaiseCanExecuteChanged();
                    }

                    if (CreateDistributionScannerSessionCommand is RelayCommand scanner)
                    {
                        scanner.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Loading project distribution workspace...");

            try
            {
                var selectedProgramId = SelectedProgram?.Id;
                await LoadProgramsAsync(selectedProgramId);
                await LoadApprovedBeneficiariesAsync();
                await LoadProjectDetailsAsync();
                SetSuccessStatus("Project distribution workspace refreshed.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to load project distribution workspace: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadProgramsAsync(int? selectedProgramId = null)
        {
            await using var context = new AppDbContext();
            var budgetService = new BudgetManagementService(context);

            Programs.Clear();
            foreach (var program in await budgetService.GetProgramsAsync())
            {
                Programs.Add(program);
            }

            SelectedProgram = selectedProgramId.HasValue
                ? Programs.FirstOrDefault(program => program.Id == selectedProgramId.Value)
                : SelectedProgram == null
                    ? Programs.FirstOrDefault()
                    : Programs.FirstOrDefault(program => program.Id == SelectedProgram.Id);
        }

        private async Task LoadApprovedBeneficiariesAsync()
        {
            await using var context = new AppDbContext();
            ApprovedBeneficiaries.Clear();

            var beneficiaries = await context.BeneficiaryStaging
                .AsNoTracking()
                .Where(item => item.VerificationStatus == VerificationStatus.Approved)
                .OrderBy(item => item.FullName ?? item.LastName)
                .ThenBy(item => item.FirstName)
                .ToListAsync();

            foreach (var beneficiary in beneficiaries)
            {
                ApprovedBeneficiaries.Add(beneficiary);
            }
        }

        private async Task LoadProjectDetailsAsync()
        {
            await using var context = new AppDbContext();
            var distributionService = new ProjectDistributionService(context);

            ProjectBeneficiaries.Clear();
            ProjectClaims.Clear();

            if (SelectedProgram == null)
            {
                return;
            }

            foreach (var beneficiary in await distributionService.GetBeneficiariesAsync(SelectedProgram.Id))
            {
                ProjectBeneficiaries.Add(beneficiary);
            }

            foreach (var claim in await distributionService.GetClaimsAsync(SelectedProgram.Id))
            {
                ProjectClaims.Add(claim);
            }
        }

        private async Task AddBeneficiaryAsync()
        {
            if (SelectedProgram == null)
            {
                MessageBox.Show("Select a project/program first.", "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedApprovedBeneficiary == null)
            {
                MessageBox.Show("Select an approved beneficiary first.", "No Beneficiary Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetNeutralStatus("Adding approved beneficiary to project...");

            try
            {
                await using var context = new AppDbContext();
                var distributionService = new ProjectDistributionService(context);
                var result = await distributionService.AddBeneficiaryAsync(SelectedProgram.Id, SelectedApprovedBeneficiary.StagingID, _currentUser.Id);
                if (!result.IsSuccess)
                {
                    SetErrorStatus(result.Message);
                    MessageBox.Show(result.Message, "Unable to Add Beneficiary", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await LoadProjectDetailsAsync();
                SetSuccessStatus(result.Message);
                MessageBox.Show(result.Message, "Beneficiary Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to add beneficiary: {ex.Message}");
                MessageBox.Show(ex.Message, "Add Beneficiary Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateDistributionScannerSessionAsync()
        {
            if (SelectedProgram == null)
            {
                MessageBox.Show("Select a project/program before opening the phone scanner.", "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var baseUrl = await LocalScannerGatewayService.Shared.EnsureStartedAsync();
                await using var context = new AppDbContext();
                var sessionService = new ScannerSessionService(context);
                var session = await sessionService.CreateDistributionSessionAsync(SelectedProgram.Id, _currentUser.Id, TimeSpan.FromMinutes(15));
                var sessionUrl = $"{baseUrl}/scanner?session={Uri.EscapeDataString(session.SessionToken)}";

                DistributionScannerSessionUrl = sessionUrl;
                DistributionScannerSessionPin = session.Pin;
                DistributionScannerSessionExpiresAtText = $"Expires {session.ExpiresAt:MMMM dd, yyyy hh:mm tt}";
                DistributionScannerQrImage = QrCodeToolkitService.GenerateQrImage(sessionUrl, 8);
                SetSuccessStatus("Distribution scanner session is ready for the staff phone.");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Unable to start the distribution scanner session: {ex.Message}");
                MessageBox.Show(ex.Message, "Scanner Session Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private static string BuildProgramWarning(AyudaProgram? program)
        {
            if (program == null)
            {
                return "Select a project/program first.";
            }

            if (!program.StartDate.HasValue && !program.EndDate.HasValue)
            {
                return "Warning: this project has no start or end date yet. Dates are optional, but staff should add them later.";
            }

            if (!program.StartDate.HasValue || !program.EndDate.HasValue)
            {
                return "Warning: one project date is still blank. Dates are optional, but complete schedules are easier to audit.";
            }

            return $"Project schedule: {program.StartDate:MMM dd, yyyy} to {program.EndDate:MMM dd, yyyy}. One claim only per beneficiary.";
        }
    }
}

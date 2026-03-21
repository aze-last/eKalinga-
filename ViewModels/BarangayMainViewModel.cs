using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Views;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class BarangayMainViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private string _currentSection = "Dashboard";
        private object _currentView;
        private string _currentSectionTitle = "Dashboard";
        private string _currentSectionSubtitle = "Monitor masterlist, beneficiary review, households, and cash-for-work at a glance.";
        private string _connectionSummary = string.Empty;
        private ImageSource? _userPhotoImage;
        private string _userDisplayName = "Barangay Administrator";

        public BarangayMainViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _currentView = BuildView(_currentSection);

            ShowDashboardCommand = new RelayCommand(_ => SwitchSection("Dashboard"));
            ShowCashForWorkCommand = new RelayCommand(_ => SwitchSection("CashForWork"));
            ShowMasterListCommand = new RelayCommand(_ => SwitchSection("MasterList"));
            ShowBeneficiariesCommand = new RelayCommand(_ => SwitchSection("Beneficiaries"));
            ShowHouseholdRegistryCommand = new RelayCommand(_ => SwitchSection("HouseholdRegistry"));

            RefreshConnectionSummary();
            LoadUserSummary();
        }

        public object CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

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
        public bool IsMasterListSelected => _currentSection == "MasterList";
        public bool IsBeneficiariesSelected => _currentSection == "Beneficiaries";
        public bool IsHouseholdRegistrySelected => _currentSection == "HouseholdRegistry";

        public RelayCommand ShowDashboardCommand { get; }
        public RelayCommand ShowCashForWorkCommand { get; }
        public RelayCommand ShowMasterListCommand { get; }
        public RelayCommand ShowBeneficiariesCommand { get; }
        public RelayCommand ShowHouseholdRegistryCommand { get; }

        public void RefreshConnectionSummary()
        {
            var settings = ConnectionSettingsService.Load();
            var preset = settings.GetPreset(settings.SelectedPreset);
            ConnectionSummary = $"{preset.DisplayName} | {preset.Server}:{preset.Port} / {preset.Database}";
        }

        public void ReloadCurrentView()
        {
            CurrentView = BuildView(_currentSection);
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
            OnPropertyChanged(nameof(IsMasterListSelected));
            OnPropertyChanged(nameof(IsBeneficiariesSelected));
            OnPropertyChanged(nameof(IsHouseholdRegistrySelected));

            CurrentView = BuildView(section);
        }

        private object BuildView(string section)
        {
            switch (section)
            {
                case "Dashboard":
                    CurrentSectionTitle = "Dashboard";
                    CurrentSectionSubtitle = "Monitor masterlist, beneficiary review, households, and cash-for-work at a glance.";
                    return new BarangayDashboardPage();
                case "MasterList":
                    CurrentSectionTitle = "Masterlist";
                    CurrentSectionSubtitle = "Review locally synced beneficiary records from the active database.";
                    return new MasterListPage();
                case "Beneficiaries":
                    CurrentSectionTitle = "Beneficiaries";
                    CurrentSectionSubtitle = "Verify staged beneficiaries and attach them to existing households.";
                    return new BeneficiaryVerificationPage();
                case "HouseholdRegistry":
                    CurrentSectionTitle = "Household registry";
                    CurrentSectionSubtitle = "Browse registered households and inspect eligible members.";
                    return new HouseholdRegistryPage();
                default:
                    CurrentSectionTitle = "Cash-for-work";
                    CurrentSectionSubtitle = "Create work events, assign participants, and save attendance.";
                    return new CashForWorkOcrPage(_currentUser);
            }
        }

        private void LoadUserSummary()
        {
            using var context = new Data.AppDbContext();
            var profile = context.UserProfiles.FirstOrDefault(item => item.UserId == _currentUser.Id);

            UserDisplayName = !string.IsNullOrWhiteSpace(profile?.FullName)
                ? profile.FullName
                : _currentUser.Email;

            UserPhotoImage = BuildImage(profile?.PhotoPath);
        }

        private static ImageSource? BuildImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}

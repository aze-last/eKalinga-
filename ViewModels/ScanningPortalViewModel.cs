using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class ScanningPortalViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private bool _isBusy;
        private string _statusMessage = "READY TO SCAN";
        private Brush _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4E89"));
        
        private MasterListBeneficiary? _scannedBeneficiary;
        private BitmapSource? _scannedBeneficiaryPhoto;
        private bool _isOverlayVisible;

        public ScanningPortalViewModel(User currentUser)
        {
            _currentUser = currentUser;
            CloseOverlayCommand = new RelayCommand(_ => IsOverlayVisible = false);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
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

        public MasterListBeneficiary? ScannedBeneficiary
        {
            get => _scannedBeneficiary;
            private set => SetProperty(ref _scannedBeneficiary, value);
        }

        public BitmapSource? ScannedBeneficiaryPhoto
        {
            get => _scannedBeneficiaryPhoto;
            private set => SetProperty(ref _scannedBeneficiaryPhoto, value);
        }

        public bool IsOverlayVisible
        {
            get => _isOverlayVisible;
            set => SetProperty(ref _isOverlayVisible, value);
        }

        public RelayCommand CloseOverlayCommand { get; }

        public async Task ProcessScanAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            IsBusy = true;
            SetNeutralStatus($"Processing scan: {barcode}...");

            try
            {
                await using var context = new AppDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);
                
                // Lookup by barcode (which matches the QrPayload in the DB)
                var lookup = await digitalIdService.LookupByQrPayloadAsync(barcode);

                if (lookup == null)
                {
                    SetErrorStatus("UNRECOGNIZED BARCODE");
                    return;
                }

                ScannedBeneficiary = new MasterListBeneficiary
                {
                    FullName = lookup.FullName,
                    BeneficiaryId = lookup.BeneficiaryId ?? string.Empty,
                    CivilRegistryId = lookup.CivilRegistryId ?? string.Empty,
                    ResidentsId = lookup.ResidentsId ?? 0
                };

                ScannedBeneficiaryPhoto = string.IsNullOrWhiteSpace(lookup.PhotoPath) 
                    ? null 
                    : LocalImageLoader.Load(lookup.PhotoPath) as BitmapSource;

                IsOverlayVisible = true;
                SetSuccessStatus($"PROFILE FOUND: {lookup.FullName}");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"SCAN ERROR: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetNeutralStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4E89"));
        }

        private void SetSuccessStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BE123C"));
        }
    }
}

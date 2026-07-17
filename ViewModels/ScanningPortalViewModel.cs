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
        private bool _isEKardResult;
        private string _validityBadgeText = string.Empty;
        private Brush _validityBadgeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
        private string _eKardDetailLine = string.Empty;

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

        public bool IsEKardResult
        {
            get => _isEKardResult;
            private set => SetProperty(ref _isEKardResult, value);
        }

        public string ValidityBadgeText
        {
            get => _validityBadgeText;
            private set => SetProperty(ref _validityBadgeText, value);
        }

        public Brush ValidityBadgeBrush
        {
            get => _validityBadgeBrush;
            private set => SetProperty(ref _validityBadgeBrush, value);
        }

        public string EKardDetailLine
        {
            get => _eKardDetailLine;
            private set => SetProperty(ref _eKardDetailLine, value);
        }

        public RelayCommand CloseOverlayCommand { get; }

        public async Task ProcessScanAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            // Municipal e-Kard cards carry a BEN-... beneficiary id; route those to
            // the CRS verification contract flow. Own ASMBID cards keep the existing path.
            if (EKardPayloadRouter.IsEKardPayload(barcode))
            {
                await ProcessEKardScanAsync(barcode);
                return;
            }

            IsBusy = true;
            SetNeutralStatus($"Processing scan: {barcode}...");

            try
            {
                await using var context = new LocalDbContext();
                var digitalIdService = new BeneficiaryDigitalIdService(context);

                // Lookup by barcode (which matches the QrPayload in the DB)
                var lookup = await digitalIdService.LookupByQrPayloadAsync(barcode);

                if (lookup == null)
                {
                    SetErrorStatus("UNRECOGNIZED BARCODE");
                    return;
                }

                IsEKardResult = false;
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

        private async Task ProcessEKardScanAsync(string payload)
        {
            IsBusy = true;
            SetNeutralStatus("Verifying e-Kard against CRS...");

            try
            {
                await using var context = new LocalDbContext();
                var verificationService = new CrsDigitalIdVerificationService(context);
                var result = await verificationService.VerifyAsync(new EKardVerificationRequest
                {
                    BeneficiaryId = payload,
                    UserId = _currentUser?.Id,
                    UserName = _currentUser?.Username ?? string.Empty
                }, CancellationToken.None);

                ApplyEKardResult(result);

                // Match the local masterlist (no auto-import) so operators can proceed
                // with local workflows when the person is already registered here.
                var localMatch = await context.BeneficiaryStaging
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BeneficiaryId == result.BeneficiaryId);

                ScannedBeneficiary = new MasterListBeneficiary
                {
                    FullName = localMatch?.FullName ?? result.BeneficiaryId,
                    BeneficiaryId = result.BeneficiaryId,
                    CivilRegistryId = localMatch?.CivilRegistryId ?? string.Empty,
                    ResidentsId = localMatch?.ResidentsId ?? 0
                };

                ScannedBeneficiaryPhoto = LocalImageLoader.LoadFromBytes(result.Photo) as BitmapSource
                    ?? (localMatch != null && !string.IsNullOrWhiteSpace(localMatch.PhotoPath)
                        ? LocalImageLoader.Load(localMatch.PhotoPath) as BitmapSource
                        : null);

                IsOverlayVisible = true;
            }
            catch (Exception ex)
            {
                SetErrorStatus($"E-KARD SCAN ERROR: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyEKardResult(EKardVerificationResult result)
        {
            IsEKardResult = true;

            var syncedNote = result.Source == EKardSource.LocalCache && result.LastSyncedAt.HasValue
                ? $" (offline — last synced {result.LastSyncedAt:MMM dd, yyyy hh:mm tt})"
                : string.Empty;

            switch (result.Validity)
            {
                case EKardValidity.Valid:
                    ValidityBadgeText = "VALID";
                    ValidityBadgeBrush = MakeBrush("#15803D");
                    EKardDetailLine = $"e-Kard {result.IdNumber} • expires {(result.ExpiryDate.HasValue ? result.ExpiryDate.Value.ToString("MMM dd, yyyy") : "never")}{syncedNote}";
                    SetSuccessStatus($"E-KARD VALID: {result.BeneficiaryId}");
                    break;
                case EKardValidity.Expired:
                    ValidityBadgeText = "EXPIRED";
                    ValidityBadgeBrush = MakeBrush("#854D0E");
                    EKardDetailLine = $"e-Kard {result.IdNumber} expired {result.ExpiryDate:MMM dd, yyyy}{syncedNote}";
                    SetWarningStatus($"E-KARD EXPIRED: {result.BeneficiaryId}");
                    break;
                case EKardValidity.Revoked:
                    ValidityBadgeText = "REVOKED";
                    ValidityBadgeBrush = MakeBrush("#BE123C");
                    EKardDetailLine = string.IsNullOrWhiteSpace(result.RevocationReason)
                        ? $"e-Kard {result.IdNumber} revoked{syncedNote}"
                        : $"e-Kard {result.IdNumber} revoked — {result.RevocationReason}{syncedNote}";
                    SetErrorStatus($"E-KARD REVOKED: {result.BeneficiaryId}");
                    break;
                case EKardValidity.NotFound:
                    ValidityBadgeText = "NOT FOUND";
                    ValidityBadgeBrush = MakeBrush("#475569");
                    EKardDetailLine = "No e-Kard Digital ID has ever been issued for this beneficiary id.";
                    SetErrorStatus($"E-KARD NOT FOUND: {result.BeneficiaryId}");
                    break;
                default:
                    ValidityBadgeText = "UNKNOWN";
                    ValidityBadgeBrush = MakeBrush("#475569");
                    EKardDetailLine = "CRS is unreachable and this card has never been verified on this device.";
                    SetWarningStatus($"E-KARD UNKNOWN (OFFLINE): {result.BeneficiaryId}");
                    break;
            }
        }

        private static SolidColorBrush MakeBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
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

        private void SetWarningStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#854D0E"));
        }

        private void SetErrorStatus(string message)
        {
            StatusMessage = message;
            StatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BE123C"));
        }
    }
}

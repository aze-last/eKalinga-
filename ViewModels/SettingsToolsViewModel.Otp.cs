using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed partial class SettingsToolsViewModel
    {
        private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan PasswordChangeAuthorizationDuration = TimeSpan.FromMinutes(3);
        private const int OtpMaxAttempts = 3;
        private const string SharedProtectedSettingsPurposeLabel = "Remote Snapshot, App Database, and GGMS Budget Source";
        private const string PasswordChangePurposeLabel = "password change";

        private RelayCommand _unlockSensitiveSettingsCommand = null!;
        private RelayCommand _sendSensitiveSettingsOtpCommand = null!;
        private RelayCommand _verifySensitiveSettingsOtpCommand = null!;
        private RelayCommand _resendSensitiveSettingsOtpCommand = null!;
        private RelayCommand _sendPasswordChangeOtpCommand = null!;
        private RelayCommand _verifyPasswordChangeOtpCommand = null!;
        private RelayCommand _resendPasswordChangeOtpCommand = null!;
        private DispatcherTimer _otpStateTimer = null!;

        private OtpChallengeSession? _sensitiveSettingsOtpSession;
        private string _sensitiveSettingsUnlockPassword = string.Empty;
        private string _sensitiveSettingsUnlockStatusMessage = "Re-enter the current admin password to unlock protected settings.";
        private Brush _sensitiveSettingsUnlockStatusBrush = CreateBrush("#6B7280");
        private string _sensitiveSettingsOtpCode = string.Empty;
        private string _sensitiveSettingsOtpStatusMessage = "OTP is only required when protected settings changes are being saved.";
        private Brush _sensitiveSettingsOtpStatusBrush = CreateBrush("#6B7280");
        private bool _isSensitiveSettingsUnlocked;
        private bool _hasSensitiveSettingsSaveAuthorization;
        private bool _showSensitiveSettingsOtpPanel;
        private Action? _pendingSensitiveSettingsAuthorizationAction;
        private string _pendingSensitiveSettingsActionDescription = SharedProtectedSettingsPurposeLabel;

        private OtpChallengeSession? _passwordChangeOtpSession;
        private DateTimeOffset? _passwordChangeOtpAuthorizedUntil;
        private string _passwordChangeOtpCode = string.Empty;
        private string _passwordChangeOtpStatusMessage = "Changing the current account password requires a separate OTP.";
        private Brush _passwordChangeOtpStatusBrush = CreateBrush("#6B7280");
        private bool _showPasswordChangeOtpPanel;

        private void InitializeOtpState()
        {
            _unlockSensitiveSettingsCommand = new RelayCommand(_ => UnlockSensitiveSettings(), _ => CanUnlockSensitiveSettings);
            _sendSensitiveSettingsOtpCommand = new RelayCommand(async _ => await SendSensitiveSettingsOtpAsync(isResend: false), _ => CanSendSensitiveSettingsOtp);
            _verifySensitiveSettingsOtpCommand = new RelayCommand(_ => VerifySensitiveSettingsOtp(), _ => CanVerifySensitiveSettingsOtp);
            _resendSensitiveSettingsOtpCommand = new RelayCommand(async _ => await SendSensitiveSettingsOtpAsync(isResend: true), _ => CanResendSensitiveSettingsOtp);
            _sendPasswordChangeOtpCommand = new RelayCommand(async _ => await SendPasswordChangeOtpAsync(isResend: false), _ => CanSendPasswordChangeOtp);
            _verifyPasswordChangeOtpCommand = new RelayCommand(_ => VerifyPasswordChangeOtp(), _ => CanVerifyPasswordChangeOtp);
            _resendPasswordChangeOtpCommand = new RelayCommand(async _ => await SendPasswordChangeOtpAsync(isResend: true), _ => CanResendPasswordChangeOtp);

            _otpStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _otpStateTimer.Tick += (_, _) => RefreshOtpTimers();
            _otpStateTimer.Start();
        }

        public bool HasValidOfficialEmailForOtp => IsValidEmail(SystemEmail.Trim());

        public string OfficialEmailMask => HasValidOfficialEmailForOtp
            ? MaskEmailAddress(SystemEmail.Trim())
            : "Official Email not configured";

        public bool IsSensitiveSettingsUnlocked
        {
            get => _isSensitiveSettingsUnlocked;
            private set
            {
                if (SetProperty(ref _isSensitiveSettingsUnlocked, value))
                {
                    OnPropertyChanged(nameof(IsSensitiveSettingsLocked));
                    RaiseOtpCommandStates();
                }
            }
        }

        public bool IsSensitiveSettingsLocked => !IsSensitiveSettingsUnlocked;

        public string SensitiveSettingsUnlockPassword
        {
            get => _sensitiveSettingsUnlockPassword;
            set
            {
                if (SetProperty(ref _sensitiveSettingsUnlockPassword, value))
                {
                    _unlockSensitiveSettingsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SensitiveSettingsUnlockPromptText => !HasCurrentUser
            ? "Sign in with the current admin account first before unlocking protected settings."
            : _currentUser?.Role != UserRole.Admin
                ? "Only admin accounts can unlock protected settings."
                : "Re-enter the current admin password to unlock Remote Snapshot, App Database, and GGMS Budget Source.";

        public string SensitiveSettingsUnlockStatusMessage
        {
            get => _sensitiveSettingsUnlockStatusMessage;
            private set => SetProperty(ref _sensitiveSettingsUnlockStatusMessage, value);
        }

        public Brush SensitiveSettingsUnlockStatusBrush
        {
            get => _sensitiveSettingsUnlockStatusBrush;
            private set => SetProperty(ref _sensitiveSettingsUnlockStatusBrush, value);
        }

        public bool ShowSensitiveSettingsOtpPanel
        {
            get => _showSensitiveSettingsOtpPanel;
            private set
            {
                if (SetProperty(ref _showSensitiveSettingsOtpPanel, value))
                {
                    RaiseOtpCommandStates();
                }
            }
        }

        public string SensitiveSettingsOtpPromptText => HasValidOfficialEmailForOtp
            ? $"OTP is only required if you save changes to {_pendingSensitiveSettingsActionDescription}. Send the code to {OfficialEmailMask}, then verify it before the save continues."
            : "Save a valid Official Email in System Profile first before requesting access to protected settings.";

        public string SensitiveSettingsOtpCode
        {
            get => _sensitiveSettingsOtpCode;
            set
            {
                if (SetProperty(ref _sensitiveSettingsOtpCode, value))
                {
                    _verifySensitiveSettingsOtpCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SensitiveSettingsOtpStatusMessage
        {
            get => _sensitiveSettingsOtpStatusMessage;
            private set => SetProperty(ref _sensitiveSettingsOtpStatusMessage, value);
        }

        public Brush SensitiveSettingsOtpStatusBrush
        {
            get => _sensitiveSettingsOtpStatusBrush;
            private set => SetProperty(ref _sensitiveSettingsOtpStatusBrush, value);
        }

        public string SensitiveSettingsOtpResendButtonText => BuildResendButtonText(_sensitiveSettingsOtpSession);

        public bool CanUnlockSensitiveSettings =>
            !IsBusy
            && IsSensitiveSettingsLocked
            && HasCurrentUser
            && (_currentUser?.Role == UserRole.Admin || _currentUser?.Role == UserRole.SuperAdmin)
            && !string.IsNullOrWhiteSpace(SensitiveSettingsUnlockPassword);

        public bool CanSendSensitiveSettingsOtp =>
            !IsBusy
            && ShowSensitiveSettingsOtpPanel
            && !_hasSensitiveSettingsSaveAuthorization
            && HasValidOfficialEmailForOtp
            && _sensitiveSettingsOtpSession == null;

        public bool CanResendSensitiveSettingsOtp =>
            !IsBusy
            && ShowSensitiveSettingsOtpPanel
            && !_hasSensitiveSettingsSaveAuthorization
            && HasValidOfficialEmailForOtp
            && _sensitiveSettingsOtpSession != null
            && OtpChallengeService.CanResend(_sensitiveSettingsOtpSession, DateTimeOffset.UtcNow);

        public bool CanVerifySensitiveSettingsOtp =>
            !IsBusy
            && ShowSensitiveSettingsOtpPanel
            && _sensitiveSettingsOtpSession != null
            && !string.IsNullOrWhiteSpace(SensitiveSettingsOtpCode);

        public string PasswordChangeOtpPromptText => HasValidOfficialEmailForOtp
            ? $"A separate OTP will be sent to {OfficialEmailMask} before the current account password can be changed."
            : "Save a valid Official Email in System Profile first before changing the current account password.";

        public string PasswordChangeOtpCode
        {
            get => _passwordChangeOtpCode;
            set
            {
                if (SetProperty(ref _passwordChangeOtpCode, value))
                {
                    _verifyPasswordChangeOtpCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string PasswordChangeOtpStatusMessage
        {
            get => _passwordChangeOtpStatusMessage;
            private set => SetProperty(ref _passwordChangeOtpStatusMessage, value);
        }

        public Brush PasswordChangeOtpStatusBrush
        {
            get => _passwordChangeOtpStatusBrush;
            private set => SetProperty(ref _passwordChangeOtpStatusBrush, value);
        }

        public bool ShowPasswordChangeOtpPanel
        {
            get => _showPasswordChangeOtpPanel;
            private set
            {
                if (SetProperty(ref _showPasswordChangeOtpPanel, value))
                {
                    RaiseOtpCommandStates();
                }
            }
        }

        public string PasswordChangeOtpResendButtonText => BuildResendButtonText(_passwordChangeOtpSession);

        public bool HasPasswordChangeAuthorization =>
            _passwordChangeOtpAuthorizedUntil.HasValue
            && DateTimeOffset.UtcNow <= _passwordChangeOtpAuthorizedUntil.Value;

        public bool CanSendPasswordChangeOtp =>
            !IsBusy
            && HasCurrentUser
            && HasValidOfficialEmailForOtp
            && !HasPasswordChangeAuthorization
            && _passwordChangeOtpSession == null;

        public bool CanResendPasswordChangeOtp =>
            !IsBusy
            && HasCurrentUser
            && HasValidOfficialEmailForOtp
            && !HasPasswordChangeAuthorization
            && _passwordChangeOtpSession != null
            && OtpChallengeService.CanResend(_passwordChangeOtpSession, DateTimeOffset.UtcNow);

        public bool CanVerifyPasswordChangeOtp =>
            !IsBusy
            && HasCurrentUser
            && _passwordChangeOtpSession != null
            && !string.IsNullOrWhiteSpace(PasswordChangeOtpCode);

        public ICommand UnlockSensitiveSettingsCommand => _unlockSensitiveSettingsCommand;
        public ICommand SendSensitiveSettingsOtpCommand => _sendSensitiveSettingsOtpCommand;
        public ICommand VerifySensitiveSettingsOtpCommand => _verifySensitiveSettingsOtpCommand;
        public ICommand ResendSensitiveSettingsOtpCommand => _resendSensitiveSettingsOtpCommand;
        public ICommand SendPasswordChangeOtpCommand => _sendPasswordChangeOtpCommand;
        public ICommand VerifyPasswordChangeOtpCommand => _verifyPasswordChangeOtpCommand;
        public ICommand ResendPasswordChangeOtpCommand => _resendPasswordChangeOtpCommand;

        public async Task HandleChangePasswordAsync()
        {
            LastPasswordChangeSucceeded = false;

            if (_currentUser == null)
            {
                SetSecurityError("No signed-in user is available for password changes.");
                return;
            }

            if (!HasPasswordChangeAuthorization && IsOtpEnabled)
            {
                ShowPasswordChangeOtpPanel = true;
                if (_passwordChangeOtpSession == null)
                {
                    await SendPasswordChangeOtpAsync(isResend: false);
                    return;
                }

                SetSecurityNeutral("Verify the separate OTP first before the password can be changed.");
                return;
            }

            CompletePasswordChange();
        }

        private void UnlockSensitiveSettings()
        {
            if (_currentUser == null)
            {
                SetSensitiveSettingsUnlockError("Sign in with the current admin account first before unlocking protected settings.");
                return;
            }

            if (_currentUser.Role != UserRole.Admin && _currentUser.Role != UserRole.SuperAdmin)
            {
                SetSensitiveSettingsUnlockError("Only admin accounts can unlock protected settings.");
                return;
            }

            using var context = _dbContextFactory();
            var result = UserAccountSettingsService.VerifyCurrentPassword(context, _currentUser, SensitiveSettingsUnlockPassword);
            if (!result.IsSuccess)
            {
                SetSensitiveSettingsUnlockError(result.Message);
                return;
            }

            SensitiveSettingsUnlockPassword = string.Empty;
            IsSensitiveSettingsUnlocked = true;
            SetSensitiveSettingsUnlockSuccess("Protected settings unlocked for this Settings session.");
        }

        private bool RequireSensitiveSettingsAuthorization(string actionDescription, Action onAuthorized)
        {
            if (_hasSensitiveSettingsSaveAuthorization || !IsOtpEnabled)
            {
                return true;
            }

            _pendingSensitiveSettingsAuthorizationAction = onAuthorized;
            _pendingSensitiveSettingsActionDescription = string.IsNullOrWhiteSpace(actionDescription)
                ? SharedProtectedSettingsPurposeLabel
                : actionDescription.Trim();
            ShowSensitiveSettingsOtpPanel = true;

            if (!HasValidOfficialEmailForOtp)
            {
                var message = "Save a valid Official Email in System Profile first before requesting OTP access.";
                SetSensitiveSettingsOtpError(message);
                return false;
            }

            SetSensitiveSettingsOtpNeutral($"OTP is required only because you changed {_pendingSensitiveSettingsActionDescription}. Send and verify the code before saving.");
            RaiseOtpPropertyChanges();
            return false;
        }

        public void Dispose()
        {
            _otpStateTimer.Stop();
            ResetSensitiveSettingsOtpSession(clearUnlockState: true);
            ResetPasswordChangeOtpFlow(clearAuthorization: true, clearStatusMessage: false);
        }

        private async Task SendSensitiveSettingsOtpAsync(bool isResend)
        {
            ShowSensitiveSettingsOtpPanel = true;

            if (!TryGetOfficialEmailRecipient(out var recipientEmail, out var validationMessage))
            {
                SetSensitiveSettingsOtpError(validationMessage);
                return;
            }

            if (isResend && _sensitiveSettingsOtpSession != null && !OtpChallengeService.CanResend(_sensitiveSettingsOtpSession, DateTimeOffset.UtcNow))
            {
                SetSensitiveSettingsOtpError($"Wait {GetCountdownSeconds(_sensitiveSettingsOtpSession.ResendAvailableAtUtc)} second(s) before requesting a new code.");
                return;
            }

            await ExecuteBusyAsync(async () =>
            {
                var issuedCode = OtpChallengeService.IssueCode(
                    SharedProtectedSettingsPurposeLabel,
                    recipientEmail,
                    DateTimeOffset.UtcNow,
                    OtpExpiry,
                    OtpResendCooldown,
                    OtpMaxAttempts);

                var sendResult = await OtpEmailService.SendOtpAsync(
                    recipientEmail,
                    issuedCode.Code,
                    SharedProtectedSettingsPurposeLabel,
                    OtpExpiry);

                if (!sendResult.IsSuccess)
                {
                    SetSensitiveSettingsOtpError(sendResult.Message);
                    return;
                }

                _sensitiveSettingsOtpSession = issuedCode.Session;
                SensitiveSettingsOtpCode = string.Empty;
                SetSensitiveSettingsOtpSuccess($"OTP sent to {OfficialEmailMask}. The code expires in 3 minutes.");
                RaiseOtpPropertyChanges();
            });
        }

        private void VerifySensitiveSettingsOtp()
        {
            var result = OtpChallengeService.VerifyCode(_sensitiveSettingsOtpSession, SensitiveSettingsOtpCode, DateTimeOffset.UtcNow);
            if (!result.IsSuccess)
            {
                if (result.RequiresNewCode)
                {
                    ResetSensitiveSettingsOtpSession(clearUnlockState: false);
                }

                SensitiveSettingsOtpCode = string.Empty;
                SetSensitiveSettingsOtpError(result.Message);
                RaiseOtpPropertyChanges();
                return;
            }

            ResetSensitiveSettingsOtpSession(clearUnlockState: false);
            ShowSensitiveSettingsOtpPanel = false;
            SetSensitiveSettingsOtpSuccess("OTP verified. Protected settings can be saved for this Settings session.");
            _hasSensitiveSettingsSaveAuthorization = true;

            var pendingAction = _pendingSensitiveSettingsAuthorizationAction;
            _pendingSensitiveSettingsAuthorizationAction = null;
            pendingAction?.Invoke();
        }

        private async Task SendPasswordChangeOtpAsync(bool isResend)
        {
            if (_currentUser == null)
            {
                SetSecurityError("No signed-in user is available for password changes.");
                return;
            }

            ShowPasswordChangeOtpPanel = true;

            if (!TryGetOfficialEmailRecipient(out var recipientEmail, out var validationMessage))
            {
                SetPasswordChangeOtpError(validationMessage);
                SetSecurityError(validationMessage);
                return;
            }

            if (isResend && _passwordChangeOtpSession != null && !OtpChallengeService.CanResend(_passwordChangeOtpSession, DateTimeOffset.UtcNow))
            {
                var countdown = GetCountdownSeconds(_passwordChangeOtpSession.ResendAvailableAtUtc);
                SetPasswordChangeOtpError($"Wait {countdown} second(s) before requesting a new code.");
                SetSecurityError($"Wait {countdown} second(s) before requesting a new password-change OTP.");
                return;
            }

            await ExecuteBusyAsync(async () =>
            {
                var issuedCode = OtpChallengeService.IssueCode(
                    PasswordChangePurposeLabel,
                    recipientEmail,
                    DateTimeOffset.UtcNow,
                    OtpExpiry,
                    OtpResendCooldown,
                    OtpMaxAttempts);

                var sendResult = await OtpEmailService.SendOtpAsync(
                    recipientEmail,
                    issuedCode.Code,
                    PasswordChangePurposeLabel,
                    OtpExpiry);

                if (!sendResult.IsSuccess)
                {
                    SetPasswordChangeOtpError(sendResult.Message);
                    SetSecurityError(sendResult.Message);
                    return;
                }

                _passwordChangeOtpSession = issuedCode.Session;
                _passwordChangeOtpAuthorizedUntil = null;
                PasswordChangeOtpCode = string.Empty;
                SetPasswordChangeOtpSuccess($"OTP sent to {OfficialEmailMask}. Enter the code below to continue.");
                SetSecurityNeutral("A separate OTP was sent for password change verification.");
                RaiseOtpPropertyChanges();
            });
        }

        private void VerifyPasswordChangeOtp()
        {
            var result = OtpChallengeService.VerifyCode(_passwordChangeOtpSession, PasswordChangeOtpCode, DateTimeOffset.UtcNow);
            if (!result.IsSuccess)
            {
                if (result.RequiresNewCode)
                {
                    _passwordChangeOtpSession = null;
                }

                PasswordChangeOtpCode = string.Empty;
                SetPasswordChangeOtpError(result.Message);
                SetSecurityError(result.Message);
                RaiseOtpPropertyChanges();
                return;
            }

            _passwordChangeOtpAuthorizedUntil = DateTimeOffset.UtcNow.Add(PasswordChangeAuthorizationDuration);
            _passwordChangeOtpSession = null;
            PasswordChangeOtpCode = string.Empty;
            ShowPasswordChangeOtpPanel = false;
            SetPasswordChangeOtpSuccess("Password-change OTP verified successfully.");
            SetSecurityNeutral("OTP verified. Click Change Password again within 3 minutes to apply the new password.");
            RaiseOtpPropertyChanges();
        }

        private void CompletePasswordChange()
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
            ResetPasswordChangeOtpFlow(clearAuthorization: true, clearStatusMessage: false);
            SetSecuritySuccess(result.Message);
        }

        private void RefreshOtpTimers()
        {
            var now = DateTimeOffset.UtcNow;
            var propertyStateChanged = false;

            if (_sensitiveSettingsOtpSession != null && now > _sensitiveSettingsOtpSession.ExpiresAtUtc)
            {
                ResetSensitiveSettingsOtpSession(clearUnlockState: false);
                SetSensitiveSettingsOtpError("The OTP expired. Request a new code.");
                propertyStateChanged = true;
            }

            if (_passwordChangeOtpSession != null && now > _passwordChangeOtpSession.ExpiresAtUtc)
            {
                _passwordChangeOtpSession = null;
                PasswordChangeOtpCode = string.Empty;
                SetPasswordChangeOtpError("The OTP expired. Request a new code.");
                SetSecurityError("The password-change OTP expired. Request a new code.");
                propertyStateChanged = true;
            }

            if (_passwordChangeOtpAuthorizedUntil.HasValue && now > _passwordChangeOtpAuthorizedUntil.Value)
            {
                _passwordChangeOtpAuthorizedUntil = null;
                SetSecurityNeutral("Password-change verification expired. Request a new OTP to continue.");
                propertyStateChanged = true;
            }

            if (propertyStateChanged || _sensitiveSettingsOtpSession != null || _passwordChangeOtpSession != null)
            {
                RaiseOtpPropertyChanges();
            }
        }

        private void HandleSystemEmailChanged()
        {
            _sensitiveSettingsOtpSession = null;
            SensitiveSettingsOtpCode = string.Empty;
            ShowSensitiveSettingsOtpPanel = false;
            SensitiveSettingsUnlockPassword = string.Empty;
            _pendingSensitiveSettingsAuthorizationAction = null;
            _pendingSensitiveSettingsActionDescription = SharedProtectedSettingsPurposeLabel;
            _hasSensitiveSettingsSaveAuthorization = false;
            _passwordChangeOtpSession = null;
            _passwordChangeOtpAuthorizedUntil = null;
            PasswordChangeOtpCode = string.Empty;
            ShowPasswordChangeOtpPanel = false;
            RaiseOtpPropertyChanges();
        }

        private void OnBusyStateChangedForOtp()
        {
            RaiseOtpCommandStates();
        }

        private void ResetSensitiveSettingsOtpSession(bool clearUnlockState)
        {
            _sensitiveSettingsOtpSession = null;
            SensitiveSettingsOtpCode = string.Empty;
            _hasSensitiveSettingsSaveAuthorization = false;

            if (clearUnlockState)
            {
                IsSensitiveSettingsUnlocked = false;
                ShowSensitiveSettingsOtpPanel = false;
                SensitiveSettingsUnlockPassword = string.Empty;
                _pendingSensitiveSettingsAuthorizationAction = null;
                _pendingSensitiveSettingsActionDescription = SharedProtectedSettingsPurposeLabel;
            }

            RaiseOtpPropertyChanges();
        }

        private void ResetPasswordChangeOtpFlow(bool clearAuthorization, bool clearStatusMessage)
        {
            _passwordChangeOtpSession = null;
            PasswordChangeOtpCode = string.Empty;
            ShowPasswordChangeOtpPanel = false;

            if (clearAuthorization)
            {
                _passwordChangeOtpAuthorizedUntil = null;
            }

            if (clearStatusMessage)
            {
                SetPasswordChangeOtpNeutral("Changing the current account password requires a separate OTP.");
            }

            RaiseOtpPropertyChanges();
        }

        private bool TryGetOfficialEmailRecipient(out string recipientEmail, out string validationMessage)
        {
            recipientEmail = SystemEmail.Trim();
            validationMessage = string.Empty;

            if (!HasValidOfficialEmailForOtp)
            {
                validationMessage = "Save a valid Official Email in System Profile first before requesting OTP access.";
                return false;
            }

            return true;
        }

        private static string MaskEmailAddress(string email)
        {
            var trimmed = email.Trim();
            var separatorIndex = trimmed.IndexOf('@');
            if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
            {
                return trimmed;
            }

            var localPart = trimmed[..separatorIndex];
            var domain = trimmed[(separatorIndex + 1)..];
            var visibleCharacters = Math.Min(1, localPart.Length);
            var maskedCharacters = Math.Max(0, localPart.Length - visibleCharacters);
            return $"{localPart[..visibleCharacters]}{new string('*', Math.Max(3, maskedCharacters))}@{domain}";
        }

        private static string BuildResendButtonText(OtpChallengeSession? session)
        {
            if (session == null)
            {
                return "RESEND OTP";
            }

            var remainingSeconds = GetCountdownSeconds(session.ResendAvailableAtUtc);
            return remainingSeconds > 0
                ? $"RESEND OTP ({remainingSeconds}s)"
                : "RESEND OTP";
        }

        private static int GetCountdownSeconds(DateTimeOffset targetUtc)
        {
            return Math.Max(0, (int)Math.Ceiling((targetUtc - DateTimeOffset.UtcNow).TotalSeconds));
        }

        private void RaiseOtpPropertyChanges()
        {
            OnPropertyChanged(nameof(HasValidOfficialEmailForOtp));
            OnPropertyChanged(nameof(OfficialEmailMask));
            OnPropertyChanged(nameof(SensitiveSettingsUnlockPromptText));
            OnPropertyChanged(nameof(SensitiveSettingsUnlockStatusMessage));
            OnPropertyChanged(nameof(SensitiveSettingsUnlockStatusBrush));
            OnPropertyChanged(nameof(ShowSensitiveSettingsOtpPanel));
            OnPropertyChanged(nameof(IsSensitiveSettingsLocked));
            OnPropertyChanged(nameof(SensitiveSettingsOtpPromptText));
            OnPropertyChanged(nameof(SensitiveSettingsOtpResendButtonText));
            OnPropertyChanged(nameof(PasswordChangeOtpPromptText));
            OnPropertyChanged(nameof(PasswordChangeOtpResendButtonText));
            OnPropertyChanged(nameof(HasPasswordChangeAuthorization));
            RaiseOtpCommandStates();
        }

        private void RaiseOtpCommandStates()
        {
            _unlockSensitiveSettingsCommand?.RaiseCanExecuteChanged();
            _sendSensitiveSettingsOtpCommand?.RaiseCanExecuteChanged();
            _verifySensitiveSettingsOtpCommand?.RaiseCanExecuteChanged();
            _resendSensitiveSettingsOtpCommand?.RaiseCanExecuteChanged();
            _sendPasswordChangeOtpCommand?.RaiseCanExecuteChanged();
            _verifyPasswordChangeOtpCommand?.RaiseCanExecuteChanged();
            _resendPasswordChangeOtpCommand?.RaiseCanExecuteChanged();
            _changePasswordCommand?.RaiseCanExecuteChanged();
        }

        private void SetSensitiveSettingsOtpNeutral(string message)
        {
            SensitiveSettingsOtpStatusMessage = message;
            SensitiveSettingsOtpStatusBrush = CreateBrush("#6B7280");
        }

        private void SetSensitiveSettingsUnlockNeutral(string message)
        {
            SensitiveSettingsUnlockStatusMessage = message;
            SensitiveSettingsUnlockStatusBrush = CreateBrush("#6B7280");
        }

        private void SetSensitiveSettingsUnlockSuccess(string message)
        {
            SensitiveSettingsUnlockStatusMessage = message;
            SensitiveSettingsUnlockStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetSensitiveSettingsUnlockError(string message)
        {
            SensitiveSettingsUnlockStatusMessage = message;
            SensitiveSettingsUnlockStatusBrush = CreateBrush("#991B1B");
        }

        private void SetSensitiveSettingsOtpSuccess(string message)
        {
            SensitiveSettingsOtpStatusMessage = message;
            SensitiveSettingsOtpStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetSensitiveSettingsOtpError(string message)
        {
            SensitiveSettingsOtpStatusMessage = message;
            SensitiveSettingsOtpStatusBrush = CreateBrush("#991B1B");
        }

        private void SetPasswordChangeOtpNeutral(string message)
        {
            PasswordChangeOtpStatusMessage = message;
            PasswordChangeOtpStatusBrush = CreateBrush("#6B7280");
        }

        private void SetPasswordChangeOtpSuccess(string message)
        {
            PasswordChangeOtpStatusMessage = message;
            PasswordChangeOtpStatusBrush = CreateBrush("#1A7A4A");
        }

        private void SetPasswordChangeOtpError(string message)
        {
            PasswordChangeOtpStatusMessage = message;
            PasswordChangeOtpStatusBrush = CreateBrush("#991B1B");
        }
    }
}

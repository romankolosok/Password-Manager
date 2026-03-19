using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.ViewModels
{
    public partial class ConfirmOtpViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        public string Email { get; set; } = string.Empty;

        public enum OtpPurpose
        {
            SignupConfirmation,
            PasswordRecovery
        }

        // Set by coordinator before showing the view.
        public OtpPurpose Purpose { get; set; } = OtpPurpose.SignupConfirmation;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit1 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit2 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit3 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit4 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit5 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit6 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit7 = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyPropertyChangedFor(nameof(ConfirmOTP))]
        private string _digit8 = string.Empty;

        partial void OnDigit1Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit2Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit3Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit4Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit5Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit6Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit7Changed(string value) => ConfirmOtpError = string.Empty;
        partial void OnDigit8Changed(string value) => ConfirmOtpError = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _confirmOtpError = string.Empty;
        
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResendOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _resendOtpError = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        private bool _isLoading;

        public string ConfirmOTP => $"{Digit1}{Digit2}{Digit3}{Digit4}{Digit5}{Digit6}{Digit7}{Digit8}";

        public string DisplayError
        {
            get
            {
                if (!string.IsNullOrEmpty(ConfirmOtpError))
                    return ConfirmOtpError;
                if (!string.IsNullOrEmpty(ResendOtpError))
                    return ResendOtpError;
                return string.Empty;
            }
        }

        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

        private bool AllFieldsFilled =>
            !string.IsNullOrWhiteSpace(Digit1)
            && !string.IsNullOrWhiteSpace(Digit2)
            && !string.IsNullOrWhiteSpace(Digit3)
            && !string.IsNullOrWhiteSpace(Digit4)
            && !string.IsNullOrWhiteSpace(Digit5)
            && !string.IsNullOrWhiteSpace(Digit6)
            && !string.IsNullOrWhiteSpace(Digit7)
            && !string.IsNullOrWhiteSpace(Digit8);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResendOtpCommand))]
        private bool _isResending;

        partial void OnIsResendingChanged(bool value) => OnPropertyChanged(nameof(ResendButtonText));

        private System.Timers.Timer? _resendTimer;
        private int _secondsRemaining;
        private const int ResendCooldownSeconds = 90;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResendOtpCommand))]
        [NotifyPropertyChangedFor(nameof(ResendButtonText))]
        [NotifyPropertyChangedFor(nameof(ResendTimerText))]
        private bool _resendTimerActive;

        public string ResendTimerText
        {
            get
            {
                var m = _secondsRemaining / 60;
                var s = _secondsRemaining % 60;
                return $"{m}:{s:D2}";
            }
        }

        public string ResendButtonText => IsResending ? "Sending..." : ResendTimerActive ? $"Resend in {ResendTimerText}" : "Resend";

        private void StartResendTimer()
        {
            _secondsRemaining = ResendCooldownSeconds;
            ResendTimerActive = true;
            OnPropertyChanged(nameof(ResendTimerText));
            OnPropertyChanged(nameof(ResendButtonText));

            _resendTimer?.Dispose();
            _resendTimer = new System.Timers.Timer(1000) { AutoReset = true };
            _resendTimer.Elapsed += (_, _) =>
            {
                _secondsRemaining--;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_secondsRemaining <= 0)
                    {
                        _resendTimer?.Stop();
                        _resendTimer?.Dispose();
                        _resendTimer = null;
                        ResendTimerActive = false;
                    }
                    OnPropertyChanged(nameof(ResendTimerText));
                    OnPropertyChanged(nameof(ResendButtonText));
                });
            };
            _resendTimer.Start();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteConfirmOtp))]
        private async Task ConfirmOtpAsync()
        {
            IsLoading = true;
            ConfirmOtpError = string.Empty;

            Result result = Purpose == OtpPurpose.PasswordRecovery
                ? await _authService.VerifyPasswordResetAsync(Email, ConfirmOTP.Trim())
                : await _authService.VerifyEmailConfirmationAsync(Email, ConfirmOTP.Trim());

            if (result.Success)
                ConfirmOtpSuccessful?.Invoke(this, EventArgs.Empty);
            else
                ConfirmOtpError = result.Message ?? "OTP confirmation failed.";

            IsLoading = false;
        }

        [RelayCommand(CanExecute = nameof(CanResendOtp))]
        private async Task ResendOtpAsync()
        {
            IsResending = true;
            ResendOtpError = string.Empty;

            var result = Purpose == OtpPurpose.PasswordRecovery
                ? await _authService.SendResetPasswordEmailAsync(Email)
                : await _authService.SendOTPConfirmationAsync(Email);

            if (!result.Success)
                ResendOtpError = result.Message ?? "Failed to resend code.";

            IsResending = false;
            StartResendTimer();
        }

        private bool CanExecuteConfirmOtp() =>
            !IsLoading
            && AllFieldsFilled;

        private bool CanResendOtp() => !IsResending && !ResendTimerActive;

        public event EventHandler? ConfirmOtpSuccessful;

        public ConfirmOtpViewModel(IAuthService authService)
        {
            _authService = authService;
            StartResendTimer();
        }
    }
}
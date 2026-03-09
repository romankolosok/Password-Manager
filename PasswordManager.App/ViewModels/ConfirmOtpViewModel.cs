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

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _confirmOtpError = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmOtpCommand))]
        private bool _isLoading;

        public string ConfirmOTP => $"{Digit1}{Digit2}{Digit3}{Digit4}{Digit5}{Digit6}{Digit7}{Digit8}";

        public string DisplayError => AllFieldsFilled ? "" : ConfirmOtpError;

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

        [RelayCommand(CanExecute = nameof(CanExecuteConfirmOtp))]
        private async Task ConfirmOtpAsync()
        {
            IsLoading = true;
            ConfirmOtpError = string.Empty;

            Result result = await _authService.VerifyEmailConfirmationAsync(Email, ConfirmOTP.Trim());

            if (result.Success)
                ConfirmOtpSuccessful?.Invoke(this, EventArgs.Empty);
            else
                ConfirmOtpError = result.Message ?? "OTP confirmation failed.";

            IsLoading = false;
        }

        private bool CanExecuteConfirmOtp() =>
            !IsLoading
            && AllFieldsFilled;

        public event EventHandler? ConfirmOtpSuccessful;

        public ConfirmOtpViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}
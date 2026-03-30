using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;

namespace PasswordManager.App.ViewModels
{
    public partial class SetNewPasswordViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly PasswordValidator _passwordValidator = new();

        public string Email { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _recoveryKey = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _newMasterPassword = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _confirmMasterPassword = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _resetError = string.Empty;

        [ObservableProperty]
        private bool _isPasswordVisible;

        [ObservableProperty]
        private bool _isConfirmPasswordVisible;

        private bool AllFieldsFilled =>
            !string.IsNullOrWhiteSpace(RecoveryKey)
            && !string.IsNullOrWhiteSpace(NewMasterPassword)
            && !string.IsNullOrWhiteSpace(ConfirmMasterPassword);

        public string DisplayError => AllFieldsFilled ? (GetValidationError() ?? ResetError) : ResetError;
        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

        private string? GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey))
                return "Recovery key is required.";

            var passwordResult = _passwordValidator.Validate(new PasswordInput { Password = NewMasterPassword.Trim() });
            if (!passwordResult.IsValid)
                return string.Join(" ", passwordResult.Errors.Select(e => e.ErrorMessage));

            if (!string.Equals(NewMasterPassword, ConfirmMasterPassword, StringComparison.Ordinal))
                return "Passwords do not match.";

            return null;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteResetPassword))]
        private async Task ResetPasswordAsync()
        {
            IsLoading = true;
            ResetError = string.Empty;

            Result result = await _authService.RecoverVaultAsync(RecoveryKey.Trim(), NewMasterPassword.Trim());
            if (result.Success)
                ResetPasswordSuccessful?.Invoke(this, EventArgs.Empty);
            else
                ResetError = result.Message ?? "Password reset failed.";

            IsLoading = false;
        }

        private bool CanExecuteResetPassword() =>
            !IsLoading
            && AllFieldsFilled
            && NewMasterPassword == ConfirmMasterPassword
            && GetValidationError() == null;

        public event EventHandler? ResetPasswordSuccessful;

        public SetNewPasswordViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}


using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;

namespace PasswordManager.App.ViewModels
{
    public partial class ChangePasswordViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly PasswordValidator _passwordValidator = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _oldMasterPassword = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _newMasterPassword = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _confirmNewMasterPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
        private string _changePasswordError = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isOldPasswordVisible;

        [ObservableProperty]
        private bool _isNewPasswordVisible;

        [ObservableProperty]
        private bool _isConfirmNewPasswordVisible;

        public string DisplayError => AllFieldsFilled ? (GetValidationError() ?? ChangePasswordError) : ChangePasswordError;

        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

        private bool AllFieldsFilled =>
            !string.IsNullOrWhiteSpace(OldMasterPassword)
            && !string.IsNullOrWhiteSpace(NewMasterPassword)
            && !string.IsNullOrWhiteSpace(ConfirmNewMasterPassword);

        [RelayCommand(CanExecute = nameof(CanExecuteChangePassword))]
        private async Task ChangePasswordAsync()
        {
            IsLoading = true;
            ChangePasswordError = string.Empty;

            string? validationError = GetValidationError();
            if (validationError != null)
            {
                ChangePasswordError = validationError;
                IsLoading = false;
                return;
            }

            var result = await _authService.ChangeMasterPasswordAsync(OldMasterPassword, NewMasterPassword);

            if (result.Success)
            {
                ChangePasswordSuccessful?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ChangePasswordError = result.Message ?? "Change Password failed";
            }

            IsLoading = false;
        }

        private bool CanExecuteChangePassword() =>
            !IsLoading
            && AllFieldsFilled
            && NewMasterPassword == ConfirmNewMasterPassword
            && GetValidationError() == null;

        private string? GetValidationError()
        {
            var passwordResult = _passwordValidator.Validate(new PasswordInput { Password = NewMasterPassword });
            if (!passwordResult.IsValid)
                return string.Join(" ", passwordResult.Errors.Select(e => e.ErrorMessage));

            if (NewMasterPassword != ConfirmNewMasterPassword)
                return "Passwords do not match.";

            return null;
        }

        public event EventHandler? ChangePasswordSuccessful;

        public ChangePasswordViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}
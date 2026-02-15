using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;

namespace PasswordManager.App.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly EmailValidator _emailValidator = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _masterPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        [NotifyPropertyChangedFor(nameof(IsNotLoading))]
        private bool _isLoading;

        /// <summary>
        /// Validation error (from validators) or server error. Shown when both fields have input or after submit.
        /// </summary>
        public string DisplayError => BothFieldsFilled ? (GetValidationError() ?? ErrorMessage) : ErrorMessage;

        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

        /// <summary>Convenience for binding "disable when loading" (e.g. Register button).</summary>
        public bool IsNotLoading => !IsLoading;

        private bool BothFieldsFilled =>
            !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(MasterPassword);

        [RelayCommand(CanExecute = nameof(CanExecuteLogin))]
        private async Task LoginAsync()
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            string? validationError = GetValidationError();
            if (validationError != null)
            {
                ErrorMessage = validationError;
                IsLoading = false;
                return;
            }

            Result result = await _authService.LoginAsync(Email.Trim(), MasterPassword);

            if (result.Success)
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
            else
                ErrorMessage = result.Message ?? "Login failed.";

            IsLoading = false;
        }

        private bool CanExecuteLogin() =>
            !IsLoading
            && BothFieldsFilled
            && GetValidationError() == null;

        /// <summary>
        /// Uses Core validators (same as AuthService). Returns null if valid, otherwise first error message.
        /// </summary>
        private string? GetValidationError()
        {
            var emailResult = _emailValidator.Validate(new EmailInput { Email = Email.Trim() });
            if (!emailResult.IsValid)
                return string.Join(" ", emailResult.Errors.Select(e => e.ErrorMessage));

            if (string.IsNullOrWhiteSpace(MasterPassword))
                return "Password is required.";

            return null;
        }

        /// <summary>
        /// Raised when login succeeds. MainWindow/coordinator can subscribe to navigate to VaultList.
        /// </summary>
        public event EventHandler? LoginSuccessful;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}

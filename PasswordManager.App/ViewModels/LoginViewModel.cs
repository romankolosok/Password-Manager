using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Exceptions;
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

        [ObservableProperty]
        private bool _isPasswordVisible;

        public string DisplayError => BothFieldsFilled ? (GetValidationError() ?? ErrorMessage) : ErrorMessage;

        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

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
            else if (result.Message == AuthMessages.EmailNotConfirmed)
                EmailNotConfirmed?.Invoke(this, EventArgs.Empty);
            else
                ErrorMessage = result.Message ?? "Login failed.";

            IsLoading = false;
        }

        private bool CanExecuteLogin() =>
            !IsLoading
            && BothFieldsFilled
            && GetValidationError() == null;

        private string? GetValidationError()
        {
            var emailResult = _emailValidator.Validate(new EmailInput { Email = Email.Trim() });
            if (!emailResult.IsValid)
                return string.Join(" ", emailResult.Errors.Select(e => e.ErrorMessage));

            if (string.IsNullOrWhiteSpace(MasterPassword))
                return "Password is required.";

            return null;
        }

        public event EventHandler? LoginSuccessful;
        
        public event EventHandler? EmailNotConfirmed;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}

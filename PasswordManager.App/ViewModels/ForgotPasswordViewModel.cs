using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.App.ViewModels
{
    public partial class ForgotPasswordViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly EmailValidator _emailValidator = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendRecoveryEmailCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendRecoveryEmailCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendRecoveryEmailCommand))]
        private bool _isLoading;

        public string DisplayError => !string.IsNullOrWhiteSpace(Email) ? (GetValidationError() ?? ErrorMessage) : ErrorMessage;
        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

        private bool AllFieldsFilled => !string.IsNullOrWhiteSpace(Email);

        private string? GetValidationError()
        {
            var emailResult = _emailValidator.Validate(new EmailInput { Email = Email.Trim() });
            if (!emailResult.IsValid)
                return string.Join(" ", emailResult.Errors.Select(e => e.ErrorMessage));

            return null;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteSendRecoveryEmail))]
        private async Task SendRecoveryEmailAsync()
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var validationError = GetValidationError();
            if (validationError != null)
            {
                ErrorMessage = validationError;
                IsLoading = false;
                return;
            }

            Result result = await _authService.SendResetPasswordEmailAsync(Email.Trim());
            if (result.Success)
            {
                RecoveryEmailSent?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = result.Message ?? "Failed to send recovery email.";
            }

            IsLoading = false;
        }

        private bool CanExecuteSendRecoveryEmail() =>
            !IsLoading
            && AllFieldsFilled
            && GetValidationError() == null;

        public event EventHandler? RecoveryEmailSent;

        public ForgotPasswordViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}


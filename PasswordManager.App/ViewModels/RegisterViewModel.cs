using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;

namespace PasswordManager.App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly EmailValidator _emailValidator = new();
        private readonly PasswordValidator _passwordValidator = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _masterPassword = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayError))]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string _registerError = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private bool _isLoading;

        /// <summary>
        /// Validation error (from validators) or server error. Shown when all fields have input or after submit.
        /// </summary>
        public string DisplayError => AllFieldsFilled ? (GetValidationError() ?? RegisterError) : RegisterError;

        public bool DisplayErrorVisible => !string.IsNullOrEmpty(DisplayError);

        private bool AllFieldsFilled =>
            !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(MasterPassword)
            && !string.IsNullOrWhiteSpace(ConfirmPassword);

        [RelayCommand(CanExecute = nameof(CanExecuteRegister))]
        private async Task RegisterAsync()
        {
            IsLoading = true;
            RegisterError = string.Empty;

            string? validationError = GetValidationError();
            if (validationError != null)
            {
                RegisterError = validationError;
                IsLoading = false;
                return;
            }

            Result result = await _authService.RegisterAsync(Email.Trim(), MasterPassword);

            if (result.Success)
                RegisterSuccessful?.Invoke(this, EventArgs.Empty);
            else
                RegisterError = result.Message ?? "Registration failed.";

            IsLoading = false;
        }

        private bool CanExecuteRegister() =>
            !IsLoading
            && AllFieldsFilled
            && MasterPassword == ConfirmPassword
            && GetValidationError() == null;

        /// <summary>
        /// Uses Core validators (same as AuthService). Returns null if valid, otherwise first error message.
        /// </summary>
        private string? GetValidationError()
        {
            var emailResult = _emailValidator.Validate(new EmailInput { Email = Email.Trim() });
            if (!emailResult.IsValid)
                return string.Join(" ", emailResult.Errors.Select(e => e.ErrorMessage));

            var passwordResult = _passwordValidator.Validate(new PasswordInput { Password = MasterPassword });
            if (!passwordResult.IsValid)
                return string.Join(" ", passwordResult.Errors.Select(e => e.ErrorMessage));

            if (MasterPassword != ConfirmPassword)
                return "Passwords do not match.";

            return null;
        }

        /// <summary>
        /// Raised when registration succeeds. Coordinator closes register and shows login.
        /// </summary>
        public event EventHandler? RegisterSuccessful;

        public RegisterViewModel(IAuthService authService)
        {
            _authService = authService;
        }
    }
}

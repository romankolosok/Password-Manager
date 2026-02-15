using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RegisterErrorVisible))]
        private string _registerError = string.Empty;

        public bool RegisterErrorVisible => !string.IsNullOrEmpty(RegisterError);

        public RegisterViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Attempts registration. Returns true if successful; otherwise sets RegisterError and returns false.
        /// Caller (view) should pass password from PasswordBox.
        /// </summary>
        public async Task<bool> TryRegisterAsync(string email, string masterPassword, string confirmPassword)
        {
            RegisterError = string.Empty;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(masterPassword))
            {
                RegisterError = "Email and password are required.";
                return false;
            }
            if (masterPassword != confirmPassword)
            {
                RegisterError = "Passwords do not match.";
                return false;
            }

            Result result = await _authService.RegisterAsync(email.Trim(), masterPassword);
            if (result.Success)
                return true;

            RegisterError = result.Message ?? "Registration failed.";
            return false;
        }
    }
}

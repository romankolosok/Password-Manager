using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LoginErrorVisible))]
        private string _loginError = string.Empty;

        public bool LoginErrorVisible => !string.IsNullOrEmpty(LoginError);

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Attempts login. Returns true if successful; otherwise sets LoginError and returns false.
        /// Caller (view) should pass password from PasswordBox and then navigate on success.
        /// </summary>
        public async Task<bool> TryLoginAsync(string email, string masterPassword)
        {
            LoginError = string.Empty;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(masterPassword))
            {
                LoginError = "Email and password are required.";
                return false;
            }

            Result result = await _authService.LoginAsync(email.Trim(), masterPassword);
            if (result.Success)
                return true;

            LoginError = result.Message ?? "Login failed.";
            return false;
        }
    }
}

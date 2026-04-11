using PasswordManager.Core.Models.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface IAuthClient
    {
        AuthSession? CurrentSession { get; }

        void AddStateChangedListener(Action<AuthStateKind> listener);

        Task<AuthSession?> SignUpAsync(string email, string password,
            Dictionary<string, object>? metadata = null);

        Task<AuthSession?> SignInAsync(string email, string password);

        Task SignOutAsync();

        Task<AuthSession?> VerifyOTPAsync(string email, string token, OtpType type);

        Task ResetPasswordForEmailAsync(string email);

        Task SignInWithOtpAsync(string email);

        Task<AuthUser?> UpdateUserAsync(string? newPassword = null);
    }
}

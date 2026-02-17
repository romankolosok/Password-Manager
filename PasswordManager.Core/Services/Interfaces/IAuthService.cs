using PasswordManager.Core.Models;
using System;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>Id of the currently logged-in user, or null when locked. Used by VaultService to scope queries.</summary>
        Guid? CurrentUserId { get; }

        /// <summary>Email of the currently logged-in user, or null when locked. For display (e.g. "Account: email").</summary>
        string? CurrentUserEmail { get; }

        public Task<Result> RegisterAsync(string email, string masterPassword);

        public Task<Result> LoginAsync(string email, string masterPassword);

        public Task LockAsync();

        public bool IsLocked();

        Task<Result> ChangeMasterPasswordAsync(string currentPassword, string newPassword);
    }
}

#nullable enable
using System;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface ISessionService : IDisposable
    {
        TimeSpan InactivityTimeout { get; set; }

        event EventHandler? VaultLocked;

        Guid? CurrentUserId { get; }
        string? CurrentUserEmail { get; }

        public void SetDerivedKey(byte[] key);
        /// <param name="accessToken">Supabase session access token (JWT) for PostgREST; required for RLS.</param>
        public void SetUser(Guid userId, string email, string? accessToken = null);
        string? GetAccessToken();

        public byte[] GetDerivedKey();

        public void ClearSession();

        public bool IsActive();

        public void ResetInactivityTimer();
    }
}

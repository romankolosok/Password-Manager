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
        public void SetUser(Guid userId, string email);

        public byte[] GetDerivedKey();

        public void ClearSession();

        public bool IsActive();

        public void ResetInactivityTimer();
    }
}

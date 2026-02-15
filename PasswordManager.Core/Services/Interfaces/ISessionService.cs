#nullable enable
using System;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface ISessionService : IDisposable
    {
        TimeSpan InactivityTimeout { get; set; }

        event EventHandler? VaultLocked;

        public void SetDerivedKey(byte[] key);

        public byte[] GetDerivedKey();

        public void ClearSession();

        public bool IsActive();

        public void ResetInactivityTimer();
    }
}

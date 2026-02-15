using PasswordManager.Core.Services.Interfaces;
using System;

namespace PasswordManager.Core.Services.Implementations
{
    public class SessionService : ISessionService, IDisposable
    {
        private byte[]? _derivedKey;
        private Guid? _currentUserId;
        private string? _currentUserEmail;
        private string? _accessToken;
        private readonly System.Timers.Timer _timer;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private TimeSpan _inactivityTimeout = TimeSpan.FromMinutes(5);
        public TimeSpan InactivityTimeout
        {
            get
            {
                lock (_lock)
                {
                    return _inactivityTimeout;
                }
            }
            set
            {
                lock (_lock)
                {
                    _inactivityTimeout = value;
                    if (_timer.Enabled)
                    {
                        ResetInactivityTimerInternal();
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionService));
            }
        }

        public event EventHandler? VaultLocked;

        public SessionService()
        {
            _timer = new System.Timers.Timer();
            _timer.AutoReset = false;
            _timer.Elapsed += OnTimerElapsed;
        }

        private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            ClearSession();
        }

        public Guid? CurrentUserId
        {
            get
            {
                lock (_lock)
                {
                    return _currentUserId;
                }
            }
        }

        public string? CurrentUserEmail
        {
            get
            {
                lock (_lock)
                {
                    return _currentUserEmail;
                }
            }
        }

        public void SetDerivedKey(byte[] key)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_derivedKey != null)
                {
                    ClearKeyFromMemory(_derivedKey);
                }

                _derivedKey = key;
                ResetInactivityTimerInternal();
            }
        }

        public void SetUser(Guid userId, string email, string? accessToken = null)
        {
            lock (_lock)
            {
                _currentUserId = userId;
                _currentUserEmail = email;
                _accessToken = accessToken;
            }
        }

        public string? GetAccessToken()
        {
            lock (_lock)
            {
                return _accessToken;
            }
        }

        public byte[] GetDerivedKey()
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_derivedKey == null)
                {
                    throw new InvalidOperationException("No active session. Derived key is not set.");
                }

                return _derivedKey;
            }
        }

        public void ClearSession()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _timer.Stop();
                _currentUserId = null;
                _currentUserEmail = null;
                _accessToken = null;

                if (_derivedKey != null)
                {
                    ClearKeyFromMemory(_derivedKey);
                    _derivedKey = null;
                }
            }

            // Invoke event outside the lock to prevent potential deadlocks
            VaultLocked?.Invoke(this, EventArgs.Empty);
        }

        public bool IsActive()
        {
            lock (_lock)
            {
                return _derivedKey != null && !_disposed;
            }
        }

        public void ResetInactivityTimer()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                ResetInactivityTimerInternal();
            }
        }

        private void ResetInactivityTimerInternal()
        {
            // Must be called within lock
            _timer.Stop();
            _timer.Interval = _inactivityTimeout.TotalMilliseconds;
            _timer.Start();
        }

        private void ClearKeyFromMemory(byte[] key)
        {
            Array.Clear(key, 0, key.Length);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_lock)
                {
                    _timer.Stop();
                    _timer.Elapsed -= OnTimerElapsed;
                    _timer.Dispose();

                    if (_derivedKey != null)
                    {
                        ClearKeyFromMemory(_derivedKey);
                        _derivedKey = null;
                    }

                    _disposed = true;
                }
            }
        }
    }
}

using PasswordManager.Core.Services.Interfaces;
using System;

namespace PasswordManager.Core.Services.Implementations
{
    public class SessionService : ISessionService, IDisposable
    {
        private static readonly TimeSpan DefaultInactivityTimeout = TimeSpan.FromMinutes(5);

        private byte[]? _derivedKey;
        private Guid? _currentUserId;
        private string? _currentUserEmail;
        private string? _accessToken;
        private readonly System.Timers.Timer _timer;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private TimeSpan _inactivityTimeout = DefaultInactivityTimeout;

        public TimeSpan InactivityTimeout
        {
            get
            {
                lock (_lock)
                {
                    ThrowIfDisposed();
                    return _inactivityTimeout;
                }
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Inactivity timeout must be positive.");
                }

                lock (_lock)
                {
                    ThrowIfDisposed();
                    _inactivityTimeout = value;
                    ResetInactivityTimerInternal();
                }
            }
        }

        public event EventHandler? VaultLocked;

        public SessionService()
        {
            _timer = new System.Timers.Timer
            {
                AutoReset = false,
                Interval = _inactivityTimeout.TotalMilliseconds
            };
            _timer.Elapsed += OnTimerElapsed;
        }

        public Guid? CurrentUserId
        {
            get
            {
                lock (_lock)
                {
                    ThrowIfDisposed();
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
                    ThrowIfDisposed();
                    return _currentUserEmail;
                }
            }
        }

        public void SetDerivedKey(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

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
            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            lock (_lock)
            {
                ThrowIfDisposed();
                _currentUserId = userId;
                _currentUserEmail = email;
                _accessToken = accessToken;
            }
        }

        public string? GetAccessToken()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
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

                return (byte[])_derivedKey.Clone();
            }
        }

        public void ClearSession()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

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

            VaultLocked?.Invoke(this, EventArgs.Empty);
        }

        public bool IsActive()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                return _derivedKey != null;
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

        private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            ClearSession();
        }

        private void ResetInactivityTimerInternal()
        {
            _timer.Stop();
            _timer.Interval = _inactivityTimeout.TotalMilliseconds;
            _timer.Start();
        }

        private void ClearKeyFromMemory(byte[] key)
        {
            Array.Clear(key, 0, key.Length);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionService));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

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

using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.Tests.Fakes
{
    public class FakeAuthClient : IAuthClient
    {
        private readonly Dictionary<string, FakeUser> _users = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _otpCodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Action<AuthStateKind>> _listeners = new();
        private AuthSession? _currentSession;
        private AuthClientException? _pendingException;

        public AuthSession? CurrentSession => _currentSession;

        public void AddStateChangedListener(Action<AuthStateKind> listener)
        {
            _listeners.Add(listener);
        }

        public void SetOtpCode(string email, string code)
        {
            _otpCodes[email] = code;
        }

        public void ShouldThrow(AuthClientException exception)
        {
            _pendingException = exception;
        }

        public void ClearPendingException()
        {
            _pendingException = null;
        }

        public Task<AuthSession?> SignUpAsync(string email, string password,
            Dictionary<string, object>? metadata = null)
        {
            ThrowIfPending();

            if (_users.ContainsKey(email))
                throw new AuthClientException("User already registered", 422);

            var userId = Guid.NewGuid().ToString();
            _users[email] = new FakeUser
            {
                Id = userId,
                Email = email,
                Password = password,
                Metadata = metadata
            };

            var session = new AuthSession
            {
                AccessToken = $"fake-token-{userId}",
                User = new AuthUser
                {
                    Id = userId,
                    Email = email,
                    UserMetadata = metadata
                }
            };

            NotifyListeners(AuthStateKind.SignedIn);
            return Task.FromResult<AuthSession?>(session);
        }

        public Task<AuthSession?> SignInAsync(string email, string password)
        {
            ThrowIfPending();

            if (!_users.TryGetValue(email, out var user) || user.Password != password)
                throw new AuthClientException("Invalid login credentials", 400);

            _currentSession = new AuthSession
            {
                AccessToken = $"fake-token-{user.Id}",
                User = new AuthUser
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserMetadata = user.Metadata
                }
            };

            NotifyListeners(AuthStateKind.SignedIn);
            return Task.FromResult<AuthSession?>(_currentSession);
        }

        public Task SignOutAsync()
        {
            ThrowIfPending();
            _currentSession = null;
            NotifyListeners(AuthStateKind.SignedOut);
            return Task.CompletedTask;
        }

        public Task<AuthSession?> VerifyOTPAsync(string email, string token, OtpType type)
        {
            ThrowIfPending();

            if (!_otpCodes.TryGetValue(email, out var expectedCode) || expectedCode != token)
                throw new AuthClientException("Token has expired or is invalid", 403);

            if (!_users.TryGetValue(email, out var user))
                throw new AuthClientException("User not found", 400);

            _currentSession = new AuthSession
            {
                AccessToken = $"fake-token-{user.Id}",
                User = new AuthUser
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserMetadata = user.Metadata
                }
            };

            _otpCodes.Remove(email);
            return Task.FromResult<AuthSession?>(_currentSession);
        }

        public Task ResetPasswordForEmailAsync(string email)
        {
            ThrowIfPending();
            return Task.CompletedTask;
        }

        public Task SignInWithOtpAsync(string email)
        {
            ThrowIfPending();
            return Task.CompletedTask;
        }

        public Task<AuthUser?> UpdateUserAsync(string? newPassword = null)
        {
            ThrowIfPending();

            if (_currentSession?.User == null)
                throw new AuthClientException("Not authenticated", 401);

            if (newPassword != null && _users.TryGetValue(_currentSession.User.Email!, out var user))
            {
                user.Password = newPassword;
            }

            NotifyListeners(AuthStateKind.UserUpdated);
            return Task.FromResult<AuthUser?>(_currentSession.User);
        }

        private void ThrowIfPending()
        {
            if (_pendingException != null)
            {
                var ex = _pendingException;
                _pendingException = null;
                throw ex;
            }
        }

        private void NotifyListeners(AuthStateKind state)
        {
            foreach (var listener in _listeners)
                listener(state);
        }

        private class FakeUser
        {
            public string Id { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public Dictionary<string, object>? Metadata { get; set; }
        }
    }
}

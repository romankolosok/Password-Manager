using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Core.Services.Interfaces;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class SupabaseAuthClient : IAuthClient
    {
        private readonly Supabase.Client _supabase;

        public SupabaseAuthClient(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public AuthSession? CurrentSession
        {
            get
            {
                var session = _supabase.Auth.CurrentSession;
                return session == null ? null : MapSession(session);
            }
        }

        public void AddStateChangedListener(Action<AuthStateKind> listener)
        {
            _supabase.Auth.AddStateChangedListener((_, state) =>
            {
                listener(MapAuthState(state));
            });
        }

        public async Task<AuthSession?> SignUpAsync(string email, string password,
            Dictionary<string, object>? metadata = null)
        {
            try
            {
                var options = metadata != null ? new SignUpOptions { Data = metadata } : null;
                var session = await _supabase.Auth.SignUp(email, password, options);
                return session == null ? null : MapSession(session);
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        public async Task<AuthSession?> SignInAsync(string email, string password)
        {
            try
            {
                var session = await _supabase.Auth.SignIn(email, password);
                return session == null ? null : MapSession(session);
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        public async Task SignOutAsync()
        {
            try
            {
                await _supabase.Auth.SignOut();
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        public async Task<AuthSession?> VerifyOTPAsync(string email, string token, OtpType type)
        {
            try
            {
                var gotrueType = type switch
                {
                    OtpType.Signup => Constants.EmailOtpType.Signup,
                    OtpType.Recovery => Constants.EmailOtpType.Recovery,
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };

                var session = await _supabase.Auth.VerifyOTP(email, token, gotrueType);
                return session == null ? null : MapSession(session);
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        public async Task ResetPasswordForEmailAsync(string email)
        {
            try
            {
                await _supabase.Auth.ResetPasswordForEmail(email);
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        public async Task SignInWithOtpAsync(string email)
        {
            try
            {
                await _supabase.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email));
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        public async Task<AuthUser?> UpdateUserAsync(string? newPassword = null)
        {
            try
            {
                var attrs = new UserAttributes();
                if (newPassword != null)
                    attrs.Password = newPassword;

                var user = await _supabase.Auth.Update(attrs);
                return user == null ? null : MapUser(user);
            }
            catch (GotrueException ex)
            {
                throw new AuthClientException(ex.Message, ex.StatusCode, ex);
            }
        }

        private static AuthSession MapSession(Session session)
        {
            return new AuthSession
            {
                AccessToken = session.AccessToken,
                User = session.User == null ? null : MapUser(session.User)
            };
        }

        private static AuthUser MapUser(User user)
        {
            return new AuthUser
            {
                Id = user.Id ?? string.Empty,
                Email = user.Email,
                UserMetadata = user.UserMetadata?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value)
            };
        }

        private static AuthStateKind MapAuthState(Constants.AuthState state)
        {
            return state switch
            {
                Constants.AuthState.SignedIn => AuthStateKind.SignedIn,
                Constants.AuthState.SignedOut => AuthStateKind.SignedOut,
                Constants.AuthState.TokenRefreshed => AuthStateKind.TokenRefreshed,
                Constants.AuthState.UserUpdated => AuthStateKind.UserUpdated,
                _ => AuthStateKind.SignedOut
            };
        }
    }
}

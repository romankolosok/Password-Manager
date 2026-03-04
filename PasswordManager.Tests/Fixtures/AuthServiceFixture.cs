using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures
{
    public class AuthServiceFixture
    {
        public Supabase.Client SupabaseClient { get; }
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IUserProfileService> UserProfileService { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<SupabaseExceptionMapper> ExceptionMapper { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        public AuthServiceFixture()
        {
            // For unit tests we never hit the network; a dummy client is sufficient.
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false
            };
            SupabaseClient = new Supabase.Client("http://localhost", "test-anon-key", options);
        }

        public AuthService CreateService() =>
            new(SupabaseClient,
                CryptoService.Object,
                UserProfileService.Object,
                SessionService.Object,
                ExceptionMapper.Object,
                Logger.Object);

        public void Reset()
        {
            CryptoService.Reset();
            UserProfileService.Reset();
            SessionService.Reset();
            ExceptionMapper.Reset();
            Logger.Reset();
        }
    }
}


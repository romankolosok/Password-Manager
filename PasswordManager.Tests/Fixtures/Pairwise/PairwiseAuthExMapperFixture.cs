using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseAuthExMapperFixture
    {
        public Mock<IAuthClient> AuthClient { get; } = new();
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IUserProfileService> UserProfileService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        private readonly IAuthExceptionMapper _exceptionMapper = new SupabaseExceptionMapper();

        public AuthService CreateService() =>
            new(
                AuthClient.Object,
                CryptoService.Object,
                UserProfileService.Object,
                VaultRepository.Object,
                SessionService.Object,
                _exceptionMapper,
                Logger.Object);

        public void Reset()
        {
            AuthClient.Reset();
            CryptoService.Reset();
            UserProfileService.Reset();
            VaultRepository.Reset();
            SessionService.Reset();
            Logger.Reset();
        }
    }
}

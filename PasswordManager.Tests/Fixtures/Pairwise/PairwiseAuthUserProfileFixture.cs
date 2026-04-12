using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseAuthUserProfileFixture
    {
        public Mock<IAuthClient> AuthClient { get; } = new();
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<IAuthExceptionMapper> ExceptionMapper { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        public IUserProfileService UserProfileService { get; private set; }

        public PairwiseAuthUserProfileFixture()
        {
            UserProfileService = new UserProfileService(VaultRepository.Object);
        }

        public AuthService CreateService() =>
            new(
                AuthClient.Object,
                CryptoService.Object,
                UserProfileService,
                VaultRepository.Object,
                SessionService.Object,
                ExceptionMapper.Object,
                Logger.Object);

        public void Reset()
        {
            AuthClient.Reset();
            CryptoService.Reset();
            VaultRepository.Reset();
            SessionService.Reset();
            ExceptionMapper.Reset();
            Logger.Reset();

            UserProfileService = new UserProfileService(VaultRepository.Object);
        }
    }
}

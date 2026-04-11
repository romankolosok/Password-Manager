using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseAuthCryptoFixture
    {
        public Mock<IAuthClient> AuthClient { get; } = new();
        public Mock<IUserProfileService> UserProfileService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<IAuthExceptionMapper> ExceptionMapper { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        private readonly CryptoService _cryptoService = new();

        public CryptoService Crypto => _cryptoService;

        public AuthService CreateService() =>
            new(
                AuthClient.Object,
                _cryptoService,
                UserProfileService.Object,
                VaultRepository.Object,
                SessionService.Object,
                ExceptionMapper.Object,
                Logger.Object);

        public void Reset()
        {
            AuthClient.Reset();
            UserProfileService.Reset();
            VaultRepository.Reset();
            SessionService.Reset();
            ExceptionMapper.Reset();
            Logger.Reset();
        }
    }
}

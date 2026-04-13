using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseAuthSessionFixture
    {
        public Mock<IAuthClient> AuthClient { get; } = new();
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IUserProfileService> UserProfileService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<IAuthExceptionMapper> ExceptionMapper { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        private ISessionService _sessionService;

        public ISessionService SessionService => _sessionService;

        public PairwiseAuthSessionFixture()
        {
            _sessionService = new SessionService();
        }

        public AuthService CreateService() =>
            new(
                AuthClient.Object,
                CryptoService.Object,
                UserProfileService.Object,
                VaultRepository.Object,
                _sessionService,
                ExceptionMapper.Object,
                Logger.Object);

        public void Reset()
        {
            AuthClient.Reset();
            CryptoService.Reset();
            UserProfileService.Reset();
            VaultRepository.Reset();
            ExceptionMapper.Reset();
            Logger.Reset();

            _sessionService.Dispose();
            _sessionService = new SessionService();
        }
    }
}

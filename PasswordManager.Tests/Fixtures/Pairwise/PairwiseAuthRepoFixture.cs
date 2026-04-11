using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Fakes;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseAuthRepoFixture
    {
        public InMemoryVaultRepository Repository { get; private set; } = new();
        public Mock<IAuthClient> AuthClient { get; } = new();
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IUserProfileService> UserProfileService { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<IAuthExceptionMapper> ExceptionMapper { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        public AuthService CreateService() => new(
            AuthClient.Object,
            CryptoService.Object,
            UserProfileService.Object,
            Repository,
            SessionService.Object,
            ExceptionMapper.Object,
            Logger.Object);

        public void Reset()
        {
            Repository = new InMemoryVaultRepository();
            AuthClient.Reset();
            CryptoService.Reset();
            UserProfileService.Reset();
            SessionService.Reset();
            ExceptionMapper.Reset();
            Logger.Reset();
        }
    }
}

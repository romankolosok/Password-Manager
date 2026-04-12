using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Fakes;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseAuthFakeAuthFixture
    {
        public FakeAuthClient FakeAuthClient { get; private set; } = new();

        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IUserProfileService> UserProfileService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<IAuthExceptionMapper> ExceptionMapper { get; } = new();
        public Mock<ILogger<AuthService>> Logger { get; } = new();

        public AuthService CreateService() =>
            new(
                FakeAuthClient,
                CryptoService.Object,
                UserProfileService.Object,
                VaultRepository.Object,
                SessionService.Object,
                ExceptionMapper.Object,
                Logger.Object);

        public void Reset()
        {
            CryptoService.Reset();
            UserProfileService.Reset();
            VaultRepository.Reset();
            SessionService.Reset();
            ExceptionMapper.Reset();
            Logger.Reset();
            FakeAuthClient = new FakeAuthClient();
        }
    }
}

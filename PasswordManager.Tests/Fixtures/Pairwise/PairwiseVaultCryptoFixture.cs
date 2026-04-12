using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseVaultCryptoFixture
    {
        private readonly CryptoService _crypto = new();

        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ILogger<VaultService>> Logger { get; } = new();

        public IVaultService CreateService() =>
            new VaultService(_crypto, SessionService.Object, VaultRepository.Object, Logger.Object);

        public void SetupActiveSession(Guid? userId, byte[]? derivedKey)
        {
            SessionService.Reset();

            if (userId == null || derivedKey == null)
            {
                SessionService.Setup(s => s.IsActive()).Returns(false);
                SessionService.Setup(s => s.CurrentUserId).Returns((Guid?)null);
                SessionService.Setup(s => s.CurrentUserEmail).Returns((string?)null);
                SessionService
                    .Setup(s => s.GetDerivedKey())
                    .Throws(new InvalidOperationException("No active session. Derived key is not set."));
            }
            else
            {
                byte[] keySnapshot = (byte[])derivedKey.Clone();
                SessionService.Setup(s => s.IsActive()).Returns(true);
                SessionService.Setup(s => s.CurrentUserId).Returns(userId);
                SessionService.Setup(s => s.CurrentUserEmail).Returns("test@example.com");
                SessionService
                    .Setup(s => s.GetDerivedKey())
                    .Returns(() => (byte[])keySnapshot.Clone());
            }
        }

        public void Reset()
        {
            SessionService.Reset();
            VaultRepository.Reset();
            Logger.Reset();
        }
    }
}

using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Fakes;
using System.Text;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseVaultRepoFixture
    {
        public IVaultRepository Repository { get; private set; } = new InMemoryVaultRepository();
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<ILogger<VaultService>> Logger { get; } = new();

        public VaultService CreateService() => new(
            CryptoService.Object,
            SessionService.Object,
            Repository,
            Logger.Object);

        public void SetupActiveSession(Guid userId)
        {
            SessionService.Setup(s => s.IsActive()).Returns(true);
            SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            SessionService.Setup(s => s.CurrentUserEmail).Returns("test@example.com");
            SessionService.Setup(s => s.GetDerivedKey()).Returns(new byte[32]);
        }
        public void SetupPassthroughCrypto()
        {
            CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns((string plaintext, byte[] _) =>
                {
                    var blob = new EncryptedBlob
                    {
                        Nonce = new byte[12],
                        Ciphertext = Encoding.UTF8.GetBytes(plaintext),
                        Tag = new byte[16]
                    };
                    return Result<EncryptedBlob>.Ok(blob);
                });

            CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns((EncryptedBlob blob, byte[] _) =>
                {
                    var plaintext = Encoding.UTF8.GetString(blob.Ciphertext);
                    return Result<string>.Ok(plaintext);
                });
        }

        public void Reset()
        {
            Repository = new InMemoryVaultRepository();
            CryptoService.Reset();
            SessionService.Reset();
            Logger.Reset();
        }
    }
}

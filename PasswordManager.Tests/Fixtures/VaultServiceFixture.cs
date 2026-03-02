using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures
{
    public class VaultServiceFixture
    {
        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<ISessionService> SessionService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ILogger<VaultService>> Logger { get; } = new();

        public VaultService CreateService() =>
            new(CryptoService.Object, SessionService.Object, VaultRepository.Object, Logger.Object);

        // Configures the session mock to report an active session with a known user and derived key.
        public void SetupActiveSession(Guid? userId = null, byte[]? derivedKey = null)
        {
            userId ??= Guid.NewGuid();
            derivedKey ??= new byte[32];

            SessionService.Setup(s => s.IsActive()).Returns(true);
            SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            SessionService.Setup(s => s.GetDerivedKey()).Returns(derivedKey);
        }

        // Configures the session mock to report a locked (inactive) session.
        public void SetupInactiveSession()
        {
            SessionService.Setup(s => s.IsActive()).Returns(false);
        }

        /// <summary>
        /// Builds a minimal valid VaultEntryEntity whose EncryptedData round-trips through the crypto mock.
        /// The crypto mock is configured to decrypt it back to the provided JSON.
        /// </summary>
        public VaultEntryEntity BuildEncryptedEntity(Guid userId, string payloadJson)
        {
            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            string base64 = blob.ToBase64String();

            CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(payloadJson));

            return new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = base64,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// Resets all mock setups so a test can start from a clean state.
        public void Reset()
        {
            CryptoService.Reset();
            SessionService.Reset();
            VaultRepository.Reset();
            Logger.Reset();
        }
    }
}
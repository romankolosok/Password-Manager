using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Tests.Fixtures.Pairwise;
using Xunit;

namespace PasswordManager.Tests.Pairwise
{
    public class AuthServiceUserProfileTests : IClassFixture<PairwiseAuthUserProfileFixture>
    {
        private const string Email = "user@example.com";
        private const string Password = "ValidPassword1!";
        private const string NewPassword = "NewValidPassword1!";

        private readonly PairwiseAuthUserProfileFixture _fixture;

        public AuthServiceUserProfileTests(PairwiseAuthUserProfileFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task LoginAsyncRetrievesProfileViaUserProfileService()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            var salt = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(salt);
            var dekBase64 = Convert.ToBase64String(new byte[32]);

            var encryptedBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[4],
                Tag = new byte[16]
            };

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = encryptedBlob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, Password))
                .ReturnsAsync(new AuthSession
                {
                    AccessToken = "t",
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(Password, salt))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dekBase64));

            _fixture.VaultRepository
                .Setup(r => r.GetUserProfileAsync(userId))
                .ReturnsAsync(profile);

            await service.LoginAsync(Email, Password);

            _fixture.VaultRepository.Verify(r => r.GetUserProfileAsync(userId), Times.Once);
        }

        [Fact]
        public async Task ChangeMasterPasswordUpdatesProfileViaUserProfileService()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            var salt = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(salt);
            var dekBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dekBytes);

            var encryptedBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[4],
                Tag = new byte[16]
            };

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = encryptedBlob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.VaultRepository
                .Setup(r => r.GetUserProfileAsync(userId))
                .ReturnsAsync(profile);

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(Password, salt))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(Convert.ToBase64String(dekBytes)));

            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService
                .Setup(c => c.DeriveKey(NewPassword, It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            var newEncrypted = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[8],
                Tag = new byte[16]
            };
            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(newEncrypted));

            _fixture.AuthClient
                .Setup(c => c.UpdateUserAsync(NewPassword))
                .ReturnsAsync(new AuthUser { Id = userId.ToString() });

            _fixture.VaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChangeMasterPasswordAsync(Password, NewPassword);

            Assert.True(result.Success);
            _fixture.VaultRepository.Verify(
                r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()),
                Times.Once);
        }

        [Fact]
        public async Task WhenRepositoryThrowsRepositoryExceptionAuthServiceReturnsFail()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, Password))
                .ReturnsAsync(new AuthSession
                {
                    AccessToken = "t",
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            _fixture.VaultRepository
                .Setup(r => r.GetUserProfileAsync(userId))
                .ThrowsAsync(new RepositoryException("db failure"));

            _fixture.AuthClient
                .Setup(c => c.SignOutAsync())
                .Returns(Task.CompletedTask);

            var result = await service.LoginAsync(Email, Password);

            Assert.False(result.Success);
            Assert.Equal("User profile not found.", result.Message);
        }
    }
}

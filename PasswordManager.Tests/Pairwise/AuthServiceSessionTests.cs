using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Tests.Fixtures.Pairwise;
using Xunit;

namespace PasswordManager.Tests.Pairwise
{
    public class AuthServiceSessionTests : IClassFixture<PairwiseAuthSessionFixture>
    {
        private const string Email = "user@example.com";
        private const string Password = "ValidPassword1!";

        private readonly PairwiseAuthSessionFixture _fixture;

        public AuthServiceSessionTests(PairwiseAuthSessionFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task LoginAsyncSetsUserIdEmailAndAccessToken()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            var accessToken = "access-token";
            var salt = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(salt);
            var dek = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dek);
            var dekBase64 = Convert.ToBase64String(dek);

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
                    AccessToken = accessToken,
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(Password, salt))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dekBase64));

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await service.LoginAsync(Email, Password);

            Assert.True(result.Success);
            Assert.Equal(userId, _fixture.SessionService.CurrentUserId);
            Assert.Equal(Email, _fixture.SessionService.CurrentUserEmail);
            Assert.Equal(accessToken, _fixture.SessionService.GetAccessToken());
        }

        [Fact]
        public async Task LoginAsyncSetsDerivedKey()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            var salt = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(salt);
            var dek = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dek);
            var dekBase64 = Convert.ToBase64String(dek);

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

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            await service.LoginAsync(Email, Password);

            var derived = _fixture.SessionService.GetDerivedKey();
            Assert.Equal(dek, derived);
        }

        [Fact]
        public async Task LockAsyncClearsSessionAndFiresVaultLocked()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            _fixture.SessionService.SetUser(userId, Email, "tok");
            var dek = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dek);
            _fixture.SessionService.SetDerivedKey(dek);

            var vaultLockedFired = false;
            _fixture.SessionService.VaultLocked += (_, _) => vaultLockedFired = true;

            _fixture.AuthClient
                .Setup(c => c.SignOutAsync())
                .Returns(Task.CompletedTask);

            await service.LockAsync();

            Assert.False(_fixture.SessionService.IsActive());
            Assert.Null(_fixture.SessionService.CurrentUserId);
            Assert.True(vaultLockedFired);
        }

        [Fact]
        public void IsLockedReturnsFalseWhenBothSessionsActive()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            _fixture.SessionService.SetUser(userId, Email);
            var dek = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dek);
            _fixture.SessionService.SetDerivedKey(dek);

            _fixture.AuthClient
                .SetupGet(c => c.CurrentSession)
                .Returns(new AuthSession
                {
                    AccessToken = "x",
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            Assert.False(service.IsLocked());
        }

        [Fact]
        public async Task IsLockedReturnsTrueAfterLock()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            _fixture.SessionService.SetUser(userId, Email);
            var dek = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dek);
            _fixture.SessionService.SetDerivedKey(dek);

            _fixture.AuthClient
                .SetupGet(c => c.CurrentSession)
                .Returns(new AuthSession
                {
                    AccessToken = "x",
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            _fixture.AuthClient
                .Setup(c => c.SignOutAsync())
                .Returns(Task.CompletedTask);

            await service.LockAsync();

            Assert.True(service.IsLocked());
        }
    }
}

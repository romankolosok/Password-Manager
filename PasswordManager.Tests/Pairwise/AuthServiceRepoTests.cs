using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Tests.Fixtures.Pairwise;
using System.Security.Cryptography;

namespace PasswordManager.Tests.Pairwise
{
    public class AuthServiceRepoTests : IClassFixture<PairwiseAuthRepoFixture>
    {
        private const string CurrentPassword = "ValidPassword1!";
        private const string NewPassword = "NewValidPassword1!";

        private readonly PairwiseAuthRepoFixture _fixture;

        public AuthServiceRepoTests(PairwiseAuthRepoFixture fixture)
        {
            _fixture = fixture;
        }

        private UserProfileEntity SeedProfile(Guid userId)
        {
            var salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
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
            _fixture.Repository.CreateUserProfileAsync(profile).GetAwaiter().GetResult();
            return profile;
        }

        private void SetupCryptoForPasswordChange(byte[] salt, byte[] dek)
        {
            _fixture.CryptoService
                .Setup(c => c.DeriveKey(CurrentPassword, salt))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(Convert.ToBase64String(dek)));

            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.Is<string>(p => p != CurrentPassword), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            var newBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[8],
                Tag = new byte[16]
            };
            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(newBlob));
        }

        [Fact]
        public async Task ChangeMasterPasswordPersistsUpdatedProfileToRepository()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            var profile = SeedProfile(userId);
            var salt = Convert.FromBase64String(profile.Salt);
            var dek = new byte[32];
            RandomNumberGenerator.Fill(dek);

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            SetupCryptoForPasswordChange(salt, dek);

            _fixture.AuthClient
                .Setup(c => c.UpdateUserAsync(NewPassword))
                .ReturnsAsync(new AuthUser { Id = userId.ToString() });

            var originalSalt = profile.Salt;
            var originalDek = profile.EncryptedDEK;

            var service = _fixture.CreateService();
            var result = await service.ChangeMasterPasswordAsync(CurrentPassword, NewPassword);

            Assert.True(result.Success);

            var stored = await _fixture.Repository.GetUserProfileAsync(userId);
            Assert.NotNull(stored);
            Assert.NotEqual(originalSalt, stored!.Salt);
            Assert.NotEqual(originalDek, stored.EncryptedDEK);
        }

        [Fact]
        public async Task SetupRecoveryKeyPersistsRecoveryDataToRepository()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            var profile = SeedProfile(userId);
            var dek = new byte[32];
            RandomNumberGenerator.Fill(dek);

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.SessionService.Setup(s => s.GetDerivedKey()).Returns(dek);
            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            var recoveryBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[8],
                Tag = new byte[16]
            };
            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(recoveryBlob));

            var service = _fixture.CreateService();
            var result = await service.SetupRecoveryKeyAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Value);

            var stored = await _fixture.Repository.GetUserProfileAsync(userId);
            Assert.NotNull(stored);
            Assert.NotNull(stored!.RecoveryEncryptedDEK);
            Assert.NotNull(stored.RecoverySalt);
        }

        [Fact]
        public async Task RecoverVaultPersistsNewCredentialsToRepository()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            var profile = SeedProfile(userId);
            profile.RecoverySalt = Convert.ToBase64String(new byte[16]);
            var recoveryBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[4],
                Tag = new byte[16]
            };
            profile.RecoveryEncryptedDEK = recoveryBlob.ToBase64String();
            await _fixture.Repository.UpdateUserProfileAsync(profile);

            var dek = new byte[32];
            RandomNumberGenerator.Fill(dek);
            var originalSalt = profile.Salt;
            var originalDek = profile.EncryptedDEK;

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(Convert.ToBase64String(dek)));

            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);

            var newBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[8],
                Tag = new byte[16]
            };
            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(newBlob));

            _fixture.AuthClient
                .Setup(c => c.UpdateUserAsync(NewPassword))
                .ReturnsAsync(new AuthUser { Id = userId.ToString() });

            var service = _fixture.CreateService();
            var result = await service.RecoverVaultAsync("recoverykey", NewPassword);

            Assert.True(result.Success);

            var stored = await _fixture.Repository.GetUserProfileAsync(userId);
            Assert.NotNull(stored);
            Assert.NotEqual(originalSalt, stored!.Salt);
            Assert.NotEqual(originalDek, stored.EncryptedDEK);
        }

        [Fact]
        public async Task ChangeMasterPasswordReturnsFailWhenRepoUpdateFails()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();

            var salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
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

            // Do not seed profile in repository so UpdateUserProfileAsync throws RepositoryException
            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var dek = new byte[32];
            SetupCryptoForPasswordChange(salt, dek);

            _fixture.AuthClient
                .Setup(c => c.UpdateUserAsync(NewPassword))
                .ReturnsAsync(new AuthUser { Id = userId.ToString() });

            var service = _fixture.CreateService();
            var result = await service.ChangeMasterPasswordAsync(CurrentPassword, NewPassword);

            Assert.False(result.Success);
            Assert.Contains("Failed to save profile changes", result.Message);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Tests.Fixtures.Pairwise;
using Xunit;

namespace PasswordManager.Tests.Pairwise
{
    public class AuthServiceCryptoTests : IClassFixture<PairwiseAuthCryptoFixture>
    {
        private const string Email = "user@example.com";
        private const string MasterPassword = "ValidPassword1!";
        private const string NewPassword = "NewValidPassword1!";

        private readonly PairwiseAuthCryptoFixture _fixture;

        public AuthServiceCryptoTests(PairwiseAuthCryptoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task RegisterAsyncProducesValidEncryptedDekBlob()
        {
            _fixture.Reset();
            var crypto = _fixture.Crypto;
            var service = _fixture.CreateService();

            Dictionary<string, object>? capturedMetadata = null;
            var userId = Guid.NewGuid();
            var session = new AuthSession
            {
                AccessToken = "at",
                User = new AuthUser { Id = userId.ToString(), Email = Email }
            };

            _fixture.AuthClient
                .Setup(c => c.SignUpAsync(Email, MasterPassword, It.IsAny<Dictionary<string, object>>()))
                .Callback<string, string, Dictionary<string, object>?>((_, _, meta) => capturedMetadata = meta)
                .ReturnsAsync(session);

            _fixture.AuthClient
                .Setup(c => c.SignOutAsync())
                .Returns(Task.CompletedTask);

            var result = await service.RegisterAsync(Email, MasterPassword);

            Assert.True(result.Success);
            Assert.NotNull(capturedMetadata);
            Assert.True(capturedMetadata!.ContainsKey("salt"));
            Assert.True(capturedMetadata.ContainsKey("encrypted_dek"));

            var saltBytes = Convert.FromBase64String((string)capturedMetadata["salt"]);
            Assert.Equal(16, saltBytes.Length);

            var encryptedDekBlob = EncryptedBlob.FromBase64String((string)capturedMetadata["encrypted_dek"]);
            Assert.True(encryptedDekBlob.Success);

            var kek = crypto.DeriveKey(MasterPassword, saltBytes);
            var decryptResult = crypto.Decrypt(encryptedDekBlob.Value, kek);
            Assert.True(decryptResult.Success);
            var dekBytes = Convert.FromBase64String(decryptResult.Value);
            Assert.Equal(32, dekBytes.Length);
        }

        [Fact]
        public async Task LoginAsyncWithCorrectPasswordDerivesSameKekAndDecryptsDek()
        {
            _fixture.Reset();
            var crypto = _fixture.Crypto;
            var service = _fixture.CreateService();

            var password = MasterPassword;
            var salt = crypto.GenerateSalt();
            var kek = crypto.DeriveKey(password, salt);
            var dekBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dekBytes);
            var dekBase64 = Convert.ToBase64String(dekBytes);
            var encDek = crypto.Encrypt(dekBase64, kek);
            Assert.True(encDek.Success);

            var userId = Guid.NewGuid();
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = encDek.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, password))
                .ReturnsAsync(new AuthSession
                {
                    AccessToken = "token",
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            await service.LoginAsync(Email, password);

            _fixture.SessionService.Verify(
                s => s.SetDerivedKey(It.Is<byte[]>(b => b.SequenceEqual(dekBytes))),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsyncWithWrongPasswordFailsDecryption()
        {
            _fixture.Reset();
            var crypto = _fixture.Crypto;
            var service = _fixture.CreateService();

            var salt = crypto.GenerateSalt();
            var kek = crypto.DeriveKey(MasterPassword, salt);
            var dekBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dekBytes);
            var encDek = crypto.Encrypt(Convert.ToBase64String(dekBytes), kek);
            Assert.True(encDek.Success);

            var userId = Guid.NewGuid();
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = encDek.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, "WrongPassword1!"))
                .ReturnsAsync(new AuthSession
                {
                    AccessToken = "token",
                    User = new AuthUser { Id = userId.ToString(), Email = Email }
                });

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.AuthClient
                .Setup(c => c.SignOutAsync())
                .Returns(Task.CompletedTask);

            var result = await service.LoginAsync(Email, "WrongPassword1!");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ChangeMasterPasswordReWrapsDecWithNewKey()
        {
            _fixture.Reset();
            var crypto = _fixture.Crypto;
            var service = _fixture.CreateService();

            var currentPassword = MasterPassword;
            var salt = crypto.GenerateSalt();
            var kek = crypto.DeriveKey(currentPassword, salt);
            var dekBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dekBytes);
            var encDek = crypto.Encrypt(Convert.ToBase64String(dekBytes), kek);
            Assert.True(encDek.Success);

            var userId = Guid.NewGuid();
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = encDek.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.AuthClient
                .Setup(c => c.UpdateUserAsync(NewPassword))
                .ReturnsAsync(new AuthUser { Id = userId.ToString() });

            UserProfileEntity? updated = null;
            _fixture.VaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .Callback<UserProfileEntity>(p => updated = p)
                .Returns(Task.CompletedTask);

            var changeResult = await service.ChangeMasterPasswordAsync(currentPassword, NewPassword);

            Assert.True(changeResult.Success);
            Assert.NotNull(updated);
            var newSalt = Convert.FromBase64String(updated!.Salt);
            var newKek = crypto.DeriveKey(NewPassword, newSalt);
            var blobResult = EncryptedBlob.FromBase64String(updated.EncryptedDEK);
            Assert.True(blobResult.Success);
            var decryptResult = crypto.Decrypt(blobResult.Value, newKek);
            Assert.True(decryptResult.Success);
            var roundTripDek = Convert.FromBase64String(decryptResult.Value);
            Assert.True(roundTripDek.SequenceEqual(dekBytes));
        }

        [Fact]
        public async Task SetupRecoveryKeyProducesRecoverableDek()
        {
            _fixture.Reset();
            var crypto = _fixture.Crypto;
            var service = _fixture.CreateService();

            var dekBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(dekBytes);

            var userId = Guid.NewGuid();
            var profileSalt = crypto.GenerateSalt();
            var profileKek = crypto.DeriveKey(MasterPassword, profileSalt);
            var profileEncDek = crypto.Encrypt(Convert.ToBase64String(dekBytes), profileKek);
            Assert.True(profileEncDek.Success);

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(profileSalt),
                EncryptedDEK = profileEncDek.Value.ToBase64String(),
                RecoveryEncryptedDEK = null,
                RecoverySalt = null,
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.SessionService
                .Setup(s => s.GetDerivedKey())
                .Returns(dekBytes);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            UserProfileEntity? updated = null;
            _fixture.VaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .Callback<UserProfileEntity>(p => updated = p)
                .Returns(Task.CompletedTask);

            var setupResult = await service.SetupRecoveryKeyAsync();

            Assert.True(setupResult.Success);
            Assert.False(string.IsNullOrEmpty(setupResult.Value));
            Assert.NotNull(updated);
            Assert.False(string.IsNullOrEmpty(updated!.RecoverySalt));
            Assert.False(string.IsNullOrEmpty(updated.RecoveryEncryptedDEK));

            var recoverySalt = Convert.FromBase64String(updated.RecoverySalt!);
            var recoveryKek = crypto.DeriveKey(setupResult.Value!, recoverySalt);
            var blobResult = EncryptedBlob.FromBase64String(updated.RecoveryEncryptedDEK!);
            Assert.True(blobResult.Success);
            var decryptResult = crypto.Decrypt(blobResult.Value, recoveryKek);
            Assert.True(decryptResult.Success);
            var recovered = Convert.FromBase64String(decryptResult.Value);
            Assert.True(recovered.SequenceEqual(dekBytes));
        }
    }
}

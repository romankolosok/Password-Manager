using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Tests.Fixtures;

namespace PasswordManager.Tests.Services
{
    public class AuthServiceTests : IClassFixture<AuthServiceFixture>
    {
        private readonly AuthServiceFixture _fixture;

        public AuthServiceTests(AuthServiceFixture fixture)
        {
            _fixture = fixture;
        }


        [Fact]
        public async Task VerifyMasterPasswordAsyncReturnsFailureWhenEncryptedDEKIsEmpty()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await service.VerifyMasterPasswordAsync(userId, "ValidPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);
        }

        [Fact]
        public async Task VerifyMasterPasswordAsyncReturnsFailureWhenEncryptedDEKIsInvalidBase64()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "!!!not-valid-base64!!!",
                CreatedAt = DateTime.UtcNow
            };

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await service.VerifyMasterPasswordAsync(userId, "ValidPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);
        }

        [Fact]
        public async Task VerifyMasterPasswordAsyncReturnsFailureWhenDecryptFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var password = "ValidPassword1!";

            var salt = new byte[16];
            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(password, salt))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Fail("decryption failed"));

            var result = await service.VerifyMasterPasswordAsync(userId, password);

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);
        }

        [Fact]
        public async Task VerifyMasterPasswordAsyncReturnsFailureWhenDecryptThrows()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var password = "ValidPassword1!";

            var salt = new byte[16];
            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(password, salt))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Throws(new InvalidOperationException("decryption failed"));

            var result = await service.VerifyMasterPasswordAsync(userId, password);

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);

            _fixture.Logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((_, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenNotLoggedIn()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns((Guid?)null);

            var result = await service.ChangeMasterPasswordAsync("current", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Not logged in.", result.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenNewPasswordIsInvalid()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(Guid.NewGuid());

            var result = await service.ChangeMasterPasswordAsync("current", "weakpass");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenProfileLoadFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Fail("DB error"));

            var result = await service.ChangeMasterPasswordAsync("current", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Failed to load user profile.", result.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenEncryptedDEKIsEmpty()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            var result = await service.ChangeMasterPasswordAsync("current", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenCurrentPasswordIsWrong()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var userId = Guid.NewGuid();
            var salt = new byte[16];
            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Fail("decryption failed"));

            var result = await service.ChangeMasterPasswordAsync("wrong-current", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenReEncryptFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var salt = new byte[16];
            var dek = Convert.ToBase64String(new byte[32]);

            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dek));

            _fixture.CryptoService
                .Setup(c => c.GenerateSalt())
                .Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Fail("encryption failed"));

            var result = await service.ChangeMasterPasswordAsync("current", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Failed to re-encrypt vault key. Please try again.", result.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncReturnsFailureWhenAuthUpdateFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var salt = new byte[16];
            var dek = Convert.ToBase64String(new byte[32]);

            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dek));

            _fixture.CryptoService
                .Setup(c => c.GenerateSalt())
                .Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(blob));

            _fixture.AuthClient
                .Setup(a => a.UpdateUserAsync(It.IsAny<string>()))
                .ThrowsAsync(new AuthClientException("auth update failed", 500));

            _fixture.ExceptionMapper
                .Setup(m => m.MapAuthException(It.IsAny<Exception>()))
                .Returns(Result.Fail("Auth update failed."));

            var result = await service.ChangeMasterPasswordAsync("current", "NewPassword1!");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenNotLoggedIn()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns((Guid?)null);

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Not logged in.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenProfileLoadFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Fail("DB error"));

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Failed to load user profile.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenEncryptedDEKIsEmpty()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Recovery key setup is not supported for legacy accounts.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenRecoveryKeyAlreadySetUp()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = blob.ToBase64String(),
                RecoveryEncryptedDEK = blob.ToBase64String(),
                RecoverySalt = Convert.ToBase64String(new byte[16]),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Recovery key is already set up for this account.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenSessionKeyNotAvailable()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.SessionService
                .Setup(s => s.GetDerivedKey())
                .Throws(new InvalidOperationException("no key in session"));

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Session key not available. Please log in again.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenEncryptFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.SessionService
                .Setup(s => s.GetDerivedKey())
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.GenerateSalt())
                .Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Fail("encryption error"));

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Failed to create recovery key. Please try again.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsFailureWhenUpdateProfileThrows()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.SessionService
                .Setup(s => s.GetDerivedKey())
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.GenerateSalt())
                .Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(blob));

            _fixture.VaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .ThrowsAsync(new Exception("DB failure"));

            var result = await service.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Failed to save recovery key. Please try again.", result.Message);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsBase64RecoveryKeyOnSuccess()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.SessionService
                .Setup(s => s.GetDerivedKey())
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.GenerateSalt())
                .Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(blob));

            _fixture.VaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .Returns(Task.CompletedTask);

            var result = await service.SetupRecoveryKeyAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            // Recovery key is 32 random bytes returned as base64
            var decoded = Convert.FromBase64String(result.Value);
            Assert.Equal(32, decoded.Length);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncPersistsRecoveryFieldsOnSuccess()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = blob.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService.Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));
            _fixture.SessionService.Setup(s => s.GetDerivedKey()).Returns(new byte[32]);
            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService.Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(new byte[32]);
            _fixture.CryptoService.Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(blob));
            _fixture.VaultRepository.Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .Returns(Task.CompletedTask);

            await service.SetupRecoveryKeyAsync();

            _fixture.VaultRepository.Verify(
                r => r.UpdateUserProfileAsync(It.Is<UserProfileEntity>(p =>
                    !string.IsNullOrEmpty(p.RecoveryEncryptedDEK) &&
                    !string.IsNullOrEmpty(p.RecoverySalt))),
                Times.Once);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenNotLoggedIn()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns((Guid?)null);

            var result = await service.RecoverVaultAsync("recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Not logged in.", result.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenNewPasswordIsInvalid()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(Guid.NewGuid());

            var result = await service.RecoverVaultAsync("recovery-key", "weak");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenProfileLoadFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Fail("DB error"));

            var result = await service.RecoverVaultAsync("recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Failed to load user profile.", result.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenNoRecoveryKeySetUp()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "some-dek",
                RecoveryEncryptedDEK = null,
                RecoverySalt = null,
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await service.RecoverVaultAsync("recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("No recovery key has been set up for this account.", result.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenRecoveryDEKIsInvalidBase64()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "some-dek",
                RecoveryEncryptedDEK = "!!!invalid-base64!!!",
                RecoverySalt = Convert.ToBase64String(new byte[16]),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            var result = await service.RecoverVaultAsync("recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid recovery data.", result.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenRecoveryKeyIsWrong()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "some-dek",
                RecoveryEncryptedDEK = blob.ToBase64String(),
                RecoverySalt = Convert.ToBase64String(new byte[16]),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Fail("bad tag"));

            var result = await service.RecoverVaultAsync("wrong-recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid recovery key.", result.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenReEncryptFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var dek = Convert.ToBase64String(new byte[32]);

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "some-dek",
                RecoveryEncryptedDEK = blob.ToBase64String(),
                RecoverySalt = Convert.ToBase64String(new byte[16]),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService
                .Setup(s => s.CurrentUserId)
                .Returns(userId);

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);

            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dek));

            _fixture.CryptoService
                .Setup(c => c.GenerateSalt())
                .Returns(new byte[16]);

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Fail("encryption failed"));

            var result = await service.RecoverVaultAsync("valid-recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Failed to re-encrypt vault key. Please try again.", result.Message);
        }

        [Fact]
        public async Task RegisterAsyncReturnsFailureWhenEmailIsInvalid()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var result = await service.RegisterAsync("not-an-email", "ValidPassword1!");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }

        [Fact]
        public async Task RegisterAsyncReturnsFailureWhenPasswordIsInvalid()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var result = await service.RegisterAsync("user@example.com", "weak");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }

        [Fact]
        public async Task RegisterAsyncReturnsFailureWhenEncryptionFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            const string email = "user@example.com";
            const string password = "ValidPassword1!";

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Fail("encryption failed"));

            var result = await service.RegisterAsync(email, password);

            Assert.False(result.Success);
            Assert.Equal("Failed to create account. Please try again.", result.Message);
        }

        [Fact]
        public void IsLockedReturnsTrueWhenInternalSessionIsInactive()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.IsActive())
                .Returns(false);

            Assert.True(service.IsLocked());
        }

        [Fact]
        public void IsLockedReturnsTrueWhenSupabaseSessionIsNull()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.SessionService
                .Setup(s => s.IsActive())
                .Returns(true);

            Assert.True(service.IsLocked());
        }

        [Fact]
        public async Task SendOTPConfirmationAsyncReturnsSuccess()
        {
            _fixture.Reset();

            _fixture.AuthClient.Setup(c => c.SignInWithOtpAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = _fixture.CreateService();

            var result = await service.SendOTPConfirmationAsync("user@example.com");
            Assert.True(result.Success);
            _fixture.AuthClient.Verify(c => c.SignInWithOtpAsync("user@example.com"), Times.Once);
        }

        [Fact]
        public async Task SendOPTConfirmationAsyncReturnsFailureOnEmailResendFailure()
        {
            _fixture.Reset();

            _fixture.AuthClient.Setup(c => c.SignInWithOtpAsync(It.IsAny<string>()))
                .ThrowsAsync(new AuthClientException("Failed to send OTP", 500));

            _fixture.ExceptionMapper
                .Setup(m => m.MapAuthException(It.IsAny<Exception>()))
                .Returns(Result.Fail("Failed to send OTP"));

            var service = _fixture.CreateService();
            var result = await service.SendOTPConfirmationAsync("user@example.com");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenUpdateUserAsyncFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var dek = Convert.ToBase64String(new byte[32]);

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "some-dek",
                RecoveryEncryptedDEK = blob.ToBase64String(),
                RecoverySalt = Convert.ToBase64String(new byte[16]),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService.Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));
            _fixture.CryptoService.Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);
            _fixture.CryptoService.Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dek));
            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService.Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(blob));

            _fixture.AuthClient.Setup(c => c.UpdateUserAsync(It.IsAny<string>()))
                .ThrowsAsync(new AuthClientException("auth update failed", 500));
            _fixture.ExceptionMapper.Setup(m => m.MapAuthException(It.IsAny<Exception>()))
                .Returns(Result.Fail("Auth update failed."));

            var result = await service.RecoverVaultAsync("valid-recovery-key", "NewPassword1!");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task RecoverVaultAsyncReturnsFailureWhenUpdateUserProfileAsyncFails()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var dek = Convert.ToBase64String(new byte[32]);

            var blob = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] };
            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "some-dek",
                RecoveryEncryptedDEK = blob.ToBase64String(),
                RecoverySalt = Convert.ToBase64String(new byte[16]),
                CreatedAt = DateTime.UtcNow
            };

            _fixture.SessionService.Setup(s => s.CurrentUserId).Returns(userId);
            _fixture.UserProfileService.Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));
            _fixture.CryptoService.Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);
            _fixture.CryptoService.Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dek));
            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService.Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(blob));

            _fixture.AuthClient.Setup(c => c.UpdateUserAsync(It.IsAny<string>()))
                .ReturnsAsync(new AuthUser { Id = userId.ToString() });

            _fixture.VaultRepository.Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .ThrowsAsync(new Exception("DB write failed"));

            var result = await service.RecoverVaultAsync("valid-recovery-key", "NewPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Failed to save profile changes. Please try again.", result.Message);
        }
    }
}
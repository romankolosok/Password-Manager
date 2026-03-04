using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Tests.Fixtures;
using System.Reflection;

namespace PasswordManager.Tests.Services
{
    public class AuthServiceTests : IClassFixture<AuthServiceFixture>
    {
        private readonly AuthServiceFixture _fixture;

        public AuthServiceTests(AuthServiceFixture fixture)
        {
            _fixture = fixture;
        }

        private static async Task<Result<byte[]>> InvokeVerifyMasterPasswordAsync(
            AuthService service,
            Guid userId,
            string password)
        {
            var method = typeof(AuthService)
                .GetMethod("VerifyMasterPasswordAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var task = (Task<Result<byte[]>>)method.Invoke(service, new object[] { userId, password })!;
            return await task;
        }

        [Fact]
        public async Task VerifyMasterPasswordAsyncReturnsFailureWhenEncryptedTokenIsInvalidBase64()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();
            var userId = Guid.NewGuid();
            var password = "ValidPassword1!";

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedVerificationToken = "!!!not-valid-base64!!!",
                CreatedAt = DateTime.UtcNow
            };

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            var result = await InvokeVerifyMasterPasswordAsync(service, userId, password);

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
                EncryptedVerificationToken = blob.ToBase64String(),
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

            var result = await InvokeVerifyMasterPasswordAsync(service, userId, password);

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
                EncryptedVerificationToken = blob.ToBase64String(),
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

            var result = await InvokeVerifyMasterPasswordAsync(service, userId, password);

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
        public void OnAuthStateChangedSignedOutClearsSession()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var method = typeof(AuthService)
                .GetMethod("OnAuthStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;

            method.Invoke(service, new object?[] { null, Supabase.Gotrue.Constants.AuthState.SignedOut });

            _fixture.SessionService.Verify(s => s.ClearSession(), Times.Once);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncAlwaysReturnsNotImplemented()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            var result = await service.ChangeMasterPasswordAsync("current", "new-password");

            Assert.False(result.Success);
            Assert.Equal("Not implemented.", result.Message);
        }
    }
}


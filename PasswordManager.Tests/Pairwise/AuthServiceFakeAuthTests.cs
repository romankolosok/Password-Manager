using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Tests.Fixtures.Pairwise;

namespace PasswordManager.Tests.Pairwise
{
    public class AuthServiceFakeAuthTests : IClassFixture<PairwiseAuthFakeAuthFixture>
    {
        private const string Email = "user@example.com";
        private const string Password = "ValidPassword1!";

        private readonly PairwiseAuthFakeAuthFixture _fixture;

        public AuthServiceFakeAuthTests(PairwiseAuthFakeAuthFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task RegisterAsyncStoresUserInFakeAuthAndReturnsSuccess()
        {
            _fixture.Reset();
            var encryptedBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[8],
                Tag = new byte[16]
            };

            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);
            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(encryptedBlob));

            var service = _fixture.CreateService();

            var result = await service.RegisterAsync(Email, Password);

            Assert.True(result.Success);
            var signIn = await _fixture.FakeAuthClient.SignInAsync(Email, Password);
            Assert.NotNull(signIn);
            Assert.Equal(Email, signIn!.User!.Email);
        }

        [Fact]
        public async Task LoginAsyncWithValidCredentialsReturnsSuccess()
        {
            _fixture.Reset();
            var signUpSession = await _fixture.FakeAuthClient.SignUpAsync(Email, Password);
            var userId = Guid.Parse(signUpSession!.User!.Id);

            var salt = new byte[16];
            var dekBytes = new byte[32];
            Random.Shared.NextBytes(dekBytes);
            var dekBase64 = Convert.ToBase64String(dekBytes);
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

            _fixture.UserProfileService
                .Setup(s => s.GetProfileAsync(userId))
                .ReturnsAsync(Result<UserProfileEntity>.Ok(profile));

            _fixture.CryptoService.Setup(c => c.DeriveKey(Password, salt)).Returns(new byte[32]);
            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok(dekBase64));

            var service = _fixture.CreateService();

            var result = await service.LoginAsync(Email, Password);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task LoginAsyncWithInvalidCredentialsReturnsFail()
        {
            _fixture.Reset();
            await _fixture.FakeAuthClient.SignUpAsync(Email, Password);

            _fixture.ExceptionMapper
                .Setup(m => m.MapAuthException(It.IsAny<Exception>()))
                .Returns(Result.Fail("Invalid email or password."));

            var service = _fixture.CreateService();

            var result = await service.LoginAsync(Email, "WrongPassword1!");

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password.", result.Message);
        }

        [Fact]
        public async Task DuplicateRegistrationThrowsAndReturnsFail()
        {
            _fixture.Reset();
            var encryptedBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[8],
                Tag = new byte[16]
            };

            _fixture.CryptoService.Setup(c => c.GenerateSalt()).Returns(new byte[16]);
            _fixture.CryptoService
                .Setup(c => c.DeriveKey(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(new byte[32]);
            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(encryptedBlob));

            _fixture.ExceptionMapper
                .Setup(m => m.MapAuthException(It.IsAny<Exception>()))
                .Returns(Result.Fail(AuthMessages.AccountAlreadyExists));

            var service = _fixture.CreateService();

            var first = await service.RegisterAsync(Email, Password);
            Assert.True(first.Success);

            var second = await service.RegisterAsync(Email, Password);

            Assert.False(second.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, second.Message);
        }

        [Fact]
        public async Task LockAsyncSignsOutAndClearsSession()
        {
            _fixture.Reset();
            await _fixture.FakeAuthClient.SignUpAsync(Email, Password);
            await _fixture.FakeAuthClient.SignInAsync(Email, Password);
            Assert.NotNull(_fixture.FakeAuthClient.CurrentSession);

            var service = _fixture.CreateService();

            await service.LockAsync();

            Assert.Null(_fixture.FakeAuthClient.CurrentSession);
        }

        [Fact]
        public async Task VerifyOTPAsyncWithPreConfiguredCodeReturnsSession()
        {
            _fixture.Reset();
            await _fixture.FakeAuthClient.SignUpAsync(Email, Password);
            const string code = "123456";
            _fixture.FakeAuthClient.SetOtpCode(Email, code);

            var service = _fixture.CreateService();

            var result = await service.VerifyEmailConfirmationAsync(Email, code);

            Assert.True(result.Success);
        }
    }
}

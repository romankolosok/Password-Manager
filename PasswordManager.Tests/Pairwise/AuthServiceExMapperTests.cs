using System.Net.Http;
using Moq;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models.Auth;
using PasswordManager.Tests.Fixtures.Pairwise;
using Xunit;

namespace PasswordManager.Tests.Pairwise
{
    public class AuthServiceExMapperTests : IClassFixture<PairwiseAuthExMapperFixture>
    {
        private const string Email = "user@example.com";
        private const string Password = "ValidPassword1!";

        private readonly PairwiseAuthExMapperFixture _fixture;

        public AuthServiceExMapperTests(PairwiseAuthExMapperFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task LoginAsyncMapsRateLimitExceptionToUserFriendlyMessage()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, Password))
                .ThrowsAsync(new AuthClientException("", 429));

            var result = await service.LoginAsync(Email, Password);

            Assert.False(result.Success);
            Assert.Contains("Too many attempts", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsyncMapsAlreadyRegisteredExceptionCorrectly()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, Password))
                .ThrowsAsync(new AuthClientException("already registered", 422));

            var result = await service.LoginAsync(Email, Password);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public async Task LoginAsyncMapsEmailNotConfirmedException()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.AuthClient
                .Setup(c => c.SignInAsync(Email, Password))
                .ThrowsAsync(new AuthClientException("Email not confirmed", 400));

            var result = await service.LoginAsync(Email, Password);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.EmailNotConfirmed, result.Message);
        }

        [Fact]
        public async Task VerifyEmailMapsInvalidOtpException()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.AuthClient
                .Setup(c => c.VerifyOTPAsync(Email, It.IsAny<string>(), OtpType.Signup))
                .ThrowsAsync(new AuthClientException("Token has expired", 403));

            var result = await service.VerifyEmailConfirmationAsync(Email, "123456");

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.OtpInvalidOrExpired, result.Message);
        }

        [Fact]
        public async Task SendResetEmailMapsNetworkException()
        {
            _fixture.Reset();
            var service = _fixture.CreateService();

            _fixture.AuthClient
                .Setup(c => c.ResetPasswordForEmailAsync(Email))
                .ThrowsAsync(new HttpRequestException("no network"));

            var result = await service.SendResetPasswordEmailAsync(Email);

            Assert.False(result.Success);
            Assert.Contains("Network error", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}

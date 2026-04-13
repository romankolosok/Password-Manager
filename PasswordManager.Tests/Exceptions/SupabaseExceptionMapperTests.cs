using PasswordManager.Core.Exceptions;

namespace PasswordManager.Tests.Exceptions
{
    public class SupabaseExceptionMapperTests
    {
        private readonly SupabaseExceptionMapper _mapper = new();

        [Fact]
        public void MapAuthExceptionStatus422ReturnsAlreadyExists()
        {
            var ex = new AuthClientException("already registered", 422);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public void MapAuthExceptionStatus400ReturnsAuthFailedWhenMessageIsEmpty()
        {
            var ex = new AuthClientException("", 400);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AuthFailed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionOtpExpiredErrorReturnsOtpInvalidOrExpired()
        {
            var ex = new AuthClientException("Token has expired or is invalid", 403);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.OtpInvalidOrExpired, result.Message);
        }

        [Fact]
        public void MapAuthExceptionStatus429ReturnsTooManyAttempts()
        {
            var ex = new AuthClientException("", 429);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.TooManyRequests, result.Message);
        }

        [Fact]
        public void MapAuthExceptionOtherStatusAlreadyRegisteredReturnsAlreadyExists()
        {
            var ex = new AuthClientException("User already registered", 500);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public void MapAuthExceptionOtherStatusUserAlreadyExistsReturnsAlreadyExists()
        {
            var ex = new AuthClientException("user_already_exists", 500);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public void MapAuthExceptionEmailNotConfirmedReturnsEmailNotConfirmedMessage()
        {
            var ex = new AuthClientException("Email not confirmed", 400);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.EmailNotConfirmed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionEmailNotConfirmedSnakeCaseReturnsEmailNotConfirmedMessage()
        {
            var ex = new AuthClientException("email_not_confirmed", 400);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.EmailNotConfirmed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionOtherStatusInvalidReturnsInvalidEmailOrPassword()
        {
            var ex = new AuthClientException("Credentials are invalid", 500);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.InvalidCredentials, result.Message);
        }

        [Fact]
        public void MapAuthExceptionOtherStatusUnknownMessageReturnsAuthenticationFailed()
        {
            var ex = new AuthClientException("auth failure", 500);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AuthFailed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionHttpRequestExceptionReturnsNetworkError()
        {
            var ex = new HttpRequestException("connection refused");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("Network error", result.Message);
        }

        [Fact]
        public void MapAuthExceptionOtherExceptionReturnsUnexpectedError()
        {
            var ex = new InvalidOperationException("unexpected error");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("unexpected error", result.Message);
        }
    }
}

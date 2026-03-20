using PasswordManager.Core;
using PasswordManager.Core.Exceptions;
using Supabase.Gotrue.Exceptions;

namespace PasswordManager.Tests.Exceptions
{
    public class SupabaseExceptionMapperTests
    {
        private readonly SupabaseExceptionMapper _mapper = new();

        private static GotrueException CreateGotrueException(int statusCode, string message = "")
        {
            var ex = new GotrueException(message);
            typeof(GotrueException)
                .GetProperty(nameof(GotrueException.StatusCode))!
                .SetValue(ex, statusCode);
            return ex;
        }

        [Fact]
        public void MapAuthExceptionGotrueStatus422ReturnsAlreadyExists()
        {
            var ex = CreateGotrueException(422, "already registered");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueStatus400ReturnsAuthFailedWhenMessageIsEmpty()
        {
            var ex = CreateGotrueException(400);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AuthFailed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtpExpiredErrorReturnsOtpInvalidOrExpired()
        {
            // GoTrue returns 403 with error_code otp_expired and msg "Token has expired or is invalid"
            var ex = CreateGotrueException(403, "Token has expired or is invalid");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.OtpInvalidOrExpired, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueStatus429ReturnsTooManyAttempts()
        {
            var ex = CreateGotrueException(429);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.TooManyRequests, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusAlreadyRegisteredReturnsAlreadyExists()
        {
            var ex = CreateGotrueException(500, "User already registered");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusUserAlreadyExistsReturnsAlreadyExists()
        {
            var ex = CreateGotrueException(500, "user_already_exists");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.AccountAlreadyExists, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueEmailNotConfirmedReturnsEmailNotConfirmedMessage()
        {
            var ex = CreateGotrueException(400, "Email not confirmed");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.EmailNotConfirmed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueEmailNotConfirmedSnakeCaseReturnsEmailNotConfirmedMessage()
        {
            var ex = CreateGotrueException(400, "email_not_confirmed");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.EmailNotConfirmed, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusInvalidReturnsInvalidEmailOrPassword()
        {
            var ex = CreateGotrueException(500, "Credentials are invalid");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Equal(AuthMessages.InvalidCredentials, result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusUnknownMessageReturnsAuthenticationFailed()
        {
            var ex = CreateGotrueException(500, "auth failure");

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

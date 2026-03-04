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
            var ex = CreateGotrueException(422);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueStatus400ReturnsInvalidRequest()
        {
            var ex = CreateGotrueException(400);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("Invalid request", result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueStatus429ReturnsTooManyAttempts()
        {
            var ex = CreateGotrueException(429);

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("Too many attempts", result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusAlreadyRegisteredReturnsAlreadyExists()
        {
            var ex = CreateGotrueException(500, "User already registered");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusUserAlreadyExistsReturnsAlreadyExists()
        {
            var ex = CreateGotrueException(500, "user_already_exists");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusInvalidReturnsInvalidEmailOrPassword()
        {
            var ex = CreateGotrueException(500, "Credentials are invalid");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("Invalid email or password", result.Message);
        }

        [Fact]
        public void MapAuthExceptionGotrueOtherStatusUnknownMessageReturnsAuthenticationFailed()
        {
            var ex = CreateGotrueException(500, "auth failure");

            var result = _mapper.MapAuthException(ex);

            Assert.False(result.Success);
            Assert.Contains("Authentication failed", result.Message);
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

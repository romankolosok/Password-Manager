using PasswordManager.Core.Models;
using System;
using System.Net.Http;

namespace PasswordManager.Core.Exceptions
{
    public interface IAuthExceptionMapper
    {
        Result MapAuthException(Exception exception);
    }

    public class SupabaseExceptionMapper : IAuthExceptionMapper
    {
        public Result MapAuthException(Exception exception)
        {
            return exception switch
            {
                AuthClientException authEx => MapAuthClientException(authEx),
                HttpRequestException => Result.Fail("Network error. Please check your connection."),
                _ => Result.Fail("An unexpected error occurred. Please try again.")
            };
        }

        private static Result MapAuthClientException(AuthClientException exception)
        {
            return exception.StatusCode switch
            {
                422 => Result.Fail(GetUserFriendlyMessage(exception)),
                400 => Result.Fail(GetUserFriendlyMessage(exception)),
                429 => Result.Fail(AuthMessages.TooManyRequests),
                _ => Result.Fail(GetUserFriendlyMessage(exception))
            };
        }

        private static string GetUserFriendlyMessage(AuthClientException exception)
        {
            var msg = exception.Message;

            if (msg.Contains("already registered", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("user_already_exists", StringComparison.OrdinalIgnoreCase))
            {
                return AuthMessages.AccountAlreadyExists;
            }

            if (msg.Contains("email not confirmed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("email_not_confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return AuthMessages.EmailNotConfirmed;
            }

            if (msg.Contains("otp_expired", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("token has expired", StringComparison.OrdinalIgnoreCase))
            {
                return AuthMessages.OtpInvalidOrExpired;
            }

            if (msg.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return AuthMessages.InvalidCredentials;
            }

            return AuthMessages.AuthFailed;
        }
    }
}

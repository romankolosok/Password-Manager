using PasswordManager.Core.Models;
using Supabase.Gotrue.Exceptions;
using System;
using System.Net.Http;

namespace PasswordManager.Core.Exceptions
{
    public interface ISupabaseExceptionMapper
    {
        Result MapAuthException(Exception exception);
    }

    public class SupabaseExceptionMapper : ISupabaseExceptionMapper
    {
        public Result MapAuthException(Exception exception)
        {
            return exception switch
            {
                GotrueException gotrueEx => MapGotrueException(gotrueEx),
                HttpRequestException httpEx => Result.Fail("Network error. Please check your connection."),
                _ => Result.Fail("An unexpected error occurred. Please try again.")
            };
        }

        private static Result MapGotrueException(GotrueException exception)
        {
            // Map based on status code where we have clear semantics; otherwise fall back to message-based mapping.
            return exception.StatusCode switch
            {
                // 422 can mean "already exists" but also other validation errors; inspect message.
                422 => Result.Fail(GetUserFriendlyMessage(exception)),
                // 400 can mean many things (invalid credentials, email not confirmed, bad input, etc.);
                // delegate to GetUserFriendlyMessage so we can inspect the message and distinguish cases.
                400 => Result.Fail(GetUserFriendlyMessage(exception)),
                429 => Result.Fail(AuthMessages.TooManyRequests),
                _ => Result.Fail(GetUserFriendlyMessage(exception))
            };
        }

        private static string GetUserFriendlyMessage(GotrueException exception)
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
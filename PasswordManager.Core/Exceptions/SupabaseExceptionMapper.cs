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
            // Map based on status code or exception type, not string matching
            return exception.StatusCode switch
            {
                422 => Result.Fail("An account with this email already exists. Sign in instead."),
                400 => Result.Fail("Invalid request. Please check your input."),
                429 => Result.Fail("Too many attempts. Please try again later."),
                _ => Result.Fail(GetUserFriendlyMessage(exception))
            };
        }

        private static string GetUserFriendlyMessage(GotrueException exception)
        {
            var msg = exception.Message;

            if (msg.Contains("already registered", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("user_already_exists", StringComparison.OrdinalIgnoreCase))
            {
                return "An account with this email already exists. Sign in instead.";
            }

            if (msg.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid email or password.";
            }

            return "Authentication failed. Please try again.";
        }
    }
}
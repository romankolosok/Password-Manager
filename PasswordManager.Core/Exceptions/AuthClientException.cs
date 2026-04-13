using System;

namespace PasswordManager.Core.Exceptions
{
    public class AuthClientException : Exception
    {
        public int? StatusCode { get; }

        public AuthClientException(string message, int? statusCode = null, Exception? inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
        }
    }
}

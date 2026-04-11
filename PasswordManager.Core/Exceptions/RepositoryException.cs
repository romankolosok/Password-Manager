using System;

namespace PasswordManager.Core.Exceptions
{
    public class RepositoryException : Exception
    {
        public RepositoryException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }
}

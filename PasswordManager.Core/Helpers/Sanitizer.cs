using System.Diagnostics.CodeAnalysis;

namespace PasswordManager.Core.Helpers
{
    [ExcludeFromCodeCoverage]
    public static class Sanitizer
    {
        public static string SanitizeEmailForLogging(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return string.Empty;
            }

            var atIndex = email.IndexOf('@');
            if (atIndex <= 0)
            {
                return "***";
            }

            var domain = email.Substring(atIndex);
            return "***" + domain;
        }
    }
}

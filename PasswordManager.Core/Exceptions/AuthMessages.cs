namespace PasswordManager.Core.Exceptions
{
    /// <summary>
    /// Shared auth messages used for mapping Supabase errors and for UI flow (e.g. opening OTP when login fails due to unconfirmed email).
    /// </summary>
    public static class AuthMessages
    {
        /// <summary>
        /// Shown when the user tries to sign in but their email is not confirmed yet. The app uses this to open the OTP confirmation window from the login screen.
        /// </summary>
        public const string EmailNotConfirmed =
            "Your email is not confirmed. Enter the verification code we sent you.";

        public const string AccountAlreadyExists =
            "An account with this email already exists. Please sign in instead.";

        public const string TooManyRequests =
            "Too many attempts. Please wait a moment and try again.";

        public const string InvalidCredentials =
            "Invalid email or password.";

        public const string OtpInvalidOrExpired =
            "Invalid or expired OTP code. Please check the code or request a new one.";

        public const string AuthFailed =
            "Authentication failed. Please try again.";

        public const string InvalidRequest =
            "Invalid request. Please check your input and try again.";
    }
}

namespace PasswordManager.Core.Models.Auth
{
    public enum AuthStateKind
    {
        SignedIn,
        SignedOut,
        TokenRefreshed,
        UserUpdated
    }
}

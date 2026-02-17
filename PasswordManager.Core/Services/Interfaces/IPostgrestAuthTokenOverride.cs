namespace PasswordManager.Core.Services.Interfaces
{
    /// <summary>
    /// Allows setting a JWT for the very next PostgREST request. Used when the auth session
    /// may not yet be visible to the client (e.g. immediately after SignUp) so RLS sees the user.
    /// </summary>
    public interface IPostgrestAuthTokenOverride
    {
        void SetTokenForNextRequest(string? accessToken);
        string? GetAndClearToken();
    }
}

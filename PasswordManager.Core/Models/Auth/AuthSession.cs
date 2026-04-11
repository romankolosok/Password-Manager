namespace PasswordManager.Core.Models.Auth
{
    public class AuthSession
    {
        public string? AccessToken { get; set; }
        public AuthUser? User { get; set; }
    }
}

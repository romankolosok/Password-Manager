using System.Collections.Generic;

namespace PasswordManager.Core.Models.Auth
{
    public class AuthUser
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public Dictionary<string, object>? UserMetadata { get; set; }
    }
}

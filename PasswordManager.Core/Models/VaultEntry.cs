using System;

namespace PasswordManager.Core.Models
{
    public class VaultEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string WebsiteName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Url { get; set; }
        public string Notes { get; set; }
        public string Category { get; set; }
        public bool IsFavorite { get; set; } = false;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; set; }
    }
}

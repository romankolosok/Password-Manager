using System;

namespace PasswordManager.Core.Models
{
    public class VaultEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string WebsiteName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsFavorite { get; set; } = false;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; set; }
    }
}
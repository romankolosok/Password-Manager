using PasswordManager.Core.Models;

namespace PasswordManager.Tests.Models
{
    public class VaultEntryTests
    {
        [Fact]
        public void TestVaultEntryDefaultConstruction()
        {
            var entry = new VaultEntry();

            Assert.NotEqual(Guid.Empty, entry.Id);
            Assert.Equal(string.Empty, entry.WebsiteName);
            Assert.Equal(string.Empty, entry.Username);
            Assert.Equal(string.Empty, entry.Password);
            Assert.Equal(string.Empty, entry.Url);
            Assert.Equal(string.Empty, entry.Notes);
            Assert.Equal(string.Empty, entry.Category);
            Assert.False(entry.IsFavorite);
            Assert.Equal(default(DateTime), entry.CreatedAt);
            Assert.Equal(default(DateTime), entry.UpdatedAt);
        }

        [Fact]
        public void TestVaultEntryCustomConstruction()
        {
            var id = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            var entry = new VaultEntry
            {
                Id = id,
                WebsiteName = "GitHub",
                Username = "user@example.com",
                Password = "SecurePassword123",
                Url = "https://github.com",
                Notes = "My GitHub account",
                Category = "Development",
                IsFavorite = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            Assert.Equal(id, entry.Id);
            Assert.Equal("GitHub", entry.WebsiteName);
            Assert.Equal("user@example.com", entry.Username);
            Assert.Equal("SecurePassword123", entry.Password);
            Assert.Equal("https://github.com", entry.Url);
            Assert.Equal("My GitHub account", entry.Notes);
            Assert.Equal("Development", entry.Category);
            Assert.True(entry.IsFavorite);
            Assert.Equal(createdAt, entry.CreatedAt);
            Assert.Equal(createdAt, entry.UpdatedAt);
        }
    }
}

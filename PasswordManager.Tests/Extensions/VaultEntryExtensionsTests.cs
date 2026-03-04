using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;

namespace PasswordManager.Tests.Extensions
{
    public class VaultEntryExtensionsTests
    {
        [Fact]
        public void ToPayloadMapsAllFieldsCorrectly()
        {
            var entry = new VaultEntry
            {
                Id = TestData.UserId(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            var payload = entry.ToPayload();

            Assert.Equal(entry.WebsiteName, payload.WebsiteName);
            Assert.Equal(entry.Username, payload.Username);
            Assert.Equal(entry.Password, payload.Password);
            Assert.Equal(entry.Url, payload.Url);
            Assert.Equal(entry.Notes, payload.Notes);
            Assert.Equal(entry.Category, payload.Category);
            Assert.Equal(entry.IsFavorite, payload.IsFavorite);
        }

        [Fact]
        public void ToPayloadDefaultsNullFieldsToEmptyString()
        {
            var entry = new VaultEntry
            {
                WebsiteName = null!,
                Username = null!,
                Password = null!,
                Url = null!,
                Notes = null!,
                Category = null!,
                IsFavorite = false
            };

            var payload = entry.ToPayload();

            Assert.Equal("", payload.WebsiteName);
            Assert.Equal("", payload.Username);
            Assert.Equal("", payload.Password);
            Assert.Equal("", payload.Url);
            Assert.Equal("", payload.Notes);
            Assert.Equal("", payload.Category);
            Assert.False(payload.IsFavorite);
        }

        [Fact]
        public void ToVaultEntryWithMetadataMapsAllFieldsCorrectly()
        {
            var payload = new VaultEntryPayload
            {
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = true
            };

            var id = TestData.UserId();
            var createdAt = DateTime.UtcNow.AddDays(-1);
            var updatedAt = DateTime.UtcNow;

            var entry = payload.ToVaultEntry(id, createdAt, updatedAt);

            Assert.Equal(id, entry.Id);
            Assert.Equal(payload.WebsiteName, entry.WebsiteName);
            Assert.Equal(payload.Username, entry.Username);
            Assert.Equal(payload.Password, entry.Password);
            Assert.Equal(payload.Url, entry.Url);
            Assert.Equal(payload.Notes, entry.Notes);
            Assert.Equal(payload.Category, entry.Category);
            Assert.Equal(payload.IsFavorite, entry.IsFavorite);
            Assert.Equal(createdAt, entry.CreatedAt);
            Assert.Equal(updatedAt, entry.UpdatedAt);
        }

        [Fact]
        public void ToVaultEntryWithMetadataDefaultsNullFieldsToEmptyString()
        {
            var payload = new VaultEntryPayload
            {
                WebsiteName = null!,
                Username = null!,
                Password = null!,
                Url = null!,
                Notes = null!,
                Category = null!,
                IsFavorite = false
            };

            var entry = payload.ToVaultEntry(TestData.UserId(), DateTime.UtcNow, DateTime.UtcNow);

            Assert.Equal("", entry.WebsiteName);
            Assert.Equal("", entry.Username);
            Assert.Equal("", entry.Password);
            Assert.Equal("", entry.Url);
            Assert.Equal("", entry.Notes);
            Assert.Equal("", entry.Category);
            Assert.False(entry.IsFavorite);
        }

        [Fact]
        public void ToVaultEntryWithEntityDelegatesToMetadataOverload()
        {
            var payload = new VaultEntryPayload
            {
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = true
            };

            var entity = new VaultEntryEntity
            {
                Id = TestData.UserId(),
                UserId = TestData.UserId(),
                EncryptedData = "irrelevant",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var entry = payload.ToVaultEntry(entity);

            Assert.Equal(entity.Id, entry.Id);
            Assert.Equal(entity.CreatedAt, entry.CreatedAt);
            Assert.Equal(entity.UpdatedAt, entry.UpdatedAt);
            Assert.Equal(payload.WebsiteName, entry.WebsiteName);
            Assert.Equal(payload.Username, entry.Username);
            Assert.Equal(payload.Password, entry.Password);
            Assert.Equal(payload.Url, entry.Url);
            Assert.Equal(payload.Notes, entry.Notes);
            Assert.Equal(payload.Category, entry.Category);
            Assert.Equal(payload.IsFavorite, entry.IsFavorite);
        }

        [Fact]
        public void RoundTripToPayloadAndBackPreservesAllFields()
        {
            var original = new VaultEntry
            {
                Id = TestData.UserId(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow
            };

            var payload = original.ToPayload();
            var roundTripped = payload.ToVaultEntry(original.Id, original.CreatedAt, original.UpdatedAt);

            Assert.Equal(original.Id, roundTripped.Id);
            Assert.Equal(original.WebsiteName, roundTripped.WebsiteName);
            Assert.Equal(original.Username, roundTripped.Username);
            Assert.Equal(original.Password, roundTripped.Password);
            Assert.Equal(original.Url, roundTripped.Url);
            Assert.Equal(original.Notes, roundTripped.Notes);
            Assert.Equal(original.Category, roundTripped.Category);
            Assert.Equal(original.IsFavorite, roundTripped.IsFavorite);
            Assert.Equal(original.CreatedAt, roundTripped.CreatedAt);
            Assert.Equal(original.UpdatedAt, roundTripped.UpdatedAt);
        }
    }
}

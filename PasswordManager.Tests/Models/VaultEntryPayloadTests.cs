using PasswordManager.Core.Models;

namespace PasswordManager.Tests.Models
{
    public class VaultEntryPayloadTests
    {
        private static VaultEntryPayload CreateDefault() => new()
        {
            WebsiteName = "Example Site",
            Username = "user123",
            Password = "passw0rd!",
            Url = "https://example.com",
            Notes = "Some notes.",
            Category = "Social",
            IsFavorite = true
        };

        // Round-trip preserves all fields
        [Fact]
        public void TestVaultEntryPayloadRoundTripPreservesAllFields()
        {
            var payload = new VaultEntryPayload
            {
                WebsiteName = "Example Site",
                Username = "user123",
                Password = "passw0rd!",
                Url = "https://example.com",
                Notes = "Some notes about this entry.",
                Category = "Social",
                IsFavorite = true
            };

            var json = payload.ToJson();
            var deserialized = VaultEntryPayload.FromJson(json);

            Assert.NotNull(deserialized);
            Assert.Equal(payload.WebsiteName, deserialized!.WebsiteName);
            Assert.Equal(payload.Username, deserialized.Username);
            Assert.Equal(payload.Password, deserialized.Password);
            Assert.Equal(payload.Url, deserialized.Url);
            Assert.Equal(payload.Notes, deserialized.Notes);
            Assert.Equal(payload.Category, deserialized.Category);
            Assert.Equal(payload.IsFavorite, deserialized.IsFavorite);
        }

        // Invalid JSON returns null
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{ invalid json")]
        public void TestVaultEntryPayloadFromJsonWithInvalidInputReturnsNull(string invalidJson)
        {
            var result = VaultEntryPayload.FromJson(invalidJson);

            Assert.Null(result);
        }

        // FromJson with valid JSON but missing optional fields
        [Fact]
        public void TestVaultEntryPayloadFromJsonWithMissingOptionalFields()
        {
            var json = """{"WebsiteName":"Site","Username":"user","Password":"pass"}""";

            var deserialized = VaultEntryPayload.FromJson(json);

            Assert.NotNull(deserialized);
            Assert.Equal("Site", deserialized!.WebsiteName);
            Assert.Equal("user", deserialized.Username);
            Assert.Equal("pass", deserialized.Password);
            Assert.Equal(string.Empty, deserialized.Url);
            Assert.Equal(string.Empty, deserialized.Notes);
            Assert.Equal(string.Empty, deserialized.Category);
            Assert.False(deserialized.IsFavorite);
        }

        [Fact]
        public void EqualPayloadsAreEqual()
        {
            var a = CreateDefault();
            var b = CreateDefault();

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void DifferentPayloadsAreNotEqual()
        {
            var a = CreateDefault();
            var b = a with { Password = "different" };

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ToStringContainsPropertyValues()
        {
            var payload = CreateDefault();

            var text = payload.ToString();

            Assert.Contains(payload.WebsiteName, text);
            Assert.Contains(payload.Username, text);
        }
    }
}

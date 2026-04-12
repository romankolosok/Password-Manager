using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Tests.Fixtures.Pairwise;

namespace PasswordManager.Tests.Pairwise
{
    public class VaultServiceRepoTests : IClassFixture<PairwiseVaultRepoFixture>
    {
        private readonly PairwiseVaultRepoFixture _fixture;

        public VaultServiceRepoTests(PairwiseVaultRepoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AddThenGetEntryRoundTripsViaRepository()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);
            _fixture.SetupPassthroughCrypto();

            var service = _fixture.CreateService();

            var entry = new VaultEntry
            {
                WebsiteName = "Example",
                Username = "alice",
                Password = "secret123",
                Url = "https://example.com",
                Notes = "test note",
                Category = "Social",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var addResult = await service.AddEntryAsync(entry);
            Assert.True(addResult.Success);

            var allEntries = await _fixture.Repository.GetAllEntriesAsync(userId);
            Assert.Single(allEntries);

            var storedEntity = allEntries[0];
            var getResult = await service.GetEntryAsync(storedEntity.Id.ToString());

            Assert.True(getResult.Success);
            Assert.Equal("Example", getResult.Value!.WebsiteName);
            Assert.Equal("alice", getResult.Value.Username);
            Assert.Equal("secret123", getResult.Value.Password);
            Assert.Equal("https://example.com", getResult.Value.Url);
            Assert.Equal("test note", getResult.Value.Notes);
            Assert.Equal("Social", getResult.Value.Category);
        }

        [Fact]
        public async Task AddMultipleEntriesThenGetAllReturnsAll()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);
            _fixture.SetupPassthroughCrypto();

            var service = _fixture.CreateService();

            var names = new[] { "SiteA", "SiteB", "SiteC" };
            foreach (var name in names)
            {
                var result = await service.AddEntryAsync(new VaultEntry
                {
                    WebsiteName = name,
                    Username = "user",
                    Password = "pass",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                Assert.True(result.Success);
            }

            var getAll = await service.GetAllEntriesAsync();

            Assert.True(getAll.Success);
            Assert.Equal(3, getAll.Value!.Count);
            Assert.Contains(getAll.Value, e => e.WebsiteName == "SiteA");
            Assert.Contains(getAll.Value, e => e.WebsiteName == "SiteB");
            Assert.Contains(getAll.Value, e => e.WebsiteName == "SiteC");
        }

        [Fact]
        public async Task DeleteEntryRemovesFromRepository()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);
            _fixture.SetupPassthroughCrypto();

            var service = _fixture.CreateService();

            var addResult = await service.AddEntryAsync(new VaultEntry
            {
                WebsiteName = "ToDelete",
                Username = "user",
                Password = "pass",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            Assert.True(addResult.Success);

            var allEntries = await _fixture.Repository.GetAllEntriesAsync(userId);
            Assert.Single(allEntries);
            var entryId = allEntries[0].Id;

            var deleteResult = await service.DeleteEntryAsync(entryId.ToString());
            Assert.True(deleteResult.Success);

            var getResult = await service.GetEntryAsync(entryId.ToString());
            Assert.False(getResult.Success);
            Assert.Equal("Entry not found", getResult.Message);
        }

        [Fact]
        public async Task GetEntryForNonExistentIdReturnsFail()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);
            _fixture.SetupPassthroughCrypto();

            var service = _fixture.CreateService();

            var result = await service.GetEntryAsync(Guid.NewGuid().ToString());

            Assert.False(result.Success);
            Assert.Equal("Entry not found", result.Message);
        }

        [Fact]
        public async Task EntriesFromDifferentUsersAreIsolated()
        {
            _fixture.Reset();
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();

            _fixture.SetupActiveSession(user1);
            _fixture.SetupPassthroughCrypto();

            var service = _fixture.CreateService();

            var addResult = await service.AddEntryAsync(new VaultEntry
            {
                WebsiteName = "User1Site",
                Username = "u1",
                Password = "p1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            Assert.True(addResult.Success);

            _fixture.Reset();
            _fixture.SetupActiveSession(user2);
            _fixture.SetupPassthroughCrypto();

            service = _fixture.CreateService();

            var getAll = await service.GetAllEntriesAsync();

            Assert.True(getAll.Success);
            Assert.Empty(getAll.Value!);
        }
    }
}

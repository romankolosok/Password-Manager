using PasswordManager.Core.Entities;
using PasswordManager.Tests.Fixtures;

namespace PasswordManager.Tests.Services
{
    [Collection("Supabase")]
    public class VaultRepositoryIntegrationTests : IClassFixture<SupabaseFixture>, IAsyncLifetime
    {
        private readonly SupabaseFixture _fixture;

        public VaultRepositoryIntegrationTests(SupabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await _fixture.AuthService.LockAsync();
        }

        [Fact]
        public async Task CreateUserProfileAsyncProfileExistsAfterRegistration()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;
            // handle_new_user_profile trigger should have created the profile row
            var profile = await _fixture.VaultRepository.GetUserProfileAsync(userId);
            Assert.NotNull(profile);
            Assert.Equal(userId, profile.Id);

            // salt and encrypted DEK were persisted
            Assert.False(string.IsNullOrWhiteSpace(profile.Salt), "Salt should not be empty");
            Assert.False(string.IsNullOrWhiteSpace(profile.EncryptedDEK),
                "EncryptedDEK should not be empty");
        }

        [Fact]
        public async Task CreateUserProfileAsyncWithDuplicateIdThrowsPostgrestException()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;
            // profile already exists from the trigger — inserting again should violate UserProfiles_pkey
            var existingProfile = await _fixture.VaultRepository.GetUserProfileAsync(userId);
            Assert.NotNull(existingProfile);

            var duplicateProfile = new UserProfileEntity
            {
                Id = userId,
                Salt = existingProfile.Salt,
                EncryptedDEK = existingProfile.EncryptedDEK,
                CreatedAt = DateTime.UtcNow
            };

            await Assert.ThrowsAsync<Supabase.Postgrest.Exceptions.PostgrestException>(
                () => _fixture.VaultRepository.CreateUserProfileAsync(duplicateProfile));
        }

        [Fact]
        public async Task GetUserProfileAsyncReturnsProfileWithCorrectSaltAndToken()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;
            var profile = await _fixture.VaultRepository.GetUserProfileAsync(userId);

            Assert.NotNull(profile);
            Assert.Equal(userId, profile.Id);

            Assert.False(string.IsNullOrWhiteSpace(profile.Salt), "Salt should not be empty");
            var saltBytes = Convert.FromBase64String(profile.Salt);
            Assert.Equal(16, saltBytes.Length);

            Assert.False(string.IsNullOrWhiteSpace(profile.EncryptedDEK),
                "EncryptedDEK should not be empty");
        }

        [Fact]
        public async Task GetUserProfileAsyncReturnsNullForNonExistentUser()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);

            var profile = await _fixture.VaultRepository.GetUserProfileAsync(Guid.NewGuid());

            Assert.Null(profile);
        }

        [Fact]
        public async Task GetUserProfileAsyncReturnsNullForAnotherUsersProfile()
        {
            var password = "IntegrationTest1!";

            var userAResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userAResult.Success, userAResult.Message);
            var (userIdA, emailA) = userAResult.Value;
            await _fixture.AuthService.LockAsync();

            var userBResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userBResult.Success, userBResult.Message);

            // user B's session is active, RLS should block reading user A's profile
            var profile = await _fixture.VaultRepository.GetUserProfileAsync(userIdA);

            Assert.Null(profile);
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsEmptyListWhenNoEntriesExist()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entries = await _fixture.VaultRepository.GetAllEntriesAsync(userId);

            Assert.NotNull(entries);
            Assert.Empty(entries);
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsAllEntriesOrderedByUpdatedAtDescending()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entryA = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "encrypted-a",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entryA);

            await Task.Delay(100);

            var entryB = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "encrypted-b",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entryB);

            var entries = await _fixture.VaultRepository.GetAllEntriesAsync(userId);

            Assert.Equal(2, entries.Count);
            Assert.Equal(entryB.Id, entries[0].Id);
            Assert.Equal(entryA.Id, entries[1].Id);
            Assert.True(entries[0].UpdatedAt >= entries[1].UpdatedAt,
                "Entries should be ordered by UpdatedAt descending");
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsEmptyListForAnotherUsersEntries()
        {
            var password = "IntegrationTest1!";

            var userAResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userAResult.Success, userAResult.Message);
            var (userIdA, emailA) = userAResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userIdA,
                EncryptedData = "encrypted-a-private",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entry);
            await _fixture.AuthService.LockAsync();

            var userBResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userBResult.Success, userBResult.Message);
            var (userIdB, emailB) = userBResult.Value;

            // user B's session is active, RLS should hide user A's entries
            var entries = await _fixture.VaultRepository.GetAllEntriesAsync(userIdB);

            Assert.NotNull(entries);
            Assert.Empty(entries);
        }

        [Fact]
        public async Task GetEntryAsyncReturnsNullWhenNoEntryExists()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entry = await _fixture.VaultRepository.GetEntryAsync(userId, Guid.NewGuid());

            Assert.Null(entry);
        }

        [Fact]
        public async Task GetEntryAsyncReturnsEntryWhenEntryExists()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "encrypted-data",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entry);

            var existingEntry = await _fixture.VaultRepository.GetEntryAsync(userId, entry.Id);

            Assert.NotNull(existingEntry);
            Assert.Equal(entry.Id, existingEntry.Id);
            Assert.Equal(entry.UserId, existingEntry.UserId);
            Assert.Equal(entry.EncryptedData, existingEntry.EncryptedData);
            Assert.True(existingEntry.CreatedAt != default, "CreatedAt should be populated");
        }

        [Fact]
        public async Task GetEntryAsyncReturnsNullForAnotherUsersEntry()
        {
            var password = "IntegrationTest1!";

            var userAResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userAResult.Success, userAResult.Message);
            var (userIdA, emailA) = userAResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userIdA,
                EncryptedData = "encrypted-a-private",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entry);
            await _fixture.AuthService.LockAsync();

            var userBResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userBResult.Success, userBResult.Message);
            var (userIdB, emailB) = userBResult.Value;

            // user B's session is active, RLS should hide user A's entries
            var privateEntry = await _fixture.VaultRepository.GetEntryAsync(userIdB, entry.Id);

            Assert.Null(privateEntry);
        }

        [Fact]
        public async Task UpsertEntryAsyncUpdatesExistingEntryDataAndUpdatedAt()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "original-data",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entry);

            var original = await _fixture.VaultRepository.GetEntryAsync(userId, entry.Id);
            Assert.NotNull(original);
            var originalUpdatedAt = original.UpdatedAt;

            await Task.Delay(1000);

            entry.EncryptedData = "updated-data";
            await _fixture.VaultRepository.UpsertEntryAsync(entry);

            var updated = await _fixture.VaultRepository.GetEntryAsync(userId, entry.Id);

            Assert.NotNull(updated);
            Assert.Equal("updated-data", updated.EncryptedData);
            Assert.True(updated.UpdatedAt > originalUpdatedAt,
                $"UpdatedAt {updated.UpdatedAt:O} should be later than original {originalUpdatedAt:O}");
        }

        [Fact]
        public async Task UpsertEntryAsyncSetsUpdatedAtToUtcNow()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "encrypted-timestamp",
                CreatedAt = DateTime.UtcNow
            };

            var beforeUpsert = DateTime.UtcNow;
            await _fixture.VaultRepository.UpsertEntryAsync(entry);
            var afterUpsert = DateTime.UtcNow;

            Assert.True(
                entry.UpdatedAt >= beforeUpsert && entry.UpdatedAt <= afterUpsert,
                $"UpdatedAt {entry.UpdatedAt:O} should be between {beforeUpsert:O} and {afterUpsert:O}");
        }

        [Fact]
        public async Task UpsertEntryAsyncWithAnotherUsersIdFailsRlsCheck()
        {
            var password = "IntegrationTest1!";

            var userAResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userAResult.Success, userAResult.Message);
            var (userIdA, emailA) = userAResult.Value;
            await _fixture.AuthService.LockAsync();

            var userBResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userBResult.Success, userBResult.Message);

            // user B's session is active, RLS WITH CHECK should reject inserting with user A's UserId
            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userIdA,
                EncryptedData = "encrypted-spoofed",
                CreatedAt = DateTime.UtcNow
            };

            await Assert.ThrowsAsync<Supabase.Postgrest.Exceptions.PostgrestException>(
                () => _fixture.VaultRepository.UpsertEntryAsync(entry));
        }

        [Fact]
        public async Task DeleteEntryAsyncDeletesExistingEntry()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "encrypted-data",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entry);

            var entries = await _fixture.VaultRepository.GetAllEntriesAsync(userId);

            Assert.Single(entries);

            await _fixture.VaultRepository.DeleteEntryAsync(userId, entry.Id);

            var entriesAfterDelete = await _fixture.VaultRepository.GetAllEntriesAsync(userId);
            Assert.NotNull(entriesAfterDelete);
            Assert.Empty(entriesAfterDelete);
        }

        [Fact]
        public async Task DeleteEntryAsyncDoesNotDeleteNonExistingEntry()
        {
            var password = "IntegrationTest1!";

            var userResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userResult.Success, userResult.Message);
            var (userId, email) = userResult.Value;

            var entry = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "encrypted-data",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entry);

            var entries = await _fixture.VaultRepository.GetAllEntriesAsync(userId);

            Assert.Single(entries);

            await _fixture.VaultRepository.DeleteEntryAsync(userId, Guid.NewGuid());

            var entriesAfterDelete = await _fixture.VaultRepository.GetAllEntriesAsync(userId);
            Assert.NotNull(entriesAfterDelete);
            Assert.Single(entriesAfterDelete);
            Assert.Equal(entry.Id, entriesAfterDelete[0].Id);
            Assert.Equal(entry.UserId, entriesAfterDelete[0].UserId);
            Assert.Equal(entry.EncryptedData, entriesAfterDelete[0].EncryptedData);
        }

        [Fact]
        public async Task DeleteEntryAsyncDoesNotDeleteAnotherUsersEntries()
        {
            var password = "IntegrationTest1!";

            var userAResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userAResult.Success, userAResult.Message);
            var (userIdA, emailA) = userAResult.Value;

            var entryA = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userIdA,
                EncryptedData = "encrypted-a-private",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entryA);
            await _fixture.AuthService.LockAsync();

            var userBResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(userBResult.Success, userBResult.Message);
            var (userIdB, emailB) = userBResult.Value;

            var entryB = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userIdB,
                EncryptedData = "encrypted-b-private",
                CreatedAt = DateTime.UtcNow
            };
            await _fixture.VaultRepository.UpsertEntryAsync(entryB);

            var userBEntries = await _fixture.VaultRepository.GetAllEntriesAsync(userIdB);
            Assert.Single(userBEntries);

            await _fixture.VaultRepository.DeleteEntryAsync(userIdB, entryA.Id);
            var userBEntriesAfterDelete = await _fixture.VaultRepository.GetAllEntriesAsync(userIdB);
            Assert.Single(userBEntriesAfterDelete);

            await _fixture.AuthService.LockAsync();
            var loginAAfterDelete = await _fixture.AuthService.LoginAsync(emailA, password);

            var userAEntriesAfterDelete = await _fixture.VaultRepository.GetAllEntriesAsync(userIdA);
            Assert.Single(userAEntriesAfterDelete);
        }
    }
}
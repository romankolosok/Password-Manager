using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fakes
{
    public class InMemoryVaultRepository : IVaultRepository
    {
        private readonly Dictionary<Guid, UserProfileEntity> _profiles = new();
        private readonly Dictionary<Guid, VaultEntryEntity> _entries = new();

        public Task CreateUserProfileAsync(UserProfileEntity profile)
        {
            if (_profiles.ContainsKey(profile.Id))
                throw new RepositoryException($"Duplicate key violation: profile {profile.Id} already exists");

            _profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public Task<UserProfileEntity?> GetUserProfileAsync(Guid userId)
        {
            _profiles.TryGetValue(userId, out var profile);
            return Task.FromResult(profile);
        }

        public Task UpdateUserProfileAsync(UserProfileEntity profile)
        {
            if (!_profiles.ContainsKey(profile.Id))
                throw new RepositoryException($"Profile {profile.Id} not found");

            _profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public Task<List<VaultEntryEntity>> GetAllEntriesAsync(Guid userId)
        {
            var entries = _entries.Values
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.UpdatedAt)
                .ToList();
            return Task.FromResult(entries);
        }

        public Task<VaultEntryEntity?> GetEntryAsync(Guid userId, Guid entryId)
        {
            _entries.TryGetValue(entryId, out var entry);
            if (entry != null && entry.UserId != userId)
                return Task.FromResult<VaultEntryEntity?>(null);
            return Task.FromResult(entry);
        }

        public Task UpsertEntryAsync(VaultEntryEntity entry)
        {
            entry.UpdatedAt = DateTime.UtcNow;
            _entries[entry.Id] = entry;
            return Task.CompletedTask;
        }

        public Task DeleteEntryAsync(Guid userId, Guid entryId)
        {
            if (_entries.TryGetValue(entryId, out var entry) && entry.UserId == userId)
                _entries.Remove(entryId);
            return Task.CompletedTask;
        }
    }
}

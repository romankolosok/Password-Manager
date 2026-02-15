#nullable enable
using PasswordManager.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface IVaultRepository
    {
        public Task<UserEntity?> GetUserByEmailAsync(string email);

        public Task CreateUserAsync(UserEntity user);

        /// <param name="userId">User's Id (Guid); in Cosmos this was the partition key string.</param>
        public Task<List<VaultEntryEntity>> GetAllEntriesAsync(Guid userId);

        /// <param name="userId">User's Id.</param>
        /// <param name="entryId">Vault entry's Id.</param>
        public Task<VaultEntryEntity?> GetEntryAsync(Guid userId, Guid entryId);

        public Task UpsertEntryAsync(VaultEntryEntity entry);

        /// <param name="userId">User's Id.</param>
        /// <param name="entryId">Vault entry's Id.</param>
        public Task DeleteEntryAsync(Guid userId, Guid entryId);
    }
}

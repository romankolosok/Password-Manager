#nullable enable
using PasswordManager.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface IVaultRepository
    {
        Task<UserProfileEntity?> GetUserProfileAsync(Guid userId);

        Task CreateUserProfileAsync(UserProfileEntity profile);

        Task<List<VaultEntryEntity>> GetAllEntriesAsync(Guid userId);

        Task<VaultEntryEntity?> GetEntryAsync(Guid userId, Guid entryId);

        Task UpsertEntryAsync(VaultEntryEntity entry);

        Task DeleteEntryAsync(Guid userId, Guid entryId);
    }
}

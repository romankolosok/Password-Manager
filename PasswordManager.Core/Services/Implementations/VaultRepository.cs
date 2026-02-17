using PasswordManager.Core.Entities;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class VaultRepository : IVaultRepository
    {
        private readonly Supabase.Client _supabase;

        public VaultRepository(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task CreateUserProfileAsync(UserProfileEntity profile)
        {
            var options = new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal };
            await _supabase.From<UserProfileEntity>().Insert(profile, options);
        }

        public async Task DeleteEntryAsync(Guid userId, Guid entryId)
        {
            await _supabase
                .From<VaultEntryEntity>()
                .Where(x => x.Id == entryId)
                .Delete();
        }

        public async Task<List<VaultEntryEntity>> GetAllEntriesAsync(Guid userId)
        {
            var response = await _supabase
                .From<VaultEntryEntity>()
                .Order(x => x.UpdatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            return response.Models;
        }

        public async Task<VaultEntryEntity?> GetEntryAsync(Guid userId, Guid entryId)
        {
            try
            {
                var response = await _supabase
                    .From<VaultEntryEntity>()
                    .Where(x => x.Id == entryId)
                    .Single();

                return response;
            }
            catch (InvalidOperationException)
            {
                // Single() throws when no record is found
                return null;
            }
        }

        public async Task<UserProfileEntity?> GetUserProfileAsync(Guid userId)
        {
            try
            {
                var response = await _supabase
                    .From<UserProfileEntity>()
                    .Where(x => x.Id == userId)
                    .Single();

                return response;
            }
            catch (InvalidOperationException)
            {
                // Single() throws when no record is found
                return null;
            }
        }

        public async Task UpsertEntryAsync(VaultEntryEntity entry)
        {
            entry.UpdatedAt = DateTime.UtcNow;
            await _supabase.From<VaultEntryEntity>().Upsert(entry);
        }
    }
}
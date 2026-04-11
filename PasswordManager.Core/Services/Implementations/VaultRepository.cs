using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Interfaces;
using Supabase.Postgrest.Exceptions;
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
            try
            {
                var options = new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal };
                await _supabase.From<UserProfileEntity>().Insert(profile, options);
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
            }
        }

        public async Task DeleteEntryAsync(Guid userId, Guid entryId)
        {
            try
            {
                await _supabase
                    .From<VaultEntryEntity>()
                    .Where(x => x.Id == entryId)
                    .Delete();
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
            }
        }

        public async Task<List<VaultEntryEntity>> GetAllEntriesAsync(Guid userId)
        {
            try
            {
                var response = await _supabase
                    .From<VaultEntryEntity>()
                    .Order(x => x.UpdatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                return response.Models;
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
            }
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
                return null;
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
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
                return null;
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
            }
        }

        public async Task UpsertEntryAsync(VaultEntryEntity entry)
        {
            try
            {
                entry.UpdatedAt = DateTime.UtcNow;
                await _supabase.From<VaultEntryEntity>().Upsert(entry);
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
            }
        }

        public async Task UpdateUserProfileAsync(UserProfileEntity profile)
        {
            try
            {
                var options = new Supabase.Postgrest.QueryOptions
                {
                    Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
                };
                await _supabase.From<UserProfileEntity>()
                    .Where(x => x.Id == profile.Id)
                    .Update(profile, options);
            }
            catch (PostgrestException ex)
            {
                throw new RepositoryException(ex.Message, ex);
            }
        }
    }
}

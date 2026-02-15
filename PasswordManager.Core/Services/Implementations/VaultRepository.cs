using Microsoft.EntityFrameworkCore;
using PasswordManager.Core.Data;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class VaultRepository : IVaultRepository
    {
        private readonly VaultDbContext _dbContext;

        public VaultRepository(VaultDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task CreateUserAsync(Entities.UserEntity user)
        {
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
        }
        public async Task DeleteEntryAsync(Guid userId, Guid entryId)
        {
            var existingEntry = await _dbContext.VaultEntries.FirstOrDefaultAsync(entry =>
                entry.UserId == userId &&
                entry.Id == entryId
            );

            if (existingEntry != null)
            {
                _dbContext.VaultEntries.Remove(existingEntry);
                await _dbContext.SaveChangesAsync();
            }
        }
        public async Task<List<Entities.VaultEntryEntity>> GetAllEntriesAsync(Guid userId)
        {
            return await _dbContext.VaultEntries
                .Where(entry => entry.UserId == userId)
                .OrderByDescending(e => e.UpdatedAt)
                .ToListAsync();
        }
        public async Task<Entities.UserEntity?> GetUserByEmailAsync(string email)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(user => user.Email == email);
        }
        public async Task<Entities.VaultEntryEntity?> GetEntryAsync(Guid userId, Guid entryId)
        {
            return await _dbContext.VaultEntries.FirstOrDefaultAsync(entry =>
                entry.UserId == userId &&
                entry.Id == entryId
            );
        }
        public async Task UpsertEntryAsync(Entities.VaultEntryEntity entry)
        {
            var existingEntry = await _dbContext.VaultEntries.FindAsync(entry.Id);

            if (existingEntry == null)
            {
                _dbContext.VaultEntries.Add(entry);
            }
            else
            {
                existingEntry.EncryptedData = entry.EncryptedData;
                existingEntry.UpdatedAt = DateTime.UtcNow;
                _dbContext.VaultEntries.Update(existingEntry);
            }
        }
    }
}

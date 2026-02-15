using PasswordManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface IVaultService
    {
        public Task<Result<List<VaultEntry>>> GetAllEntriesAsync();

        public Task<Result<VaultEntry>> GetEntryAsync(string id);

        public Task<Models.Result> AddEntryAsync(VaultEntry entry);

        public Task<Models.Result> DeleteEntryAsync(string entryId);

        public List<VaultEntry> SearchEntries(string query, List<VaultEntry> entries);
    }
}

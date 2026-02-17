using Microsoft.Extensions.Logging;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class VaultService : IVaultService
    {
        private readonly ICryptoService _cryptoService;
        private readonly ISessionService _sessionService;
        private readonly IVaultRepository _vaultRepository;
        private readonly ILogger<VaultService> _logger;

        public VaultService(ICryptoService crypto,
            ISessionService session,
            IVaultRepository repository,
            ILogger<VaultService> logger)
        {
            _cryptoService = crypto;
            _sessionService = session;
            _vaultRepository = repository;
            _logger = logger;
        }

        public async Task<Result<List<VaultEntry>>> GetAllEntriesAsync()
        {
            if (!_sessionService.IsActive())
                return Result<List<VaultEntry>>.Fail("Vault is locked");

            // RLS handles user filtering automatically via auth.uid()
            List<VaultEntryEntity> entities = await _vaultRepository.GetAllEntriesAsync(_sessionService.CurrentUserId!.Value);
            byte[] key = _sessionService.GetDerivedKey();
            var decryptedEntries = new List<VaultEntry>();

            foreach (VaultEntryEntity entity in entities)
            {
                Result<VaultEntry>? entryResult = TryDecryptEntry(entity, key);
                if (entryResult != null && entryResult.Success)
                    decryptedEntries.Add(entryResult.Value!);
                // On failure we skip this entry
            }

            return Result<List<VaultEntry>>.Ok(decryptedEntries);
        }

        public async Task<Result<VaultEntry>> GetEntryAsync(string id)
        {
            if (!_sessionService.IsActive())
                return Result<VaultEntry>.Fail("Vault is locked");

            if (!Guid.TryParse(id, out Guid entryId))
                return Result<VaultEntry>.Fail("Invalid entry id");

            try
            {
                VaultEntryEntity? entity = await _vaultRepository.GetEntryAsync(_sessionService.CurrentUserId!.Value, entryId);
                if (entity == null)
                    return Result<VaultEntry>.Fail("Entry not found");

                byte[] key = _sessionService.GetDerivedKey();
                Result<VaultEntry>? entryResult = TryDecryptEntry(entity, key);
                if (entryResult != null && entryResult.Success)
                    return Result<VaultEntry>.Ok(entryResult.Value!);

                return Result<VaultEntry>.Fail("Failed to decrypt entry");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vault entry {EntryId}", entryId);
                return Result<VaultEntry>.Fail("Failed to retrieve entry");
            }
        }

        public async Task<Result> AddEntryAsync(VaultEntry entry)
        {
            if (!_sessionService.IsActive())
                return Result.Fail("Vault is locked");

            Guid? userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result.Fail("No user logged in");

            bool isNew = entry.Id == Guid.Empty || entry.CreatedAt == default;
            Guid effectiveId = isNew ? Guid.NewGuid() : entry.Id;
            DateTime effectiveCreated = isNew ? DateTime.UtcNow : entry.CreatedAt;
            DateTime effectiveUpdated = DateTime.UtcNow;

            // Convert VaultEntry to payload and serialize using extension methods
            VaultEntryPayload payload = entry.ToPayload();
            string json = payload.ToJson();

            Result<EncryptedBlob> encryptResult = _cryptoService.Encrypt(json, _sessionService.GetDerivedKey());
            if (!encryptResult.Success)
                return Result.Fail(encryptResult.Message ?? "Failed to encrypt entry");

            var entity = new VaultEntryEntity
            {
                Id = effectiveId,
                UserId = userId.Value,
                EncryptedData = encryptResult.Value.ToBase64String(),
                CreatedAt = effectiveCreated,
                UpdatedAt = effectiveUpdated
            };

            try
            {
                await _vaultRepository.UpsertEntryAsync(entity);
                return Result.Ok();
            }
            catch (Exception)
            {
                return Result.Fail("Failed to save entry");
            }
        }

        public async Task<Result> DeleteEntryAsync(string entryId)
        {
            if (!_sessionService.IsActive())
                return Result.Fail("Vault is locked");

            if (!Guid.TryParse(entryId, out Guid id))
                return Result.Fail("Invalid entry id");

            // RLS handles user filtering automatically - user can only delete their own entries
            try
            {
                await _vaultRepository.DeleteEntryAsync(_sessionService.CurrentUserId!.Value, id);
                return Result.Ok();
            }
            catch (Exception)
            {
                return Result.Fail("Failed to delete entry");
            }
        }

        public List<VaultEntry> SearchEntries(string query, List<VaultEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(query))
                return entries;

            string lowerQuery = query.Trim().ToLowerInvariant();

            return entries.Where(e =>
                (e.WebsiteName != null && e.WebsiteName.ToLowerInvariant().Contains(lowerQuery)) ||
                (e.Username != null && e.Username.ToLowerInvariant().Contains(lowerQuery)) ||
                (e.Url != null && e.Url.ToLowerInvariant().Contains(lowerQuery)) ||
                (e.Notes != null && e.Notes.ToLowerInvariant().Contains(lowerQuery)) ||
                (e.Category != null && e.Category.ToLowerInvariant().Contains(lowerQuery))
            ).ToList();
        }

        /// <summary>
        /// Decrypts entity and maps to VaultEntry. Returns null on failure.
        /// </summary>
        private Result<VaultEntry>? TryDecryptEntry(VaultEntryEntity entity, byte[] key)
        {
            Result<EncryptedBlob> parseResult = EncryptedBlob.FromBase64String(entity.EncryptedData);
            if (!parseResult.Success)
                return null;

            Result<string> decryptResult = _cryptoService.Decrypt(parseResult.Value, key);
            if (!decryptResult.Success)
                return null;

            VaultEntryPayload? payload = VaultEntryPayload.FromJson(decryptResult.Value);
            if (payload == null)
                return null;

            // Use extension method with entity directly
            VaultEntry entry = payload.ToVaultEntry(entity);
            return Result<VaultEntry>.Ok(entry);
        }
    }
}
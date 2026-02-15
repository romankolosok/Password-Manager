using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    /// <summary>
    /// Payload stored in EncryptedData (JSON). Id, CreatedAt, UpdatedAt are on VaultEntryEntity only.
    /// </summary>
    internal class VaultEntryPayload
    {
        public string WebsiteName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Url { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsFavorite { get; set; }
    }

    public class VaultService : IVaultService
    {
        private readonly ICryptoService _cryptoService;
        private readonly ISessionService _sessionService;
        private readonly IVaultRepository _vaultRepository;

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        public VaultService(ICryptoService crypto,
            ISessionService session,
            IVaultRepository repository)
        {
            _cryptoService = crypto;
            _sessionService = session;
            _vaultRepository = repository;
        }

        public async Task<Result<List<VaultEntry>>> GetAllEntriesAsync()
        {
            if (!_sessionService.IsActive())
                return Result<List<VaultEntry>>.Fail("Vault is locked");

            Guid? userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result<List<VaultEntry>>.Fail("No user logged in");

            List<VaultEntryEntity> entities = await _vaultRepository.GetAllEntriesAsync(userId.Value);
            byte[] key = _sessionService.GetDerivedKey();
            var decryptedEntries = new List<VaultEntry>();

            foreach (VaultEntryEntity entity in entities)
            {
                Result<VaultEntry>? entryResult = TryDecryptEntry(entity, key);
                if (entryResult != null && entryResult.Success)
                    decryptedEntries.Add(entryResult.Value!);
                // On failure we skip this entry (log could be added here)
            }

            return Result<List<VaultEntry>>.Ok(decryptedEntries);
        }

        public async Task<Result<VaultEntry>> GetEntryAsync(string id)
        {
            if (!_sessionService.IsActive())
                return Result<VaultEntry>.Fail("Vault is locked");

            if (!Guid.TryParse(id, out Guid entryId))
                return Result<VaultEntry>.Fail("Invalid entry id");

            Guid? userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result<VaultEntry>.Fail("No user logged in");

            VaultEntryEntity? entity = await _vaultRepository.GetEntryAsync(userId.Value, entryId);
            if (entity == null)
                return Result<VaultEntry>.Fail("Entry not found");

            byte[] key = _sessionService.GetDerivedKey();
            Result<VaultEntry>? entryResult = TryDecryptEntry(entity, key);
            if (entryResult != null && entryResult.Success)
                return Result<VaultEntry>.Ok(entryResult.Value!);

            return Result<VaultEntry>.Fail("Failed to decrypt entry");
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

            string json = SerializePayload(entry);
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

            Guid? userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result.Fail("No user logged in");

            try
            {
                await _vaultRepository.DeleteEntryAsync(userId.Value, id);
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
        /// Decrypts entity and maps to VaultEntry. Returns null on failure (caller can skip or log).
        /// </summary>
        private Result<VaultEntry>? TryDecryptEntry(VaultEntryEntity entity, byte[] key)
        {
            Result<EncryptedBlob> parseResult = EncryptedBlob.FromBase64String(entity.EncryptedData);
            if (!parseResult.Success)
                return null;

            Result<string> decryptResult = _cryptoService.Decrypt(parseResult.Value, key);
            if (!decryptResult.Success)
                return null;

            VaultEntryPayload? payload = DeserializePayload(decryptResult.Value);
            if (payload == null)
                return null;

            var entry = new VaultEntry
            {
                Id = entity.Id,
                WebsiteName = payload.WebsiteName ?? "",
                Username = payload.Username ?? "",
                Password = payload.Password ?? "",
                Url = payload.Url ?? "",
                Notes = payload.Notes ?? "",
                Category = payload.Category ?? "",
                IsFavorite = payload.IsFavorite,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
            return Result<VaultEntry>.Ok(entry);
        }

        private static string SerializePayload(VaultEntry entry)
        {
            var payload = new VaultEntryPayload
            {
                WebsiteName = entry.WebsiteName ?? "",
                Username = entry.Username ?? "",
                Password = entry.Password ?? "",
                Url = entry.Url ?? "",
                Notes = entry.Notes ?? "",
                Category = entry.Category ?? "",
                IsFavorite = entry.IsFavorite
            };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static VaultEntryPayload? DeserializePayload(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<VaultEntryPayload>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}

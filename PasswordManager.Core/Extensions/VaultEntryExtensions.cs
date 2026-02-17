using PasswordManager.Core.Entities;
using System;

namespace PasswordManager.Core.Models
{
    /// <summary>
    /// Extension methods for converting between VaultEntry and VaultEntryPayload.
    /// </summary>
    internal static class VaultEntryExtensions
    {
        /// <summary>
        /// Converts a VaultEntry to a VaultEntryPayload (excludes Id, CreatedAt, UpdatedAt).
        /// </summary>
        public static VaultEntryPayload ToPayload(this VaultEntry entry)
        {
            return new VaultEntryPayload(
                WebsiteName: entry.WebsiteName ?? "",
                Username: entry.Username ?? "",
                Password: entry.Password ?? "",
                Url: entry.Url ?? "",
                Notes: entry.Notes ?? "",
                Category: entry.Category ?? "",
                IsFavorite: entry.IsFavorite
            );
        }

        /// <summary>
        /// Converts a VaultEntryPayload to a VaultEntry with metadata from entity.
        /// </summary>
        public static VaultEntry ToVaultEntry(this VaultEntryPayload payload, Guid id, DateTime createdAt, DateTime updatedAt)
        {
            return new VaultEntry
            {
                Id = id,
                WebsiteName = payload.WebsiteName ?? "",
                Username = payload.Username ?? "",
                Password = payload.Password ?? "",
                Url = payload.Url ?? "",
                Notes = payload.Notes ?? "",
                Category = payload.Category ?? "",
                IsFavorite = payload.IsFavorite,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
        }

        /// <summary>
        /// Converts a VaultEntryPayload to a VaultEntry using metadata from VaultEntryEntity.
        /// </summary>
        public static VaultEntry ToVaultEntry(this VaultEntryPayload payload, VaultEntryEntity entity)
        {
            return payload.ToVaultEntry(entity.Id, entity.CreatedAt, entity.UpdatedAt);
        }
    }
}
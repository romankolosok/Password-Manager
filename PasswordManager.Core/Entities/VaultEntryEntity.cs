using System;

namespace PasswordManager.Core.Entities
{
    /// <summary>
    /// Vault entry row in PostgreSQL. UserId is a foreign key to UserEntity.Id (enforced by the database).
    /// </summary>
    public class VaultEntryEntity
    {
        /// <summary>Primary key. PostgreSQL native UUID.</summary>
        public Guid Id { get; init; }

        /// <summary>Foreign key to Users.Id. Indexed for fast "all entries for this user" queries.</summary>
        public Guid UserId { get; set; }

        /// <summary>Base64 encrypted payload.</summary>
        public string EncryptedData { get; set; } = string.Empty;

        /// <summary>Store and pass as UTC; PostgreSQL column type is timestamptz.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Store and pass as UTC; PostgreSQL column type is timestamptz.</summary>
        public DateTime UpdatedAt { get; set; }
    }
}

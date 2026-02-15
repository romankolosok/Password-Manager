using System;

namespace PasswordManager.Core.Entities
{
    /// <summary>
    /// User row in PostgreSQL (relational). Id is the user identifier; no separate partition key like Cosmos DB.
    /// </summary>
    public class UserEntity
    {
        /// <summary>Primary key. PostgreSQL native UUID; was string in Cosmos for document ID.</summary>
        public Guid Id { get; init; }

        /// <summary>Unique login identifier. Enforced unique in DB (Cosmos could not enforce this).</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Base64 salt for password hashing.</summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>Base64 encrypted verification token.</summary>
        public string EncryptedVerificationToken { get; set; } = string.Empty;

        /// <summary>Store and pass as UTC; PostgreSQL column type is timestamptz.</summary>
        public DateTime CreatedAt { get; init; }
    }
}

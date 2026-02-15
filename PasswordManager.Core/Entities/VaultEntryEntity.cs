using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace PasswordManager.Core.Entities
{
    /// <summary>
    /// Vault entry row. UserId references auth.users(id). RLS restricts to current user.
    /// </summary>
    [Table("VaultEntries")]
    public class VaultEntryEntity : BaseModel
    {
        [PrimaryKey("Id", false)]
        public Guid Id { get; set; }

        [Column("UserId")]
        public Guid UserId { get; set; }

        [Column("EncryptedData")]
        public string EncryptedData { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}

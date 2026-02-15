using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace PasswordManager.Core.Entities
{
    /// <summary>
    /// Row in the UserProfiles table. Id matches Supabase Auth user UUID; email is in auth.users.
    /// </summary>
    [Table("UserProfiles")]
    public class UserProfileEntity : BaseModel
    {
        [PrimaryKey("Id", false)]
        public Guid Id { get; set; }

        [Column("Salt")]
        public string Salt { get; set; } = string.Empty;

        [Column("EncryptedVerificationToken")]
        public string EncryptedVerificationToken { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }
}

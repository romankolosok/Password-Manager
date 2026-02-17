using System.Text.Json;

namespace PasswordManager.Core.Models
{
    /// <summary>
    /// Payload stored in EncryptedData (JSON). Id, CreatedAt, UpdatedAt are stored in VaultEntryEntity.
    /// </summary>
    internal record VaultEntryPayload(
        string WebsiteName,
        string Username,
        string Password,
        string Url,
        string Notes,
        string Category,
        bool IsFavorite)
    {
        /// <summary>
        /// Serializes this payload to JSON.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });
        }

        /// <summary>
        /// Deserializes a VaultEntryPayload from JSON.
        /// </summary>
        public static VaultEntryPayload? FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<VaultEntryPayload>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
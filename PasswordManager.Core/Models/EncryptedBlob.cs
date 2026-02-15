using PasswordManager.Core.Validators;
using System;
using System.Linq;

namespace PasswordManager.Core.Models
{
    public class EncryptedBlob
    {
        public byte[] Nonce { get; set; } = new byte[12];
        public byte[] Ciphertext { get; set; }
        public byte[] Tag { get; set; } = new byte[16];

        public String ToBase64String()
        {
            byte[] concat = Nonce.Concat(Ciphertext).Concat(Tag).ToArray();

            return Convert.ToBase64String(concat);
        }

        /// <summary>
        /// Parses a base64-encoded blob (nonce + ciphertext + tag). Uses a validator instead of throwing;
        /// returns Result so callers can handle invalid format like other crypto operations (Encrypt/Decrypt).
        /// </summary>
        public static Result<EncryptedBlob> FromBase64String(string base64)
        {
            var validator = new EncryptedBlobFormatValidator();
            var input = new EncryptedBlobParseInput { Base64 = base64 ?? string.Empty };
            var validationResult = validator.Validate(input);

            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<EncryptedBlob>.Fail(errors);
            }

            byte[] concat = Convert.FromBase64String(base64!);
            var blob = new EncryptedBlob
            {
                Nonce = concat.Take(12).ToArray(),
                Ciphertext = concat.Skip(12).Take(concat.Length - 28).ToArray(),
                Tag = concat.Skip(concat.Length - 16).Take(16).ToArray()
            };

            return Result<EncryptedBlob>.Ok(blob);
        }
    }
}

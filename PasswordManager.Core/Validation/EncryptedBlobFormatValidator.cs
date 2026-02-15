using FluentValidation;
using System;

namespace PasswordManager.Core.Validators
{
    /// <summary>
    /// Input for parsing a base64-encoded encrypted blob (e.g. from storage).
    /// </summary>
    internal class EncryptedBlobParseInput
    {
        public string Base64 { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates a string that should be a base64-encoded EncryptedBlob (nonce + ciphertext + tag).
    /// Replaces throwing from EncryptedBlob.FromBase64String so callers get Result instead of exceptions.
    /// </summary>
    internal class EncryptedBlobFormatValidator : AbstractValidator<EncryptedBlobParseInput>
    {
        private const int MinBlobLength = 12 + 0 + 16; // nonce + min ciphertext + tag = 28

        public EncryptedBlobFormatValidator()
        {
            RuleFor(x => x.Base64)
                .NotEmpty()
                .WithMessage("Encrypted data cannot be null or empty.");

            RuleFor(x => x.Base64)
                .Must(BeValidBase64EncryptedBlob)
                .WithMessage("Invalid encrypted blob format. Expected valid Base64 with at least nonce (12 bytes) and tag (16 bytes).");
        }

        private static bool BeValidBase64EncryptedBlob(string? base64)
        {
            if (string.IsNullOrEmpty(base64))
                return false;

            try
            {
                byte[] concat = Convert.FromBase64String(base64);
                return concat.Length >= MinBlobLength;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}

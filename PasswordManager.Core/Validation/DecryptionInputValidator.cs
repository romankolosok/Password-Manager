using FluentValidation;
using PasswordManager.Core.Models;

namespace PasswordManager.Core.Validators
{
    internal class DecryptionInput
    {
        public EncryptedBlob Blob { get; set; }
        public byte[] Key { get; set; }
    }

    internal class DecryptionInputValidator : AbstractValidator<DecryptionInput>
    {
        private const int KEY_SIZE = 32;
        private const int NONCE_SIZE = 12;
        private const int TAG_SIZE = 16;

        public DecryptionInputValidator()
        {
            RuleFor(x => x.Blob)
                .NotNull()
                .WithMessage("Encrypted blob cannot be null.");

            RuleFor(x => x.Blob.Nonce)
                .Must(nonce => nonce != null && nonce.Length == NONCE_SIZE)
                .When(x => x.Blob != null)
                .WithMessage($"Nonce must be exactly {NONCE_SIZE} bytes.");

            RuleFor(x => x.Blob.Ciphertext)
                .NotEmpty()
                .When(x => x.Blob != null)
                .WithMessage("Ciphertext cannot be null or empty.");

            RuleFor(x => x.Blob.Tag)
                .Must(tag => tag != null && tag.Length == TAG_SIZE)
                .When(x => x.Blob != null)
                .WithMessage($"Tag must be exactly {TAG_SIZE} bytes.");

            RuleFor(x => x.Key)
                .NotNull()
                .WithMessage("Decryption key cannot be null.")
                .Must(key => key.Length == KEY_SIZE)
                .WithMessage($"Decryption key must be exactly {KEY_SIZE} bytes.");
        }
    }
}
using FluentValidation;

namespace PasswordManager.Core.Validators
{
    internal class EncryptionInput
    {
        public string Plaintext { get; set; }
        public byte[] Key { get; set; }
    }

    internal class EncryptionInputValidator : AbstractValidator<EncryptionInput>
    {
        private const int KEY_SIZE = 32;

        public EncryptionInputValidator()
        {
            RuleFor(x => x.Plaintext)
                .NotEmpty()
                .WithMessage("Plaintext cannot be null or empty.");

            RuleFor(x => x.Key)
                .NotNull()
                .WithMessage("Encryption key cannot be null.")
                .Must(key => key.Length == KEY_SIZE)
                .WithMessage($"Encryption key must be exactly {KEY_SIZE} bytes.");
        }
    }
}
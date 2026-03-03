using FluentValidation;
using PasswordManager.Core.Models;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PasswordManager.Core.Validators
{
    /// <summary>
    /// Input for validating a password (master password, vault entry password, etc.).
    /// </summary>
    internal class PasswordInput
    {
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates password strength. Reusable for master password (register, login, change password),
    /// vault entry passwords, or any password that should match <see cref="PasswordPolicy"/> and
    /// <see cref="T:PasswordManager.Core.Services.Interfaces.IPasswordGenerator"/> output.
    /// </summary>
    internal class PasswordValidator : AbstractValidator<PasswordInput>
    {
        public PasswordValidator()
        {
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password cannot be empty.");

            RuleFor(x => x.Password)
                .MinimumLength(PasswordPolicy.MinLength)
                .WithMessage($"Password must be at least {PasswordPolicy.MinLength} characters.");

            RuleFor(x => x.Password)
                .MaximumLength(PasswordPolicy.MaxLength)
                .WithMessage($"Password must not exceed {PasswordPolicy.MaxLength} characters.");

            RuleFor(x => x.Password)
                .Must(ContainsUppercase)
                .WithMessage("Password must contain at least one uppercase letter.")
                .When(x => !string.IsNullOrEmpty(x.Password) && x.Password.Length >= PasswordPolicy.MinLength);

            RuleFor(x => x.Password)
                .Must(ContainsLowercase)
                .WithMessage("Password must contain at least one lowercase letter.")
                .When(x => !string.IsNullOrEmpty(x.Password) && x.Password.Length >= PasswordPolicy.MinLength);

            RuleFor(x => x.Password)
                .Must(ContainsDigit)
                .WithMessage("Password must contain at least one digit.")
                .When(x => !string.IsNullOrEmpty(x.Password) && x.Password.Length >= PasswordPolicy.MinLength);

            RuleFor(x => x.Password)
                .Must(ContainsSpecialCharacter)
                .WithMessage($"Password must contain at least one special character (e.g. from: {PasswordPolicy.SpecialCharacters}).")
                .When(x => !string.IsNullOrEmpty(x.Password) && x.Password.Length >= PasswordPolicy.MinLength);
        }

        [ExcludeFromCodeCoverage]
        private static bool ContainsUppercase(string? value) =>
            !string.IsNullOrEmpty(value) && value.Any(char.IsUpper);

        [ExcludeFromCodeCoverage]
        private static bool ContainsLowercase(string? value) =>
            !string.IsNullOrEmpty(value) && value.Any(char.IsLower);

        [ExcludeFromCodeCoverage]
        private static bool ContainsDigit(string? value) =>
            !string.IsNullOrEmpty(value) && value.Any(char.IsDigit);

        [ExcludeFromCodeCoverage]
        private static bool ContainsSpecialCharacter(string? value) =>
            !string.IsNullOrEmpty(value) && value.Any(c => PasswordPolicy.SpecialCharacters.Contains(c));
    }
}

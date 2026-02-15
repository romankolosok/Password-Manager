using FluentValidation;
using PasswordManager.Core.Models;

namespace PasswordManager.Core.Validators
{
    internal class PasswordOptionsValidator : AbstractValidator<PasswordOptions>
    {
        public PasswordOptionsValidator()
        {
            RuleFor(x => x.Length)
                .InclusiveBetween(PasswordPolicy.MinLength, PasswordPolicy.MaxLength)
                .WithMessage($"Password length must be between {PasswordPolicy.MinLength} and {PasswordPolicy.MaxLength} characters.");

            RuleFor(x => x)
                .Must(x => x.IncludeUppercase || x.IncludeLowercase || x.IncludeDigits || x.IncludeSpecialCharacters)
                .WithMessage("At least one character set must be selected.");
        }
    }
}
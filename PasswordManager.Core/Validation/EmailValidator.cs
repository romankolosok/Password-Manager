using FluentValidation;
using System.Text.RegularExpressions;

namespace PasswordManager.Core.Validators
{
    /// <summary>
    /// Input for auth operations that require an email (register, login).
    /// </summary>
    public class EmailInput
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates email for registration and login. Use in RegisterAsync, LoginAsync, etc.
    /// </summary>
    public class EmailValidator : AbstractValidator<EmailInput>
    {
        private const int MaxEmailLength = 256;

        private readonly Regex EmailRegex =
            new Regex("^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$");

        public EmailValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email cannot be empty.");

            RuleFor(x => x.Email)
                .MaximumLength(MaxEmailLength)
                .WithMessage($"Email must not exceed {MaxEmailLength} characters.");

            RuleFor(x => x.Email)
                .Matches(EmailRegex)
                .WithMessage("Email must be a valid email address.")
                .When(x => !string.IsNullOrEmpty(x.Email));
        }
    }
}

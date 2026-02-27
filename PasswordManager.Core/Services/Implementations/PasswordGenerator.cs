using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace PasswordManager.Core.Services.Implementations
{
    /// <summary>
    /// Generates random passwords from options. Uses <see cref="PasswordOptionsValidator"/> for input validation
    /// and produces passwords compatible with <see cref="PasswordPolicy"/> so they pass <see cref="PasswordValidator"/>.
    /// </summary>
    public class PasswordGenerator : IPasswordGenerator
    {
        private readonly PasswordOptionsValidator _optionsValidator = new();

        /// <inheritdoc />
        public Result<string> Generate(PasswordOptions options)
        {
            var validationResult = _optionsValidator.Validate(options);

            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<string>.Fail(errors);
            }

            // Define character sets (special set aligned with PasswordPolicy so generated passwords pass validation)
            const string UPPERCASE = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string LOWERCASE = "abcdefghijklmnopqrstuvwxyz";
            const string DIGITS = "0123456789";
            const string SYMBOLS = PasswordPolicy.SpecialCharacters;
            const string AMBIGUOUS = "0OolI1|`'\"";

            // Build character pool using string concatenation (avoid StringBuilder for security)
            string characterPool = string.Empty;
            if (options.IncludeUppercase) characterPool += UPPERCASE;
            if (options.IncludeLowercase) characterPool += LOWERCASE;
            if (options.IncludeDigits) characterPool += DIGITS;
            if (options.IncludeSpecialCharacters) characterPool += SYMBOLS;

            if (options.ExcludeAmbiguousCharacters)
            {
                characterPool = new string(characterPool.Where(c => !AMBIGUOUS.Contains(c)).ToArray());
            }

            if (characterPool.Length == 0)
                return Result<string>.Fail("At least one character type must be included.");

            char[] poolArray = characterPool.ToCharArray();
            char[] passwordChars = new char[options.Length];

            try
            {
                // Fill password array with random characters
                for (int i = 0; i < options.Length; i++)
                {
                    int index = RandomNumberGenerator.GetInt32(poolArray.Length);
                    passwordChars[i] = poolArray[index];
                }

                // Ensure required character types are present by replacing random positions
                if (options.IncludeUppercase && !passwordChars.Any(char.IsUpper))
                {
                    string validChars = options.ExcludeAmbiguousCharacters
                        ? new string(UPPERCASE.Where(c => !AMBIGUOUS.Contains(c)).ToArray())
                        : UPPERCASE;
                    int position = RandomNumberGenerator.GetInt32(options.Length);
                    int charIndex = RandomNumberGenerator.GetInt32(validChars.Length);
                    passwordChars[position] = validChars[charIndex];
                }

                if (options.IncludeLowercase && !passwordChars.Any(char.IsLower))
                {
                    string validChars = options.ExcludeAmbiguousCharacters
                        ? new string(LOWERCASE.Where(c => !AMBIGUOUS.Contains(c)).ToArray())
                        : LOWERCASE;
                    int position = RandomNumberGenerator.GetInt32(options.Length);
                    int charIndex = RandomNumberGenerator.GetInt32(validChars.Length);
                    passwordChars[position] = validChars[charIndex];
                }

                if (options.IncludeDigits && !passwordChars.Any(char.IsDigit))
                {
                    string validChars = options.ExcludeAmbiguousCharacters
                        ? new string(DIGITS.Where(c => !AMBIGUOUS.Contains(c)).ToArray())
                        : DIGITS;
                    int position = RandomNumberGenerator.GetInt32(options.Length);
                    int charIndex = RandomNumberGenerator.GetInt32(validChars.Length);
                    passwordChars[position] = validChars[charIndex];
                }

                if (options.IncludeSpecialCharacters && !passwordChars.Any(c => SYMBOLS.Contains(c)))
                {
                    string validChars = options.ExcludeAmbiguousCharacters
                        ? new string(SYMBOLS.Where(c => !AMBIGUOUS.Contains(c)).ToArray())
                        : SYMBOLS;
                    int position = RandomNumberGenerator.GetInt32(options.Length);
                    int charIndex = RandomNumberGenerator.GetInt32(validChars.Length);
                    passwordChars[position] = validChars[charIndex];
                }

                // Additional check: if excluding ambiguous chars, ensure none are present
                if (options.ExcludeAmbiguousCharacters)
                {
                    for (int i = 0; i < passwordChars.Length; i++)
                    {
                        if (AMBIGUOUS.Contains(passwordChars[i]))
                        {
                            // Replace with a random character from the valid pool
                            int charIndex = RandomNumberGenerator.GetInt32(poolArray.Length);
                            passwordChars[i] = poolArray[charIndex];
                        }
                    }
                }

                // Create password string
                string password = new string(passwordChars);

                return Result<string>.Ok(password);
            }
            finally
            {
                // CRITICAL: Clear sensitive data from memory
                Array.Clear(passwordChars, 0, passwordChars.Length);
                Array.Clear(poolArray, 0, poolArray.Length);
            }
        }
    }
}

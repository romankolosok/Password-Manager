using PasswordManager.Core.Models;

namespace PasswordManager.Core.Services.Interfaces
{
    /// <summary>
    /// Generates random passwords from options. Implementations validate options and produce
    /// passwords compatible with <see cref="Validators.PasswordValidator"/>.
    /// </summary>
    public interface IPasswordGenerator
    {
        /// <summary>
        /// Generates a password according to the given options.
        /// </summary>
        /// <param name="options">Length and character-set options.</param>
        /// <returns>Success with the generated password, or failure with validation/generation errors.</returns>
        Result<string> Generate(PasswordOptions options);
    }
}

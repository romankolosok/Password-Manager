using Konscious.Security.Cryptography;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Core.Services.Implementations
{
    public class CryptoService : ICryptoService
    {
        private const int SALT_SIZE = 16;                    // bytes
        private const int KEY_SIZE = 32;                     // bytes (256 bits for AES-256-GCM)
        private const int NONCE_SIZE = 12;                   // bytes (required by AES-GCM)
        private const int TAG_SIZE = 16;                     // bytes (GCM authentication tag)
        private const int ARGON2_MEMORY = 65536;             // KB (64 MB)
        private const int ARGON2_ITERATIONS = 3;
        private const int ARGON2_DEGREE_OF_PARALLELISM = 4;

        private readonly EncryptionInputValidator _encryptionValidator = new();
        private readonly DecryptionInputValidator _decryptionValidator = new();
        private readonly PasswordOptionsValidator _passwordOptionsValidator = new();

        public byte[] DeriveKey(string masterPassword, byte[] salt)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(masterPassword);

            var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = ARGON2_DEGREE_OF_PARALLELISM,
                Iterations = ARGON2_ITERATIONS,
                MemorySize = ARGON2_MEMORY
            };

            passwordBytes = passwordBytes.Select(b => (byte)0x00).ToArray(); // Clear password bytes from memory

            var keyBytes = argon2.GetBytes(KEY_SIZE);

            return keyBytes;
        }

        public Result<EncryptedBlob> Encrypt(string plaintext, byte[] key)
        {
            // Validate input
            var input = new EncryptionInput { Plaintext = plaintext, Key = key };
            var validationResult = _encryptionValidator.Validate(input);

            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<EncryptedBlob>.Fail(errors);
            }

            try
            {
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var nonce = new byte[NONCE_SIZE];
                RandomNumberGenerator.Fill(nonce);
                byte[] ciphertextBytes = new byte[plaintextBytes.Length];
                byte[] tagBytes = new byte[TAG_SIZE];

                using (var aesGcm = new AesGcm(key, TAG_SIZE))
                {
                    aesGcm.Encrypt(nonce, plaintextBytes, ciphertextBytes, tagBytes);
                }

                plaintextBytes = plaintextBytes.Select(b => (byte)0x00).ToArray(); // Clear plaintext bytes from memory

                return Result<EncryptedBlob>.Ok(new EncryptedBlob
                {
                    Nonce = nonce,
                    Ciphertext = ciphertextBytes,
                    Tag = tagBytes
                });
            }
            catch (CryptographicException ex)
            {
                return Result<EncryptedBlob>.Fail($"Encryption failed: {ex.Message}");
            }
        }

        public Result<string> Decrypt(EncryptedBlob blob, byte[] key)
        {
            // Validate input
            var input = new DecryptionInput { Blob = blob, Key = key };
            var validationResult = _decryptionValidator.Validate(input);

            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<string>.Fail(errors);
            }

            try
            {
                var plaintextBytes = new byte[blob.Ciphertext.Length];

                using (var aesGcm = new AesGcm(key, TAG_SIZE))
                {
                    aesGcm.Decrypt(blob.Nonce, blob.Ciphertext, blob.Tag, plaintextBytes);
                }

                var plaintext = Encoding.UTF8.GetString(plaintextBytes);
                plaintextBytes = plaintextBytes.Select(b => (byte)0x00).ToArray(); // Clear plaintext bytes from memory

                return Result<string>.Ok(plaintext);
            }
            catch (CryptographicException)
            {
                return Result<string>.Fail("Decryption failed. Possible causes: incorrect master password or data corruption.");
            }
        }

        public byte[] GenerateSalt()
        {
            var salt = new byte[SALT_SIZE];
            RandomNumberGenerator.Fill(salt);

            return salt;
        }

        public Result<string> GeneratePassword(PasswordOptions options)
        {
            var validationResult = _passwordOptionsValidator.Validate(options);

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
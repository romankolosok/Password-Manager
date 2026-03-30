using Konscious.Security.Cryptography;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
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
    }
}
using PasswordManager.Core.Models;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface ICryptoService
    {
        public byte[] DeriveKey(string masterPassword, byte[] salt);

        public Result<EncryptedBlob> Encrypt(string plaintext, byte[] key);

        public Result<string> Decrypt(EncryptedBlob blob, byte[] key);

        public byte[] GenerateSalt();

        public Result<string> GeneratePassword(PasswordOptions options);

        public double CalcuateEntropy(string password);
    }
}
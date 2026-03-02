using PasswordManager.Core.Services.Implementations;
using System.Security.Cryptography;

namespace PasswordManager.Tests.Fixtures
{
    public class CryptoServiceFixture : IDisposable
    {
        public CryptoService CryptoService { get; } = new();

        // Known password used to derive key
        public string KnownPassword { get; } = "TestMasterPassword1!";

        // Known salt used to derive key
        public byte[] Salt { get; }

        /// <summary>
        /// 32-byte AES-256 key derived once via Argon2id. Saves  ~500 ms Argon2 cost across all tests.
        /// </summary>
        public byte[] DerivedKey { get; }

        public CryptoServiceFixture()
        {
            Salt = new byte[16];
            RandomNumberGenerator.Fill(Salt);
            DerivedKey = CryptoService.DeriveKey(KnownPassword, Salt);
        }

        public void Dispose()
        {
            Array.Clear(Salt, 0, Salt.Length);
            Array.Clear(DerivedKey, 0, DerivedKey.Length);
        }
    }
}
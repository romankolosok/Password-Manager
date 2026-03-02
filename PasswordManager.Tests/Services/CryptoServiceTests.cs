using PasswordManager.Tests.Fixtures;
using System.Security.Cryptography;

namespace PasswordManager.Tests.Services
{
    public class CryptoServiceTests : IClassFixture<CryptoServiceFixture>
    {
        private readonly CryptoServiceFixture _fixture;

        public CryptoServiceTests(CryptoServiceFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void DerivedKeyReturns32Bytes()
        {
            var key = _fixture.DerivedKey;

            Assert.NotNull(key);
            Assert.Equal(32, key.Length);
        }

        [Fact]
        public void SamePasswordAndSaltReturnsSameKey()
        {
            var newKey = _fixture.CryptoService.DeriveKey(_fixture.KnownPassword, _fixture.Salt);
            Assert.Equal(_fixture.DerivedKey, newKey);
        }

        [Fact]
        public void DifferentPasswordReturnsDifferentKey()
        {
            var newKey = _fixture.CryptoService.DeriveKey("DifferentPassword!2", _fixture.Salt);
            Assert.NotEqual(_fixture.DerivedKey, newKey);
        }

        [Fact]
        public void DifferentSaltReturnsDifferentKey()
        {
            var differentSalt = new byte[16];
            RandomNumberGenerator.Fill(differentSalt);
            var newKey = _fixture.CryptoService.DeriveKey(_fixture.KnownPassword, differentSalt);
            Assert.NotEqual(_fixture.DerivedKey, newKey);
        }

        [Fact]
        public void GenerateSaltReturns16Bytes()
        {
            var salt = _fixture.CryptoService.GenerateSalt();
            Assert.NotNull(salt);
            Assert.Equal(16, salt.Length);
        }

        [Fact]
        public void GenerateSaltReturnsDifferentValues()
        {
            var salt1 = _fixture.CryptoService.GenerateSalt();
            var salt2 = _fixture.CryptoService.GenerateSalt();
            Assert.NotEqual(salt1, salt2);
        }

        [Fact]
        public void EncryptValidInputReturnsSuccessWithBlob()
        {
            var result = _fixture.CryptoService.Encrypt("Hello, World!", _fixture.DerivedKey);

            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Equal(12, result.Value.Nonce.Length);
            Assert.Equal(16, result.Value.Tag.Length);
            Assert.NotEmpty(result.Value.Ciphertext);
        }

        [Fact]
        public void EncryptSamePlaintextProducesDifferentNonces()
        {
            // AES-GCM nonces are randomly generated per call
            // same plaintext must never produce the same nonce
            var result1 = _fixture.CryptoService.Encrypt("Hello, World!", _fixture.DerivedKey);
            var result2 = _fixture.CryptoService.Encrypt("Hello, World!", _fixture.DerivedKey);

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.NotEqual(result1.Value.Nonce, result2.Value.Nonce);
        }

        [Fact]
        public void EncryptSamePlaintextProducesDifferentCiphertexts()
        {
            var result1 = _fixture.CryptoService.Encrypt("Hello, World!", _fixture.DerivedKey);
            var result2 = _fixture.CryptoService.Encrypt("Hello, World!", _fixture.DerivedKey);

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.NotEqual(result1.Value.Ciphertext, result2.Value.Ciphertext);
        }

        [Fact]
        public void EncryptCiphertextLengthMatchesPlaintextLength()
        {
            // AES-GCM is a stream cipher mode, thus ciphertext is always the same length as plaintext.
            const string plaintext = "Hello, World!";
            var result = _fixture.CryptoService.Encrypt(plaintext, _fixture.DerivedKey);

            Assert.True(result.Success);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(plaintext), result.Value.Ciphertext.Length);
        }


        [Theory]
        [InlineData("")]
        [InlineData("           ")]
        [InlineData(null)]
        public void EncryptEmptyPlaintextReturnsFailure(string plaintext)
        {
            var result = _fixture.CryptoService.Encrypt(plaintext, _fixture.DerivedKey);

            Assert.False(result.Success);
            Assert.Contains("Plaintext cannot be null or empty.", result.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(33)]
        public void EncryptWrongKeySizeReturnsFailure(int keySize)
        {
            var result = _fixture.CryptoService.Encrypt("Hello, World!", new byte[keySize]);

            Assert.False(result.Success);
            Assert.Contains("Encryption key must be exactly 32 bytes.", result.Message);
        }

        [Fact]
        public void DecryptValidBlobReturnsOriginalPlaintext()
        {
            const string plaintext = "Hello, World!";
            var encryptResult = _fixture.CryptoService.Encrypt(plaintext, _fixture.DerivedKey);
            Assert.True(encryptResult.Success);

            var decryptResult = _fixture.CryptoService.Decrypt(encryptResult.Value, _fixture.DerivedKey);

            Assert.True(decryptResult.Success);
            Assert.Equal(plaintext, decryptResult.Value);
        }

        [Fact]
        public void DecryptWithWrongKeyReturnsFailure()
        {
            const string plaintext = "Hello, World!";
            var encryptResult = _fixture.CryptoService.Encrypt(plaintext, _fixture.DerivedKey);
            Assert.True(encryptResult.Success);

            var wrongKey = new byte[10];
            RandomNumberGenerator.Fill(wrongKey);

            var decryptResult = _fixture.CryptoService.Decrypt(encryptResult.Value, wrongKey);
            Assert.False(decryptResult.Success);
            Assert.Contains("Decryption key must be exactly 32 bytes.", decryptResult.Message);
        }

        [Fact]
        public void DecryptWithTamperedCipherTextReturnsFailure()
        {
            const string plaintext = "Hello, World!";
            var encryptResult = _fixture.CryptoService.Encrypt(plaintext, _fixture.DerivedKey);
            Assert.True(encryptResult.Success);

            var encryptedBlob = encryptResult.Value;
            encryptedBlob.Ciphertext[0] ^= 0xFF; // Tamper with the first byte of the ciphertext

            var decryptResult = _fixture.CryptoService.Decrypt(encryptedBlob, _fixture.DerivedKey);
            Assert.False(decryptResult.Success);
            Assert.Contains("Decryption failed. Possible causes: incorrect master password or data corruption.", decryptResult.Message);
        }

        [Fact]
        public void DecryptWithTamperedTagReturnsFailure()
        {
            const string plaintext = "Hello, World!";
            var encryptResult = _fixture.CryptoService.Encrypt(plaintext, _fixture.DerivedKey);
            Assert.True(encryptResult.Success);

            var encryptedBlob = encryptResult.Value;
            encryptedBlob.Tag[0] ^= 0xFF; // Tamper with the first byte of the tag

            var decryptResult = _fixture.CryptoService.Decrypt(encryptedBlob, _fixture.DerivedKey);
            Assert.False(decryptResult.Success);
            Assert.Contains("Decryption failed. Possible causes: incorrect master password or data corruption.", decryptResult.Message);
        }
    }
}

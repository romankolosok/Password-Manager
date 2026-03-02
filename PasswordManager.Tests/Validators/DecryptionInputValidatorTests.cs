using PasswordManager.Core.Models;
using PasswordManager.Core.Validators;

namespace PasswordManager.Tests.Validators
{
    public class DecryptionInputValidatorTests
    {
        private readonly DecryptionInputValidator _validator = new();

        private static EncryptedBlob Blob(byte[] nonce, byte[] ciphertext, byte[] tag)
        {
            return new EncryptedBlob
            {
                Nonce = nonce,
                Ciphertext = ciphertext,
                Tag = tag
            };
        }
        private static DecryptionInput Input(EncryptedBlob blob, byte[] key)
        {
            return new DecryptionInput
            {
                Blob = blob,
                Key = key
            };
        }

        [Fact]
        public void ValidInputReturnsSuccess()
        {
            var blob = Blob(new byte[12], new byte[] { 1, 2, 3 }, new byte[16]);
            var input = Input(blob, new byte[32]);
            var result = _validator.Validate(input);
            Assert.True(result.IsValid);
        }

        public static IEnumerable<object[]> BlobsInvalidNonce()
        {
            yield return new object[] { Blob(null!, new byte[] { 1, 2, 3 }, new byte[16]) };
            yield return new object[] { Blob(new byte[11], new byte[] { 1, 2, 3 }, new byte[16]) };
            yield return new object[] { Blob(new byte[13], new byte[] { 1, 2, 3 }, new byte[16]) };
        }

        [Theory]
        [MemberData(nameof(BlobsInvalidNonce))]
        public void InvalidNonceReturnsFailure(EncryptedBlob blob)
        {
            var input = Input(blob, new byte[32]);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Nonce must be exactly 12 bytes.", result.Errors.Select(e => e.ErrorMessage));
        }

        [Fact]
        public void EmptyCiphertextReturnsFailure()
        {
            var blob = Blob(new byte[12], new byte[0], new byte[16]);
            var input = Input(blob, new byte[32]);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Ciphertext cannot be null or empty.", result.Errors.Select(e => e.ErrorMessage));
        }

        public static IEnumerable<object[]> BlobsInvalidTag()
        {
            yield return new object[] { Blob(new byte[12], new byte[] { 1, 2, 3 }, null!) };
            yield return new object[] { Blob(new byte[12], new byte[] { 1, 2, 3 }, new byte[15]) };
            yield return new object[] { Blob(new byte[12], new byte[] { 1, 2, 3 }, new byte[17]) };
        }

        [Theory]
        [MemberData(nameof(BlobsInvalidTag))]
        public void InvalidTagReturnsFailure(EncryptedBlob blob)
        {
            var input = Input(blob, new byte[32]);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Tag must be exactly 16 bytes.", result.Errors.Select(e => e.ErrorMessage));
        }

        public static IEnumerable<object[]> InvalidKeys()
        {
            yield return new object[] { new byte[0] };
            yield return new object[] { new byte[16] };
            yield return new object[] { new byte[31] };
            yield return new object[] { new byte[33] };
            yield return new object[] { new byte[35] };
        }

        [Theory]
        [MemberData(nameof(InvalidKeys))]
        public void InvalidKeyReturnsFailure(byte[] key)
        {
            var blob = Blob(new byte[12], new byte[] { 1, 2, 3 }, new byte[16]);
            var input = Input(blob, key);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Decryption key must be exactly 32 bytes.", result.Errors.Select(e => e.ErrorMessage));
        }
    }
}

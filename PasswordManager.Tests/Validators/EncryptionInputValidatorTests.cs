using PasswordManager.Core.Validators;

namespace PasswordManager.Tests.Validators
{
    public class EncryptionInputValidatorTests
    {
        private readonly EncryptionInputValidator _validator = new();

        private static EncryptionInput Input(string plaintext, byte[] key)
        {
            return new EncryptionInput
            {
                Plaintext = plaintext,
                Key = key
            };
        }

        [Fact]
        public void ValidInputReturnsSuccess()
        {
            var input = Input("Hello, World!", new byte[32]);
            var result = _validator.Validate(input);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void EmptyPlaintextReturnsFailure()
        {
            var input = Input("", new byte[32]);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Plaintext cannot be null or empty.", result.Errors.Select(e => e.ErrorMessage));
        }

        public static IEnumerable<object[]> InvalidKeys()
        {
            yield return new object[] { new byte[0] };
            yield return new object[] { new byte[16] };
            yield return new object[] { new byte[31] };
            yield return new object[] { new byte[33] };
        }

        [Theory]
        [MemberData(nameof(InvalidKeys))]
        public void InvalidKeyReturnsFailure(byte[] key)
        {
            var input = Input("Hello, World!", key);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Encryption key must be exactly 32 bytes.", result.Errors.Select(e => e.ErrorMessage));
        }

    }
}

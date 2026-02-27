using PasswordManager.Core.Validators;

namespace PasswordManager.Tests.Validators
{
    public class EncryptedBlobFormatValidatorTests
    {
        private readonly EncryptedBlobFormatValidator _validator = new();

        private EncryptedBlobParseInput Blob(string b64)
        {
            return new EncryptedBlobParseInput
            {
                Base64 = b64
            };
        }

        public static IEnumerable<object[]> BlobsInvalidB64()
        {
            yield return new object[] { "" };
            yield return new object[] { "            " };
            yield return new object[] { null };
            yield return new object[] { Convert.ToBase64String(new byte[0]) };
        }

        [Theory]
        [MemberData(nameof(BlobsInvalidB64))]
        public void EmptyBase64ReturnsFailure(string b64)
        {
            var blob = Blob(b64);
            var result = _validator.Validate(blob);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count);
        }

        [Fact]
        public void InvalidBase64ReturnsFailure()
        {
            var blob = Blob("not a valid base64 string");
            var result = _validator.Validate(blob);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Contains("Invalid encrypted blob format", result.Errors[0].ErrorMessage);
        }

        public static IEnumerable<object[]> BlobsWrongSize()
        {
            yield return new object[] { Convert.ToBase64String(new byte[27]) };
            yield return new object[] { Convert.ToBase64String(new byte[10]) };
        }

        [Theory]
        [MemberData(nameof(BlobsWrongSize))]
        public void Base64WithWrongSizeReturnsFailure(string b64)
        {
            var blob = Blob(b64);
            var result = _validator.Validate(blob);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Contains("Invalid encrypted blob format", result.Errors[0].ErrorMessage);
        }

        public static IEnumerable<object[]> BlobsValid()
        {
            yield return new object[] { Convert.ToBase64String(new byte[28]) };
            yield return new object[] { Convert.ToBase64String(new byte[29]) };
            yield return new object[] { Convert.ToBase64String(new byte[50]) };
        }

        [Theory]
        [MemberData(nameof(BlobsValid))]
        public void ValidBase64ReturnsSuccess(string b64)
        {
            var blob = Blob(b64);
            var result = _validator.Validate(blob);
            Assert.True(result.IsValid);
        }

    }
}

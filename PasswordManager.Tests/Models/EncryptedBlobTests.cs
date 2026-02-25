using PasswordManager.Core.Models;
using PasswordManager.Tests.Fixtures;

namespace PasswordManager.Tests.Models
{
    public class EncryptedBlobTests : IClassFixture<EncryptedBlobFixture>
    {
        private readonly EncryptedBlobFixture _fixture;

        public EncryptedBlobTests(EncryptedBlobFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestEncryptedBlobDoubleConvertation()
        {
            var original = _fixture.StandardBlob;

            var base64 = original.ToBase64String();
            var result = EncryptedBlob.FromBase64String(base64);

            Assert.True(result.Success);
            Assert.Equal(original.Nonce, result.Value.Nonce);
            Assert.Equal(original.Ciphertext, result.Value.Ciphertext);
            Assert.Equal(original.Tag, result.Value.Tag);
        }

        // Invalid inputs
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-base64!!")]
        [MemberData(nameof(GetBase64Of27Bytes))]
        public void TestFromBase64StringWithInvalidInputFails(string input)
        {
            var result = EncryptedBlob.FromBase64String(input);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Message);
        }

        // Valid inputs
        [Theory]
        [MemberData(nameof(GetValidBase64Inputs))]
        public void TestFromBase64StringWithValidInputSucceeds(
            string base64,
            int expectedNonceLength,
            int expectedCiphertextLength,
            int expectedTagLength)
        {
            var result = EncryptedBlob.FromBase64String(base64);

            Assert.True(result.Success);
            Assert.Equal(expectedNonceLength, result.Value.Nonce.Length);
            Assert.Equal(expectedCiphertextLength, result.Value.Ciphertext.Length);
            Assert.Equal(expectedTagLength, result.Value.Tag.Length);
        }

        public static IEnumerable<object[]> GetBase64Of27Bytes()
        {
            yield return new object[] { Convert.ToBase64String(new byte[27]) };
        }

        public static IEnumerable<object[]> GetValidBase64Inputs()
        {
            // Minimum valid (28 bytes = 12 nonce + 0 ciphertext + 16 tag)
            var fixture = new EncryptedBlobFixture();
            yield return new object[] { fixture.MinimumValidBase64, 12, 0, 16 };

            // Large blob
            yield return new object[] { fixture.LargeValidBase64, 12, 256, 16 };
            fixture.Dispose();
        }
    }
}
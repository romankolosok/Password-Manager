using PasswordManager.Core.Models;

namespace PasswordManager.Tests.Fixtures
{
    public class EncryptedBlobFixture : IDisposable
    {
        // Test case 1: Round-trip blob
        public EncryptedBlob StandardBlob { get; }

        // Test case 6: Exact minimum (28 bytes = 12 nonce + 0 ciphertext + 16 tag)
        public string MinimumValidBase64 { get; }

        // Test case 7: Large blob
        public string LargeValidBase64 { get; }

        // Test case 4: Invalid base64 string
        public string InvalidBase64String { get; } = "not-base64!!";

        // Test case 5: Just under minimum (27 bytes)
        public string Base64Of27Bytes { get; }

        public EncryptedBlobFixture()
        {
            // Test case 1: Standard blob for round-trip
            StandardBlob = new EncryptedBlob
            {
                Nonce = new byte[12] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
                Ciphertext = new byte[] { 65, 66, 67, 68 }, // "ABCD"
                Tag = new byte[16] { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 }
            };

            // Test case 6: Exact minimum - 28 bytes (nonce + empty ciphertext + tag)
            var minimumBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = Array.Empty<byte>(),
                Tag = new byte[16]
            };
            MinimumValidBase64 = minimumBlob.ToBase64String();

            // Test case 7: Large blob
            var largeCiphertext = new byte[256];
            Random.Shared.NextBytes(largeCiphertext);
            var largeBlob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = largeCiphertext,
                Tag = new byte[16]
            };
            LargeValidBase64 = largeBlob.ToBase64String();

            // Test case 5: 27 bytes (just under minimum)
            var bytes27 = new byte[27];
            Base64Of27Bytes = Convert.ToBase64String(bytes27);
        }

        public void Dispose()
        {
            // Clear sensitive data
            if (StandardBlob?.Nonce != null)
                Array.Clear(StandardBlob.Nonce, 0, StandardBlob.Nonce.Length);
            if (StandardBlob?.Ciphertext != null)
                Array.Clear(StandardBlob.Ciphertext, 0, StandardBlob.Ciphertext.Length);
            if (StandardBlob?.Tag != null)
                Array.Clear(StandardBlob.Tag, 0, StandardBlob.Tag.Length);
        }
    }
}

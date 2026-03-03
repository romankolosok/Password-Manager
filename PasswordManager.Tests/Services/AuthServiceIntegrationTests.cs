using PasswordManager.Core.Models;
using PasswordManager.Tests.Fixtures;

namespace PasswordManager.Tests.Services
{
    public class AuthServiceIntegrationTests : IClassFixture<SupabaseFixture>, IAsyncLifetime
    {
        private readonly SupabaseFixture _fixture;

        public AuthServiceIntegrationTests(SupabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Leave the fixture in a neutral state after every test
            await _fixture.AuthService.LockAsync();
        }

        [Fact]
        public async Task RegisterAsyncCreatesAuthUserAndProfileWithSaltAndVerificationToken()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var result = await _fixture.AuthService.RegisterAsync(email, password);

            // registration succeeded
            Assert.True(result.Success, $"RegisterAsync failed: {result.Message}");

            // user exists in Supabase Auth, can log in with the same credentials
            var loginResult = await _fixture.AuthService.LoginAsync(email, password);
            Assert.True(loginResult.Success, $"LoginAsync failed: {loginResult.Message}");

            var userId = _fixture.SessionService.CurrentUserId;
            Assert.NotNull(userId);

            // UserProfiles row created by handle_new_user_profile trigger
            var profileResult = await _fixture.UserProfileService.GetProfileAsync(userId.Value);
            Assert.True(profileResult.Success, $"GetProfileAsync failed: {profileResult.Message}");

            var profile = profileResult.Value;

            // profile Id matches the auth user
            Assert.Equal(userId.Value, profile.Id);

            // salt is stored and is a valid 16-byte Argon2id salt
            Assert.False(string.IsNullOrWhiteSpace(profile.Salt), "Salt should not be empty");
            var saltBytes = Convert.FromBase64String(profile.Salt);
            Assert.Equal(16, saltBytes.Length);

            // encrypted verification token is stored and is a valid EncryptedBlob
            Assert.False(string.IsNullOrWhiteSpace(profile.EncryptedVerificationToken),
                "EncryptedVerificationToken should not be empty");

            var blobResult = EncryptedBlob.FromBase64String(profile.EncryptedVerificationToken);
            Assert.True(blobResult.Success, "Stored token is not a valid EncryptedBlob");

            // token round-trips: derive key then decrypt must succeed
            var derivedKey = _fixture.SessionService.GetDerivedKey();
            var decryptResult = _fixture.CryptoService.Decrypt(blobResult.Value, derivedKey);
            Assert.True(decryptResult.Success, "Verification token decryption failed — key mismatch");
        }
    }
}